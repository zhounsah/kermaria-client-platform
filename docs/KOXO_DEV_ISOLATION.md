# Isolation KoXo DEV / PROD

> **État au 2026-09-28 : isolation KoXo DEV et E2E identité standard validés.**
> Après les Run 1/2, l'API DEV en `controlled_write` a exécuté le parcours
> complet signup → vérification → approbation → password setup → KoXo → AD →
> convergence PIB dans le seul namespace DEV. La matérialisation **headless**
> du stockage n'est toujours **pas** validée.

## État déployé et validé (2026-09-27)

| Élément | État |
|---|---|
| Scripts SRV-21 | `f15e314` déployés le 2026-09-26 (sauvegarde `CSVSynchro\backups\pre-f15e314-20260926-234926`) ; plan PROD inchangé. Le correctif `8336cc1` du lanceur d'instance n'est pas redéployé : la tâche 8043 n'en dépend pas |
| Groupe primaire `CLIENTS DEV` | `OU=CLIENTS DEV,OU=Utilisateurs,OU=KoXoAdm,DC=clients,DC=home,DC=bzh`, groupe AD `CLIENTS DEV` (membre de `Utilisateurs de KoXo Administrator CLIENTS`, comme `CLIENTS` et `CLIENTS DÉMO`), fiche `Data\Users\CLIENTS DEV.xml` : `AllowRDS=0`, `AllowDialin=0`, quotas désactivés |
| Lieux de stockage | `Espaces mutuels "Clients DEV"` et `Espaces personnels "Clients DEV"` : `KERMARIA-FS-01.HOME.BZH`, `F:\KoXoDATA\CLIENTS DEV`, point de montage `Sans partage`, quota désactivé. La fiche `CLIENTS DEV` les utilise (`GroupStorage` et `GroupsPreferredStorage` = mutuels, `UsersPreferredStorage` = personnels). Aucun partage, aucun quota FSRM |
| Modèle `Config\Models\PrimaryGroups\CLIENTS DEV.xml` | voir ci-dessous |
| Profil `Data\CSVSynchro\CLIENTS-DEV.xml` | CSV `Data\CSVSynchro-DEV\clients-dev.csv`, `GenerateLabels=0`, `SyncDoNotDeleteUsers=1`, `DisableOrphanedAccounts=1`, `UseUniqueIDFirst=1` |
| Définition d'instance et jetons | `C:\ProgramData\Kermaria\koxo-dev\` (SYSTEM et Administrateurs seulement) ; jeton du receveur 8043 distinct de la PROD |
| Receveur 8043 | tâche `Kermaria-KoXoWebhookReceiver-DEV-8043`, préfixe `http://+:8043/internal/koxo/sync/`, route stockage fermée |
| SRV-12 `:3100` | réservé à SRV-11 (`ufw`) |
| SRV-11, vhost DEV | `X-Forwarded-For` et `X-Real-IP` = `$remote_addr` : SRV-12 reçoit `192.168.100.221` pour SRV-21, une valeur usurpée est écrasée |

### Modèle `CLIENTS DEV`

Copie de « Archivage Utilisateur », à laquelle ont été ajoutées **uniquement**
les primitives de dossier de `CLIENTS`, recopiées à l'identique (mêmes ACE) :

- `Group/Root` : dossier `%SECONDARY_GROUP%` (`Root=1`, `PropagateACL=1`) ;
- `User/Root` : racine `%SECONDARY_GROUP%`, puis un seul dossier `%USER_ID%`
  (`Root=1`, `PropagateACL=1`).

Pas de `Share`, `CONFIG`, `Web.zip`, `Bienvenue.htm`, Documents/Desktop/…,
`HomePath`, profil (RDS compris), script d'ouverture de session ni quota. Règle
de nommage inchangée : `%FIRST_NAME[10]%.%NAME[9]%`. Arborescence produite :

```
F:\KoXoDATA\CLIENTS DEV\<DEV-CLI-XXXXXX>\<prenom.nom>\
```

