# Isolation KoXo DEV / PROD — fondations logicielles

> **État au 2026-09-26 : seules les fondations LOGICIELLES existent.**
> L'infrastructure DEV décrite ici (receveur 8043, profil `CLIENTS DEV`, OU,
> compte de service, jetons) **n'est PAS déployée**. `controlled_write` reste
> interdit en DEV tant qu'elle ne l'est pas et que les validations runtime de
> la fin de ce document n'ont pas été faites.

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

| | PROD (en service) | DEV (cible, **non déployée**) |
|---|---|---|
| Receveur SRV-21 | `http://+:8042/internal/koxo/sync/`, tâche `Kermaria-KoXoWebhookReceiver-8042` | `http://+:8043/internal/koxo/sync/`, aucune tâche |
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

## À valider en runtime AVANT toute activation (non vérifié)

1. **Isolation par profil dans KoXoAdm.** Une synchronisation `CLIENTS DEV`
   ne doit désactiver ni modifier aucun compte des profils `CLIENTS` et
   `CLIENTS DÉMO`. La séparation actuelle `CLIENTS` / `CLIENTS DÉMO` le suggère
   sans le prouver pour un nouveau profil. À vérifier sur un CSV DEV factice,
   comptes PROD relevés avant et après.
2. **Longueur et forme acceptées par KoXo.** Vérifier que KoXo accepte
   `CLI-D000001` comme `identifiantUnique` (reporté dans `employeeNumber`) et
   `DEV-CLI-XXXXXX` / `DEMO-DEV-CLI-XXXXXX` comme `GroupeSecondaire`, et que
   les OU sont bien créées sous l'OU du profil `CLIENTS DEV`.
3. **Emplacement du CSV DEV** lu par le profil `CLIENTS-DEV.xml`, à aligner
   sur `csvTargetPath`.
4. **Adresse source vue par le WebPortal DEV.** La route
   `/api/internal/koxo/users` prend la première entrée de `X-Forwarded-For`,
   puis `X-Real-IP`. Il faut vérifier que SRV-11 transmet bien l'adresse de
   SRV-21 avant de poser `KOXO_EXPORT_ALLOWED_IPS`. Cet en-tête n'est fiable
   que si le proxy l'écrase.
5. **Profil de démonstration DEV.** Tant que `CLIENTS DEV DEMO` n'existe pas
   dans KoXo et n'est pas déclaré dans la définition, un essai DEV bloque toute
   la synchronisation DEV (aucun profil ne le réclame). C'est un refus sûr,
   mais il faut le savoir.
