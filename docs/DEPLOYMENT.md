# Déploiement

## Topologie

| Composant | Cible | Exposition |
|---|---|---|
| `WEBPORTAL` | Ubuntu Server LTS | Reverse proxy HTTPS uniquement |
| `API-INTERNAL` | Local en V0.8, future VM Windows Server Core | Privée |
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
- `SESSION_COOKIE_NAME`
- `SESSION_COOKIE_SECURE`

En production, `SESSION_COOKIE_SECURE=true` est obligatoire. Le cookie est
toujours `HttpOnly` et `SameSite=Lax`.

## MariaDB

La base de test est `TEST_WEB` et le compte SQL de test est `TEST_WEB`. Le mot
de passe est injecté hors dépôt. La chaîne est construite en mémoire par
`API-INTERNAL` et n'est jamais loggée.

Les migrations V0.7 ajoutent `portal_sessions` et
`portal_users.password_hash`. La migration V0.8
`003_admin_and_auth_hardening.sql` ajoute le rôle, le compteur d'échecs et le
verrouillage temporaire. Les tokens bruts ne sont jamais stockés.

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

La communication interservice doit utiliser HTTPS privé et une identité
service-à-service avant production.

## Sauvegardes et supervision

- Inclure les tables applicatives dans la politique MariaDB existante.
- Tester restauration, rétention et chiffrement.
- Centraliser logs système, application et audit.
- Surveiller disponibilité, latence, erreurs SQL contrôlées, refus AD,
  certificats, espace disque et sauvegardes.
- Ne jamais exporter un secret dans la télémétrie.

## Mise en service

1. Déployer sans secret dans les artefacts.
2. Valider réseau privé, HTTPS, identité de service et supervision.
3. Sauvegarder puis appliquer les migrations de façon contrôlée.
4. Valider MariaDB avec le compte SQL limité.
5. Conserver `AD_INTEGRATION_MODE=disabled`.
6. Vérifier les attributs `Secure`, `HttpOnly` et `SameSite=Lax` du cookie.
7. Tester expiration, révocation et isolation entre deux clients fictifs.
8. Tester le lockout, le reset après succès et les refus croisés de rôle.
9. Vérifier que les vues `/admin` restent strictement en lecture seule.
10. Créer une OU AD dédiée de test et revoir les délégations avant tout essai.
11. Repasser immédiatement en `disabled` en cas de doute.
