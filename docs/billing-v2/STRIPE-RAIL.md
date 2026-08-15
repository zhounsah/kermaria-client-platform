# Rail Stripe Billing V2 (Phase 2)

Raccordement du premier checkout Stripe V2 au cœur financier décrit dans
`FINANCIAL-CORE.md`. PayPal n'est **pas** branché dans cette phase.

## Chaîne

```text
SubscriptionChange (intention serveur)
        ↓
BillingEvent finalized + lignes figées
        ↓
PaymentAttempt persistée (avant tout appel réseau)
        ↓
Stripe (price_data au montant local)
        ↓
refetch Stripe
        ↓
settlement vérifié
        ↓
activation locale (compare-and-swap sur version)
```

Aucune étape ne peut être sautée : chaque maillon refuse de s'exécuter si le
précédent n'est pas dans l'état attendu.

## 1. L'ancre d'idempotence est serveur

Le navigateur fournit un `client_request_id` (l'en-tête `Idempotency-Key` déjà
envoyé par le BFF). Il n'est **plus** l'ancre : il n'est qu'une entrée.

L'ancre est le `SubscriptionChange`, résolu côté serveur en deux temps :

1. **par clé** — `hash(customer, provider, environment, offre, client_request_id)`.
   Couvre le double clic et le retry réseau ;
2. **par sélection métier** — s'il n'y a pas de correspondance par clé, on
   cherche une intention encore `pending` et non expirée pour le même
   `(client, offre, provider, environnement)`. Couvre le rafraîchissement de
   navigateur, qui fabrique forcément un nouveau `client_request_id`.

Un choix volontairement différent (autre offre, autre rail) ne matche ni l'un ni
l'autre et ouvre bien une nouvelle intention.

Sous concurrence, l'insertion est un `INSERT IGNORE` suivi d'une relecture : le
perdant annule sa propre transaction — donc son abonnement brouillon — et repart
de l'intention du gagnant. Aucun second contrat n'est créé.

## 2. Aucun dispatch sans BillingEvent

`BillingV2StripeDispatchGuard` refuse le dispatch si l'événement est absent, non
`finalized`, sans ligne, sans devise valide, à total nul ou négatif, si la
décomposition `récurrent + one-shot` ne retombe pas sur le total, s'il est déjà
réglé, ou si une TVA non nulle est présente (franchise en base ; le contraire
exigerait de déclarer la fiscalité à Stripe, hors périmètre Phase 2).

`billing_v2_authoritative_checkout_requests` porte désormais
`subscription_change_id` et `billing_event_id`. Après finalisation, **plus aucune
relecture du catalogue** n'intervient sur le chemin de paiement.

## 3. PaymentAttempt avant l'appel

`provider_request_key = bv2-evt-<billing_event_id>` — dérivée de l'événement,
donc identique à chaque tentative. La ligne porte `expected_amount_cents` et
`expected_currency` figés avant le premier appel.

Un timeout réseau ne crée jamais une seconde tentative :

1. l'appel renvoie `BILLING_V2_STRIPE_CALL_INDETERMINATE` ;
2. on interroge Stripe avec la clé/les identifiants persistés ;
3. si la session existe, on la rattache ; sinon la tentative reste `in_flight`
   et sera rejouée avec **la même clé**, ce qui fait renvoyer par Stripe la
   session existante au lieu d'en créer une seconde.

## 4. Stripe est un rail d'encaissement, pas une source de prix

Le montant est transmis en `price_data` inline. **Aucun `price_id` externe** n'est
envoyé : c'est ce qui garantit que le prélèvement égale le montant local.

| Mode | Représentation Stripe | Récurrence |
|---|---|---|
| Mensuel | `mode=subscription`, `price_data` récurrente mensuelle au MRR contractuel (remise déjà intégrée) | oui |
| Comptant 6/12 mois | `mode=payment`, paiement unique du montant upfront exact | **non** |

La *setup fee* est toujours une ligne one-shot distincte de la part abonnement.

`billing_v2_provider_price_mappings` reste utilisé comme référence Stripe
(produit/prix) et gagne `expected_amount_cents` / `expected_currency` comme
contrôle croisé facultatif, plus `amount_authority` contraint à `'local'` — pour
qu'un futur contributeur ne puisse pas redonner l'autorité au provider par un
simple `UPDATE`. Aucun mapping de production n'est seedé, et aucun Stripe Price
ID legacy n'est réutilisé.

## 5. Le webhook est un signal, le refetch fait foi

Tout événement entrant ne fait que **déclencher une relecture** de la session
chez Stripe. La vérification exige simultanément :

- métadonnées correspondant à l'événement financier, à l'abonnement et à la
  tentative attendus ;
- mode Stripe conforme au mode de paiement contractuel ;
- `payment_status = paid` ;
- devise identique à `expected_currency` ;
- `amount_total` identique à `expected_amount_cents`.

Tout écart de montant ou de devise produit `amount_mismatch` sur la tentative
**et** sur l'événement financier : aucune activation, aucun document, aucun
`paid` par défaut, et une réconciliation à traiter.

### Événements et effets

| Événement Stripe | Effet Phase 2 |
|---|---|
| `checkout.session.completed` | signal : marque la session terminée, **déclenche la relecture**, n'active rien |
| `customer.subscription.created` | signal inerte (Phase 1), ne déclenche même pas de relecture |
| `customer.subscription.updated` | signal inerte (Phase 1) |
| `invoice.payment_failed` | `past_due` |
| `customer.subscription.deleted` | `cancelled` |

Une session `complete` avec `payment_status=unpaid` — le cas 3DS abandonné — ne
peut donc plus provisionner quoi que ce soit.

## 6. Concurrence

L'activation prend un verrou `SELECT ... FOR UPDATE` sur l'abonnement, relit
l'état de settlement sous ce verrou (rejeu = no-op), puis applique un
compare-and-swap sur `version`. Zéro ligne affectée remonte
`BILLING_V2_SUBSCRIPTION_VERSION_CONFLICT` et laisse l'opération en
réconciliation : jamais de lost update silencieux.

## 7. Ce qui reste hors périmètre

PayPal V2, Customer Credit Ledger, downgrades, remboursements, chargebacks,
réconciliateur périodique, refonte BPCE, résiliation automatisée.