Patch du 2026-09-27 : SHA-256 avant
`2BDF89EEEE3167C92EE7720DAF9BBF692064FFC107B3D641D433105F008E1A2C`, après
`13B1DD3B960B3F0C56E5C065B89DC75780DEB34F35B3D0C848292E44EE6D3CF6`. Sauvegarde :
`Data\CSVSynchro-DEV\backups\model-clients-dev-20260927-105211\`.

## Tests d'isolation (2026-09-27) : PASS

Chaque passage lance KoXoAdm **en SYSTEM** par une tâche planifiée à usage
unique (même identité que le receveur), via `Invoke-KoxoProcess`, donc sous le
mutex `Global\Kermaria-KoXoAdm`, délai 1800 s. Instantanés AD (`OU=KoXoAdm`,
`GG_*`, `CLIENTS*`) et empreintes des fichiers KoXo avant et après.

**Run 1** : `/Synchro=CLIENTS-DEV.xml`, une ligne `CLI-D999901` /
`DEV-CLI-TST001` (identité fictive). PASS :
- OU et groupe `DEV-CLI-TST001` créés sous `CLIENTS DEV`, groupe membre de
  `CLIENTS DEV` ;
- utilisateur `devkoxo.testisola`, `employeeNumber=CLI-D999901`, membre de
  `DEV-CLI-TST001` seulement, sans `homeDirectory`, profil ni script, ouverture
  de session RDS refusée ;
- aucun changement hors `CLIENTS DEV` (`CLIENTS`, `CLIENTS DÉMO`, `GG_*`,
  fichiers KoXo PROD, `F:\KoXoDATA\CLIENTS`).

**Run 2** : même profil, la ligne `CLI-D999901` remplacée par `CLI-D999902`
(`devkoxo2.testisola`). PASS :
- `devkoxo.testisola` conservé, non supprimé, toujours membre de
  `DEV-CLI-TST001`, **désactivé** (`userAccountControl` 66048 → 66050) ;
- `devkoxo2.testisola` créé, actif, `employeeNumber=CLI-D999902`, membre de
  `DEV-CLI-TST001` ;
- aucun changement hors `CLIENTS DEV`, aucune écriture de stockage provoquée
  par `/Synchro`.

Les objets de test (`DEV-CLI-TST001`, les deux comptes, le dossier) sont
conservés.

## E2E identité standard DEV — 2026-09-28 — PASS

Le parcours manuel complet a confirmé, sans action sur PROD :

1. Signup et vérification e-mail : PASS.
2. Approbation : création atomique du client `DEV-CLI-PDXVX6`, de
   `CLI-D000001` et du PIB `awaiting_password`.
3. `signup_approved` : webhook DEV reçu avec `202`; avant password setup,
   l'export ne publiait aucune identité et le profil `CLIENTS DEV` a été
   ignoré sans CSV ni KoXoAdm.
4. Password setup : PIB `awaiting_password → koxo_pending`; webhook
   `password_set` reçu avec `202`.
5. Export : `clients-dev.csv` a porté exactement une identité DEV,
   `DEV-CLI-PDXVX6` / `CLI-D000001` / `CLIENTS DEV`. Les valeurs sensibles de
   la colonne mot de passe ne sont ni relues ni documentées.
6. `/Synchro=CLIENTS-DEV.xml` : mutex pris puis libéré, KoXoAdm terminé avec
   exit `0` et marqueur `synchronized_and_launched`; aucun
   `RepairSecondaryGroup`, `RepairUser` ni repair storage.
7. AD : OU/groupe `DEV-CLI-PDXVX6` créé sous `CLIENTS DEV`; Melis Rochedune
   active, `employeeNumber=CLI-D000001`, membre du seul groupe secondaire.
   Aucun `GG_VPN` ni `GG_RDS`; les groupes `CLIENTS` et `CLIENTS DÉMO` sont
   restés inchangés.
8. Convergence : objet adopté strictement par `employeeNumber`,
   `customer_ad_links` créé, secret KoXo acquitté, PIB `completed` et
   `PRIMARY_IDENTITY_COMPLETED` observé.

Le stockage n'a pas fait partie de l'E2E et reste non validé en headless.

### Dettes explicitement conservées

- `scripts/dev-env/Install-ApiInternalDev.ps1` ne doit pas être utilisé : il
  régénère la configuration DEV depuis une source contenant encore des valeurs
  LIVE/PROD.
- `dev.env.ps1` / la source DEV associée doit être assainie avant toute
  réutilisation globale ; aucun secret ne doit être ajouté au dépôt.
- `RepairSecondaryGroup Type=Storage` headless n'est pas validé.
- `CLIENTS DEV DEMO` n'existe pas encore.
- Les objets de test Run 1/2 sont conservés.

### `/Synchro` et stockage : deux opérations distinctes

- **`/Synchro`** fait l'identité AD : création, `employeeNumber`, appartenance,
  cycle de vie, orphelins. Il ne crée **aucun** dossier.
- **La réparation de stockage** matérialise l'arborescence
  (`/RepairSecondaryGroup … Type="Storage"`, `/RepairUser … Type="Storage"`,
  utilisés par la route stockage du receveur).

Après Run 1, une **réparation complète interactive** de `CLIENTS DEV` depuis
l'IHM KoXo a créé `F:\KoXoDATA\CLIENTS DEV\DEV-CLI-TST001\devkoxo.testisola\` :
le modèle DEV produit bien l'arborescence attendue.

**HEADLESS STORAGE REPAIR : NOT YET VALIDATED.** En SYSTEM,
`/RepairSecondaryGroup Group="DEV-CLI-TST001" PrimaryGroup="CLIENTS DEV" Type="Storage"`
s'est bloqué juste après « Réparation de type "Stockage" » : 0 % CPU, threads
en attente `UserRequest` (vraisemblablement une boîte de dialogue invisible),
aucune écriture sur FS-01. Arrêté par le délai de 1800 s, `RepairUser` non
lancé, mutex tenu pendant tout ce temps. Ne pas présenter la matérialisation
headless comme opérationnelle.

### Analyse code-only du blocage headless — 2026-09-28

Preuves convergentes, sans nouvelle exécution KoXo :

- **Confirmé par le code.** `Get-KoxoStorageRepairArguments` construit
  exactement `/RepairSecondaryGroup … Type="Storage"` ou
  `/RepairUser … Type="Storage"`. `Invoke-KoxoStorageReconcile` transmet cet
  argument sans drapeau de non-interactivité à `Invoke-KoxoProcess`.
- **Confirmé par les tests.** `KoxoStorage.Tests.ps1` injecte un
  `RepairInvoker` de remplacement ; il valide les arguments, le rollback et la
  relecture XML, mais ne lance jamais `KoXoAdm.exe`. Il ne peut donc pas
  prouver le comportement SYSTEM.
- **Fait d'exploitation déjà mesuré.** L'appel réel SYSTEM a attendu
  `UserRequest`, sans CPU ni écriture, jusqu'au délai de 1800 secondes, alors
  que la réparation complète interactive créait l'arborescence attendue.
- **Hypothèse la plus probable (non prouvée par le code).** KoXoAdm attend une
  interaction native pour `Type="Storage"`; sous SYSTEM, cette interaction est
  invisible. Aucun commutateur headless ou mécanisme de réponse à un dialogue
  n'apparaît dans le wrapper versionné.

Décision sûre tant qu'un test KoXo réel autorisé n'a pas levé ce doute : ne pas
déclencher `RepairSecondaryGroup` ou `RepairUser` avec `Type="Storage"` depuis
un worker headless. Conserver la synchronisation `/Synchro=CLIENTS-DEV.xml`
distincte et ne pas la détourner pour créer les dossiers. Les options à valider
avec Zachary et KoXo sont, dans cet ordre : un commutateur fournisseur documenté
et réellement non interactif, puis un worker explicitement interactif et isolé
avec journal/timeout ; aucune réparation ne doit être simulée par une écriture
directe sur FS-01 ou AD.

## Incident FS-01 du 2026-09-27 (création des lieux de stockage)

Pendant la création des lieux de stockage dans l'IHM, un lieu a été créé à
10:30:42 sur `F:\KoXoDATA\` lui-même, puis supprimé à 10:31:10. KoXo a alors
**tenté de supprimer tout `F:\KoXoDATA\`** ; l'opération a échoué
(`[ERROR] Suppression du répertoire`). Aucune donnée perdue (arborescence et
dates vérifiées). Effets restants, **non corrigés** à ce jour :

- `F:\KoXoDATA` : l'ACE `HOME\Administrateurs de KoXo Administrator` a été
  remplacée par `CLIENTS\CLIENTS-KOXO-ADM` ;
- `F:\KoXoDATA\CLIENTS` a perdu sa protection d'héritage (mêmes ACE, désormais
  héritées) ; `F:\KoXoDATA\CLIENTS DEV` hérite aussi ;
- les autres arbres (`CLASSES`, `CULTUREVAP`, `ELEVES`, …) sont protégés et
  inchangés.

Correctif proposé, non appliqué : rendre explicites et protégées les ACL de
`CLIENTS` et de `CLIENTS DEV`, **puis** rétablir l'ACE HOME sur `F:\KoXoDATA`.
Leçon : ne jamais créer un lieu de stockage sur la racine `KoXoDATA`, sa
suppression tente d'effacer le dossier.

Hors périmètre KoXo : `Utilisateurs DHCP` et `Administrateurs DHCP` sont apparus
dans `CN=Users,DC=clients,DC=home,DC=bzh` le 2026-09-27 à 07:37:19 UTC (SRV-21
est contrôleur de domaine et serveur DHCP). Hors `OU=KoXoAdm`, vides, non
attribués ; ne pas y toucher.

## Pourquoi

DEV et PROD partagent le domaine `clients.home.bzh` et le **même** `KoXoAdm.exe`
sur SRV-21. Constaté le 2026-09-26 :

- la PROD porte déjà `employeeNumber` `CLI-000001`, `CLI-000002` et `CLI-000003` ;
- le compteur `koxo_identifier_counters` de `kermaria_dev` repart de 1. Le
  premier compte DEV aurait donc reçu `CLI-000001` ;
- le lanceur de synchronisation rechargeait sans condition les variables
  Machine `KOXO_*`, donc l'URL et le jeton de la PROD.

Chaque synchronisation KoXo fait autorité sur son profil : une ligne absente du
CSV désactive le compte correspondant. Une synchronisation alimentée par la DEV
dans l'espace de la PROD réécrirait des comptes PROD ou les désactiverait.

## Les deux instances

| | PROD (en service) | DEV (en place, isolation validée) |
|---|---|---|
| Receveur SRV-21 | `http://+:8042/internal/koxo/sync/`, tâche `Kermaria-KoXoWebhookReceiver-8042` | `http://+:8043/internal/koxo/sync/`, tâche `Kermaria-KoXoWebhookReceiver-DEV-8043` |
| Lanceur | `Start-KoxoSyncWebhookReceiver-8042.cmd` (inchangé) | `Start-KoxoSyncWebhookReceiver-Instance.cmd <définition.json>` |
| Configuration | variables Machine `KOXO_*` | fichier de définition JSON (`scripts/koxo/instances/koxo-instance.dev.example.json`) |
| Source de l'export | `https://dashboard.zacharyhounsa.ovh/api/internal/koxo/users` → API PROD `:5000` → `kermaria` | `https://dev.zachary-it.fr/api/internal/koxo/users` → API DEV `:5100` → `kermaria_dev` |
| Profil KoXo | `CLIENTS` (`/Synchro=CLIENTS.xml`, `clients.csv`) et `CLIENTS DÉMO` (`/Synchro=CLIENTS-DEMO.xml`, `clients-demo.csv`) | `CLIENTS DEV` (`/Synchro=CLIENTS-DEV.xml`, `clients-dev.csv`) |
| Identifiants (`employeeNumber`) | `CLI-000001`, `CLI-000002`… | `CLI-D000001`, `CLI-D000002`… |
| Références client (OU) | `CLI-XXXXXX`, essais `DEMO-CLI-XXXXXX` | `DEV-CLI-XXXXXX`, essais `DEMO-DEV-CLI-XXXXXX` |
| Route de stockage | ouverte | fermée (`storageRouteEnabled: false`) |

