---
name: koxo-api-ne-cree-plus
description: "LIVRÉ le 2026-08-06 (4b3a9ff) : l'API n'écrit plus d'identité AD quand KoXo fait autorité, elle l'adopte par employeeNumber. Garde-fou KoxoOwnsDirectory = ControlledWrite seulement, car le mode Mock n'a pas de KoXo derrière."
metadata: 
  node_type: memory
  type: project
  originSessionId: d79dbe2b-9ae3-4e1a-bc48-9b5d713aee39
  modified: 2026-08-06T08:09:51.874Z
---

Décision d'architecture de ZH : **KoXo est maître de l'annuaire, l'API ne crée
plus d'identité AD.** Voir [[koxo-ad-password-mastery]] pour les mesures.

**Livré le 2026-08-06, commit `4b3a9ff`.** Smoke tests API au vert, contrat
signup vitrine 48/48.

**Correction du 2026-08-06** : j'avais écrit ici que « la recette tourne en
Mock ». C'est faux — **SRV-13 est en `controlled_write` depuis au moins le
2026-08-05**, la sonde `/health/ready` le dit. Le nouveau chemin sera donc actif
dès le déploiement du binaire, sans bascule de mode à faire. Voir
[[srv13-config-volatile]].

À vérifier au premier passage réel : adoption par `employeeNumber`, réponse
`AD_IDENTITY_NOT_READY` quand la synchro n'a pas encore tourné, et absence de
doublon.

## Ce que la modification comportait, et qui compilait

1. `ISignupRepository.GetKoxoUniqueIdentifierAsync(portalUserId)` + les deux
   implémentations (MariaDb : `SELECT koxo_unique_identifier FROM portal_users`,
   Mock : lecture de `ApprovedUserKoxoUniqueIdentifier`).
2. `SignupService` : injection de `IAdGroupProvisioner`, et dans
   `ProvisionActiveDirectoryAsync` remplacement de `EnsurePortalAdUserAsync` par
   `ResolveUserByEmployeeNumberAsync(koxoIdentifier)`. Si absent → déclencher la
   synchro et renvoyer `AD_IDENTITY_NOT_READY` (réessayer), **jamais créer**.
   Statut du lien : `koxo_provisioned` et non `koxo_pending`.
3. Suppression de `EnsurePortalAdUserAsync`, `ResolveAvailableSamAccountNameAsync`,
   `BuildSamAccountNameBase`, `BuildSamCandidate`, `NormalizeSamSegment`.
4. Trigger généralisé : `SendKoxoSyncTriggerAsync(signupId, userId, ref, trigger)`
   **sans filtre sur `koxo_export_status`** (le filtre `koxo_pending` rendait les
   modifications ultérieures invisibles de l'annuaire), plus un déclenchement
   `signup_approved` dès l'approbation pour que l'identité existe avant que le
   client suive son lien.
5. `MariaDbKoxoRepository.ListExportCandidatesAsync` : la branche « demo trial »
   devient « tout utilisateur à état civil complet », en **gardant** la branche
   `ad_link IS NOT NULL` pour que la règle reste MONOTONE — personne ne sort du
   CSV, car une ligne retirée vaut désactivation.
6. `verify-signup-contract.mjs` : l'assertion `BuildSamAccountNameBase` est
   remplacée par `doesNotMatch(/CreateUserAsync/)` + `ResolveUserByEmployeeNumberAsync`.

## Le point qui a failli tout casser, et comment il est resolu

`MockAdGroupProvisioner.ResolveUserByEmployeeNumberAsync` renvoie **toujours
`null`** (« le mock ne simule pas d'annuaire peuplé »), et
`MockActiveDirectoryService.SetUserPasswordAsync` résout par `sAMAccountName`
dans son propre `_objectsByDn`. Donc en `AdIntegrationMode.Mock` — c'est-à-dire
en recette, où les inscriptions sont ouvertes — **tout `set-password`
échouerait** en `AD_IDENTITY_NOT_READY`.

**Tranché par ZH** : la règle ne vaut qu'en LDAP réel. D'où
`AdRuntimeConfiguration.KoxoOwnsDirectory` (`Mode is ControlledWrite`) : en Mock
l'application continue de créer, ce qui n'expose à aucun doublon puisqu'aucun
KoXo ne synchronise en face. `EnsurePortalAdUserAsync` et les helpers de
dérivation du `sAMAccountName` sont donc **conservés**, pas supprimés.

Symptôme observé : le smoke test `RunSignupKoxoWebhookTriggerTestsAsync` échoue
sur « Le mot de passe doit rester defini meme si le webhook KoXo est un effet de
bord ».

**Why:** la modification compile et paraît juste ; c'est à l'exécution en mode
Mock qu'elle casse le parcours d'inscription. Sans cette note on la relande et
on casse la recette.

**How to apply:** trancher le mock d'abord, refaire les 6 points ensuite, et ne
basculer `DoNotWritePasswordsInActiveDirectory=1` sur SRV-21 qu'au moment du
déploiement.

## Piège d'outillage rencontré

Un `dotnet` local verrouille `apps/api-internal/bin/.../Kermaria.ApiInternal.dll`
et le harnais de smoke tests (`Directory.Build.targets`) pointe **en dur** sur ce
chemin : les tests tournent alors contre un DLL périmé sans le dire. Contournement
sans tuer le processus : `dotnet test -p:BaseOutputPath=<dossier>` puis lancer
`dotnet <scratch>/bin/Debug/net10.0-windows/...SmokeTests.dll <scratch>/bin/Debug/net10.0/Kermaria.ApiInternal.dll`.
