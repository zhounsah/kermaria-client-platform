# Suivi des sauvegardes Veeam dans le portail client

## État vérifié le 7 octobre 2026

Relevé **en lecture seule** depuis RDC-07 par WinRM sur SRV-16 et SRV-01.
Il décrit la configuration observée à cette date, pas une garantie de
restauration ni un engagement commercial.

| Sujet | Constat | Limite de preuve |
| --- | --- | --- |
| Serveur | `VeeamBackupSvc` actif sur SRV-16 ; module PowerShell Veeam `13.0.2.29`. La VM SRV-16 est en cours d'exécution sur SRV-01. | Une panne de SRV-01 peut donc aussi rendre le serveur Veeam indisponible. |
| Sauvegarde de configuration VBR | `Backup Configuration Job` activé, chiffrement activé, conservation de **30 points de restauration**, dernier résultat `Success`, prochain lancement annoncé le 8 octobre à 02 h 00 (heure de Paris). | **30 points ne signifient pas 30 jours.** Le résultat du job ne prouve ni la présence d'un fichier `.bco` exploitable ni sa restauration. |
| Destination de configuration | Référentiel `Disques Sauvegardes R740xd`, dont le chemin configuré est un partage CIFS sur FS-01. | La session WinRM n'a pas pu lister ce partage (`Access denied`, accès réseau depuis la session distante). La présence et la copie hors du domaine de panne de ce fichier restent à vérifier depuis un accès autorisé. |
| Collecteur portail | Tâche `Kermaria Veeam Backup Collector` présente et prête ; dernier lancement le 7 octobre à 19 h 30 avec code `0`, prochain lancement annoncé à 20 h 00. La tâche lance un script relais via `powershell.exe`, qui appelle `pwsh` et `Invoke-VeeamBackupCollection.ps1`. | Le code `0` signifie que la tâche a terminé sans erreur signalée ; il ne prouve pas qu'un rapport a été envoyé, accepté ou affiché dans `/backups`. Le collecteur peut terminer avec succès avec zéro rapport. |
| Jobs visibles | `Get-VBRJob` retourne 23 jobs : 18 planifiés, 5 non planifiés. Parmi ces 23, le dernier résultat associé est `Success` pour 18, `Failed` pour 1, et aucune session n'a été associée à 4. | Veeam avertit que `Get-VBRJob` ne couvre plus les jobs de sauvegarde d'ordinateurs et de copie ; ce relevé n'est pas l'inventaire exhaustif de toutes les protections. |
| Couverture `KoXoDATA` | Le propriétaire confirme que `F:\KoXoDATA` contient tous les dossiers concernés. Le job NAS planifié `KoXoDATA` a pour objet cette racine, sans masque d'inclusion ni d'exclusion déclaré. Sa dernière session et son unique tâche ont le résultat `Success` le 7 octobre à 02 h 33. | C'est une preuve de périmètre configuré et de résultat **commun au job**. Ce relevé n'inspecte pas le contenu d'un point de restauration pour chaque dossier. |

Le job dont le dernier résultat associé est `Failed` est `KoXoDATA - OVH
180d` (non planifié, session terminée le 3 octobre à 03 h 54). Les quatre jobs
sans session associée sont `Backup Postes Kermaria - OVH_clone1`, `Backup VM -
OVH`, `Postes Kermaria` et `Backup Postes Kermaria - OVH`. L'absence de session
dans ce relevé ne prouve pas qu'aucune sauvegarde d'ordinateur ou copie n'a eu
lieu : vérifier chaque type de job avec sa cmdlet Veeam dédiée.

### Points à vérifier dans Veeam et dans le portail

1. Depuis un accès autorisé au référentiel, confirmer la présence, la date et
   la lisibilité d'une sauvegarde `.bco` de configuration ; en conserver une
   copie chiffrée hors du domaine de panne SRV-01/FS-01 et tester une
   restauration de configuration dans un environnement isolé.
2. Examiner le dernier échec de `KoXoDATA - OVH 180d` et déterminer si ce job
   non planifié doit encore protéger des données. Vérifier les quatre jobs sans
   session avec les cmdlets adaptées à leur type.
3. Contrôler le journal du collecteur, le nombre de rapports effectivement
   envoyés et acceptés par `POST /internal/backups/report`, puis les mappings
   dans `/admin/backups` et l'affichage d'un compte client autorisé dans
   `/backups`. Aucun de ces trois maillons n'a été validé lors de ce relevé.
4. Effectuer un test de restauration de données dans un réseau isolé avant de
   déclarer la restauration vérifiée. Un job `Success` ne suffit pas.

Les valeurs du [brief commercial](BRIEF_CODEX_SAUVEGARDES_ZACHARY_IT.md)
(sauvegarde quotidienne, rétention de 30 jours, restauration incluse et délais
de suppression) restent **des propositions à confirmer par service**. La
configuration de 30 points du job de configuration VBR ne valide aucune de ces
promesses pour les données client. La [procédure MariaDB](BACKUP_RESTORE.md)
concerne un autre mécanisme de sauvegarde.

## Architecture

Le portail ne contacte jamais Veeam depuis le navigateur ni depuis une route
publique. Le flux attendu est :

```text
Veeam Backup & Replication
  -> collecteur interne PowerShell
  -> API-INTERNAL POST /internal/backups/report
  -> MariaDB
  -> WEBPORTAL / BFF
  -> navigateur : espace client /backups
```