## API : namespace KoXo

Quatre clés, **toutes absentes en PROD** : leur défaut est l'espace historique,
au bit près.

| Clé | Défaut (PROD) | DEV cible |
|---|---|---|
| `KOXO_IDENTIFIER_PREFIX` | `CLI-` | `CLI-D` |
| `CUSTOMER_REFERENCE_PREFIX` | `CLI-` | `DEV-CLI-` |
| `KOXO_PRIMARY_GROUP_CLIENTS` | `CLIENTS` | `CLIENTS DEV` |
| `KOXO_PRIMARY_GROUP_DEMO` | `CLIENTS DÉMO` | `CLIENTS DEV DEMO` |

- Code : `apps/api-internal/Services/KoxoNamespace.cs`. Le namespace est résolu
  une fois au démarrage et journalisé (`KoXo namespace | …`). L'allocateur
  d'identifiants, le générateur de références, la validation `employeeNumber`
  (export, adoption, stockage) et l'aiguillage vers le groupe primaire le lisent.
- **Tout ou rien.** Un namespace partiel est refusé au démarrage, par exemple
  des identifiants DEV avec le groupe `CLIENTS`. Une référence hors production
  ne peut commencer ni par `CLI-` ni par `DEMO-`.
- **Validation à double sens.** La PROD refuse `CLI-D000001`, la DEV refuse
  `CLI-000001`. Un export DEV contenant un identifiant de production échoue en
  entier (fail-closed).
