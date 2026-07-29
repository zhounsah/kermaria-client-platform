# Deploiement R740xd par VM dediees (SRV-11 / SRV-12 / SRV-13)

Statut : **cible active au 2026-07-29** pour la mise en configuration du
R740xd. Ce runbook remplace comme **chemin nominal** la variante plus
ancienne "mono-hote R740xd" conservee dans
[PRODUCTION_DEPLOYMENT.md](PRODUCTION_DEPLOYMENT.md). Le runbook Windows
split-host de recette sur `SRV-01/02/07` reste utile comme source de
commandes et de preuves de deploiement, mais il n'est plus la topologie
cible.

## 1. Objet

Ce document fige le lot operatoire immediate pour :

- `SRV-11` : reverse proxy d'entree unique ;
- `SRV-12` : `WEBPORTAL` / BFF ;
- `SRV-13` : `API-INTERNAL` sensible ;
- `SRV-02` et `SRV-03` : surfaces de capacite et de reprise, pas cibles
  applicatives de ce lot.

La base `MariaDB` reste hors de ce lot et demeure sur le tier SQL existant.
La logique `KoXo` et l'activation AD de V0.38 restent preparees mais non
activees ici.

## 2. Topologie retenue

### Mapping des VM

| VM | Role | OS invite | IPs prevues | Exposition |
|---|---|---|---|---|
| `SRV-11` | Reverse proxy / TLS | Ubuntu Server | `192.168.100.211`, `192.168.10.211` | `80/443` uniquement |
| `SRV-12` | `WEBPORTAL` / BFF | Ubuntu Server | `192.168.100.212`, `192.168.10.212` | aucune exposition Internet directe |
| `SRV-13` | `API-INTERNAL` sensible | Windows Server 2025 Standard | `192.168.100.213` | reseau prive uniquement |

### Flux nominal

```text
Internet / Cloudflare
  -> SRV-11 (nginx, TLS, logs)
  -> SRV-12 (Node 24, Next standalone, systemd)
  -> SRV-13 (.NET 10, service Windows)
  -> SQL / AD / NAS / KoXo futurs
```

Contraintes figees :

- `SRV-11` est le seul point d'entree public.
- `SRV-12` ne parle qu'a `SRV-13` pour les appels applicatifs prives.
- `SRV-13` est le seul composant autorise a joindre `MariaDB`, `AD`,
  les partages et la future chaine `KoXo`.
- `cloudflared` peut etre installe sur `SRV-11`, mais reste desactive par
  defaut.

## 3. Outils retenus

| Surface | Outil nominal | Usage |
|---|---|---|
| Hyperviseur R740xd | Windows Admin Center + Hyper-V PowerShell | vue operateur, vSwitch, VLAN, checkpoints, sauvegardes |
| `SRV-11` / `SRV-12` | SSH + Bash + `systemd` + `nginx` | bootstrap Ubuntu, services, rotation logs |
| `SRV-13` | PowerShell + WinRM + SCM Windows | service API, ACL, pare-feu, journaux |
| Artefacts Kermaria | `npm`, `dotnet publish`, scripts du repo | build et validation |
| Supervision | checks HTTP + supervision de `SRV-10` | health, certificats, saturation, restart |

Artefacts du repo a reutiliser :

- `scripts/build-api-config.ps1`
- `scripts/build-webportal-config.ps1` pour le chemin Windows historique
- `scripts/r740xd-vm/` pour la cible `SRV-11/12/13`
- `docs/DEPLOYMENT_WINDOWS.md` comme source de build et de garde-fous

## 4. Phase 1 - Mise en configuration exploitable

### Etat d'execution au 2026-07-29

- `SRV-11` : nom/IP valides (`KERMARIA-SRV-11`, `192.168.100.211`),
  `dhcp-identifier: mac` valide par `netplan try`, source chrony interne
  `KERMARIA-SRV-17.home.bzh` selectionnee et horloge synchronisee ;
