# Exploitation V0.21

> Navigation 1.0.0 : ce document reste le runbook principal pour lancer,
> verifier, superviser et rollbacker la plateforme. Revenir a
> [`V1.0.0_DOCUMENTATION.md`](V1.0.0_DOCUMENTATION.md) pour la vue
> d'ensemble et les autres points d'entree.

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

## En-tetes de securite

Source de verite unique : **l'application** (`SECURITY_HEADERS` dans
`apps/webportal/next.config.ts`). Le reverse proxy SRV-11 relaie sans ajouter.
Justification et regle d'ecriture nginx : `docs/SECURITY.md`.

### Chaine reelle devant WEBPORTAL (relevee le 2026-08-05)

```text
Internet -> 82.67.32.172 (NAT) -> SRV-11 :443
                                     |
                              HAProxy (mode tcp, routage SNI)
                                     |-- rdgateway.home.bzh --> SRV-27:443 (TLS brut)
                                     `-- defaut --------------> 127.0.0.1:8443 (PROXY v2)
                                                                     |
                                                              nginx (terminaison TLS)
                                                                     |
                                                              SRV-12:3000 (Node)
```

Consequence pour les en-tetes : **HAProxy est en `mode tcp`**, il ne lit pas le
HTTP et ne peut donc ni ajouter ni modifier un en-tete. Sur ce chemin, nginx
est le seul intermediaire capable d'en emettre — ce qui rend le diagnostic
sans ambiguite.

`cloudflared` tourne aussi sur SRV-11, mais avec un tunnel a jeton : ses
regles d'ingress vivent dans le tableau de bord Cloudflare, pas sur la machine.
Il ne sert pas `www.zacharyhounsa.ovh` : le DNS public de ce nom pointe
`82.67.32.172`, pas une adresse Cloudflare.

⚠️ Le DNS interne est en vue dedoublee (SRV-19 rend `192.168.100.211`). Un
`curl` depuis le LAN mesure donc le chemin direct. Ici les deux chemins se
rejoignent sur le meme nginx, mais ne pas generaliser pour un autre nom.

Controle en ligne (le seul qui voie le proxy — `test:operations` et `test:seo`
lisent le code source) :

```bash
npm run assert:security:headers -- --url https://www.zacharyhounsa.ovh/
```

Il echoue si un en-tete est absent, duplique par un intermediaire, ou si
`X-Robots-Tag` reapparait sur la vitrine publique.

### Canonicalisation de la vitrine, robots.txt et sitemap

Un seul hote public doit repondre `200` : `www.zacharyhounsa.ovh`. L'apex
`zacharyhounsa.ovh` est redirige en **301 permanent** par `proxy.ts`, chemin
et query string conserves ; `/.well-known/acme-challenge/` en est exempte pour
ne pas casser le renouvellement des certificats.

```bash
curl -sS -o /dev/null -w '%{http_code} %{redirect_url}\n' \
  "https://zacharyhounsa.ovh/offres?utm_source=test"
curl -sS https://www.zacharyhounsa.ovh/robots.txt
curl -sS -o /dev/null -w '%{http_code} %{content_type}\n' \
  https://www.zacharyhounsa.ovh/sitemap.xml
```

Attendu : `301 https://www.zacharyhounsa.ovh/offres?utm_source=test`, un
`robots.txt` sans directive `Host` (non standard) dont la seule ligne
`Sitemap:` pointe `https://www.zacharyhounsa.ovh/sitemap.xml`, et un sitemap en
`200 application/xml`. Ces deux ressources restent publiques : ni session, ni
`X-Robots-Tag`.

Dans le sitemap, `lastmod` n'apparait que sur les pages adossees a un contenu
administrable (`updatedAt` en base) — pages legales, `/a-propos`, fiches de
pack. Les pages purement statiques n'en portent pas : mieux vaut omettre la
date que la recalculer a chaque requete, ce qui annonce a tort tout le site
comme modifie a chaque passage du robot. Garde-fou statique : `npm run
test:seo`.

### Retirer un add_header en trop sur SRV-11

> **Applique le 2026-08-05.** Les huit directives ont ete retirees et nginx
> recharge ; le controle en ligne passe. La procedure ci-dessous reste le
> mode operatoire de reference si l'ecart reapparait.

Constat brut :

```bash
curl -sSk -o /dev/null -D - https://www.zacharyhounsa.ovh/
```

Un en-tete affiche deux fois vient du proxy : `add_header` **n'ecrase pas**
la valeur amont, il en ajoute une seconde.

⚠️ **Ne jamais ecraser le vhost de production avec le gabarit du depot.**
`scripts/r740xd-vm/srv11/kermaria-nginx.conf` a diverge : 83 lignes et 2 blocs
`server` contre 148 lignes et 4 blocs en production, TLS sur `443` contre
`127.0.0.1:8443 ssl proxy_protocol`, et le vhost `portfolio.zacharyhounsa.ovh`
absent du gabarit. Un `cp` ferait perdre le portfolio et l'ecoute derriere le
frontal PROXY protocol. **Editer le fichier en place**, jamais le remplacer.

Etat releve sur SRV-11 le 2026-08-05 : les quatre `add_header` apparaissent
**deux fois** dans `/etc/nginx/sites-available/kermaria`, une fois par bloc TLS
(lignes 70-73 pour les trois FQDN principaux, 114-117 pour `portfolio`). Ils
figurent aussi dans `kermaria-tls.pending`, inactif. Le vhost
`nextcloud.home.bzh` a ses propres `add_header`, **hors perimetre**, a ne pas
toucher.

Reperer :

```bash
sudo grep -rn "add_header" /etc/nginx/sites-available/ /etc/nginx/conf.d/ /etc/nginx/nginx.conf
```

Sauvegarder puis retirer les quatre directives du seul vhost kermaria :

```bash
sudo cp -a /etc/nginx/sites-available/kermaria /etc/nginx/sites-available/kermaria.bak-$(date -u +%Y%m%dT%H%M%SZ)
```

```bash
sudo sed -i '/add_header X-Frame-Options "SAMEORIGIN" always;/d; /add_header X-Content-Type-Options "nosniff" always;/d; /add_header Referrer-Policy "strict-origin-when-cross-origin" always;/d; /add_header Permissions-Policy "camera=(), microphone=(), geolocation=()" always;/d' /etc/nginx/sites-available/kermaria
```

Puis :

```bash
sudo nginx -t && sudo systemctl reload nginx
```

Verifier enfin depuis le poste de build :

```bash
npm run assert:security:headers -- --url https://www.zacharyhounsa.ovh/
```

Rollback : restaurer la sauvegarde du vhost, `nginx -t`, `systemctl reload
nginx`. Le retrait est sans effet fonctionnel — les memes protections restent
servies par l'application.

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
