---
name: billing-v2-additional-users
description: "Billing V2 Phase 4 USER-ADDITIONAL (2026-08-18) : une place payée n'est pas une identité ; cycle de vie awaiting_password → koxo_pending → directory_ready → ready ; remise du mot de passe à KoXo chiffrée et atomique ; export fail-closed ; aucun oracle cross-customer. Statut : current (revalider le déploiement)."
metadata:
  node_type: memory
  type: project
  status: current
  source: "transcripts Claude du 2026-08-18 (sessions f0ab179d, a8d459ed), revérifiés dans le code le 2026-09-17"
---

# Billing V2 — utilisateurs additionnels (Phase 4)

Commits de la phase : `edbf6c5`, `9de63f3`, `4be9aca`, `8b11e33`, `830c936`,
puis raccordement produit. Migration `065_billing_v2_additional_user_identity.sql`.

## Modèle

- Une ligne `billing_v2_subscription_users` non primaire est une **capacité
  commerciale**, pas une personne : `identity_reference = NULL`,
  `email = NULL`, « Utilisateur additionnel N ». Une place vide est un état
  **légitime**.
- La vraie identité reste `portal_users`. **Ne pas introduire de statut
  `pending` sur `portal_users`** : l'export KoXo exige `status='active'`. Un
  utilisateur `active` sans `password_hash` n'est de toute façon pas
  connectable (condensat factice dans `AuthenticationService`).
- Cycle de vie distinct : `awaiting_password → koxo_pending →
  directory_ready → ready` (plus `failed`, `disabled`).

## Invariants à ne pas défaire

1. **Une place réelle ≠ `is_primary = 0`.** Elle est définie par le fragment
   SQL unique `UserSlotEntitlementSource` (item `scope_type='user'` actif ⋈
   service actif ⋈ règle `user_slot` active) et `AdministrableSlotPredicate`
   (`slot.status='active'` + `subscription.status='active'`). Les mêmes
   constantes servent à l'attribution, à la lecture et aux quatre compteurs
   de `BillingV2PortalSubscriptionProjection` : l'écran ne peut pas proposer
   une place que la transaction refuserait.
2. **Remise du mot de passe atomique** : verrou du jeton (`FOR UPDATE`, avec
   vérification du `purpose` *sous ce verrou*), `password_hash`,
   `consumed_at`, UPSERT du chiffré AES-256-GCM dans
   `koxo_pending_directory_passwords`, transition `awaiting_password →
   koxo_pending` — **une seule transaction**, aucun appel réseau dedans ; le
   déclenchement KoXo suit le COMMIT. Même règle que pour le changement de
   mot de passe (voir [admin-configuration-center.md](admin-configuration-center.md)).
3. **Le secret KoXo survit au redémarrage** (plus de magasin RAM), se relit
   sans se consommer (`PeekAsync`), est lié à sa ligne (AAD + `key_id`) et
   n'est acquitté qu'**après relecture confirmée du lien AD**, avant
   `MarkReadyAsync`.
4. **Export fail-closed** : `RequiresPendingPassword` est calculé dans la
   requête d'export. Secret absent, expiré, sous une autre clé ou illisible →
   export invalide, jamais un utilisateur sans mot de passe. Un utilisateur
   déjà lié à l'AD n'exige rien.
5. **Fenêtre de crash** : `directory_ready` sans `customer_ad_links` reste
   exportable avec exactement les gardes de `koxo_pending` — sinon
   l'identité sort du CSV et KoXo la **désactive**. `awaiting_password`,
   `failed`, `disabled`, `ready` n'ont pas cette exception.
6. **Course sur une place** : le perdant reçoit `SLOT_ALREADY_ASSIGNED`,
   pas `LIFECYCLE_ALREADY_EXISTS`. En cas de violation UNIQUE, relire l'état
   de la place avant de classer le conflit, jamais d'après le nom de la
   contrainte.
7. **Aucun oracle cross-customer** : place inexistante, place d'un autre
   client, place d'un autre abonnement → même HTTP, même code, même message
   (`SlotNotVisible`). Le `subscriptionId` de l'URL est vérifié aussi pour
   le renvoi d'invitation, et un refus n'émet pas de nouveau jeton.
8. `/set-password?flow=billing-v2-additional-user` : `flow` **sélectionne**
   le parcours, il n'autorise rien et n'est pas transmis en amont. Sans
   `flow`, le parcours d'inscription historique est inchangé.
9. `TryMaterializeAsync` et `DisableAsync` ne sont **pas** exposés en route.
   `BILLING_V2_PROVISIONING_ENABLED=false` refuse attribution, mot de passe,
   matérialisation, renvoi et désactivation avant tout point de non-retour.

## Limite de preuve

La suite `BillingV2AdditionalUserIdentitySchemaTests` exige
`BILLING_V2_TEST_MARIADB_CONNECTION`. Le 2026-08-18, l'utilisateur a rapporté
qu'elle passait sur MariaDB 11.8.6 réelle, sauf le test de sérialisation,
corrigé ensuite (`830c936`) mais **non rejoué** sur base réelle dans ces
sessions.
