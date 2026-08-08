# Suivi des sauvegardes Veeam dans le portail client

## Architecture

Le portail ne contacte jamais Veeam depuis le navigateur ni depuis une route
publique. Le flux attendu est :

```text
Veeam Backup & Replication
  -> collecteur interne PowerShell
  -> API-INTERNAL POST /internal/backups/report
  -> MariaDB
  -> espace client /backups
```

Le collecteur est a installer sur SRV-16 ou sur un hote interne disposant soit
du module `Veeam.Backup.PowerShell`, soit de l'acces HTTPS a l'API REST VBR.
Sur SRV-16, le module Veeam detecte exige PowerShell 7 : utiliser `pwsh.exe`,
pas `powershell.exe` 5.1.

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

Le collecteur choisit REST si les variables REST sont presentes, sinon
PowerShell. Les docs officielles Veeam publient les references REST VBR v12.3 et
v13, dont `/api/v1/sessions`, ainsi que les cmdlets PowerShell
`Get-VBRJob` et `Get-VBRBackupSession`. Si une version locale expose des champs
differents, adapter uniquement les modules `scripts/veeam/Veeam.Rest.psm1` ou
`scripts/veeam/Veeam.PowerShell.psm1`.

## Creer un mapping

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
