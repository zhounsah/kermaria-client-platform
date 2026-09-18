---
name: billing-v2-only-cancellation
description: "Bascule Billing V2-only (commit 64cb3c5, migrations 070/071, déployée en v2.0.0.0 le 2026-08-25) et chaîne de résiliation V2 : pending_cancellation, résolveur d'ancre provider multi-source, fin de terme PayPal en deux gestes, conservation des droits payés. Statut : current."
metadata:
  node_type: memory
  type: project
  status: current
  source: "transcript Claude 2026-08-23/24 (session 53e4fc3a), revérifié dans le code et docs/BILLING_V2_ONLY.md le 2026-09-17"
---

# Billing V2-only et résiliation

Référence produit : `docs/BILLING_V2_ONLY.md`. Cette fiche ne garde que ce
que la doc ne dit pas ou ce qu'une session future risque de casser.

## Bascule

- Décidée parce qu'**aucun client réel** n'existait (seul le compte de test
  de ZH) : pas de couche de compatibilité, un seul gros refactor.
- Commit `64cb3c5` (255 fichiers, −32 088 lignes), poussé le 2026-08-24 ;
  migrations `070` et `071` appliquées au déploiement `v2.0.0.0`
  (2026-08-25). Supprimés : `IBillingCatalog` et ses adaptateurs legacy/ombre,
  `CartService`, `RecurringCheckoutService`, `commercial_offers` et ses
  consommateurs.
- `071` supprime `commercial_documents.subscription_id` et
  `commercial_document_lines.offer_id`. Le rattachement passe **uniquement**
  par `billing_v2_subscription_documents` ; les lignes de document sont des
  instantanés autonomes. Un contrat statique empêche de réutiliser une colonne
  supprimée : les suites mock ne l'auraient pas vu.

## Chaîne de résiliation

`demande → BillingV2CancellationPolicy → transaction {statut local + outbox +
audit} → dispatcher → fournisseur → convergence`, entièrement dans
API-INTERNAL.

- `cancelled` n'est posé **que** s'il n'existe aucun abonnement fournisseur.
  Partout ailleurs : `pending_cancellation`. **Une résiliation immédiate
  pose aussi `pending_cancellation`** : le statut seul ne dit donc pas si
  les droits restent ouverts.
- **Ancre fournisseur** : `BillingV2ProviderAnchorResolver` lit trois
  sources (`payment_agreements`, `provider_checkout_sessions`,
  `payment_attempts` réussis). Deux triplets
  `(provider, environment, subscription_id)` différents → conflit, revue
  manuelle, **aucun appel**, statut inchangé. Pas de « plus récent gagne ».
  Le renouvellement et les mutations Stripe utilisent ce même lecteur.
- Absence d'ancre ≠ achat ponctuel : s'il existe une composante mensuelle
  effective (`billing_v2_subscription_item_effective_price_components`) →
  `..._PROVIDER_ANCHOR_MISSING`, jamais `cancelled`.
- **PayPal en fin de terme = deux gestes** : `suspend_pending_term_end`
  immédiat puis `cancel_at_term`, dont l'échéance est portée par
  `available_at` en base (survit à un redémarrage). Un webhook
  `billing.subscription.suspended` sur un abonnement en
  `pending_cancellation` est attendu et ne provoque aucune transition.
  `pending_cancellation` a le même rang que `past_due`, au-dessus
  d'`active` : un `activated` tardif ne ressuscite pas l'abonnement.
- L'environnement persisté est comparé au mode en cours d'exécution
  (`..._PROVIDER_RUNTIME_ENVIRONMENT_MISMATCH`, non rejouable) avant toute
  requête fournisseur.

## Conservation des droits

`BillingV2EntitlementRetention` sépare deux questions :

- `GrantsAcquiredRights` (conserver) : `active`, ou `pending_cancellation`
  tant que `current_period_ends_at` n'est pas passé (fin de la période
  **encaissée**, pas `renews_at`). Utilisé par les téléchargements et les
  groupes de provisioning.
- `AllowsNewMutations` (ouvrir) : `active` seulement. Utilisé par
  l'attribution de places additionnelles (`AdministrableSlotPredicate`).

Ne pas remettre `subscription.status = 'active'` en dur dans une projection
de droits : le client perdrait sa période payée dès le clic « résilier ».
