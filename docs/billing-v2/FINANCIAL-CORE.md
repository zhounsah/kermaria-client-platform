# Cœur financier Billing V2

Ce document décrit le noyau financier introduit en **Phase 1** de la refondation
Billing V2. Il fait autorité sur `ARCHITECTURE.md` pour tout ce qui touche à
l'argent : intention, événement financier, tentative de paiement, settlement,
document.

Statut d'implémentation au terme de la Phase 1 :

- schéma et invariants : **créés** (migration `057`) ;
- politiques applicatives pures : **créées et testées** ;
- branchement des flux provider/documentaire sur le nouveau cœur : **non fait**,
  c'est l'objet de la Phase 2.

Tant que la Phase 2 n'est pas livrée, les tables du cœur financier sont
**dormantes** : elles existent, elles sont contraintes, elles ne sont écrites par
aucun flux de production.

## 1. Chaîne cible

```text
Subscription / SubscriptionChange     intention utilisateur persistée
        ↓
Pricing Engine                        fonction pure, versionnée
        ↓
BillingEvent + BillingEventLines      intention financière immuable
        ↓
PaymentAttempt                        persistée AVANT tout appel provider
        ↓
Provider                              Stripe / PayPal
        ↓
settlement vérifié                    montant réellement constaté
        ↓
Document                              document commercial puis facture BPCE
        ↓
Entitlement / Provisioning            droits et actions techniques
```

Chaque flèche est un franchissement d'état contrôlé. Aucune étape ne peut être
sautée : un document sans BillingEvent finalisé, ou un provisioning sans
settlement vérifié, sont des états interdits.

## 2. SubscriptionChange — l'intention persistante

`billing_v2_subscription_changes` cesse d'être une table descriptive pour
devenir **l'ancre d'idempotence** de toute opération monétaire.

Règle : une intention utilisateur (un clic « souscrire », « upgrader »,
« résilier ») crée exactement une ligne `subscription_changes`, identifiée par un
`client_request_id` fourni par l'appelant.

```text
client_request_id          identifiant d'intention fourni par l'appelant
idempotency_key_canonical  chaîne canonique lisible, auditable
idempotency_key_hash       SHA-256 de la canonique, UNIQUE
base_subscription_version  version de Subscription lue au moment de l'intention
status                     pending → applied | expired | failed | cancelled
requested_at / expires_at / applied_at
failure_reason_code / reconciliation_reason_code
```

Deux conséquences directes :

- une clé d'idempotence portée par un état de composant côté navigateur n'est
  **pas** une ancre valable. La clé doit exister en base avant l'appel provider ;
- `base_subscription_version` matérialise le compare-and-swap : une intention
  calculée sur une version périmée de l'abonnement doit être rejetée, pas
  appliquée en écrasant silencieusement.

### Expiration

Une intention porte `expires_at`. Au-delà, elle passe `expired`. Un paiement
provider qui arrive **après** l'expiration ne provisionne rien automatiquement :
il part en réconciliation avec `reconciliation_reason_code`. C'est la traduction
de l'invariant « paiement tardif ≠ activation ».

## 3. BillingEvent — l'intention financière immuable

Un `billing_v2_billing_events` est une **créance ou une dette figée** : ce que
nous avons décidé de facturer, au prix connu à cet instant, pour une période
donnée.

### Trois axes d'état, orthogonaux et séparés

| Axe | Colonne | Valeurs |
|---|---|---|
| Financier | `financial_status` | `draft`, `finalized`, `void` |
| Règlement | `settlement_status` | `none`, `pending`, `settled`, `partially_settled`, `failed`, `amount_mismatch`, `refunded` |
| Documentaire | `document_status` | `none`, `pending`, `issued`, `failed` |

Ces trois axes ne doivent jamais être fusionnés en un seul champ « statut ». Un
événement peut être `finalized` + `settled` + `none` (payé, pas encore facturé)
sans que ce soit une anomalie.

### Immuabilité

Sont figés à la création et ne doivent **jamais** être mis à jour :

