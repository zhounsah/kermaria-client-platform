# Deploiement

Ce runbook V0.17 couvre la mise en place de `Development`, `Staging` et
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
| Active Directory | Infrastructure existante | Desactivee en V0.17 |

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
- `SESSION_DURATION_MINUTES`
- `LOGIN_MAX_FAILURES`
- `LOGIN_LOCKOUT_MINUTES`
- `AD_INTEGRATION_MODE=disabled`

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
- MariaDB reelle obligatoire
- aucun `DEMO_*`
- secrets hors Git
- validation recommandee : `npm run validate:staging`

### Production / preproduction finale

- `NODE_ENV=production`
- `ASPNETCORE_ENVIRONMENT=Production`
- `DOTNET_ENVIRONMENT=Production`
- `AD_INTEGRATION_MODE=disabled`
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

## Garde-fous V0.17

- aucune connexion SQL directe depuis `WEBPORTAL` ;
- aucune AD reelle ;
- aucun paiement, e-mail, SMS, push, WebSocket ou provisioning ;
- aucune suppression client destructive ;
- aucune confusion volontaire staging -> production.
