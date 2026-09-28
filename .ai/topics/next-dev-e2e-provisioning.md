---
name: next-dev-e2e-provisioning
description: "Préparation code-only du prochain E2E DEV abonnement → desired state → appartenance AD, arrêtée le 2026-09-28 faute de groupe de service strictement CLIENTS DEV."
metadata:
  type: project
---

Le prochain E2E « un service réellement provisionnable » ne peut pas être
lancé sans décision : les mappings AD runtime connus sont `ACCES-VPN → GG_VPN`
et `ACCES-RDS → GG_RDS`, groupes parent-domain exclus du périmètre DEV-only.
Le stockage dépend d'une réparation `Type="Storage"` headless non validée ;
`USER-ADDITIONAL`, `SUPPORT-PLUS` et l'onboarding sont des entitlements sans
groupe AD de service.

Le plan versionné est `docs/DEV_NEXT_E2E_PROVISIONING_PLAN.md`. Il impose un
preview V2 lecture seule avant toute écriture et trois portes exclusives :
groupe de service DEV dédié, autorisation parent-domain, ou validation préalable
du stockage headless. Ne pas réutiliser Melis ni les objets Run 1/2.

## Mise à jour 2026-09-29 (souscription DEV `43bee433`)

- Décision appliquée : service de test `SERVICE-E2E-DEV` → `GG_SERVICE_E2E_DEV`
  (règle `ad_group_membership`, sans tier, scope user). Paiement Stripe TEST OK
  mais refus `BILLING_V2_PROVISIONING_INCOMPLETE_MATERIALIZATION`.
- Cause : le planner exigeait un stockage personnel pour toute règle AD. Depuis
  `7da7309`, seul `VPN-ACCESS`/`RDS-ACCESS` l'exige. Les catalogues nomment ces
  services `VPN-ACCESS`/`RDS-ACCESS` (pas `ACCES-*`, qui sont des noms de
  mapping historiques).
- **Piège de validation** : le bouton admin « réconcilier le provisioning »
  (`/internal/admin/subscriptions/{id}/provisioning/reconcile`) passe par
  `BillingV2SubscriptionProvisioningManager`, pas par
  `BillingV2ProvisioningPlanner` + gate + memberships 097. Il ne prouve donc
  pas le chemin V2. La route V2 équivalente n'a pas d'interface, et un renvoi
  d'événement Stripe ne rejoue le provisioning que si l'événement porte
  `SUBSCRIPTION_ACTIVATED`. Preuve fiable : un nouvel achat Stripe TEST.
- État PROD relevé en lecture : migrations jusqu'à `095`, `096`/`097` absentes
  (additives).
