# AGENTS.md

Ce fichier s'applique a tout le depot `kermaria-client-platform`.

## Architecture A Ne Pas Casser

- Flux obligatoire : `browser -> WEBPORTAL / BFF -> API-INTERNAL -> MariaDB`.
- `apps/webportal` est le portail Next.js et le BFF public ; il ne contacte jamais MariaDB, AD, NAS, RDS, VPN ni BPCE directement.
- `apps/api-internal` est l'API ASP.NET Core privee et le seul composant autorise a parler a MariaDB, AD, BPCE, SMTP et aux integrations internes.
- `packages/shared` contient seulement des contrats TypeScript non sensibles ; ne pas y mettre d'URL interne, secret ou logique serveur.
- L'architecture applicative reste limitee aux VM `WEBPORTAL` et `API-INTERNAL` ; utiliser le serveur SQL existant, ne pas ajouter de VM SQL.
- `API-INTERNAL` n'est pas exposee a Internet ; `/internal/*` exige `X-Service-Auth` hors `Development`.

## Toolchain

- Node.js `>=24` avec npm et `package-lock.json` ; utiliser `npm install`, pas pnpm/yarn.
- .NET SDK fixe par `global.json` : `10.0.301` avec `rollForward: latestFeature` ; projets `net10.0`.
- `NuGet.Config` restaure dans `.nuget/packages` et lit aussi `.nuget-local` ; ne pas remplacer par une config globale.
- Sous PowerShell restrictif, remplacer `npm` par `npm.cmd`.

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
- `AD_INTEGRATION_MODE=disabled` par defaut ; `controlled_write` reste borne a `OU=TEST_SITE_WEB,DC=home,DC=bzh` ; pas de hard delete AD, reset password, OU production ou compte Domain Admin.
- `BPCE_INTEGRATION_MODE=live`, `PAYPAL_MODE=live` et `EMAIL_INTEGRATION_MODE=live` demandent validation explicite ; en phase de tests, pas de client reel, email externe reel ni prelevement recurrent actif.
- Ne pas journaliser tokens, cookies, mots de passe, chaines de connexion, `BPCE_REFRESH_TOKEN`, `PAYPAL_CLIENT_SECRET` ni montants complets de facture.

## MariaDB Et Migrations

- Le fallback mock est `Development` seulement ; MariaDB reelle est obligatoire en staging/preprod.
- Les migrations sont `apps/api-internal/Migrations/MariaDb/[0-9]*.sql` et sont separees par `-- statement-break`.
- Les migrations ne s'executent pas au demarrage normal ; commande explicite `Development` : `dotnet run --project apps/api-internal/Kermaria.ApiInternal.csproj -- --apply-migrations`.
- Le seed fictif exige aussi `--seed-demo-data` et les variables `DEMO_PORTAL_*` / `DEMO_INTERNAL_ADMIN_*` ; il est ignore hors `Development`.
- Avant une migration reelle : `npm run backup:mariadb` ; ne jamais versionner un dump.
- Tests MariaDB opt-in : fournir `SQL_*`, `SERVICE_AUTH_TOKEN`, `DEMO_*`, puis `npm run validate:mariadb` ; le script pose `RUN_MARIADB_TESTS=true`.

## Staging/Preprod

- `npm run validate:staging` exige `NODE_ENV=production`, `ASPNETCORE_ENVIRONMENT=Staging`, `DOTNET_ENVIRONMENT=Staging`, `SQL_PROVIDER=mariadb`, `AD_INTEGRATION_MODE=disabled`, `SESSION_COOKIE_SECURE=true` et aucun `DEMO_*`.
- `npm run validate:preprod` exige `ASPNETCORE_ENVIRONMENT=Production` et `DOTNET_ENVIRONMENT=Production` avec les memes garde-fous.
- Ces validateurs appellent `git ls-files` ; les lancer depuis la racine d'un clone Git, pas depuis une archive.

## Conventions De Code Et Docs

- Documentation utilisateur/exploitant/admin en francais ; noms techniques, routes, variables, types et classes peuvent rester en anglais.
- Quand un contrat API change, synchroniser `packages/shared/src/index.ts`, les routes BFF `apps/webportal/app/api/*`, l'API dans `Program.cs`/services/repositories et le script de verification web concerne.
- Les mutations admin sensibles doivent rester bornees via le BFF et CSRF (`apps/webportal/lib/csrf-server.ts`), puis revalidees/auditees dans API-INTERNAL.
- Les offres catalogue se desactivent par PATCH `status: inactive` ; ne pas ajouter de DELETE d'offre sans changer explicitement le contrat.
- Mettre a jour `docs/` lorsque le comportement, les flux de securite, les variables ou le deploiement changent.
