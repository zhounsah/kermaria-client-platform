# AGENTS.md

Ce fichier s'applique a tout le depot `kermaria-client-platform`.

## Architecture A Ne Pas Casser

- Flux obligatoire : `browser -> WEBPORTAL / BFF -> API-INTERNAL -> MariaDB`.
- `apps/webportal` est le portail Next.js et le BFF public ; il ne contacte jamais MariaDB, AD, NAS, RDS, VPN ni BPCE directement.
- `apps/api-internal` est l'API ASP.NET Core privee et le seul composant autorise a parler a MariaDB, AD, BPCE, SMTP et aux integrations internes.
- `packages/shared` contient seulement des contrats TypeScript non sensibles ; ne pas y mettre d'URL interne, secret ou logique serveur.
- L'architecture applicative reste limitee aux VM `WEBPORTAL` et `API-INTERNAL` ; utiliser le serveur SQL existant, ne pas ajouter de VM SQL.
- `API-INTERNAL` n'est pas exposee a Internet ; `/internal/*` exige `X-Service-Auth` hors `Development`.

## Git Et Orchestration Des Agents

- Git est la seule source de vérité du projet.
- Toute tâche commence par vérifier la racine du dépôt, la branche, l’index et
  le worktree.
- Ne jamais travailler en HEAD détachée.
- Une conversation ou une mémoire d’agent ne remplace pas la documentation
  versionnée.
- Les analyses architecture, tests, sécurité et documentation peuvent être
  parallélisées en lecture seule.
- Un seul agent d’écriture intervient sur un même groupe fonctionnel.
- Ne jamais lancer plusieurs agents modifiant les mêmes fichiers.
- Ne jamais restaurer globalement un snapshot, un patch ou une branche de
  sauvegarde.
- Restaurer les comportements groupe par groupe, avec tests et commits
  atomiques.
- Tout constat de revue doit être classé VALIDE, FAUX POSITIF ou INCERTAIN
  avant correction.
- Aucun agent ne commit, push, merge, rebase, tague ou déploie sans demande
  explicite.
- Avant de terminer une tâche, exécuter les validations applicables puis
  examiner :
  - git diff --check
  - git status --short
  - git diff

## Toolchain

- Node.js `>=24` avec npm et `package-lock.json` ; utiliser `npm install`, pas pnpm/yarn.
- .NET SDK fixe par `global.json` : `10.0.301` avec `rollForward: latestFeature` ; projets `net10.0`.
- `NuGet.Config` restaure dans `.nuget/packages` et lit aussi `.nuget-local` ; ne pas remplacer par une config globale.
- Sous PowerShell restrictif, remplacer `npm` par `npm.cmd`.
- Windows PowerShell 5.1 transforme chaque ligne stderr d'un executable natif en `ErrorRecord` : avec `$ErrorActionPreference = "Stop"`, un simple avertissement (le client MariaDB 12.x en emet un a chaque appel) coupe le processus en pleine execution. Dans un script, encadrer l'appel natif d'un `$ErrorActionPreference = "Continue"` et ne juger que sur `$LASTEXITCODE`.

## Commandes

- Installation : `npm install` puis `dotnet restore`.
- Dev API : `$env:ASPNETCORE_ENVIRONMENT="Development"; $env:DOTNET_ENVIRONMENT="Development"; $env:AD_INTEGRATION_MODE="disabled"; dotnet run --project apps/api-internal/Kermaria.ApiInternal.csproj --urls http://localhost:5000`.
- Dev web : `$env:INTERNAL_API_URL="http://localhost:5000"; $env:ALLOW_LOCAL_INTERNAL_API_URL="true"; npm run dev:web`.
- Verification web rapide : `npm run typecheck:webportal` puis `npm run lint:webportal` ; ajouter `npm run typecheck:shared` si `packages/shared` change.
- Verification web complete : `npm run check:web` lance typecheck shared, lint webportal, typecheck webportal, build webportal.
- Builds cibles : `npm run build:web` pour Next.js, `npm run build:api` pour ASP.NET Core.
- Validation globale : `npm run validate` execute `check:secrets`, lint/typecheck/build, smoke tests API et la plupart des contrats web.
- Contrats web cibles : `npm --prefix apps/webportal run test:<name>` pour `forms`, `auth`, `admin`, `operations`, `ux`, `workflow`, `notifications`, `replies`, `activity`, `commercial`, `ad-security`, `bpce`, `payments`, `subscriptions`.
- Attention : `payments` et `subscriptions` existent dans `apps/webportal/package.json`, pas comme scripts racine ; ne pas les lancer via `npm run test:payments` depuis la racine sauf si `package.json` change.
- Smoke API seul : `npm run test:api` ou `dotnet test tests/api-internal/Kermaria.ApiInternal.SmokeTests.csproj -c Release` ; le target MSBuild lance l'executable de test avec le DLL API construit.
- Health checks : `npm run check:health` attend API `http://127.0.0.1:5000` et WEBPORTAL `http://127.0.0.1:3000`, ou `API_INTERNAL_BASE_URL` / `WEBPORTAL_BASE_URL`.

