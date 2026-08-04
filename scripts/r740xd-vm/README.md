# Templates R740xd VM dediees

> **Attention (2026-08-05).** Ce README a ete rapatrie depuis la branche
> `codex/r740xd-automation` **sans la plupart des scripts qu'il decrit**. Seul
> le sous-dossier `srv11/` est present dans `main`. Tout autre fichier cite
> ci-dessous (`configure-linux-baseline.py`, `deploy-srv11.py`,
> `deploy-srv12.py`, `snapshot-public-dns.ps1`, `srv12/`, `srv13/`, `phase2/`…)
> vit encore sur cette branche. Le document sert donc d'inventaire de ce qui
> existe ailleurs, pas de description du contenu de ce dossier.

Ce dossier regroupe les gabarits et scripts de base pour la cible :

- `SRV-11` : reverse proxy `nginx`
- `SRV-12` : `WEBPORTAL` Node.js 24 sous `systemd`
- `SRV-13` : `API-INTERNAL` `.NET 10` en service Windows

Contenu :

- `configure-linux-baseline.py` : audit et configuration idempotente de
  `netplan` et `chrony` sur `SRV-11/12`, avec verification des empreintes SSH
  et sauvegarde distante avant modification. Depend de `paramiko` et accepte
  les modes `audit` et `apply`.
- `deploy-srv11.py` : installation epinglee SSH du vhost HTTP de
  pre-bascule et depot inactif de la configuration TLS finale.
- `srv11/kermaria-nginx-bootstrap.conf` : reverse proxy HTTP permettant de
  valider la chaine avant la bascule DNS/TLS.
- `srv11/kermaria-nginx.conf` : vhost `nginx` TLS final.
- `srv11/activate-kermaria-tls.sh` : validation SAN, expiration et
  correspondance cle/certificat avant activation atomique de TLS.
- `deploy-srv12.py` : deploiement versionne du standalone avec verification
  SHA-256, bascule atomique et rollback sur echec des health checks.
- `verify-linux-reboot.py` : redemarrage controle de `SRV-12`, puis `SRV-11`,
  avec preuve du nouvel identifiant de boot et retour des services/health.
- `snapshot-public-dns.ps1` : inventaire public en lecture seule de la zone,
  y compris les noms absents, avant toute bascule OVH ou Cloudflare.
- `srv12/kermaria-webportal.service` : unite `systemd` pour le serveur
  `Next standalone`.
- `srv12/webportal.env.example` : variables minimales a fournir sur
  `SRV-12`.
- `srv12/install-node-runtime.sh` : installation du binaire officiel Node.js
  24 LTS avec verification SHA-256.
- `srv12/convert-webportal-config-to-env.ps1` : conversion du JSON filtre en
  `EnvironmentFile` systemd sans afficher les valeurs.
- `srv12/kermaria-webportal.logrotate` : rotation quotidienne des journaux
  du portail.
- `srv13/install-api-internal-service.ps1` : creation / mise a jour du
  service Windows `KermariaApiInternal`.
- `srv13/install-dotnet-runtime.ps1` : installation signee des runtimes
  `.NET`, ASP.NET Core et Windows Desktop requis par l'API.
- `srv13/bootstrap-koxo-exchange.ps1` : preparation des dossiers futurs
  `KoXo` sur `D:` avec un chemin canonique sous `C:\ProgramData`.
- `phase2/` : playbook Ansible idempotent pour `SRV-11/12`, controle de
  conformite PowerShell pour `SRV-13` et creation API de l'action
  d'auto-enregistrement Linux dans Zabbix, avec recette authentifiee en
  lecture seule du portail et test de boot restaure sur vSwitch prive.

Ces fichiers ne remplacent pas les runbooks :

- `docs/DEPLOYMENT_R740XD_VM.md`
- `docs/DEPLOYMENT_WINDOWS.md`
- `docs/v0.38/V0.38_KOXO_AUTOMATION_RUNBOOK.md`

Exemple depuis le poste d'administration :

```powershell
python -m pip install paramiko
python scripts/r740xd-vm/configure-linux-baseline.py audit `
  --credentials C:\chemin\vers\MDP.txt
python scripts/r740xd-vm/configure-linux-baseline.py apply `
  --credentials C:\chemin\vers\MDP.txt
python scripts/r740xd-vm/configure-linux-baseline.py verify `
  --credentials C:\chemin\vers\MDP.txt
python scripts/r740xd-vm/deploy-srv11.py `
  --credentials C:\chemin\vers\MDP.txt
python scripts/r740xd-vm/verify-linux-reboot.py `
  --credentials C:\chemin\vers\MDP.txt
```

Une reprise ciblee est possible avec `--component netplan` ou
`--component chrony`.