```text
event_type, direction, currency,
period_start, period_end,
payment_mode_snapshot, commitment_months_snapshot,
discount_basis_points_snapshot,
gross_amount_cents, discount_amount_cents, net_amount_cents,
tax_amount_cents, total_amount_cents,
pricing_engine_version,
idempotency_key_canonical, idempotency_key_hash,
toutes les lignes de billing_v2_billing_event_lines
```

Seuls les trois statuts, leurs horodatages (`finalized_at`, `voided_at`) et les
motifs sont mutables.

### Correction

**Une facture historique n'est jamais recalculée. Un BillingEvent n'est jamais
réécrit.** Une erreur se corrige par un **nouvel** événement de type
`adjustment`, portant `corrects_billing_event_id` vers l'événement fautif.

La clé d'idempotence de l'événement corrigé reste consommée à vie : elle n'est
jamais réutilisée, même après un `void`. C'est ce qui garantit qu'un retry
concurrent ne peut pas ressusciter un événement annulé.

### Snapshot de prix

`pricing_engine_version` est obligatoire. Sans elle, une facture ancienne n'est
pas re-vérifiable : on saurait ce qui a été facturé, pas pourquoi. Les entrées
tarifaires (prix de service, remise, engagement, mode de paiement) sont
snapshotées sur l'événement et sur ses lignes, jamais relues au catalogue courant
au moment d'un contrôle.

## 4. BillingEventLines — le détail figé

`billing_v2_billing_event_lines` porte le détail immuable : service, tier, prix
catalogue référencé, item d'abonnement éventuel, quantité, montant unitaire,
brut, remise ventilée, net, taxe, total, période, ordre d'affichage.

### Ventilation de la remise

La remise est calculée **globalement** sur l'événement, puis **ventilée** sur les
lignes de façon déterministe. La ventilation doit satisfaire :

```text
Σ ligne.discount_allocated_amount_cents = event.discount_amount_cents
Σ ligne.net_amount_cents                = event.net_amount_cents
Σ ligne.total_amount_cents              = event.total_amount_cents
```

Le reliquat de centime se répartit par la méthode des plus grands restes, avec
un tri stable sur `(display_order, id)`. Deux exécutions sur les mêmes entrées
doivent produire exactement la même ventilation, sinon les documents ne sont pas
reproductibles.

Aucune ligne négative dans un événement `debit`.

## 5. PaymentAttempt — la tentative persistée avant l'appel

`billing_v2_payment_attempts` existe pour une seule raison : **rendre un retry
provider sûr**.

Règle absolue : **une PaymentAttempt est écrite en base AVANT le premier appel
provider.** Jamais après, jamais « au retour ».

```text
billing_event_id
provider / environment
provider_request_key      clé d'idempotence envoyée au provider, UNIQUE
provider_payment_id       identifiant provider constaté au retour
provider_session_id
expected_amount_cents     ce que NOUS avons décidé de facturer
expected_currency
settled_amount_cents      ce qui a RÉELLEMENT été encaissé (nullable)
settled_currency
provider_fee_cents
status                    created → in_flight → succeeded | failed | abandoned
                                   | amount_mismatch
attempted_at / responded_at / reconciled_at
```

`provider_request_key` est unique par `(provider, environment)`. Un retry
réutilise **la même ligne et la même clé** : c'est ce qui garantit que le
provider renvoie l'objet existant au lieu d'en créer un second.

### Attendu vs constaté

`expected_amount_cents` et `settled_amount_cents` sont deux colonnes distinctes,
et c'est délibéré.

- `expected` vient du Pricing Engine. Il fait foi sur ce qui **doit** être payé.
- `settled` vient du provider, après vérification. Il constate ce qui **a** été
  payé.

Un règlement n'est `succeeded` que si `settled_amount_cents == expected_amount_cents`
et `settled_currency == expected_currency`. Tout écart produit
`amount_mismatch`, un état d'exception qui **bloque** la suite de la chaîne et
demande une revue humaine. Il ne doit jamais être arrondi, ignoré, ou traité
comme un succès.

Corollaire : aucun montant facturé ne peut être déterminé par Stripe ou PayPal.
Le provider constate, il ne décide pas.

## 5.1 Remboursement intégral canonique (V1)