- `SRV-12` : nom/IP valides (`KERMARIA-SRV-12`, `192.168.100.212`),
  `dhcp-identifier: mac` valide par `netplan try`, source chrony interne
  `KERMARIA-SRV-17.home.bzh` selectionnee et horloge synchronisee ;
- `SRV-12` : Node `24.18.0` et npm `11.16.0` installes depuis l'archive
  officielle verifiee ; release standalone `20260729-193949` active sous
  `systemd`, empreinte d'archive
  `EF53E21262082E538F647335B216956E6F875D32FB7D06B17D2501D93DC8F8D3` ;
  health `live` et `ready` a `200`, y compris la dependance SRV-13 ;
- `SRV-11` : nginx `1.28.3` installe, active et valide sur les trois FQDN
  lorsqu'ils sont diriges vers `192.168.100.211:80` ; configuration TLS
  finale deposee mais inactive, faute de certificat present sur la VM ;
- le DNS interne des trois FQDN pointe encore vers `192.168.100.201` : ne
  basculer DNS/NAT vers SRV-11 qu'apres activation et test du vrai certificat ;
- le DNS public est encore heberge chez OVH (`dns110.ovh.net` et
  `ns110.ovh.net`) : seul `www` possede un enregistrement public vers
  `82.67.32.172`, tandis que `dashboard` et `administration` sont encore en
  `NXDOMAIN`. Cloudflare DNS/proxy n'est donc pas actif au moment de cette
  reception ;
- snapshot public en lecture seule conserve sous
  `.artifacts/r740xd/dns-public-20260729.json` : A racine et `www` vers
  `82.67.32.172`, MX `mx1/mx2/mx3.mail.ovh.net`, SPF
  `include:mx.ovh.com`, aucun AAAA, et `dashboard`, `administration`,
  `clients`, `portfolio`, `tests-mail` absents. Les noms `_dmarc` et les
  selecteurs DKIM courants testes sont egalement absents ;
- test de reprise du `2026-07-29` reussi avec
  `scripts/r740xd-vm/verify-linux-reboot.py` : `SRV-12` a redemarre sur le
  boot ID `064b0a1c-6cd0-428b-a6e9-42d3d91c04c4`, puis `SRV-11` sur
  `cf8e078b-1152-4502-a631-a7cf67706fa6` ; chrony, WEBPORTAL et nginx sont
  revenus `enabled/active`, les sondes SRV-13/SRV-12 ont repondu `200` et le
  proxy a repondu `200` pour les trois FQDN ;
- pools Ubuntu renommes en `ubuntu-ntp-pools.sources.disabled` et
  `/etc/systemd/timesyncd.conf` absent sur les deux VM ;
- sauvegardes de rollback creees sous
  `/var/backups/kermaria-baseline/20260729T114023Z` et
  `/var/backups/kermaria-baseline/20260729T114321Z` sur `SRV-11`, puis
  `/var/backups/kermaria-baseline/20260729T114352Z` sur `SRV-12` ;
- `SRV-13` : FQDN, domaine `home.bzh`, IP `192.168.100.213` et acces WinRM
  authentifie valides. Le service `KermariaApiInternal` est installe sous
  `HOME\svc-kermaria`, demarre automatiquement et repond `200` sur les
  health checks apres redemarrage. MariaDB et le bind LDAP sur l'OU
  `Clients` ont ete verifies depuis la VM ;
- decision operateur : le pare-feu Windows et toute GPO associee sont hors de
  ce lot. Le filtrage reseau sera reetudie uniquement pendant la phase finale
  de durcissement ; aucune modification de domaine n'a ete appliquee.

La verification reproductible est fournie par
`scripts/r740xd-vm/configure-linux-baseline.py verify`.

#### Reception technique SRV-13 du 2026-07-29

