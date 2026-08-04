# Automatisation phase 2 R740xd

> **Attention (2026-08-05).** Ce README a ete rapatrie seul depuis la branche
> `codex/r740xd-automation` : **aucun** des playbooks Ansible, roles et
> fichiers d'inventaire qu'il decrit n'est present dans `main`. Ils vivent sur
> cette branche. A lire comme une specification de ce qui existe ailleurs.

Ce dossier rend reproductible l'etat valide de `SRV-11`, `SRV-12` et
`SRV-13` sans stocker de secret. Il ne gere ni `SRV-16`, ni Veeam, ni le
pare-feu Windows, ni les GPO.

## Linux avec Ansible

Prerequis du poste de controle : Ansible Core, OpenSSH, Python et un acces
SSH/sudo aux deux VM. Installer aussi la collection UFW epinglee :

```bash
ansible-galaxy collection install -r ansible/collections/requirements.yml
```

Depuis le dossier `phase2` :

```powershell
Set-Location ansible
```

1. Copier `inventory.example.yml` vers `inventory.yml`.
2. Construire le fichier `known_hosts` apres verification des empreintes :

   ```powershell
   python prepare-known-hosts.py `
     --credentials C:\chemin\vers\MDP.txt
   ```

3. Executer d'abord le mode controle :

   ```bash
   ansible-playbook site.yml --ask-pass --ask-become-pass --check --diff
   ```

4. Appliquer ensuite, un hote a la fois :

   ```bash
   ansible-playbook site.yml --ask-pass --ask-become-pass
   ```

5. Verifier le resultat depuis Windows, avec les memes empreintes epinglees :

   ```powershell
   python ../verify-linux-phase2.py --credentials C:\chemin\vers\MDP.txt
   ```

Le playbook gere `chrony`, le depot officiel et l'agent actif Zabbix, nginx
sur `SRV-11`, ainsi que l'unite `systemd` et `logrotate` sur `SRV-12`. Il
active aussi UFW : SSH depuis `172.16.90.0/24` sur les deux VM, HTTP/HTTPS
publics sur `SRV-11`, et `SRV-12:3000` uniquement depuis `SRV-11`. Les
certificats TLS, `/etc/kermaria/webportal.env` et les releases applicatives
sont seulement verifies : ils ne sont jamais copies depuis Git.

## Zabbix

Le script `zabbix/Set-LinuxAutoregistrationAction.ps1` cree ou remet en
conformite l'action d'auto-enregistrement `LinuxServer`. Sans `-Apply`, il
reste en lecture seule.

```powershell
$token = Read-Host "Jeton API Zabbix" -AsSecureString
./zabbix/Set-LinuxAutoregistrationAction.ps1 -ApiToken $token
./zabbix/Set-LinuxAutoregistrationAction.ps1 -ApiToken $token -Apply
```

`zabbix/Set-KermariaHealthMonitoring.ps1` gere les checks applicatifs sans
ouvrir `SRV-12:3000` a `SRV-10` : scenario HTTPS de bout en bout sur `SRV-11`,
scenario prive de l'API sur `SRV-13`, et cle native active executee localement
par l'agent de `SRV-12`. Les trois alertes attendent trois echecs consecutifs
ou cinq minutes sans donnee. Le mode sans `-Apply` est un audit.

```powershell
$credential = Get-Credential -UserName Admin
./zabbix/Set-KermariaHealthMonitoring.ps1 -Credential $credential
./zabbix/Set-KermariaHealthMonitoring.ps1 `
  -Credential $credential `
  -Apply
```

La session API obtenue avec les identifiants est fermee en fin d'execution.
Un jeton API peut etre fourni avec `-ApiToken` a la place de `-Credential`.
Apres application, relancer la commande sans `-Apply` : les six lignes doivent
etre `conformant`. Dans Zabbix, les valeurs attendues sont `web.test.fail=0`,
un code `200` pour chaque scenario et `healthy` pour l'item local de `SRV-12`.

## SRV-13

`powershell/Test-Srv13DesiredState.ps1` est un controle sans mutation. Il
verifie le runtime, les chemins, les ACL, le service et le health check sans
afficher les valeurs de configuration.

```powershell
./powershell/Test-Srv13DesiredState.ps1
./powershell/Test-Srv13DesiredState.ps1 -AsJson
```

## Test de boot Hyper-V isole

`powershell/Test-HyperVRestoreBoot.ps1` valide ponctuellement qu'une copie de
restauration peut demarrer sur un autre hote. Ce n'est ni une replication, ni
un placement de production. Le script refuse une configuration source hors de
`D:\Kermaria\RestoreTests`, connecte la VM uniquement a un vSwitch prive,
desactive son demarrage automatique et l'arrete dans tous les cas. Par defaut,
il supprime ensuite seulement l'inscription Hyper-V et conserve les fichiers
du test.

Lancer le script localement, dans une console PowerShell elevee de l'hote de
restauration choisi, apres verification de l'espace disque et de l'absence de
VM portant le meme nom :

```powershell
./powershell/Test-HyperVRestoreBoot.ps1 `
  -SourceVmcx 'D:\Kermaria\RestoreTests\<baseline>\<vm>\Virtual Machines\<id>.vmcx' `
  -TestVmName 'RESTORE-TEST-SRV11-AAAAMMJJ' `
  -AuthorizeIsolatedBoot
```

`-AuthorizeIsolatedBoot` est obligatoire pour rendre l'operation explicite.
Ne pas utiliser `-KeepImportedVm` sauf besoin d'analyse : cette option laisse
la VM arretee mais inscrite. Toute suppression ulterieure des fichiers de test
reste une operation manuelle distincte.

## Recette authentifiee en lecture seule

`powershell/Test-PortalAuthenticatedReadOnly.ps1` valide les connexions client
et admin, les cookies, les principales pages et API de lecture, ainsi que le
refus d'une route admin avec la session client. Le fichier d'identifiants n'est
jamais execute : seules quatre affectations PowerShell litterales `DEMO_*` sont
lues via l'AST, sans afficher leurs valeurs. Les deux sessions sont fermees en
fin de controle.

```powershell
./powershell/Test-PortalAuthenticatedReadOnly.ps1 `
  -CredentialFile C:\chemin\vers\kermaria-client-platform.local.env.ps1

./powershell/Test-PortalAuthenticatedReadOnly.ps1 `
  -CredentialFile C:\chemin\vers\kermaria-client-platform.local.env.ps1 `
  -AsJson
```

Le verificateur MariaDB peut aussi confirmer anonymement les comptes presents
depuis `SRV-13` :

```powershell
C:\ProgramData\Kermaria\tools\verify-mariadb\Kermaria.VerifyMariaDb.exe `
  --account-summary C:\ProgramData\Kermaria\api-internal.config.json
```
