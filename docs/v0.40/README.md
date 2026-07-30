# V0.40 - Bilan

Statut : implemente dans le depot et recette technique validee le 2026-07-30.

## Portee livree

- migration MariaDB `035_v040_koxo_sync.sql`
- `birth_date` sur signup et `portal_users`
- `koxo_unique_identifier` immuable sur `portal_users`
- export KoXo prive `webportal -> api-internal -> JSON`
- validation bloquante `KOXO_EXPORT_VALIDATION_FAILED`
- tableau de bord admin `/admin/koxo`
- scripts PowerShell `Sync-KoXoClients.ps1` et `Install-KoXoScheduledTask.ps1`
- generation CSV 13 colonnes avec remplacement sur
  `C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\clients.csv`
- lancement KoXo via `KoXoAdm.exe /Synchro=CLIENTS.xml`

## Recette SRV-21

Constats verifies le 2026-07-30 :

- `SRV-21` recupere bien le payload KoXo depuis le BFF prive
- le script genere bien le CSV et cree un backup date
- le vrai glob de journaux KoXo est
  `C:\Program Files\KoXo Dev\KoXoAdm\Data\Logs\*.log`
- KoXo retourne un `exit code = 1`, mais le journal prouve une execution
  normale avec :
  `Parametre accepte`, `Ajout/Modification`, `Fin de l'operation`
- le script traite donc ce cas comme `completed_with_nonzero_exit`

## Variables stabilisees

### Webportal

- `KOXO_EXPORT_API_TOKEN`
- `KOXO_EXPORT_ALLOWED_IPS`
- `KOXO_EXPORT_REQUIRE_HTTPS`

### Script PowerShell

- `KOXO_API_URL`
- `KOXO_API_TOKEN`
- `KOXO_ALLOW_INSECURE_HTTP`
- `KOXO_CSV_ENCODING`
- `KOXO_MIN_USER_COUNT`
- `KOXO_MAX_USER_DROP_PERCENT`
- `KOXO_SYNC_TIMEOUT_SECONDS`
- `KOXO_LOG_DIRECTORY`
- `KOXO_KOXO_LOG_GLOB`
- `KOXO_BACKUP_RETENTION_COUNT`

## Points de vigilance avant deploiement serveurs

- retirer `KOXO_ALLOW_INSECURE_HTTP=true` apres la recette technique
- utiliser une URL HTTPS cible pour le BFF KoXo en exploitation durable
- conserver le glob reel des journaux KoXo
- ne pas deployer la vraie tache planifiee sans validation explicite
