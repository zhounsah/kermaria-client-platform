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
