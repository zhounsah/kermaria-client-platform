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

subscriptions
  ├── subscription_users
  ├── subscription_items
  │    └── subscription_item_provisioning
  ├── subscription_changes
  ├── payment_agreements
  └── subscription_price_locks

provider_price_mappings

legacy_offer_mappings
legacy_service_mappings

outbox_events
audit_log
```

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
