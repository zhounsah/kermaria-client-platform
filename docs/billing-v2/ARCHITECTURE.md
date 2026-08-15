# Architecture cible

## Legacy

La table `commercial_offers` mélange actuellement :

- définition commerciale ;
- prix ;
- engagement ;
- mode de paiement ;
- identifiants Stripe ;
- plans PayPal ;
- références techniques ;
- groupes AD ;
- preset public.

Les offres PACK-* existent en plusieurs variantes de durée et de paiement.

## Billing V2

```text
services
  ├── service_tiers
  ├── service_prices
  ├── service_dependencies
  └── provisioning_rules

offer_presets
  └── preset_items

commitment_terms

subscriptions                        (version : optimistic locking)
  ├── subscription_users
  ├── subscription_items
  │    └── subscription_item_provisioning
  ├── subscription_changes           (intention persistante + idempotence)
  ├── payment_agreements
  └── subscription_price_locks

billing_events                       (intention financière immuable)
  ├── billing_event_lines            (détail figé, remise ventilée)
  └── payment_attempts               (persistées AVANT tout appel provider)

provider_price_mappings

legacy_offer_mappings
legacy_service_mappings

outbox_events
audit_log
```

## Chaîne financière

Le cœur financier est décrit en détail dans `FINANCIAL-CORE.md`, qui fait
autorité sur ce document pour tout ce qui touche à l'argent.

```text
Subscription / SubscriptionChange
        ↓
Pricing Engine
        ↓
BillingEvent + BillingEventLines
        ↓
PaymentAttempt
        ↓
Provider
        ↓
settlement vérifié
        ↓
Document
        ↓
Entitlement / Provisioning
```

Points structurants :

- `SubscriptionChange` porte l'intention utilisateur et sert d'ancre
  d'idempotence. Une clé d'idempotence qui ne vit que côté client n'est pas une
  ancre valable ;
- `BillingEvent` est immuable ; une correction est un nouvel événement
  `adjustment`, jamais une réécriture ;
- `BillingEvent` ↔ document est **1:1 en V2.0** ;
- `PaymentAttempt` sépare le montant **attendu** (décidé par le Pricing Engine)
  du montant **réellement constaté** chez le provider ;
- un webhook est un **signal**, jamais une preuve de paiement suffisante ;
- aucun provisioning ne découle d'un événement provider brut ;
- toute mutation de `subscriptions` s'écrit en compare-and-swap sur `version`.

## Sources de vérité

### Nouveau contrat V2

La base Zachary IT est la source de vérité.

Stripe et PayPal sont des fournisseurs de paiement, pas le modèle métier.

### Provisioning

Le provisioning est déterminé par les services souscrits.

Exemples :

```text
VPN-ACCESS
→ groupe AD VPN

RDS-ACCESS
→ groupe AD RDS

STORAGE-PERSONAL 128
→ quota utilisateur 128 GiB

STORAGE-SHARED 256
→ quota espace partagé 256 GiB
```

## Droit acheté vs état provisionné

Pour les paiements comptants :

```text
droit contractuel acheté
!=
capacité provisionnée
!=
utilisation réelle
```

Un client ayant payé 128 Go pour un an peut demander un quota technique inférieur sans remboursement, puis remonter jusqu'à 128 Go sans supplément pendant la période prépayée.
