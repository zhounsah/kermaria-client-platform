# Cycle de vie du renouvellement Stripe (Phase 3)

Rend un abonnement mensuel Stripe V2 viable **au-delà du checkout initial**.
S'appuie sur `FINANCIAL-CORE.md` (événement financier immuable) et
`STRIPE-RAIL.md` (encaissement vérifié).

## Chaîne d'un cycle

```text
signal Stripe (invoice.*)          ← ne décide de rien
        ↓
relecture bornée de l'invoice      ← identifiants persistés uniquement
        ↓
rang du cycle = f(ancre, période)  ← jamais l'heure courante
        ↓
BillingEvent renewal_charge        ← unique par (abonnement, cycle)
        ↓
PaymentAttempt propre au cycle
        ↓
settlement vérifié (montant + devise + état réel)
        ↓
document du cycle + émission BPCE retry-safe
```

## 1. Réconciliation périodique

`BillingV2StripeReconciliationWorker` déclenche
`BillingV2StripeReconciliationService`.

| Propriété | Valeur |
|---|---|
| Activation | `BILLING_V2_RECONCILIATION_WORKER_ENABLED` — **OFF par défaut** |
| Fréquence | `BILLING_V2_RECONCILIATION_INTERVAL_SECONDS` (défaut 300 s, plancher 30 s) |
| Flag OFF | le service n'est **pas enregistré** : aucun appel provider possible |
| Concurrence | arbitrée en base par le bail, pas par une élection de leader |
| Isolation | une exception sur une tentative n'interrompt pas le lot ; le bail est rendu |

Métrique émise à chaque passage non vide :

```text
pending / reconciled / failed / reconciliation_required
```

`pending` est le restant à traiter. S'il ne descend jamais, quelque chose ne
converge pas — c'est la seule ligne à surveiller.

## 2. L'état Stripe ne se résume pas à `payment_status`

Pour `mode=subscription`, une session `paid` ne suffit plus : l'abonnement peut
basculer `past_due` quelques heures après. Le rail relit donc **trois** objets.

| Objet | Ce qu'il prouve |
|---|---|
| Checkout Session | que le tunnel s'est terminé |
| Subscription | que le contrat provider est sain (`active` / `trialing`) |
| Invoice | **le seul encaissement opposable** : montant payé, devise, client, abonnement |

Un renouvellement n'a pas de session du tout : l'invoice est alors la seule
source. `customer.subscription.created` et `customer.subscription.updated` ne
peuvent, par construction, que **dégrader** l'état local.

## 3. Identité d'un renouvellement

`subscription_id + cycle_sequence`, garanti par
`uq_billing_v2_billing_events_cycle`. Convention : **cycle 1 = charge
initiale**, cycle 2 = premier renouvellement.

Le rang vient de `billing_v2_subscriptions.billing_anchor_at` (valeur initiale
déterministe : `COALESCE(started_at, created_at)`). Stripe fournit seulement la
*période* concernée ; c'est l'ancre locale qui la convertit en rang. Sans
période exploitable, `BillingV2RenewalCycleResolver` **échoue en fermé** plutôt
que de facturer « le cycle du moment ».

### Ce que le BillingEvent fige

items contractuels applicables · versions de prix verrouillées · engagement ·
remise · plancher éventuel · période · montant attendu · devise.

Les montants viennent de `billing_v2_subscription_items.amount_cents_snapshot`,
figés à la souscription. **Le catalogue n'est jamais relu** : une hausse
tarifaire postérieure ne peut pas repricer un contrat en cours.

Le plancher d'engagement (45 %) s'applique **au renouvellement**, jamais à la
charge initiale, et se matérialise en **ligne explicite** — sinon
`total = somme des lignes` tomberait.

## 4. Politique de grâce V2.0 (impayé)

Un renouvellement échoué produit un état local **visible**, séparé du statut
d'abonnement.

| `payment_state` | Sens |
|---|---|
| `current` | aucun incident connu |
| `payment_attention` | échec ou paiement non prouvé ; **accès conservé** |
| `manual_review` | incohérence financière ; plus aucun automatisme |

**Aucune issue ne déprovisionne** : pas de retrait de groupe AD, pas de
réduction de quota, pas de suppression de donnée, pas de résiliation.
`BillingV2RenewalGracePolicy.AutomaticDeprovisioningEnabled = false`, et le
test le vérifie sur *toutes* les issues possibles.

C'est un choix produit assumé, pas une limite technique : une coupure d'accès
est irréversible pour le client alors qu'un impayé se rattrape. Tant que la
détection n'a pas fait ses preuves en exploitation, le coût d'un faux positif
dépasse celui d'un jour de grâce. À resserrer une fois le rail éprouvé.

## 5. Document de cycle

Un document par cycle, construit **uniquement** depuis les snapshots du
BillingEvent — aucun recalcul, aucune relecture du catalogue. Émis seulement
après un encaissement prouvé.

Double garde-fou en base :
`uq_billing_v2_subscription_document_billing_event` (1:1, migration 057) et
`uq_billing_v2_subscription_document_cycle` (migration 061). La seconde couvre
le cas que la première ne voit pas : deux calculs de période divergeant d'un
jour.

Rejeu — webhook ×N, réconciliateur ×N, worker facture ×N — conserve :
**1 BillingEvent · 1 PaymentAttempt logique · 1 document · 1 facture logique.**

## 6. Recherche Stripe bornée

`FindCheckoutSessionAsync` ne balaye plus le compte. `BillingV2StripeSessionLookupPolicy`
choisit une cible parmi les identifiants **persistés** :

1. `provider_session_id` → lecture directe ;
2. `provider_payment_id` → requête filtrée côté serveur, `limit=1` ;
3. `provider_subscription_id` → idem ;
4. aucun → **fail closed**.

Dans le dernier cas, le retry normal repart avec la même clé d'idempotence, que
Stripe déduplique. Aucun scan n'est nécessaire.

## 7. BPCE

Inchangé par rapport à `STRIPE-RAIL.md §9` : l'API n'expose pas de recherche de
facture, donc un retour indéterminé part en `reconciliation_required` plutôt que
de risquer un second numéro fiscal.

Ajout Phase 3 : le nombre de dossiers bloqués remonte dans l'instantané de
readiness admin (`BILLING_V2_DOCUMENT_ISSUANCE_AWAITING_REVIEW`) et en
`LogLevel: Warning`. Un dossier bloqué ne reste plus silencieux.

## 8. Matrice de readiness

`BillingV2LifecycleReadinessGate` — trois états, un seul sens de lecture :
`READY` (automatisé et testé), `MANUAL` (possible avec intervention humaine
assumée), `NOT_READY` (ne pas utiliser en l'état).

Seuls les composants de `RequiredForStripeLaunch` bloquent un lancement Stripe.
**PayPal reste explicitement `NOT_READY` sans bloquer Stripe**, parce qu'il
n'en fait pas partie.

## 9. Compensation interne après remboursement intégral

La confirmation d'un `BillingV2Refund` intégral est un workflow interne de
compensation, distinct de `SelfServiceCancellationEnabled`. Dans la même
transaction qui rend le settlement `refunded`, il bloque le renouvellement local
et enfile l'annulation provider idempotente. Tant que le provider converge,
l'abonnement reste `pending_cancellation` : le moteur de renouvellement ne peut
pas créer de nouveau cycle, et le système ne prétend pas à tort que le contrat
provider est déjà annulé.

## 10. Hors périmètre

Customer Credit Ledger, upgrades, downgrades, remboursements partiels,
chargebacks, avoir BPCE/reprise comptable, PayPal V2, TVA non nulle.