Le remboursement n'est pas un raccourci consistant à écrire
`settlement_status = refunded`. La migration `082` porte une intention durable
`billing_v2_refunds`, générique et indépendante de tout produit consommateur.
Elle est liée à un `BillingEvent` et à la `PaymentAttempt` Stripe réellement
settled ; un même événement ne peut avoir qu'un remboursement V1.

```text
BillingEvent settled + PaymentAttempt Stripe vérifiée
        ↓
BillingV2Refund requested (montant/devise relus en base, audit, outbox atomique)
        ↓
Stripe Refund avec clé idempotente persistée
        ↓
relecture ciblée du refund Stripe / reprise après timeout
        ↓
preuve : id + status=succeeded + montant intégral + devise + PaymentIntent
        ↓
BillingV2Refund confirmed + BillingEvent.settlement_status=refunded
```

V1 ne couvre que le remboursement **intégral** : le montant et la devise ne
proviennent jamais du navigateur ni d'un workflow produit. Un timeout HTTP est
indéterminé : le worker relit d'abord Stripe, par l'identifiant de refund connu
ou par `(PaymentIntent persistant, metadata billing_v2_refund_id)`, puis
réutilise exactement la même clé Stripe. Un webhook est un signal répétable ;
il ne suffit pas à confirmer le refund sans cette relecture.

Le passage à `refunded` exige simultanément : paiement initial `settled`, refund
provider identifié en `succeeded`, même PaymentIntent, même montant intégral et
devise identique. `requested`, `pending_provider` et `failed` ne valent jamais
`refunded`.

Une confirmation bloque localement les renouvellements (`renews_at` est retiré,
abonnement en `pending_cancellation`) et place une annulation provider durable
dans l'outbox existante. Cette compensation interne est distincte de
`SelfServiceCancellationEnabled`, qui ne concerne que le droit client.

L'exécution externe est fermée par défaut : seul
`BILLING_V2_REFUNDS_ENABLED=true` active le worker serveur, et n'accorde aucun
droit public de remboursement. Aucun endpoint portal `refund(paymentId)`
n'existe.

### Limite documentaire non contournée

Si `document_status=issued`, la demande est refusée avec
`BILLING_V2_REFUND_CREDIT_NOTE_REQUIRED`. Si un document est seulement en cours
(`pending` ou `failed`), elle est aussi refusée avec
`BILLING_V2_REFUND_DOCUMENT_IN_PROGRESS` : cela évite une course avec BPCE. Le
modèle actuel sait émettre des factures mais pas encore un avoir/reprise BPCE
canonique. Stripe peut rembourser le paiement, mais cela ne permet pas
d'improviser la correction comptable. Ce cas reste un bloqueur de mise en
service du refund pour des événements documentés ou en cours de documentation.
Le modèle de correction durable, les clés d'idempotence et les preuves exigées
sont détaillés dans [REFUND-CORE-HARDENING.md](REFUND-CORE-HARDENING.md).

## 6. Webhook = signal, jamais preuve

Un webhook provider est une **notification qu'un objet a changé**. Ce n'est pas
une preuve de paiement, et son contenu n'est pas une source de vérité.

Règles :

1. un webhook déclenche une **re-lecture de l'objet chez le provider**
   (refetch) ; c'est cette lecture qui fait foi ;
2. l'ordre d'arrivée des webhooks n'est pas garanti — les transitions d'état
   restent monotones par rang, un état terminal ne redescend jamais ;
3. **aucun provisioning, aucune émission documentaire, aucun passage à `paid`
   ne peut découler de la seule réception d'un événement provider brut** ;
4. un webhook perdu ne doit pas bloquer le système indéfiniment : la convergence
   est assurée par un réconciliateur qui repolle les états non terminaux.
   Ce réconciliateur est un livrable de Phase 2.

En particulier, `customer.subscription.created` et `customer.subscription.updated`
ne prouvent **rien** sur le paiement : Stripe crée l'objet en statut
`incomplete` tant qu'une authentification 3DS est en attente, et émet
`updated` à chaque changement, y compris vers `past_due`. Ces deux événements
sont depuis la Phase 1 traités comme des signaux inertes.