- **Disjonction prouvée.** Le test parcourt les 1 000 000 rangs à six chiffres :
  aucune valeur DEV n'égale une valeur PROD.
- **Longueurs.** Identifiant DEV : 11 caractères (colonnes `VARCHAR(32)`).
  Référence DEV : 14 caractères (`koxo_group_reference VARCHAR(32)`,
  `external_reference VARCHAR(80)`). OU d'essai DEV : 19 caractères.
- **Garde-fou `APP_ENV`** (`DeploymentEnvironmentGuard`) :
  - `APP_ENV=Development` refuse le namespace de production dès que
    `AD_INTEGRATION_MODE=controlled_write` ou `KOXO_SYNC_WEBHOOK_URL` est posé ;
  - `APP_ENV=Production` refuse tout namespace hors production ;
  - un namespace invalide est refusé partout.

La DEV actuelle (AD `test`, pas de webhook) démarre donc inchangée.

## Scripts SRV-21 : lanceur paramétrable

Primitive commune : `Resolve-KoxoSyncLaunchPlan` (module `KoxoSync.Common.psm1`).

- **Sans `-InstanceConfigPath` (PROD).** Plan identique à l'ancien lanceur :
  - rechargement des variables Machine `KOXO_*` non vides ;
  - `KOXO_OTHER_CSV_PATHS` neutralisée ;
  - deux profils `CLIENTS` et `CLIENTS DÉMO`, mêmes CSV, mêmes fichiers de
    profil, même répertoire de travail.

  Le `.cmd` 8042 et les valeurs par défaut du receveur sont inchangés.
