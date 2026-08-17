---
name: billing-v2-koxo-storage-targets
description: "Phase 3 Billing V2 (2026-08-17) TERMINÉE : topologie KoXo partagée, résolution fail-closed des cibles (GiB→MiB), route ciblée /internal/koxo/storage/reconcile/ sur SRV-21, provider HTTP réel et subordination des droits AD au stockage. Dormant en prod par les drapeaux. Corrige la fausse causalité « le stockage personnel crée le compte annuaire »."
metadata:
  node_type: memory
  type: project
---

## Ce qui est livré (local, non poussé)

Préparation du provider KoXo Storage. **Rien n'est appliqué** : les hard-stops
de `BillingV2ProvisioningService` restent en place, `DormantBillingV2KoxoStorageProvider`
reste enregistré dans `Program.cs`, et le nouveau resolver n'est branché nulle
part. Voir [[koxo-api-ne-cree-plus]] et [[koxo-fiche-utilisateur-maitre]].

- `KoxoDirectoryTopology` (`apps/api-internal/Services/`) : nommage KoXo pur —
  `PrimaryGroupClients` / `PrimaryGroupDemo`, `DemoGroupPrefix`,
  `ResolveSecondaryGroup`, `ResolvePrimaryGroup`, forme `CLI-NNNNNN`.
  `KoxoExportService` l'utilise désormais et n'expose plus que des alias.
- `BillingV2KoxoStorageTargetResolver` (`Services/Provisioning/`) : fonction
  pure, sans I/O, qui traduit un `BillingV2StorageQuotaPlan` en cible KoXo.

## Correction d'un fait qui était faux dans le code

Les commentaires affirmaient que **le stockage personnel crée le compte
annuaire**. C'est faux et ça a été corrigé.

`MariaDbKoxoRepository.ListExportCandidatesAsync` exige
`ad_link.portal_user_id IS NOT NULL` pour tout client réel : un client payant
ordinaire **a déjà son `customer_ad_links`** avant même de partir dans le CSV.
Seul un essai (`is_demo = TRUE AND demo_kind = 'trial'`) est exporté sans lien,
et c'est alors KoXo qui crée le compte, adopté ensuite par `employeeNumber`.

Le prérequis `PersonalStorageRequired` reste en place, mais c'est une **règle
d'ordre voulue** (pas d'accès VPN/RDS vers un poste inexistant), pas une
dépendance technique de création d'identité.

## Invariants du resolver

- Unité : le plan reste en **GiB** (catalogue), la cible KoXo est en **MiB**,
  conversion `checked(valeur * 1024)` faite à un seul endroit. Débordement ou
  quota ≤ 0 = refus, jamais de rebouclage silencieux.
- Identité utilisateur : `portal_users.id` → `koxo_unique_identifier`
  (`CLI-NNNNNN`) → `employeeNumber` → objet AD. Exactement **un**
  `customer_ad_link` (via `GetUserLinksByPortalUserIdAsync`), même client, et
  cohérence sur le **triplet** `objectGUID` / `objectSID` / `sAMAccountName`
  avec la résolution par `employeeNumber`. Aucun rapprochement par nom : KoXo
  translittère le nom et dérive lui-même le `sAMAccountName`.
- Utilisateur non matérialisé = **bloquant**. Le provider de quota ne crée
  aucune identité : un seul propriétaire de la création, la chaîne KoXo.