## Env, Secrets Et Modes

- `.env.example` est un inventaire ; injecter les vraies valeurs hors Git.
- Ne jamais introduire `NEXT_PUBLIC_INTERNAL_API_URL`, `PUBLIC_INTERNAL_API_URL`, `NEXT_PUBLIC_SERVICE_AUTH_TOKEN` ou `PUBLIC_SERVICE_AUTH_TOKEN`.
- Garder `apps/webportal/lib/runtime-config.ts`, `internal-api.ts`, `auth.ts`, `session-cookie.ts` et `csrf-server.ts` server-only.
- Session : token brut uniquement dans cookie `HttpOnly` ; jamais de token/cookie en `localStorage` ou `sessionStorage`.
- `SERVICE_AUTH_TOKEN` doit correspondre entre WEBPORTAL et API-INTERNAL ; le BFF propage aussi `X-Portal-Session` et `X-Correlation-Id`.
- Non-`Development` refuse placeholders, `DEMO_*`, `SESSION_COOKIE_SECURE=false`, `RUN_MARIADB_TESTS=true` et SQL non MariaDB.
- `AD_INTEGRATION_MODE=disabled` par defaut ; pas de hard delete AD, reset password, OU production ou compte Domain Admin.
- En production (SRV-13), `controlled_write` est borne par `AD_ALLOWED_ROOTS` = `OU=KoXoAdm,DC=clients,DC=home,DC=bzh` et `OU=Groupes_TEST,DC=clients,DC=home,DC=bzh` sur le domaine `clients.home.bzh`. L'ancienne mention `OU=TEST_SITE_WEB,DC=home,DC=bzh` etait obsolete.
- `AD_USE_CURRENT_WINDOWS_CREDENTIALS` doit valoir `false` : a `true`, le code ignore `AD_SERVICE_ACCOUNT_USERNAME` et se lie sous l'identite du service Windows, qui n'a aucune delegation. Symptome trompeur : `AD_ACCESS_DENIED` alors que la delegation est correctement posee.
- `BPCE_INTEGRATION_MODE=live`, `PAYPAL_MODE=live` et `EMAIL_INTEGRATION_MODE=live` demandent validation explicite ; en phase de tests, pas de client reel, email externe reel ni prelevement recurrent actif.
- Ne pas journaliser tokens, cookies, mots de passe, chaines de connexion, `BPCE_REFRESH_TOKEN`, `PAYPAL_CLIENT_SECRET` ni montants complets de facture.

## MariaDB Et Migrations

- Le fallback mock est `Development` seulement ; MariaDB reelle est obligatoire en staging/preprod.
- Les migrations sont `apps/api-internal/Migrations/MariaDb/[0-9]*.sql` et sont separees par `-- statement-break`.
- Les migrations ne s'executent pas au demarrage normal ; commande explicite `Development` : `dotnet run --project apps/api-internal/Kermaria.ApiInternal.csproj -- --apply-migrations`.
- Le seed fictif exige aussi `--seed-demo-data` et les variables `DEMO_PORTAL_*` / `DEMO_INTERNAL_ADMIN_*` ; il est ignore hors `Development`.
- Aucun code de requete ne doit appliquer de migration ni executer de DDL : le compte applicatif (`kermaria_api`) n'a pas les droits de schema, la requete echoue en `MySqlException` et l'API repond `SQL_UNAVAILABLE`. Verifier une precondition de schema en lecture seule (`information_schema.tables` ou `schema_migrations`) et remonter une erreur explicite.
- Avant une migration reelle : `npm run backup:mariadb` ; ne jamais versionner un dump.
- Tests MariaDB opt-in : fournir `SQL_*`, `SERVICE_AUTH_TOKEN`, `DEMO_*`, puis `npm run validate:mariadb` ; le script pose `RUN_MARIADB_TESTS=true`.

## Staging/Preprod

- `npm run validate:staging` exige `NODE_ENV=production`, `ASPNETCORE_ENVIRONMENT=Staging`, `DOTNET_ENVIRONMENT=Staging`, `SQL_PROVIDER=mariadb`, `AD_INTEGRATION_MODE=disabled`, `SESSION_COOKIE_SECURE=true` et aucun `DEMO_*`.
- `npm run validate:preprod` exige `ASPNETCORE_ENVIRONMENT=Production` et `DOTNET_ENVIRONMENT=Production` avec les memes garde-fous.
- Ces validateurs appellent `git ls-files` ; les lancer depuis la racine d'un clone Git, pas depuis une archive.

