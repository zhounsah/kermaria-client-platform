# V0.40 / V0.40.1 - Synchronisation KoXo privee, validee et non destructive

## Objet

V0.40 ajoute une chaine privee `webportal -> api-internal -> PowerShell -> CSV -> KoXo`
sans SMB cote site, sans secret reel dans le depot, sans execution KoXo cote site,
et sans creation automatique de la vraie tache planifiee.

V0.40.1 fige explicitement la regle mot de passe :

- aucun mot de passe n'est exporte vers KoXo, ni dans le JSON, ni dans le CSV ;
- `password_hash` en base SQL reste un hash local non reversible ;
- l'alignement du mot de passe avec l'infrastructure Windows continue a se faire
  au moment du `set-password` puis, pour les evolutions futures, via les flux
  dedies portail <-> AD, pas via KoXo ;
- KoXo reste limite a la synchronisation des identites et metadonnees
  utilisateurs.

## Architecture retenue

1. `apps/webportal/app/api/internal/koxo/users/route.ts`
   expose un endpoint BFF prive protege par bearer token, HTTPS et allowlist IP optionnelle.
2. `apps/api-internal/Program.cs` expose `GET /internal/koxo/users` et les routes admin
   `GET /internal/admin/koxo` + `POST /internal/admin/koxo/validate`.
3. `apps/api-internal/Services/KoxoExportService.cs` charge, trie, valide et audite le
   payload JSON KoXo sans reparation silencieuse.
4. `scripts/koxo/Sync-KoXoClients.ps1` consomme le JSON prive, applique les garde-fous,
   genere un CSV 13 colonnes, calcule le hash, valide la relecture, remplace la cible
   de facon sure, puis peut lancer `KoXoAdm.exe /Synchro=CLIENTS.xml`.
5. `scripts/koxo/Install-KoXoScheduledTask.ps1` documente et simule la tache planifiee ;
   aucune creation reelle n'est effectuee depuis le depot.

## Donnees exportees

Chaque utilisateur exporte contient exactement 7 champs JSON :

- `civilite`
- `nom`
- `prenom`
- `dateNaissance`
- `identifiantUnique`
- `groupeSecondaire`
- `email`

Le CSV genere 13 colonnes avec `;` comme separateur :

1. `civilite`
2. `nom`
3. `prenom`
4. `dateNaissance`
5. `identifiantUnique`
6. `groupeSecondaire`
7. `email`
8. vide
9. vide
10. vide
11. vide
12. vide
13. vide

La premiere ligne contient l'en-tete exact KoXo :

`Civilite;Nom;Prenom;DateNaissance;IdentifiantUnique;GroupeSecondaire;Email;Telephone;TelephoneMobile;Fax;PageWeb;ChampLibre;Fonction`

## Variables d'environnement

### Webportal / BFF

- `KOXO_EXPORT_API_TOKEN`
- `KOXO_EXPORT_ALLOWED_IPS` optionnelle, liste separee par `;`
- `KOXO_EXPORT_REQUIRE_HTTPS` par defaut `true` hors local

### Script PowerShell

- `KOXO_API_URL`
- `KOXO_API_TOKEN`
- `KOXO_ALLOW_INSECURE_HTTP` optionnelle, `false` par defaut, reservee a la recette technique hors HTTPS
- `KOXO_CSV_ENCODING`
- `KOXO_MIN_USER_COUNT`
- `KOXO_MAX_USER_DROP_PERCENT`
- `KOXO_SYNC_TIMEOUT_SECONDS`
- `KOXO_LOG_DIRECTORY`
- `KOXO_KOXO_LOG_GLOB`
- `KOXO_BACKUP_RETENTION_COUNT`

Valeur validee en recette SRV-21 pour les journaux KoXo :

- `KOXO_KOXO_LOG_GLOB=C:\Program Files\KoXo Dev\KoXoAdm\Data\Logs\*.log`

## Utilisation locale / simulation

### Validation admin sans KoXo

1. Ouvrir `/admin/koxo`
2. verifier les compteurs et l'aperÃ§u JSON
3. lancer `Tester la validation`
4. corriger les erreurs listees tant que le statut reste `validation_failed`