| Controle | Preuve relevee apres redemarrage |
|---|---|
| Disque de donnees | disque `1`, GPT, NTFS, `D:` nomme `KERMARIA-DATA`, sain et en ligne, `63,98 Gio` dont `63,84 Gio` libres |
| Runtime | `Microsoft.NETCore.App`, `Microsoft.AspNetCore.App` et `Microsoft.WindowsDesktop.App` en `10.0.10` |
| ACL | heritage coupe sur l'application, le JSON, `Downloads`, `Logs` et `KoXoExchange`; `SYSTEM`/Administrateurs en controle total, `HOME\svc-kermaria` en lecture-execution ou modification selon le besoin |
| Configuration | JSON valide de `75` cles sous `C:\ProgramData\Kermaria`, sans cle de demo; empreinte SHA-256 `88EC4502E424589B7990C4034447B617B1FD5129431DF8401A8DD14F2FD65E7E` |
| Service | `KermariaApiInternal`, compte `HOME\svc-kermaria`, demarrage automatique, code de sortie `0`, trois reprises automatiques a `5 s` |
| Binaire | empreinte identique a l'artefact publie : `B3E6E2EF8A07440DFF6A69E1FC84B3F45552BFA73B70448533CE01D6723D853B` |
| Health | `GET /health/live` et `GET /health/ready` renvoient `200` apres redemarrage; appel `ready` egalement valide depuis `SRV-12` |
| MariaDB | connexion reelle a `test_web` sur `KERMARIA-SRV-06`, identite effective `test_web@%` |
| Active Directory | bind signe reussi sur `OU=Clients,DC=clients,DC=home,DC=bzh` en mode `controlled_write`, sans mutation |
| Journaux | fichier quotidien alimente, aucune fuite des quatre secrets controles et aucun evenement applicatif critique pendant la reception |

La configuration precedente est sauvegardee sous
`C:\ProgramData\Kermaria\backups\20260729-145100`. Les valeurs secretes ne
sont pas reproduites dans ce document.

### 4.1 Baseline hyperviseur

Avant de toucher aux services :

1. verifier le `vSwitch` externe et les VLAN requis (`10`, `90`, `100`) ;
2. figer IP statiques, DNS et NTP des trois VM ;
3. verifier que la sauvegarde VM est fonctionnelle ;
4. verifier qu'aucun checkpoint de prod durable n'est present ;
5. documenter l'hote Hyper-V porteur de chaque VM ;
6. reserver `SRV-02` et `SRV-03` a la capacite, au failover et aux tests de
   restauration.

### 4.2 SRV-11 - Reverse proxy

Objectif :

- terminer TLS ;
- journaliser ;
- relayer vers `SRV-12` ;
- ne rien heberger d'applicatif metier.

Checklist :

1. durcir Ubuntu (`openssh-server`, comptes admin limites, `sudo`,
   `unattended-upgrades`, `ufw`) ;
2. installer `nginx` ;
3. executer `scripts/r740xd-vm/deploy-srv11.py` pour activer le vhost HTTP de
   pre-bascule et deposer le vhost TLS final sans l'activer ;
4. activer uniquement `80/443` cote public et `22` cote reseau admin ;
5. stocker les certificats hors du repo ;
6. verifier les logs d'acces et d'erreur ;
7. garder `HSTS` desactive ou court tant que la bascule n'est pas stabilisee.

Activation TLS, uniquement apres obtention d'un certificat couvrant les trois
FQDN :

```bash
sudo install -o root -g root -m 644 fullchain.pem /etc/ssl/kermaria/fullchain.pem
sudo install -o root -g root -m 600 privkey.pem /etc/ssl/kermaria/privkey.pem
sudo /usr/local/lib/kermaria/activate-kermaria-tls.sh
```

Le script refuse un certificat expirant dans moins de 30 jours, un SAN
manquant ou une cle privee qui ne correspond pas. Il restaure le vhost HTTP si
`nginx -t` echoue ou si nginx ne sert pas le certificat attendu. Un certificat
Cloudflare Origin est adapte si les trois noms restent proxies par Cloudflare
avec le mode `Full (strict)` ; il n'est pas approuve lors d'un acces navigateur
direct a l'origine. Aucun certificat autosigne ne doit etre utilise pour la
bascule publique.

