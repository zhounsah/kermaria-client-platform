# Déploiement

## Topologie

| Composant | Cible | Exposition |
|---|---|---|
| `WEBPORTAL` | Ubuntu Server LTS | Reverse proxy HTTPS uniquement |
| `API-INTERNAL` | Local en V0.9, future VM Windows Server Core | Privée |
| MariaDB `TEST_WEB` | Serveur existant | `API-INTERNAL` uniquement |
| Active Directory | Infrastructure existante | `API-INTERNAL` uniquement |

La VM finale `API-INTERNAL` n'étant pas encore initialisée, le développement et
les tests tournent localement. Cela n'autorise aucune publication Internet.

## Variables API-INTERNAL

MariaDB :

- `SQL_PROVIDER=mariadb`
- `SQL_HOST`
- `SQL_PORT`
- `SQL_DATABASE=TEST_WEB`
- `SQL_USERNAME=TEST_WEB`
- `SQL_PASSWORD`

Active Directory :

- `AD_INTEGRATION_MODE=disabled|mock|test|enabled`
- `AD_DOMAIN`
- `AD_CLIENTS_OU_DN`
- `AD_SERVICE_ACCOUNT_USERNAME`
- `AD_SERVICE_ACCOUNT_PASSWORD`
- `AD_ALLOWED_GROUPS`

Commun :

- `ASPNETCORE_ENVIRONMENT`
- `SERVICE_AUTH_TOKEN`
- `LOG_LEVEL`
- `SESSION_DURATION_MINUTES`
- `LOGIN_MAX_FAILURES`
- `LOGIN_LOCKOUT_MINUTES`
- `DEMO_PORTAL_EMAIL` en développement uniquement
- `DEMO_PORTAL_PASSWORD` en développement uniquement
- `DEMO_INTERNAL_ADMIN_EMAIL` en développement uniquement
- `DEMO_INTERNAL_ADMIN_PASSWORD` en développement uniquement

`INTERNAL_API_URL` appartient à `WEBPORTAL` côté serveur. Aucun secret ou URL
interne ne doit être incorporé au bundle navigateur.

Variables `WEBPORTAL` :

- `INTERNAL_API_URL`
- `ALLOW_LOCAL_INTERNAL_API_URL=false`
- `SERVICE_AUTH_TOKEN`
- `SESSION_COOKIE_NAME`
- `SESSION_COOKIE_SECURE`

En production, `SESSION_COOKIE_SECURE=true` est obligatoire. Le cookie est
toujours `HttpOnly` et `SameSite=Lax`.

`SERVICE_AUTH_TOKEN` doit être identique sur les deux applications. WEBPORTAL
l'ajoute uniquement aux appels serveur ; API-INTERNAL le vérifie sur
`/internal/*` en Production. Il n'est jamais exposé au navigateur.

En Production, `INTERNAL_API_URL` ne peut pas pointer vers localhost, sauf
dérogation documentée avec `ALLOW_LOCAL_INTERNAL_API_URL=true`. Cette
dérogation est destinée aux topologies contrôlées où les deux processus
partagent volontairement le même hôte.

## MariaDB

La base de test est `TEST_WEB` et le compte SQL de test est `TEST_WEB`. Le mot
de passe est injecté hors dépôt. La chaîne est construite en mémoire par
`API-INTERNAL` et n'est jamais loggée.

Les migrations V0.7 ajoutent `portal_sessions` et
`portal_users.password_hash`. La migration V0.8
`003_admin_and_auth_hardening.sql` ajoute le rôle, le compteur d'échecs et le
verrouillage temporaire. V0.9 ne modifie pas le schéma. Les tokens bruts ne
sont jamais stockés.

Le seed des comptes démo exige les variables `DEMO_PORTAL_*` et
`DEMO_INTERNAL_ADMIN_*`, ainsi que la commande explicite
`--apply-migrations --seed-demo-data`.

Migrations en développement :

```powershell
dotnet run --project apps/api-internal/Kermaria.ApiInternal.csproj -- --apply-migrations
dotnet run --project apps/api-internal/Kermaria.ApiInternal.csproj -- --apply-migrations --seed-demo-data
```

En production, les migrations doivent être exécutées par une étape
d'exploitation distincte, revue et sauvegardée. Le runtime normal n'applique
aucune migration.

La procédure complète, incluant la sauvegarde préalable et les vérifications,
est décrite dans [OPERATIONS.md](OPERATIONS.md).

## Pare-feu

Flux minimaux :

| Source | Destination | Port |
|---|---|---|
| Internet / reverse proxy | `WEBPORTAL` | TCP 443 |
| `WEBPORTAL` | `API-INTERNAL` | TCP 443 privé |
| `API-INTERNAL` | MariaDB | TCP 3306 |
| `API-INTERNAL` | AD futur | LDAPS 636, autres flux documentés seulement |
| Réseau admin / VPN | `WEBPORTAL` | SSH |
| Réseau admin / VPN | `API-INTERNAL` | RDP ou WinRM |

Sont interdits : Internet vers API/SQL/AD, WEBPORTAL vers SQL/AD, et tout flux
non listé. La règle SQL doit limiter la source à l'adresse d'`API-INTERNAL`.

## Services

`WEBPORTAL` s'exécute sous un compte système non privilégié sur Ubuntu.
`API-INTERNAL` s'exécute sous une identité dédiée non administratrice sur
Windows Server Core. Le compte de service AD est distinct et limité à l'OU de
test validée.

La communication interservice utilise `SERVICE_AUTH_TOKEN` en Production et
doit être transportée sur HTTPS privé. Le pare-feu reste la première barrière :
le token ne rend pas API-INTERNAL publiable.

## Sauvegardes et supervision

- Suivre [BACKUP_RESTORE.md](BACKUP_RESTORE.md).
- Inclure les tables applicatives dans la politique MariaDB existante.
- Tester restauration, rétention et chiffrement hors dépôt.
- Centraliser logs système, application et audit.
- Surveiller `/health/live`, `/health/ready`, disponibilité, latence, erreurs
  SQL contrôlées, refus interservice/AD, certificats, espace disque et
  sauvegardes.
- Ne jamais exporter un secret dans la télémétrie.

## Mise en service

1. Déployer sans secret dans les artefacts.
2. Tourner les secrets exposés selon
   [SECRET_ROTATION.md](SECRET_ROTATION.md).
3. Exécuter `npm run validate`.
4. Valider réseau privé, HTTPS, identité de service et supervision.
5. Sauvegarder puis appliquer les migrations de façon contrôlée.
6. Exécuter les tests MariaDB opt-in.
7. Exiger HTTP 200 sur les quatre health checks live/ready.
8. Conserver `AD_INTEGRATION_MODE=disabled`.
9. Vérifier les attributs `Secure`, `HttpOnly` et `SameSite=Lax` du cookie.
10. Tester expiration, révocation et isolation entre deux clients fictifs.
11. Tester le lockout, le reset après succès et les refus croisés de rôle.
12. Vérifier que les vues `/admin` restent strictement en lecture seule.
13. Vérifier `X-Robots-Tag: noindex, nofollow`.
14. Tester une restauration sur une base distincte.

Les commandes détaillées Windows et Linux sont dans
[OPERATIONS.md](OPERATIONS.md).