## 7. BillingEvent ↔ Document : 1:1 en V2.0

En V2.0, **un BillingEvent finalisé produit au plus un document commercial, et
un document commercial référence exactement un BillingEvent.**

Conséquences assumées :

- une charge et un crédit sur la même période produisent **deux** documents
  (une facture et un avoir), pas un document net ;
- l'agrégation de plusieurs événements sur une facture mensuelle unique est
  **hors périmètre V2.0**. Elle nécessiterait une relation N:1 et un
  `document_status` dérivé, pas stocké.

La numérotation reste assurée par **une seule autorité**. Aujourd'hui c'est BPCE,
via `ValidateInvoiceAsync`. Aucun autre composant n'alloue de numéro de facture.

## 8. Optimistic locking sur Subscription

`billing_v2_subscriptions.version` est un `BIGINT NOT NULL DEFAULT 1`.

Toute mutation d'un abonnement doit s'écrire en compare-and-swap :

```sql
UPDATE billing_v2_subscriptions
SET    status = @new_status,
       version = version + 1,
       updated_at = UTC_TIMESTAMP(6)
WHERE  id = @id
  AND  version = @expected_version;
```

Zéro ligne affectée = conflit de concurrence. Le conflit doit **remonter** et
faire échouer l'opération, jamais être avalé en « rien à faire ». Une écriture
d'abonnement sans clause de version est un défaut, pas un raccourci.

Cela ne remplace pas le verrouillage pessimiste là où plusieurs événements
provider concurrents visent le même abonnement : la Phase 2 devra sérialiser par
`SELECT ... FOR UPDATE` sur l'abonnement en tête de transaction entrante.

## 9. Invariants garantis par la base

Exprimés en `CHECK` / `UNIQUE` / `FOREIGN KEY` dans la migration `057` :

| # | Invariant | Mécanisme |
|---|---|---|
| DB-1 | `total = net + tax` sur l'événement | `CHECK` |
| DB-2 | `net = gross - discount` sur l'événement | `CHECK` |
| DB-3 | montants bruts/nets/taxes/totaux ≥ 0 | `CHECK` |
| DB-4 | remise ≤ brut | `CHECK` |
| DB-5 | devise non vide, 3 caractères | `CHECK` |
| DB-6 | `period_end > period_start` | `CHECK` |
| DB-7 | `direction ∈ {debit, credit}` | `CHECK` |
| DB-8 | `financial_status ∈ {draft, finalized, void}` | `CHECK` |
| DB-9 | `settlement_status` dans l'énumération | `CHECK` |
| DB-10 | `document_status` dans l'énumération | `CHECK` |
| DB-11 | `finalized` ⇒ `finalized_at` non nul | `CHECK` |
| DB-12 | `void` ⇒ `voided_at` non nul | `CHECK` |
| DB-13 | clé d'idempotence événement unique | `UNIQUE` |
| DB-14 | clé d'idempotence intention unique | `UNIQUE` |
| DB-15 | `provider_request_key` unique par provider/env | `UNIQUE` |
| DB-16 | même arithmétique sur les lignes | `CHECK` |
| DB-17 | quantité de ligne > 0 | `CHECK` |
| DB-18 | un document V2 référence au plus un BillingEvent | `UNIQUE` |
| DB-19 | `settled_amount_cents` ≥ 0 si renseigné | `CHECK` |
| DB-20 | `expected_amount_cents` ≥ 0 | `CHECK` |

## 10. Invariants applicatifs