Choix a figer avant emission du certificat :

- cible nominale du present plan : migrer/ajouter les enregistrements chez
  Cloudflare, activer le proxy sur les trois FQDN, puis utiliser un certificat
  Cloudflare Origin et le mode `Full (strict)` ;
- variante sans migration DNS : conserver les serveurs DNS OVH, ajouter les
  deux noms manquants et emettre un certificat public Let's Encrypt par
  challenge DNS-01 OVH.

Le changement de serveurs de noms, des enregistrements publics et de la NAT
est une bascule externe : il doit etre confirme et execute seulement apres le
test TLS local force vers `192.168.100.211`.

Avant toute bascule, regenerer la preuve DNS :

```powershell
scripts/r740xd-vm/snapshot-public-dns.ps1 `
  -OutputPath .artifacts/r740xd/dns-public-pre-cutover.json
```

### 4.3 SRV-12 - WEBPORTAL

Objectif :

- executer le `Next standalone` sous `systemd` ;
- sortir toute configuration du paquet ;
- exposer un health local, pas public.

Checklist :

1. installer Node.js 24 LTS ;
2. deployer le paquet standalone versionne avec
   `scripts/r740xd-vm/deploy-srv12.py` ;
3. creer l'utilisateur systeme `kermaria-web` sans shell interactif ;
4. creer `/etc/kermaria/webportal.env` a partir du gabarit
   `scripts/r740xd-vm/srv12/webportal.env.example` ;
5. verifier que `INTERNAL_API_URL` vise `http://192.168.100.213:5000` ;
6. installer le service `scripts/r740xd-vm/srv12/kermaria-webportal.service` ;
7. ouvrir seulement `22` cote admin et `3000` depuis `SRV-11` ;
8. valider
   `curl http://192.168.100.212:3000/api/health/live` et
   `/api/health/ready`.

### 4.4 SRV-13 - API-INTERNAL

Objectif :

- executer l'API en service Windows natif ;
- borner les ACL ;
- preparer le futur point d'entree `KoXo`.

Checklist :

1. installer le runtime `.NET 10` ;
2. publier `apps/api-internal` en `win-x64` depuis le poste de dev ;
3. copier le publish vers `C:\apps\api-internal` ;
4. generer `C:\ProgramData\Kermaria\api-internal.config.json` avec
   `scripts/build-api-config.ps1` ;
5. installer ou mettre a jour le service avec
   `scripts/r740xd-vm/srv13/install-api-internal-service.ps1` ;
6. preparer `C:\ProgramData\Kermaria\koxo-exchange\...` avec
   `scripts/r740xd-vm/srv13/bootstrap-koxo-exchange.ps1` ;
7. valider `curl.exe http://192.168.100.213:5000/health/ready`.

### 4.5 Cutover, restart et rollback

Ordre nominal :

1. sauvegarder les configurations et l'etat precedent ;
2. demarrer `SRV-13` et valider ses health checks ;
3. demarrer `SRV-12` et valider les health du portail ;
4. activer le certificat TLS sur `SRV-11`, tester en forcant la resolution
   locale vers `.211`, puis seulement ouvrir/basculer le trafic ;
5. rejouer les checks externes.

Ordre de restart :

1. `SRV-13`
2. `SRV-12`
3. `SRV-11`

La preuve automatisee pour les deux VM Linux s'execute depuis le poste
d'administration :

```powershell
python scripts/r740xd-vm/verify-linux-reboot.py `
  --credentials C:\chemin\vers\MDP.txt