- **Binding explicite, ajouté en 3A.1** : la clé du dictionnaire d'instantanés
  ne prouve rien (elle vient de l'appelant), et le triplet GUID/SID/SAM ne
  prouve que la cohérence lien ↔ objet AD, pas l'appartenance au bon
  `portal_users.id`. On vérifie donc en `Ordinal` que
  `snapshot.IdentityReference` **et** `link.PortalUserId` valent bien la
  référence portée par le quota. Idem côté partagé : le snapshot de groupe
  porte un `CustomerId` qui doit égaler celui du resolver — une OU partagée
  n'a aucune autre attache qui démentirait une erreur d'alimentation.
- `TargetKey` d'un groupe = `group:<primaire>/<secondaire>` : `CLIENTS/X` et
  `CLIENTS DÉMO/X` sont deux OU distinctes, la séparation des groupes primaires
  les cloisonne justement.
- Refus **global** : une seule ligne douteuse refuse toute la résolution.
- Deux quotas visant le même objet = refus (sinon le résultat dépend de l'ordre
  de lecture).
- `BillingV2KoxoQuotaPolicy` classe les transitions ; **une réduction est
  déclarée non applicable** (abaisser sous l'occupation réelle bloque
  l'utilisateur sans rien libérer). Modélisé, pas encore branché.

## 3A.2 — alimentation read-only

`BillingV2KoxoStorageTargetResolutionService` construit les snapshots depuis les
données réelles puis appelle le resolver pur. **Enregistré en DI mais branché
nulle part** : rien ne consomme sa sortie, le provider dormant et les hard-stops
sont intacts.

Lectures ciblées via un dépôt dédié `IBillingV2KoxoTargetingRepository`,
**volontairement séparé de `IKoxoRepository`** : `ListExportCandidatesAsync`
porte la politique de population du CSV global (état civil complet, tolérance
`demo_kind = 'trial'`, exclusion `showcase`) et n'a rien à dire sur « où poser
le quota de cet abonnement ». S'en servir ferait dépendre le ciblage de règles
d'export sans rapport.

- `FindPortalUserAsync(customerId, portalUserId)` — bornée par les **deux**
  identifiants exacts. Conséquence assumée : « utilisateur inexistant » et
  « utilisateur d'un autre client » sont indistinguables
  (`…_PORTAL_USER_NOT_FOUND`) ; les distinguer confirmerait l'existence d'une
  ligne d'un autre client.
- `FindCustomerAsync(customerId)` — pour la cible partagée uniquement.

**Piège mesuré :** `LdapAdGroupProvisioner.SearchRoot` construit
`AdDirectoryObjectSummary` avec `CustomerReference = string.Empty`. La recherche
par `employeeNumber` ne renseigne donc **jamais** cette référence. Exiger une
égalité stricte rendrait toute résolution impossible ; la règle retenue est
« une référence absente n'est pas une contradiction, une référence renseignée et
divergente en est une ». Ne pas reconstruire la référence depuis le DN.

## Correction 2026-08-17 : `ResolveUserByEmployeeNumberAsync` et les modes AD

J'avais rapporté que cette méthode « ne rend rien hors `controlled_write` ».
**C'est faux.** `LdapAdGroupProvisioner.ResolveUserByEmployeeNumberAsync` teste
`_configuration.ConfigurationValid`, pas le mode ; et la résolution DI dans
`Program.cs` instancie `LdapAdGroupProvisioner` pour `ReadOnly` **et**
`ControlledWrite`. Une lecture par `employeeNumber` fonctionne donc en
`read_only`. Le comportement exact selon `AD_INTEGRATION_MODE` (`disabled`
rend `DisabledAdGroupProvisioner`, `mock` rend le provisioner mock) reste à
étudier séparément avant 3B — ne pas re-déduire cette contrainte de mode.

## Phase 3 terminée — exécution réelle câblée

Le chemin complet existe : plan → résolution → `/internal/koxo/storage/reconcile/`
sur SRV-21 → quota posé et vérifié → seulement ensuite les droits AD. Il reste
**inaccessible en production** par `BILLING_V2_PROVISIONING_ENABLED=false`, non
touché.

### Deux routes, portées incomparables

`/internal/koxo/sync/` reconcilie **toute** la branche et, avec
`DisableOrphanedAccounts`, désactive ce qui manque au CSV. Ne jamais s'en servir
pour poser un quota. `/internal/koxo/storage/reconcile/` ne touche qu'un objet.
Même récepteur, même port, même mécanisme d'authentification — mais un jeton
`KOXO_STORAGE_WEBHOOK_TOKEN` facultatif, qui devient exclusif sur la route de
stockage quand il est posé.

### Emplacement de la fiche

`Data\Users\<PRIMAIRE>\<SECONDAIRE>\<userId>.xml` pour une personne,
`Data\Users\<PRIMAIRE>\<SECONDAIRE>.xml` pour un groupe. Les deux premiers
segments viennent de `KoxoDirectoryTopology`, donc de la **même OU que
l'export**. Le `userId` est le `sAMAccountName` **lu** dans `customer_ad_links`,
jamais prédit. Aucun balayage par nom : si la fiche n'est pas à cet endroit,
l'objet n'est pas matérialisé et c'est bloquant. Le système de fichiers est donc
lui-même la vérification — c'est pourquoi aucun contrôle sur le DN n'a été
ajouté, il n'aurait apporté qu'un risque de faux blocage.

### Décision desired ↔ actual

absent → `not_materialized` · égal → `noop` · inférieur → augmentation ·
supérieur → `blocked_reduction`, **jamais appliqué** · illisible ou ambigu →
`failed`. Un quota désactivé n'est jamais un `noop` : l'activer est une
modification, et on refuse quand même d'abaisser au passage une valeur déjà
enregistrée.

### Pièges intégrés

- **Édition byte-safe obligatoire** : substitution ciblée sur les deux éléments,
  jamais `[xml]` + `.Save()` (voir [[koxo-fiche-utilisateur-maitre]]). Le reste
  de la fiche est préservé au caractère près, KoXo la réapplique intégralement.
- `<FolderQuota>` et `<UserFolderQuota>` sont deux éléments distincts ; le `<`
  ancre la distinction dans le motif.
- `KoXoAdm.exe` sort en **code 1 même en succès** : `Invoke-KoxoProcess` tranche
  sur les marqueurs de journal, réutilisé tel quel.
- **Verrou partagé** avec la synchro globale : KoXoAdm ne supporte pas deux
  instances, quel que soit le chemin qui l'invoque.
- La relecture après réparation est obligatoire : l'écriture n'est pas sa preuve,
  une réparation qui réécrirait la fiche depuis la base KoXo annulerait tout.

### Niveau de preuve, dit honnêtement

`xml_verified` = fiche relue après réparation. `fully_verified` = quota effectif
constaté côté FSRM, seulement si `KOXO_STORAGE_FSRM_ENABLED=true` et gabarit de
chemin fourni. **FSRM n'a pas été validé en réel** : aucun rôle FSRM sur SRV-21,
le volume « Stockage dossiers personnels » est ailleurs. Une vérification
demandée mais non concluante ferme le résultat.

### Côté API

`HttpBillingV2KoxoStorageProvider` ne résout rien : il reçoit des cibles déjà
vérifiées. Une requête par cible, séquentielle, arrêt à la première non
appliquée, les suivantes rendues **non tentées**. Un lot partiel échoue
globalement. `BillingV2KoxoStorageGate` porte l'invariant « pas de droit AD
dépendant sans socle de stockage » et est testable seul.

Configuration absente ⇒ provider dormant qui bloque tout ; configuration à
moitié posée ⇒ échec au démarrage, volontairement.