Le collecteur est a installer sur SRV-16 ou sur un hote interne disposant soit
du module `Veeam.Backup.PowerShell`, soit de l'acces HTTPS a l'API REST VBR.
Sur SRV-16, le module Veeam détecté exige PowerShell 7 : la collecte Veeam
elle-même doit s'exécuter avec `pwsh.exe`. La tâche planifiée actuelle démarre
un relais en Windows PowerShell 5.1, qui lance ensuite `pwsh` ; ce relais ne
change pas cette exigence.

## Variables

Variables cote collecteur :

- `VEEAM_COLLECTOR_MODE` : `auto`, `rest` ou `powershell`.
- `VEEAM_PORTAL_API_URL` : URL interne de API-INTERNAL, par exemple
  `http://192.168.100.213:5000`.
- `SERVICE_AUTH_TOKEN` : secret partage avec API-INTERNAL pour `X-Service-Auth`.
- `VEEAM_REST_BASE_URL` : URL REST VBR si le mode REST est utilise.
- `VEEAM_REST_USERNAME` / `VEEAM_REST_PASSWORD` : compte Veeam interne.
- `VEEAM_REST_API_VERSION` : par defaut `1.3-rev2`.

Ne pas stocker ces secrets dans le depot. Utiliser les variables systeme, un
compte de service ou la configuration securisee de l'hote.

## Compatibilite Veeam

Le collecteur choisit REST si les variables REST sont présentes, sinon
PowerShell. Le module PowerShell actuel du dépôt utilise `Get-VBRJob` et
`Get-VBRBackupSession`. Sur VBR 13.0.2.29, `Get-VBRJob` émet un avertissement
pour les jobs de sauvegarde d'ordinateurs et de copie : vérifier la couverture
de ces types avant de considérer le suivi du portail comme exhaustif. Si la
version locale expose des champs différents, adapter les modules
`scripts/veeam/Veeam.Rest.psm1` ou `scripts/veeam/Veeam.PowerShell.psm1` et
valider le contrat de bout en bout.

## Granularité réelle du statut

Le collecteur transmet l'identifiant **du job Veeam** et le résultat de sa
dernière session. Son rapport ne contient ni chemin de dossier, ni liste des
objets inclus, ni résultat par dossier. La table `backup_integrations` associe
un couple `(provider, external_job_id)` à **un seul** client et **un seul**
service ; ce couple est unique en base. L'API applique le résultat du job au
service mappé. Elle ne vérifie pas que le dossier de ce service figure dans le
périmètre réel du job, qu'il n'est pas exclu, ou qu'un point de restauration
contient ses fichiers.

Dans le cas vérifié ici, le propriétaire confirme que `F:\KoXoDATA` contient
tous les dossiers concernés ; Veeam configure cette racine comme objet du job
`KoXoDATA`, sans masque d'inclusion ou d'exclusion déclaré. La dernière session
et sa tâche sont réussies. On peut donc dire que **l'ensemble des dossiers est
dans le périmètre configuré d'un job ayant réussi**, avec un statut commun au
job. On ne peut pas déduire du seul rapport API qu'un fichier précis figure
dans un point de restauration utilisable.

Le modèle actuel ne peut pas représenter fidèlement ce job partagé entre
plusieurs clients : un identifiant de job ne peut être associé qu'à un seul
couple client/service. Le champ `itemPath` sert à **demander** une restauration
après coup ; il n'est pas une preuve de couverture.

Pour fournir un statut à chaque client, il faudrait représenter le lien entre
son service, son chemin sous `F:\KoXoDATA` et le job partagé. Pour prouver la
récupérabilité d'un dossier donné, il faudrait contrôler un point de
restauration et effectuer un test de restauration adapté.

## Créer un mapping

1. Ouvrir `/admin/backups`.
2. Renseigner `provider = veeam`.
3. Renseigner l'identifiant stable du job Veeam dans `externalJobId`.
4. Renseigner le `customerId` et le `serviceId` du portail.
5. Definir les seuils :
   - intervalle attendu : `1440` minutes pour une sauvegarde quotidienne ;
   - critique apres : `2160` minutes pour 36 h ;
   - collecteur silencieux apres : `180` minutes si la collecte tourne souvent.

Le nom du job Veeam ne doit pas servir a determiner le client. Le mapping est la
source de verite.

## Collecte manuelle

Depuis l'hote collecteur :

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\veeam\Invoke-VeeamBackupCollection.ps1 -Mode auto
```

Test sans envoi :

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\veeam\Invoke-VeeamBackupCollection.ps1 -Mode powershell -WhatIfReport
```

## Planification recommandee

Pour des sauvegardes quotidiennes, lancer le collecteur toutes les 15 a 60
minutes. Le seuil `stale_after_minutes` doit rester superieur a la frequence de
collecte, mais assez bas pour detecter un collecteur silencieux avant que le
client voie un faux etat positif.

## Diagnostic

Verifier dans cet ordre :

1. Le collecteur demarre et liste des rapports en `-WhatIfReport`.
2. `SERVICE_AUTH_TOKEN` est identique cote collecteur et API-INTERNAL.
3. `POST /internal/backups/report` retourne une reponse acceptee.
4. `/admin/backups` affiche une collecte recente.
5. `/backups` affiche uniquement les donnees metier du client connecte.

Les logs ne doivent pas contenir de credentials, chemins SMB, hostnames internes
ou noms de repository. Une sauvegarde `Success` signifie une execution reussie,
pas une restauration testee. N'afficher une verification de restauration que si
un processus reel alimente `last_verified_at` et `verification_status`.