```

Rollback minimal :

1. retirer `SRV-11` du trafic ;
2. restaurer la config `nginx` precedente ;
3. reployer l'artefact precedent de `SRV-12` ou `SRV-13` si le probleme est
   applicatif ;
4. redemarrer dans l'ordre `SRV-13 -> SRV-12 -> SRV-11` ;
5. rejouer les health checks et les parcours critiques.

## 5. Tableau des ports et flux

| Source | Destination | Port | Statut | Usage |
|---|---|---|---|---|
| Internet / Cloudflare | `SRV-11` | TCP `80/443` | autorise | entree publique |
| Reseau admin / VPN | `SRV-11` | TCP `22` | autorise | SSH admin |
| `SRV-11` | `SRV-12` | TCP `3000` | autorise | reverse proxy Node |
| Reseau admin / VPN | `SRV-12` | TCP `22` | autorise | SSH admin |
| `SRV-12` | `SRV-13` | TCP `5000` | autorise | appels BFF -> API |
| Reseau admin / VPN | `SRV-13` | TCP `5986` ou `3389` | autorise | WinRM/RDP admin |
| `SRV-13` | SQL existant | TCP `3306` | autorise | MariaDB |
| `SRV-13` | AD / DNS / Kerberos / LDAPS | ports minimaux documentes | autorise sous controle | identite |
| `SRV-13` | partage `KoXo` / NAS | SMB ou chemin local dedie | futur | lots et archives |
| Internet | `SRV-12` / `SRV-13` | tout | interdit | aucune exposition |
| `SRV-12` | SQL / AD / NAS | tout | interdit | separation stricte |
| Navigateur client | `SRV-13` | tout | interdit | pas d'acces direct |

## 6. Matrice de dependances

| Composant | Dependances bloquantes | Indicateurs de sante |
|---|---|---|
| `SRV-11` | DNS, certificats, reachability `SRV-12:3000` | `nginx -t`, HTTP `200/301`, logs propres |
| `SRV-12` | Node 24, artefact standalone, reachability `SRV-13:5000` | `/api/health/live`, service `systemd`, logs Node |
| `SRV-13` | `.NET 10`, config JSON, reachability SQL/AD | `/health/live`, `/health/ready`, Event Log, logs fichiers |
| `WEBPORTAL` | `SERVICE_AUTH_TOKEN`, `INTERNAL_API_URL`, FQDN coherents | login, BFF, headers, cookies |
| `API-INTERNAL` | `SQL_*`, `SERVICE_AUTH_TOKEN`, `AD_*` selon mode | readiness MariaDB, auth, refus de roles croises |
| `KoXo` futur | AD ok, depot lots, tache planifiee | lots `processed/failed`, etat de rejeu |

## 7. Phase 2 - Viabilite future

### Identite et provisioning

- brancher la creation AD au `set-password` ;
- emettre ensuite un lot `CSV/XML` ;
- traiter ce lot localement sur `SRV-13` via tache planifiee ;
- garder `Kermaria` source de verite et `KoXo` asynchrone.

### Automatisation

- `Ansible` pour `SRV-11` et `SRV-12` ;
- PowerShell signe puis `DSC/GPO` pour `SRV-13` ;
- secrets hors VM et hors repo ;
- images de reference et patching mensuel.

### Exploitation

- supervision systeme + HTTP sur `SRV-11/12/13` ;
- sauvegarde des configs `nginx`, `systemd`, service Windows et JSON ;
- test de restauration par VM avant toute replication ou repositionnement
  entre `SRV-01/02/03`.

## 8. Verification minimale avant ouverture

1. test reseau complet `SRV-11 -> SRV-12 -> SRV-13 -> SQL/AD` ;
2. refus explicite des flux interdits pendant la phase finale de durcissement
   reseau, hors du lot actuel `SRV-13` ;
3. demarrage correct des services `nginx`, `WEBPORTAL`, `API-INTERNAL` ;
4. reponse `200` sur `health/live` et `health/ready` la ou attendu ;
5. login client et admin sans mutation destructive ;
6. absence de secrets dans stdout/stderr et dans les journaux.
