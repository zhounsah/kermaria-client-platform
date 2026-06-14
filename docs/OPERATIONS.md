# Exploitation V0.9

## Objectif

Ce runbook couvre l'installation, la validation, le démarrage, la supervision
de base et le rollback en environnement local ou de pré-production contrôlé.
Il ne remplace pas la gestion des services, certificats, pare-feu et secrets de
l'infrastructure cible.

## Prérequis

- Node.js 24 et npm ;
- SDK .NET 10 ;
- clients MariaDB `mysql` et `mysqldump` pour les opérations de base ;
- accès réseau privé de `API-INTERNAL` vers MariaDB ;
- secrets injectés hors Git ;
- `AD_INTEGRATION_MODE=disabled`.

Sous PowerShell avec une politique d'exécution restrictive, utiliser `npm.cmd`
à la place de `npm`.

## Fresh clone

```powershell
git clone <URL_DU_DEPOT>
Set-Location .\kermaria-client-platform
npm.cmd install
dotnet restore
npm.cmd run validate
```

```bash
git clone <URL_DU_DEPOT>
cd kermaria-client-platform
npm install
dotnet restore
npm run validate
```

`npm run validate` n'utilise pas MariaDB réelle. Il exécute le scan de secrets,
le lint, les typechecks, les builds et les smoke tests V0.8/V0.9.

## Configuration locale

Injecter les variables dans le processus ou dans le gestionnaire de secrets de
l'hôte. `.env.example` est un inventaire, pas un fichier de production.

Variables critiques API :

- `ASPNETCORE_ENVIRONMENT` et `DOTNET_ENVIRONMENT` ;
- `SQL_PROVIDER`, `SQL_HOST`, `SQL_PORT`, `SQL_DATABASE`, `SQL_USERNAME`,
  `SQL_PASSWORD` ;
- `SERVICE_AUTH_TOKEN` ;
- `SESSION_DURATION_MINUTES` ;
- `AD_INTEGRATION_MODE=disabled`.

Variables critiques WEBPORTAL :

- `NODE_ENV` ;
- `INTERNAL_API_URL` ;
- `SERVICE_AUTH_TOKEN`, identique à API-INTERNAL ;
- `SESSION_COOKIE_NAME` ;
- `SESSION_COOKIE_SECURE=true` hors développement local.

En Production, API-INTERNAL refuse les secrets absents ou manifestement
factices. WEBPORTAL retourne une readiness en échec si son URL interne ou son
token interservice sont invalides. `INTERNAL_API_URL` ne doit pas pointer vers
localhost, sauf dérogation explicite `ALLOW_LOCAL_INTERNAL_API_URL=true`.

## Migration et seed

Toujours réaliser une sauvegarde avant migration. Le runtime normal n'applique
jamais les migrations.

```powershell
dotnet run --project .\apps\api-internal\Kermaria.ApiInternal.csproj -- --apply-migrations
```

Seed fictif, uniquement en `Development` et uniquement si les variables
`DEMO_*` sont injectées :

```powershell
dotnet run --project .\apps\api-internal\Kermaria.ApiInternal.csproj -- --apply-migrations --seed-demo-data
```

## Démarrage local

API-INTERNAL :

```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
$env:DOTNET_ENVIRONMENT="Development"
$env:AD_INTEGRATION_MODE="disabled"
dotnet run --project .\apps\api-internal\Kermaria.ApiInternal.csproj --urls http://localhost:5000
```

WEBPORTAL, dans un second terminal :

```powershell
$env:INTERNAL_API_URL="http://localhost:5000"
$env:ALLOW_LOCAL_INTERNAL_API_URL="true"
npm.cmd run dev:web
```

## Build et lancement serveur

```powershell
npm.cmd run build:web
npm.cmd run build:api
dotnet .\apps\api-internal\bin\Release\net10.0\Kermaria.ApiInternal.dll --urls http://127.0.0.1:5000
npm.cmd --prefix apps/webportal run start
```

Sous Linux, lancer les mêmes DLL et scripts sous des comptes système non
privilégiés. La supervision de processus peut être assurée par `systemd` pour
WEBPORTAL et par un service Windows dédié pour API-INTERNAL.

## Health checks

PowerShell :

```powershell
Invoke-RestMethod http://localhost:5000/health/live
Invoke-RestMethod http://localhost:5000/health/ready
Invoke-RestMethod http://localhost:3000/api/health/live
Invoke-RestMethod http://localhost:3000/api/health/ready
```

Linux :

```bash
curl --fail http://localhost:5000/health/live
curl --fail http://localhost:5000/health/ready
curl --fail http://localhost:3000/api/health/live
curl --fail http://localhost:3000/api/health/ready
```

`live` vérifie uniquement le processus. `ready` vérifie la configuration et,
pour API-INTERNAL, `SELECT 1` sur MariaDB. La readiness WEBPORTAL appelle la
readiness API par le réseau serveur. HTTP 503 interdit la mise en trafic.

## Validation MariaDB opt-in

PowerShell :

```powershell
$env:RUN_MARIADB_TESTS="true"
npm.cmd run test:api
```

Alternative portable :

```powershell
npm.cmd run validate:mariadb
```

Linux :

```bash
RUN_MARIADB_TESTS=true npm run test:api
```

Les variables SQL et démo doivent déjà être définies. Aucun secret n'est lu
depuis Git.

## Logs

Les logs JSON API doivent être collectés par l'hôte sans enregistrer les
headers complets, cookies, tokens, mots de passe ou chaînes de connexion.
Surveiller au minimum :

- échecs de readiness ;
- erreurs MariaDB synthétiques ;
- lockouts et échecs de connexion ;
- refus d'accès admin et d'identité interservice ;
- échecs de persistance d'audit ;
- espace disque et état des sauvegardes.

## Rollback

1. Retirer WEBPORTAL du trafic.
2. Conserver `AD_INTEGRATION_MODE=disabled`.
3. Restaurer l'artefact applicatif précédent.
4. Si une migration est en cause, restaurer la sauvegarde validée selon
   [BACKUP_RESTORE.md](BACKUP_RESTORE.md).
5. Redémarrer API-INTERNAL puis WEBPORTAL.
6. Vérifier les quatre health checks.
7. Tester login client, login admin, refus admin client et une lecture métier.

## Checklist pré-production

1. `git status` ne montre aucun fichier sensible ou généré.
2. Secrets injectés hors Git et tournés si exposés.
3. `npm run validate` réussit.
4. Sauvegarde MariaDB réalisée avant migration.
5. Migration appliquée explicitement.
6. Tests MariaDB opt-in réussis.
7. Les quatre health checks répondent 200.
8. Login client et admin validés.
9. `/admin` est refusé à `client_user`.
10. Demandes support/service retournent `persisted:true`.
11. Logs inspectés sans secret.
12. Headers HTTP et `X-Robots-Tag` vérifiés.
13. Restauration testée sur une base de test.
14. AD reste `disabled`.
15. Paiement et facturation réelle restent absents.