- **Avec `-InstanceConfigPath` (instance isolée).**
  - Toute variable `KOXO_*` est **retirée** du processus.
  - Chaque réglage est fixé par surcharge explicite depuis la définition JSON.
    Une variable Machine ne peut donc ni fournir ni remplacer un paramètre de
    l'instance.
  - Les jetons sont lus dans des fichiers (`apiTokenPath`, `receiver.tokenPath`),
    jamais passés en ligne de commande ni journalisés.
- **La définition est refusée** si elle reprend un élément de la PROD :
  - nom `prod`, préfixe `CLI-`, port 8042 ;
  - groupe `CLIENTS` ou `CLIENTS DÉMO` ;
  - fichier `clients.csv` ou `clients-demo.csv` ;
  - profil `/Synchro=CLIENTS.xml` ou `/Synchro=CLIENTS-DEMO.xml` ;
  - répertoire `CSVSynchro\work` ou `CSVSynchro\Logs` de la PROD ;
  - URL ou jeton identiques aux variables Machine de la PROD. La comparaison se
    fait sans jamais afficher la valeur.
- **`-PlanOnly`** rend le plan effectif, sans jeton, et s'arrête : aucun appel à
  l'API, aucun CSV, aucun KoXoAdm.
- **Validation du payload.** Elle suit le préfixe de l'instance :
  `identifiantUnique must match CLI-000000.` en PROD, `CLI-D000000` en DEV.

```powershell
# Vérifier le plan d'une instance, sans rien exécuter
.\Invoke-KoxoSyncFromWebhook.ps1 -SyncScriptPath .\Sync-KoXoClients.ps1 `
    -InstanceConfigPath 'C:\ProgramData\Kermaria\koxo-dev\koxo-instance.dev.json' -PlanOnly