### DryRun PowerShell

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\koxo\Sync-KoXoClients.ps1 `
  -CsvTargetPath C:\Temp\koxo\users.csv `
  -WorkingDirectory C:\Temp\koxo\work `
  -DryRun
```

Le mode `DryRun` :

- prend le verrou
- appelle l'API HTTPS ou consomme la charge injectee en test
- valide le JSON
- genere le CSV temporaire
- relit et reverifie le CSV
- journalise l'operation
- n'ecrase jamais le fichier cible
- n'actualise pas l'etat precedent

### Execution cible SRV-21

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\koxo\Sync-KoXoClients.ps1 `
  -CsvTargetPath "C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\clients.csv" `
  -WorkingDirectory "C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\work" `
  -LaunchKoxo `
  -KoxoExecutablePath "C:\Program Files\KoXo Dev\KoXoAdm\KoXoAdm.exe" `
  -KoxoWorkingDirectory "C:\Program Files\KoXo Dev\KoXoAdm" `
  -KoxoSyncArgument "/Synchro=CLIENTS.xml"
```

Quand `-LaunchKoxo` est active :

- le CSV est ecrit et relu avant tout lancement KoXo
- `KoXoAdm.exe` est lance localement sur SRV-21
- le script attend la fin du processus avec le timeout configure
- un code retour non nul reste tolere si le journal KoXo recent prouve une
  fin d'operation correcte avec les marqueurs attendus
- les journaux KoXo recents peuvent etre relus via `KOXO_KOXO_LOG_GLOB`

## Procedure cible SRV-21

1. definir les variables d'environnement KoXo sur SRV-21
2. verifier que `KOXO_API_URL` pointe vers le BFF HTTPS prive
3. tester le script en `-DryRun`
4. verifier les logs locaux et le hash produit
5. verifier la lecture des journaux KoXo via `KOXO_KOXO_LOG_GLOB`
6. executer ensuite sans `-DryRun` et avec `-LaunchKoxo`
7. seulement apres recette exploitable, utiliser `Install-KoXoScheduledTask.ps1`
   en dehors du depot, avec confirmation explicite

Pour une recette technique ponctuelle avant exposition HTTPS, `KOXO_ALLOW_INSECURE_HTTP=true`
peut etre active explicitement sur SRV-21. Cette option ne doit pas rester active
en cible durable.

Recette reelle confirmee le 2026-07-30 sur SRV-21 :

- appel prive BFF KoXo via bearer token ;
- generation locale du CSV `clients.csv` ;
- backup automatique du CSV precedent ;
- lancement de `KoXoAdm.exe /Synchro=CLIENTS.xml` ;
- code retour KoXo `1` tolere si le journal recent confirme :
  `Parametre accepte`, `Ajout/Modification`, `Fin de l'operation`.

## Remplacement sur disque et rollback

- ecriture dans un fichier temporaire
- validation immediate de relecture
- remplacement sur la cible avec backup
- retention bornee des backups
- rollback manuel = remettre en place le dernier `.bak`

## Encodage

Valeurs supportees par le module :

- `utf8`
- `utf8bom`
- `unicode`
- `ascii`
- `latin1`

Le choix doit etre valide avec KoXo avant mise en production controlee.

## Permissions minimales

- lecture HTTPS sur le BFF prive
- ecriture sur le dossier cible CSV
- ecriture sur le dossier de logs locaux
- lecture sur le glob de journaux KoXo si active
- eventuellement droit de creation de tache planifiee si l'installation est
  finalement confirmee hors depot

## Mise en production controlee

Avant une vraie activation :

- renseigner un vrai `KOXO_EXPORT_API_TOKEN` hors depot
- confirmer l'encodage attendu par KoXo
- confirmer le chemin reel du CSV cible
- confirmer le compte d'execution SRV-21
- confirmer l'intervalle de planification
- valider la retention des backups et des logs
- faire une premiere execution `DryRun`
- faire une premiere execution manuelle hors heures sensibles

