# Première mission Codex — AUDIT UNIQUEMENT

## Mode

READ ONLY.

Aucun changement de code.
Aucune migration exécutée.
Aucun appel de production.
Aucun commit.

## Objectif

Auditer le dépôt pour préparer l'intégration de Billing V2 sans casser la facturation, les abonnements, Stripe, PayPal ou le provisioning.

## À inspecter

Chercher toutes les références à :

```text
commercial_offers
public_pack_code
external_reference
technical_service_references
provisioning_group_sam_account_names
stripe_price_id_test
stripe_price_id_live
paypal_plan_id_sandbox
paypal_plan_id_live
billing_cadence
billing_interval_months
commitment_months
payment_mode
setup_fee_amount_cents
```

Identifier aussi :

```text
checkout
subscription
invoice
webhook
Stripe
PayPal
Active Directory
group membership
provisioning
Nextcloud quota
RDS
VPN
```

## Livrable attendu

Créer un rapport, sans modifier le code fonctionnel, contenant :

### 1. Dependency map

Pour chaque dépendance au modèle legacy :

```text
fichier
fonction / route
lecture / écriture
impact
criticité
```

### 2. Billing flow actuel

Décrire précisément :

```text
sélection offre
→ checkout
→ provider
→ webhook
→ abonnement interne
→ facture
→ provisioning
```

### 3. Source of truth

Identifier pour chaque domaine la source de vérité actuelle :

```text
catalogue
prix
contrat
paiement
facture
provisioning
```

### 4. Risques

Lister les endroits où une introduction de V2 pourrait :

- changer un prix existant ;
- casser un checkout ;
- créer un double abonnement ;
- doubler un provisioning ;
- désactiver un utilisateur ;
- recalculer une facture ;
- rendre un webhook non idempotent.

### 5. Tests existants

Lister les tests couvrant la facturation et les trous de couverture.

### 6. Proposition de découpage

Proposer les changements en petits lots indépendants.

Ne pas implémenter ces changements pendant cette mission.

## Documentation à lire en premier

```text
docs/billing-v2/README.md
docs/billing-v2/ARCHITECTURE.md
docs/billing-v2/CATALOG.md
docs/billing-v2/PRICING-RULES.md
docs/billing-v2/PRESETS.md
docs/billing-v2/LEGACY-MAPPING.md
docs/billing-v2/BILLING-INVARIANTS.md
docs/billing-v2/MIGRATION-PLAN.md
docs/billing-v2/TEST-PLAN.md
docs/billing-v2/ROLLBACK.md
```
