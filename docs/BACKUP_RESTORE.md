# Sauvegarde et restauration MariaDB

## Périmètre

Cette procédure couvre la base applicative MariaDB du portail. Les sauvegardes
doivent être stockées hors du dépôt, chiffrées si elles contiennent des données
réelles et accessibles uniquement aux opérateurs autorisés.

Ne jamais :

- versionner un dump ;
- placer le mot de passe dans la ligne de commande ;
- restaurer directement en production sans validation ;
- appliquer une migration sans sauvegarde préalable.

## Sauvegarde

Commande PowerShell V0.16 recommandée :

```powershell
npm run backup:mariadb
```

Le script :

- lit `SQL_HOST`, `SQL_PORT`, `SQL_DATABASE` et `SQL_USERNAME` ;
- demande `SQL_PASSWORD` localement si besoin ;
- génère un dump horodaté ;
- calcule un hash SHA-256 ;
- n'écrit jamais le mot de passe dans le dépôt.

Linux :

```bash
mkdir -p /var/backups/kermaria
mysqldump -h <SQL_HOST> -P <SQL_PORT> -u <SQL_USERNAME> -p \
  --single-transaction --routines --triggers <SQL_DATABASE> \
  > /var/backups/kermaria/backup_<YYYYMMDD_HHMMSS>.sql
```

PowerShell, avec `--result-file` pour éviter une conversion d'encodage :

```powershell
$date = Get-Date -Format "yyyyMMdd_HHmmss"
$path = "D:\Backups\Kermaria\backup_$date.sql"
mysqldump -h <SQL_HOST> -P <SQL_PORT> -u <SQL_USERNAME> -p `
  --single-transaction --routines --triggers <SQL_DATABASE> `
  --result-file="$path"
```

L'option `-p` demande le mot de passe de manière interactive.

## Vérification immédiate

1. Vérifier que la commande retourne un code succès.
2. Vérifier que le fichier existe et n'est pas vide.
3. Calculer et conserver un hash hors dépôt.
4. Vérifier l'espace disque restant.
5. Copier la sauvegarde vers le stockage sécurisé prévu.

PowerShell :

```powershell
Get-Item "D:\Backups\Kermaria\backup_<DATE>.sql"
Get-FileHash "D:\Backups\Kermaria\backup_<DATE>.sql" -Algorithm SHA256
```

Linux :

```bash
ls -lh /var/backups/kermaria/backup_<DATE>.sql
sha256sum /var/backups/kermaria/backup_<DATE>.sql
```

## Restauration de test

Commande PowerShell V0.16 recommandée :

```powershell
npm run restore:mariadb -- -DumpPath C:\Backups\Kermaria\kermaria_mariadb_<DATE>.sql -TargetDatabase TEST_WEB_RESTORE -VerifySchema
```

`-VerifySchema` relit `schema_migrations` après restauration.

Restaurer d'abord vers une base vide dédiée, jamais par-dessus la source.

Linux :

```bash
mysql -h <SQL_HOST> -P <SQL_PORT> -u <SQL_USERNAME> -p \
  <RESTORE_TEST_DATABASE> < backup_<DATE>.sql
```

PowerShell :

```powershell
mysql -h <SQL_HOST> -P <SQL_PORT> -u <SQL_USERNAME> -p `
  <RESTORE_TEST_DATABASE> `
  --execute="source D:/Backups/Kermaria/backup_<DATE>.sql"
```

Adapter les chemins Windows avec `/` dans la commande `source`.

## Contrôles après restauration

```sql
SELECT COUNT(*) FROM portal_users;
SELECT COUNT(*) FROM portal_sessions;
SELECT COUNT(*) FROM support_requests;
SELECT COUNT(*) FROM service_requests;
SELECT COUNT(*) FROM audit_logs;
SELECT migration_id, applied_at
FROM schema_migrations
ORDER BY applied_at;
```

Comparer les volumes avec la source au moment de la sauvegarde. Démarrer une
instance API pointant vers la base restaurée, puis vérifier `/health/ready`,
`/ready`, login, lectures client/admin et isolation.

Checklist rapide de test :

1. restaurer vers une base distincte ;
2. vérifier le hash du dump ;
3. vérifier `schema_migrations` ;
4. vérifier `/health/ready` et `/ready` ;
5. vérifier login client/admin et refus `/admin` pour `client_user`.

## Avant et après migration

Avant :

1. vérifier l'espace disque ;
2. réaliser et hacher la sauvegarde ;
3. vérifier le dernier test de restauration ;
4. arrêter les écritures ou prévoir une fenêtre contrôlée ;
5. noter la version applicative et les migrations déjà appliquées.

Après :

1. vérifier `schema_migrations` ;
2. exécuter les tests MariaDB opt-in ;
3. vérifier les health checks ;
4. tester une demande support et une demande service fictives ;
5. conserver la sauvegarde pré-migration pendant la durée de rollback.

## Restauration d'urgence

1. retirer WEBPORTAL du trafic ;
2. arrêter API-INTERNAL pour stopper les écritures ;
3. confirmer le dump et son hash ;
4. restaurer vers une base neuve si possible ;
5. basculer `SQL_DATABASE` vers la base restaurée hors Git ;
6. redémarrer API-INTERNAL ;
7. attendre `/health/ready` à 200 ;
8. redémarrer WEBPORTAL et vérifier `/api/health/ready` ;
9. contrôler les audits et l'isolation client.

## Rotation

Conserver plusieurs générations selon la politique de l'organisation, avec au
moins une copie hors hôte. Tester régulièrement une restauration complète. La
suppression des anciennes générations doit être automatisée par
l'infrastructure, jamais par un script applicatif non revu.

Voir aussi [V0.16_PREPRODUCTION_TECHNIQUE.md](V0.16_PREPRODUCTION_TECHNIQUE.md).
