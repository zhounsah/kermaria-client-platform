# Deploiement

Ce runbook V0.21 couvre la mise en place de `Development`, `Staging` et
`Production` en conservant strictement l'architecture :

```text
browser -> WEBPORTAL / BFF -> API-INTERNAL -> MariaDB
```

`WEBPORTAL` ne doit jamais acceder directement a MariaDB.

## Topologie cible

| Composant | Cible | Exposition |
|---|---|---|
| `WEBPORTAL` | Ubuntu Server LTS | Reverse proxy HTTPS uniquement |
| `API-INTERNAL` | VM Windows Server Core ou hote interne dedie | Reseau prive uniquement |
| MariaDB | Serveur existant | `API-INTERNAL` uniquement |
| Active Directory | Infrastructure existante | Bornee a l'OU de test en V0.18 |

## Variables API-INTERNAL

MariaDB :

- `SQL_PROVIDER=mariadb`
- `SQL_HOST`
- `SQL_PORT`
- `SQL_DATABASE`
- `SQL_USERNAME`
- `SQL_PASSWORD`

Commun :

- `ASPNETCORE_ENVIRONMENT`
- `DOTNET_ENVIRONMENT`
- `SERVICE_AUTH_TOKEN`
- `LOG_LEVEL`
- `LOG_FILE_DIRECTORY` (optionnel, active la journalisation fichier
  rotative quotidienne) ; `LOG_FILE_LEVEL` et `LOG_FILE_RETENTION_DAYS`
  reglent niveau et purge
- `SESSION_DURATION_MINUTES`
- `LOGIN_MAX_FAILURES`
- `LOGIN_LOCKOUT_MINUTES`
- `AD_INTEGRATION_MODE=disabled`

Facturation BPCE (V0.20) :

- `BPCE_INTEGRATION_MODE=disabled|mock|live` (defaut `disabled`)
- `BPCE_BASE_URL=https://www.gestion-factures.banquepopulaire.fr`
- `BPCE_REFRESH_TOKEN` (**secret strict**, jamais commit, jamais log)
- `BPCE_SENDER_ID` (renseigne via `--verify-bpce-sender`)
- `BPCE_REQUEST_TIMEOUT_MS` (defaut 10000)

Developpement uniquement :

- `DEMO_PORTAL_EMAIL`
- `DEMO_PORTAL_PASSWORD`
- `DEMO_INTERNAL_ADMIN_EMAIL`
- `DEMO_INTERNAL_ADMIN_PASSWORD`

## Variables WEBPORTAL

- `NODE_ENV=production` en staging et production
- `INTERNAL_API_URL`
- `ALLOW_LOCAL_INTERNAL_API_URL=false`
- `SERVICE_AUTH_TOKEN`
- `SESSION_COOKIE_NAME`
- `SESSION_COOKIE_SECURE=true`
- `SESSION_COOKIE_SAME_SITE=lax|strict|none`

`SESSION_COOKIE_SAME_SITE=none` exige `SESSION_COOKIE_SECURE=true`.

Paiement et reglement (V0.21) :

- `PAYPAL_MODE=sandbox|live` (defaut `sandbox`)
- `PAYPAL_CLIENT_ID`
- `PAYPAL_CLIENT_SECRET` (**secret strict**, jamais commit, jamais log)
- `BILLING_IBAN`, `BILLING_BIC`, `BILLING_TRANSFER_LABEL` (affiches sur la
  section reglement des factures emises)
- `BILLING_PAYPAL_URL` (fallback PayPal.me si l'integration PayPal Orders
  n'est pas configuree)

Le mode `live` PayPal n'est jamais active sans validation explicite (cf.
V1.0 beta 1, R740xd).

## Profils d'environnement

### Development

- `NODE_ENV=development`
- `ASPNETCORE_ENVIRONMENT=Development`
- `DOTNET_ENVIRONMENT=Development`
- MariaDB facultative
- fallback mock autorise si SQL absente
- `ALLOW_LOCAL_INTERNAL_API_URL=true` possible

### Staging

- `NODE_ENV=production`
- `ASPNETCORE_ENVIRONMENT=Staging`
- `DOTNET_ENVIRONMENT=Staging`
- `AD_INTEGRATION_MODE=disabled`
- `BPCE_INTEGRATION_MODE=disabled` ou `mock`
- `PAYPAL_MODE=sandbox`
- MariaDB reelle obligatoire
- aucun `DEMO_*`
- secrets hors Git
- validation recommandee : `npm run validate:staging`