MariaDB ne peut pas exprimer une contrainte inter-lignes ou inter-tables dans un
`CHECK` (pas de sous-requête, pas d'agrégat). Les invariants suivants sont donc
**applicatifs**, portés par des politiques pures testées, et doivent être
appelés par tout code qui écrit le cœur financier.

| # | Invariant | Politique |
|---|---|---|
| APP-1 | un événement `finalized` a ≥ 1 ligne | `BillingV2BillingEventPolicy` |
| APP-2 | Σ lignes = totaux de l'événement (brut, remise, net, taxe, total) | `BillingV2BillingEventPolicy` |
| APP-3 | toutes les lignes partagent la devise de l'événement | `BillingV2BillingEventPolicy` |
| APP-4 | aucune ligne négative dans un événement `debit` | `BillingV2BillingEventPolicy` |
| APP-5 | `void` interdit si un settlement a réussi | `BillingV2BillingEventStateMachine` |
| APP-6 | `void` interdit si un document légal est émis | `BillingV2BillingEventStateMachine` |
| APP-7 | transitions financières autorisées uniquement `draft→finalized`, `draft→void`, `finalized→void` | `BillingV2BillingEventStateMachine` |
| APP-8 | pas de transition depuis un état terminal | `BillingV2BillingEventStateMachine` |
| APP-9 | une clé d'idempotence n'est jamais réutilisée, même après `void` | `BillingV2BillingEventStateMachine` + `UNIQUE` |
| APP-10 | `settled == expected` sinon `amount_mismatch` | `BillingV2SettlementPolicy` |
| APP-11 | devise settled == devise attendue | `BillingV2SettlementPolicy` |
| APP-12 | PaymentAttempt persistée avant appel provider | `BillingV2PaymentAttemptPolicy` |
| APP-13 | un retry réutilise la clé provider existante | `BillingV2PaymentAttemptPolicy` |
| APP-14 | conflit de version Subscription ⇒ échec explicite | `BillingV2SubscriptionVersionPolicy` |
| APP-15 | ambiguïté de prix ⇒ résolution versionnée ou fail-closed | `BillingV2ServicePriceResolutionPolicy` |
| APP-R1 | remboursement V1 seulement après settlement Stripe prouvé | `BillingV2RefundPolicy` |
| APP-R2 | montant/devise du refund sont ceux du BillingEvent settled | `BillingV2RefundPolicy` |
| APP-R3 | un BillingEvent produit une seule intention/outbox/refund provider | contraintes `UNIQUE` + `BillingV2RefundOutbox` |
| APP-R4 | timeout/réponse perdue ⇒ relecture Stripe avant nouvelle création | `BillingV2RefundService` + `IBillingV2StripeGateway` |
| APP-R5 | seul un refund Stripe relu, intégral et réussi peut écrire `refunded` | `BillingV2RefundConfirmationPolicy` |
| APP-R6 | refund confirmé bloque tout renouvellement et enfile l'annulation provider | `BillingV2RefundService` + cancellation outbox |

**Le fait qu'un invariant soit applicatif et non DB n'en fait pas une
recommandation.** C'est une contrainte, simplement portée ailleurs. Tout nouveau
chemin d'écriture doit passer par ces politiques.

## 11. Exécuter les tests

Invariants applicatifs (purs, aucune base requise) — inclus dans
`npm run test:billing-legacy` :

```bash
npm run test:billing-legacy
```

Invariants DB — exigent une **MariaDB jetable** portant les migrations 001 à
082. La suite échoue explicitement si la variable n'est pas définie : elle n'est
jamais silencieusement verte par absence de base.

```bash
BILLING_V2_TEST_MARIADB_CONNECTION="Server=127.0.0.1;Port=3399;Database=billing_v2_scratch;User ID=root;Password=;" npm run test:billing-v2-schema
```

Ne jamais pointer cette variable vers une base de recette ou de production :
la suite écrit et supprime des lignes de test.

## 12. Hors périmètre Phase 1

Explicitement **non livrés**, et à ne pas présumer disponibles :

- **Customer Credit Ledger** — table append-only nécessaire aux downgrades
  mensuels avec avoir. C'est le prérequis de la Phase suivante : un
  `downgrade_credit` doit produire un BillingEvent `credit` → un avoir → une
  entrée de ledger. Un crédit consommé sur une facture ultérieure agit comme
  **moyen de règlement** et ne réduit pas la base fiscale de cette nouvelle
  facture. Rien de tout cela n'existe aujourd'hui ;
- upgrades / downgrades complets et prorata branché ;
- remboursements partiels, chargebacks ;
- worker de réconciliation provider ;
- refonte du chemin BPCE ;
- checkout Stripe / PayPal réel sur le nouveau cœur.