## Exploitation — Topologie Reelle Et Pieges

Faits verifies en production, valables pour **tout** agent. Detail complet dans
`docs/v1.1/deploy/` et `docs/koxo-sync.md`.

### SRV-12 (webportal) — Ubuntu, pas Windows

- **Ubuntu 26.04**, hors domaine, **SSH par cle uniquement** (pas de WinRM, 445 ferme).
- Service systemd `kermaria-webportal.service` ; `/opt/kermaria/webportal` est un **lien symbolique** vers `/opt/kermaria/releases/<horodatage>-<version>`.
- Ecoute sur `192.168.100.212:3000`, **pas** sur `localhost`.
- **Livrer en `.tar.gz`, jamais en `.zip`** : un zip fabrique sous Windows porte des separateurs `\` qui deviennent des noms de fichiers litteraux a l'extraction, d'ou une arborescence a plat, `status=226/NAMESPACE` et un **502 nginx** trompeur.
- `.next/cache` n'est pas dans l'archive : le creer au deploiement, proprietaire `kermaria-web`, sinon meme panne.
- `sudo` exige un mot de passe : les etapes privilegiees reviennent a l'exploitant.

### SRV-13 (api-internal) — Windows

- Service `KermariaApiInternal`, compte `HOME\svc-kermaria`, dossier `C:\apps\api-internal`, sauvegardes `api-internal-old-<yyyyMMdd-HHmmss>`.
- Joignable en **WinRM/Kerberos depuis RDC-07 sans mot de passe**. Double saut : une requete LDAP **depuis** une session WinRM echoue — lancer l'ADSI en local sur RDC-07.
- Le csproj porte `<UseAppHost>false</UseAppHost>` : publier avec **`-p:UseAppHost=true`**, sinon `Kermaria.ApiInternal.exe` manque et le service n'a plus d'executable.
- Configuration : JSON **plat** `C:\ProgramData\Kermaria\api-internal.config.json`, UTF-8 **sans BOM**, valeurs en chaines. Genere depuis `<repo-parent>/kermaria-client-platform.local.env.ps1` par `scripts/build-api-config.ps1` : corriger un reglage **aux deux endroits**, sinon la regeneration l'annule.
- Journaux JSON dans `C:\apps\api-internal\logs\` : **ne pas filtrer sur `Error|Exception`** (chaque ligne contient `"Exception":null`), filtrer sur `"LogLevel":"(Error|Warning|Critical)"`. La « Reference » affichee dans l'interface est le `correlation_id`.

### Migrations en base reelle

- `.local.env.ps1` definit aussi `SQL_USERNAME`/`SQL_PASSWORD` (compte `kermaria_api`, **sans DDL**) : le charger **d'abord**, surcharger `kermaria_migrator` **ensuite**, sinon `CREATE command denied`.
- Passer `--project <chemin absolu>` : lance depuis une autre racine, le runner applique le mauvais checkout.
- MySqlConnector materialise les colonnes `CHAR(36)` en **`Guid`**, pas en `string` : utiliser le helper `ReadIdentifier`, jamais `reader.GetString`. Les smoke tests tournant en persistance **mock**, cette classe de bug leur est structurellement invisible.

### Chaine KoXo

- Le **CSV fait autorite** a la synchronisation : retirer une ligne **desactive** le compte AD correspondant. En revanche il **ne porte pas les permissions** — les groupes restent pilotes par l'API.
- `GroupeSecondaire` designe l'OU cible et KoXo **la cree si elle n'existe pas**.
- `identifiantUnique` est reporte dans l'attribut AD **`employeeNumber`** : seule cle fiable pour rattacher une identite creee par KoXo (le nom est translittere, le `sAMAccountName` est derive par KoXo).
- `KOXO_CSV_ENCODING=utf8bom`. Les accents **majuscules** restent supprimes par KoXo — comportement externe, non corrigeable cote application.
- `KoXoAdm.exe` sort en **code 1 meme en succes** : se fier aux marqueurs de journal, pas au code de sortie.

## Conventions De Code Et Docs

- Documentation utilisateur/exploitant/admin en francais ; noms techniques, routes, variables, types et classes peuvent rester en anglais.
- Quand un contrat API change, synchroniser `packages/shared/src/index.ts`, les routes BFF `apps/webportal/app/api/*`, l'API dans `Program.cs`/services/repositories et le script de verification web concerne.
- Les mutations admin sensibles doivent rester bornees via le BFF et CSRF (`apps/webportal/lib/csrf-server.ts`), puis revalidees/auditees dans API-INTERNAL.
- Les offres catalogue se desactivent par PATCH `status: inactive` ; ne pas ajouter de DELETE d'offre sans changer explicitement le contrat.
- Mettre a jour `docs/` lorsque le comportement, les flux de securite, les variables ou le deploiement changent.