```

## Verrou inter-instance KoXoAdm

- **Mutex système** `Global\Kermaria-KoXoAdm`. Il est posé dans
  `Invoke-KoxoProcess`, seul point de lancement de `KoXoAdm.exe`, pour la
  synchronisation comme pour la réconciliation de stockage. Il couvre deux
  instances, ou deux passages PROD.
- **Nom non configurable** par une instance : un nom propre à la DEV annulerait
  le verrou.
- **Délai explicite.** `KOXO_ADM_LOCK_TIMEOUT_SECONDS` vaut 600 s par défaut et
  se règle via `admLockTimeoutSeconds` dans une définition d'instance. Au-delà :
  - échec `KOXO_ADM_LOCK_TIMEOUT`, qui nomme le verrou et l'instance demandeuse ;
  - le journal porte l'erreur ;
  - `KoXoAdm.exe` n'est pas lancé.
- **Libération.** Le verrou est libéré dans un `finally`, donc aussi quand le
  passage échoue. Un détenteur tué le rend « abandonné » : il est alors repris et
  journalisé en avertissement.
- **Tenue.** Il est tenu jusqu'après la lecture du journal KoXo, pour que ce
  journal soit bien celui du passage.
- Le verrou fichier historique (`koxo-sync.lock`, par répertoire de journaux)
  est conservé.

## Tests

- **API** — `dotnet tests/api-internal/bin/Release/net10.0-windows/Kermaria.ApiInternal.SmokeTests.dll --koxo-namespace`,
  également inclus dans le smoke complet. Il couvre :
  - PROD au bit près ;
  - la disjonction sur un million de rangs ;
  - les références et les OU ;
  - les refus du résolveur ;
  - le caractère définitif de l'initialisation ;
  - l'export DEV sur fixtures, y compris le refus d'un identifiant PROD.

  Les garde-fous `APP_ENV` sont dans la suite de démarrage du smoke.
- **Scripts** — `Invoke-Pester scripts/koxo/tests` (Pester 3.4). Le fichier
  `KoxoInstance.Tests.ps1` couvre :
  - la compatibilité PROD ;
  - l'instance isolée non écrasée par des variables `KOXO_*` héritées ;
  - les refus de la définition ;
  - l'absence de jeton dans le plan ;
  - le verrou en processus réels : délai, reprise après abandon, libération sur
    erreur, et deux instances concurrentes sérialisées.

## Validations runtime (état au 2026-09-27)

1. **Isolation par profil dans KoXoAdm** : *validée* (Run 1 et Run 2). Une
   synchronisation `CLIENTS DEV` ne touche ni `CLIENTS` ni `CLIENTS DÉMO`, et
   son traitement des orphelins reste borné à `CLIENTS DEV`.
2. **Formes acceptées par KoXo** : *validées* pour `CLI-D999901` et
   `CLI-D999902` (`employeeNumber`) et pour `DEV-CLI-TST001` (OU et groupe sous
   `CLIENTS DEV`). `DEMO-DEV-CLI-XXXXXX` n'est pas testé (point 5).
3. **Emplacement du CSV DEV** : *fait le 2026-09-26*. Le profil
   `CLIENTS-DEV.xml` lit `Data\CSVSynchro-DEV\clients-dev.csv`, le chemin de la
   définition d'instance.
4. **Adresse source vue par le WebPortal DEV** : *vérifiée le 2026-09-27*. La
   route `/api/internal/koxo/users` prend la première entrée de
   `X-Forwarded-For`, puis `X-Real-IP`. Le vhost DEV de SRV-11 écrase les deux
   avec `$remote_addr` : SRV-12 `:3100` reçoit `192.168.100.221` pour SRV-21,
   y compris quand le client envoie une valeur usurpée.
   `KOXO_EXPORT_ALLOWED_IPS` peut donc s'appuyer sur `192.168.100.221` (non
   posé).
5. **Profil de démonstration DEV** : *non fait*. Tant que `CLIENTS DEV DEMO`
   n'existe pas dans KoXo et n'est pas déclaré dans la définition, un essai DEV
   bloque toute la synchronisation DEV (aucun profil ne le réclame). C'est un
   refus sûr, mais il faut le savoir.
6. **Stockage** : *résolu*. Lieux de stockage DEV dédiés sur FS-01, aucune
   référence au stockage PROD. La matérialisation headless reste **non
   validée** (voir plus haut).
7. **IHM KoXo fermée pendant un test** : règle. L'IHM ne prend pas le mutex
   `Global\Kermaria-KoXoAdm` et peut écrire dans `Data\Users` : la fermer avant
   de lancer KoXoAdm en ligne de commande.
