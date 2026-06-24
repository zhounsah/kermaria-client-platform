# Exploitation V0.21

## Objectif

Ce runbook couvre l'installation, la validation, le demarrage, la supervision
de base et le rollback en environnement local, staging ou preproduction
controlee.

## Prerequis

- Node.js 24 et npm ;
- SDK .NET 10 ;
- clients MariaDB `mysql` et `mysqldump` si operations SQL reelles ;
- secrets injectes hors Git ;
- `AD_INTEGRATION_MODE=disabled`.

Sous PowerShell restrictif, utiliser `npm.cmd`.

## Fresh clone

```powershell
git clone <URL_DU_DEPOT>
Set-Location .\kermaria-client-platform
npm.cmd install
dotnet restore
npm.cmd run validate
```

## Configuration

Injecter les variables dans le processus ou dans le gestionnaire de secrets de
l'hote. `.env.example` reste un inventaire, jamais un fichier de production.

Variables critiques WEBPORTAL :

- `NODE_ENV`
- `INTERNAL_API_URL`
- `SERVICE_AUTH_TOKEN`
- `SESSION_COOKIE_NAME`
- `SESSION_COOKIE_SECURE`
- `SESSION_COOKIE_SAME_SITE`

Variables critiques API-INTERNAL :

- `ASPNETCORE_ENVIRONMENT`
- `DOTNET_ENVIRONMENT`
- `SQL_PROVIDER`, `SQL_HOST`, `SQL_PORT`, `SQL_DATABASE`, `SQL_USERNAME`,
  `SQL_PASSWORD`
- `SERVICE_AUTH_TOKEN`
- `SESSION_DURATION_MINUTES`
- `LOGIN_MAX_FAILURES`
- `LOGIN_LOCKOUT_MINUTES`
- `AD_INTEGRATION_MODE=disabled`
- `BPCE_INTEGRATION_MODE=disabled|mock|live` (defaut `disabled`)
- `BPCE_BASE_URL`, `BPCE_REFRESH_TOKEN` (secret), `BPCE_SENDER_ID`
- `LOG_FILE_DIRECTORY`, `LOG_FILE_LEVEL`, `LOG_FILE_RETENTION_DAYS`
  (journalisation fichier rotative quotidienne, voir
  `apps/api-internal/Infrastructure/FileLoggerProvider.cs`)

Paiement et reglement (V0.21) :

- `PAYPAL_MODE=sandbox|live`
- `PAYPAL_CLIENT_ID`, `PAYPAL_CLIENT_SECRET`
- `BILLING_IBAN`, `BILLING_BIC`, `BILLING_TRANSFER_LABEL`,
  `BILLING_PAYPAL_URL`

## Validations

Toujours commencer par :

```powershell
npm.cmd run validate
```

Validation staging :

```powershell
$env:NODE_ENV="production"
$env:ASPNETCORE_ENVIRONMENT="Staging"
$env:DOTNET_ENVIRONMENT="Staging"
npm.cmd run validate:staging
```

Validation preproduction :

```powershell
$env:NODE_ENV="production"
$env:ASPNETCORE_ENVIRONMENT="Production"
$env:DOTNET_ENVIRONMENT="Production"
npm.cmd run validate:preprod
```

Validation MariaDB opt-in :

```powershell
npm.cmd run validate:mariadb
```

Validation des health checks :

```powershell
npm.cmd run check:health
```

## Demarrage local

API-INTERNAL :

```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
$env:DOTNET_ENVIRONMENT="Development"
$env:AD_INTEGRATION_MODE="disabled"
$env:BPCE_INTEGRATION_MODE="disabled"
dotnet run --project .\apps\api-internal\Kermaria.ApiInternal.csproj --urls http://localhost:5000
```

Pour tester la facturation BPCE en local, basculer en `mock` (aucun appel
sortant) ou exceptionnellement en `live` (refresh token requis, emission
fiscale reelle).

Verification du sender BPCE (lecture seule) :

```powershell
$env:BPCE_INTEGRATION_MODE="live"
$env:BPCE_REFRESH_TOKEN="<inject_depuis_secret_local>"
dotnet run --project .\apps\api-internal\Kermaria.ApiInternal.csproj -- --verify-bpce-sender
```

WEBPORTAL :

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

## Health checks

PowerShell :

```powershell
Invoke-RestMethod http://localhost:5000/health/live
Invoke-RestMethod http://localhost:5000/health/ready
Invoke-RestMethod http://localhost:5000/ready
Invoke-RestMethod http://localhost:3000/api/health/live
Invoke-RestMethod http://localhost:3000/api/health/ready
```

Attendus :

- HTTP 200 sur `live` ;
- HTTP 200 sur `ready` uniquement si configuration et dependances sont saines ;
- `X-Correlation-Id` present ;
- reponse JSON sans contenu sensible.

## Logs

Surveiller au minimum :

- echec de readiness ;
- refus d'acces admin ou interservice ;
- erreurs MariaDB synthétiques ;
- lockouts et echecs de connexion ;
- audits importants ;
- correlation id, code HTTP et duree ;
- erreurs BPCE (status code retourne par la banque) sans corps complet
  cote logs publics ;
- erreurs PayPal Create/Capture sans le `client_secret` ni le corps
  complet de la reponse ;
- absence de token, cookie, mot de passe, chaine de connexion, secret
  BPCE/PayPal et montant complet de facture.

La journalisation fichier est activee si `LOG_FILE_DIRECTORY` est defini.
Les fichiers `api-internal-YYYY-MM-DD.log` plus anciens que
`LOG_FILE_RETENTION_DAYS` (defaut 30) sont purges au demarrage suivant.

## Checklist staging

1. `git status` ne montre aucun fichier sensible.
2. `npm run validate` reussit.
3. `npm run validate:staging` reussit.
4. `npm run check:health` reussit sur les URLs de staging.
5. Les cookies sont `HttpOnly`, `Secure` et `SameSite` conformes.
6. Les headers de securite V0.19 sont servis.
7. `AD_INTEGRATION_MODE=disabled`.
8. Les secrets restent hors Git.
9. La checklist de recette V0.19 est planifiee.

## Checklist preproduction

1. `git status`
2. `npm run check:secrets`
3. `npm run validate:preprod`
4. `npm run validate:mariadb` si disponible
5. `npm run build`
6. `npm run validate`
7. `npm run check:health` si services demarres
8. `git diff --check`
9. execution de `docs/V0.17_RECETTE_PREPRODUCTION.md`

## Rollback

1. Retirer `WEBPORTAL` du trafic.
2. Conserver `AD_INTEGRATION_MODE=disabled`,
   `BPCE_INTEGRATION_MODE=disabled` et `PAYPAL_MODE=sandbox`.
3. Restaurer l'artefact precedent.
4. Si migration en cause (007/008/009 incluses), restaurer la sauvegarde
   validee.
5. Redemarrer `API-INTERNAL` puis `WEBPORTAL`.
6. Rejouer les health checks.
7. Verifier login client, login admin et refus de role croise.
8. Si une facture BPCE a ete emise par erreur, elle reste immuable cote
   banque : creer un avoir cote dashboard BPCE plutot que d'essayer de la
   "supprimer" cote application.