### Production / preproduction finale

- `NODE_ENV=production`
- `ASPNETCORE_ENVIRONMENT=Production`
- `DOTNET_ENVIRONMENT=Production`
- `AD_INTEGRATION_MODE=disabled`
- `BPCE_INTEGRATION_MODE=disabled` tant que V1.0 beta 1 n'a pas valide la cible
- `PAYPAL_MODE=sandbox` tant que V1.0 beta 1 n'a pas valide la cible
- MariaDB reelle obligatoire
- aucun `DEMO_*`
- secrets hors Git
- validation recommandee : `npm run validate:preprod`

## Staging checklist

WEBPORTAL :

- `INTERNAL_API_URL` privee et server-only ;
- `SERVICE_AUTH_TOKEN` identique a l'API ;
- cookie `HttpOnly`, `Secure`, `SameSite` verifie ;
- headers de securite servis ;
- `X-Robots-Tag: noindex, nofollow`.

API-INTERNAL :

- `SERVICE_AUTH_TOKEN` present ;
- `AD_INTEGRATION_MODE=disabled` ;
- configuration non `Development` sans placeholder ;
- routes `/health/ready` et `/ready` fonctionnelles ;
- logs structurés sans donnees sensibles.

MariaDB staging :

- compte applicatif dedie ;
- sauvegarde effectuee avant migration ;
- restauration testee hors base principale ;
- isolation entre deux clients fictifs revalidee si possible.

URLs :

- `WEBPORTAL_BASE_URL` et `API_INTERNAL_BASE_URL` documentees pour
  `npm run check:health` ;
- pas de confusion entre URLs staging et production.

Secrets :

- hors Git ;
- rotates si deja exposes ;
- non recopies dans captures, tickets ou documentation.

## Pare-feu minimal

| Source | Destination | Port |
|---|---|---|
| Internet / reverse proxy | `WEBPORTAL` | TCP 443 |
| `WEBPORTAL` | `API-INTERNAL` | TCP 443 prive |
| `API-INTERNAL` | MariaDB | TCP 3306 |
| Reseau admin / VPN | `WEBPORTAL` | SSH |
| Reseau admin / VPN | `API-INTERNAL` | RDP ou WinRM |

Sont interdits :

- Internet vers `API-INTERNAL`, MariaDB ou AD ;
- `WEBPORTAL` vers MariaDB ou AD ;
- tout flux non liste.

## Mise en service

1. Deployer sans secret dans les artefacts.
2. Injecter les variables hors Git.
3. Executer `npm run validate`.
4. Executer `npm run validate:staging` ou `npm run validate:preprod` selon la
   cible.
5. Executer `npm run check:health`.
6. Sauvegarder MariaDB puis appliquer les migrations de facon controlee.
7. Executer `npm run validate:mariadb` si l'environnement opt-in est disponible.
8. Verifier les headers, cookies et refus de role croise.
9. Executer la recette de
   [V0.17](V0.17_RECETTE_PREPRODUCTION.md).

## Pare-feu sortant additionnel V0.20-V0.21

| Source | Destination | Port |
|---|---|---|
| `API-INTERNAL` | `www.gestion-factures.banquepopulaire.fr` (BPCE) | TCP 443 |
| `WEBPORTAL` | `api-m.sandbox.paypal.com` / `api-m.paypal.com` | TCP 443 |

Ces sorties ne sont autorisees que lorsque les modes correspondants ne
sont pas `disabled`. Le navigateur ne contacte jamais BPCE ; il est
redirige vers `paypal.com` pour l'approbation buyer puis ramene sur le
portail.

## Garde-fous V0.21

- aucune connexion SQL directe depuis `WEBPORTAL` ;
- aucune AD hors de l'OU de test validee ;
- aucun appel BPCE depuis `WEBPORTAL` ; tout passe par `API-INTERNAL` ;
- aucun secret PayPal cote navigateur (le client id est public mais le
  flux Create Order reste cote serveur) ;
- aucun mode `live` BPCE ou PayPal sans validation explicite (V1.0 beta 1) ;
- aucun e-mail, SMS, push, WebSocket ou provisioning declenche par un
  encaissement ;
- aucune suppression client destructive ;
- aucune confusion volontaire staging -> production.
