# Accès VPN/RDS DEV — état du 2026-10-03

## Périmètre et limite de preuve

Préparation autorisée de connexions VPN/RDS réelles pour une nouvelle identité
DEV, stockage personnel géré par KoXo, Stripe TEST puis résiliation et retrait
des droits. Aucun nouveau client ni achat n'a été exécuté dans cette passe.
Les deux groupes dédiés sont encore vides. Une configuration de droits ne
constitue pas une preuve de connexion ni de révocation.

## Groupes et catalogue

Groupes Global/Security dans
`OU=CLIENTS DEV,OU=Utilisateurs,OU=KoXoAdm,DC=clients,DC=home,DC=bzh` :

| Groupe | SID |
| --- | --- |
| `GG_VPN_E2E_DEV` | `S-1-5-21-2041662347-3856961323-164109739-1690` |
| `GG_RDS_E2E_DEV` | `S-1-5-21-2041662347-3856961323-164109739-1691` |

Les clés `AD_PROVISIONING_GROUP_DNS__GG_VPN_E2E_DEV` et
`AD_PROVISIONING_GROUP_DNS__GG_RDS_E2E_DEV` pointent vers ces DN dans le JSON
API DEV et sa source locale hors Git. Redémarrage DEV et readiness 200 vérifiés.
L'alias de recette fourni par l'utilisateur a été ajouté à l'allowlist SMTP,
en conservant `EMAIL_LIVE_ALLOWLIST_ONLY=true` et les entrées existantes.

Modifications enregistrées par l'interface admin DEV, via BFF :

- `RDS-ACCESS`, service `89eb93c4-b5e4-11f1-aa29-00155d00d106` : règle
  service entier active, ordre 20, cible `GG_RDS_E2E_DEV`.
- `VPN-ACCESS`, service `89eb937b-b5e4-11f1-aa29-00155d00d106` : seule la règle
  `ESSENTIAL`, active, ordre 10, cible désormais `GG_VPN_E2E_DEV`.
  Les quatre autres paliers restent inchangés et hors de la recette.

## RDS

Courtier `KERMARIA-SRV-27.home.bzh`, collection `Clients`, hôte
`KERMARIA-SRV-30.clients.home.bzh`. Le groupe DEV figure maintenant dans les
autorisations de collection et dans le groupe local Utilisateurs du Bureau
à distance de l'hôte (`S-1-5-32-555`). Les trois entrées existantes sont
conservées : `HOME\zhounsah`, `CLIENTS\GG_DEMO_RDS`,
`CLIENTS\GG_RDS_CLIENTS`.

Deux difficultés distinctes ont été observées :

1. Les nouveaux groupes étaient présents sur SRV-21 mais absents des GC
   SRV-17/18/19. Le lien intersites est réglé sur 180 minutes. Une réplication
   **des seuls deux objets groupes DEV** via `Sync-ADObject`, depuis SRV-21
   vers ces trois GC, a permis la résolution des noms sur le courtier.
   Aucun changement de portée de groupe, d'ACL d'annuaire ou de calendrier.
2. `Set-RDSessionCollectionConfiguration` exécuté sous SYSTEM sur le courtier
   a modifié la collection, puis échoué lors de l'accès distant à SRV-30.
   Il s'agissait donc d'un succès partiel. Après relecture de la collection,
   l'ajout exact du même groupe au groupe local RDU de SRV-30 a été terminé
   par `Add-LocalGroupMember`, sous l'administration Kerberos existante,
   avec contrôle du SID et de l'ensemble des membres avant/après.

Les deux tâches ponctuelles de lecture/configuration sur SRV-27 sont désactivées.
Le service de passerelle RDS était arrêté lors de l'inventaire ; aucune
activation de passerelle ni preuve d'accès externe n'est comprise ici.

Préflight complémentaire sur SRV-30 : `TermService` démarré,
`fDenyTSConnections=0`, `SeRemoteInteractiveLogonRight` contient le groupe
local RDU `S-1-5-32-555`. L'export des droits utilisateur ne contient aucune
entrée `SeDenyRemoteInteractiveLogonRight`. Adresse de l'hôte :
`192.168.100.230/24`. Ces prérequis ne prouvent pas encore une ouverture de
session ni la joignabilité depuis le poste VPN final.

## VPN et NPS

SoftEther tourne sur SRV-24. Son interface serveur est `192.168.100.224`.
Le DNS du serveur a renvoyé `10.35.64.253` (une autre interface), expliquant
les premiers échecs WinRM ; aucun changement DNS/réseau n'a été fait.
Le hub `Clients` est en ligne, SecureNAT désactivé, et aucun RADIUS n'est
configuré, constat confirmé par l'utilisateur dans sa session administrateur.

Le fichier de configuration SoftEther ne contient aucun utilisateur dans
le hub `Clients` et référence un pont local pour ce hub. Le pont seul ne
prouve ni l'attribution DHCP ni les autorisations réseau jusqu'à SRV-30.

NPS/IAS est déjà installé et démarré sur SRV-21. Préparation additive :

- client RADIUS `SoftEther-Clients-E2E-DEV`, adresse `192.168.100.224`,
  **désactivé**, Message-Authenticator obligatoire ;
- stratégie `VPN-CLIENTS-E2E-DEV`, ordre 1, **désactivée**, conditions
  cumulatives groupe SID `...-1690`, IP client `192.168.100.224`,
  NAS-Identifier `^Clients$` ;
- autorisation par stratégie, propriétés dial-in individuelles ignorées,
  méthodes PAP et MS-CHAPv2 ; PAP est prévu par la documentation SoftEther
  pour son authentification RADIUS ;
- stratégies de refus existantes conservées. Aucun redémarrage IAS.

Secret aléatoire dédié de 32 octets, jamais affiché ni versionné :
`C:\ProgramData\Kermaria\vpn-dev-e2e-20261003\radius-secret.txt` sur SRV-21.
Répertoire protégé SYSTEM/Administrateurs uniquement, exports NPS avant et
après préparation conservés au même endroit. Ne pas copier ces exports dans Git.

Relais humain nécessaire dans la session SoftEther déjà ouverte : renseigner
RADIUS `192.168.100.221:1812` avec ce secret et régler l'option du hub
`UseHubNameAsRadiusNasId` à `1`. Ne pas substituer une authentification NT
ouverte à tous les utilisateurs du domaine. Après ce raccordement : vérifier
la configuration, activer les éléments NPS préparés, configurer le chemin
d'authentification externe du hub puis effectuer les tests négatif/positif.
Ces étapes ne sont **pas encore réalisées**.

### Reprise après saisie SoftEther

Le serveur RADIUS enregistré est `KERMARIA-SRV-21.clients.home.bzh`, résolu
en `192.168.100.221`, port 1812. L'utilisateur générique `*` du hub est en
authentification RADIUS (type 4). `UseHubNameAsRadiusNasId=true` est désormais
confirmé dans le fichier enregistré. Attention : `AutoSaveConfigSpan=300` ;
ce fichier peut avoir cinq minutes de retard sur la configuration en mémoire.

La stratégie NPS a été activée après contrôle des trois conditions. Le
client RADIUS a ensuite été **redésactivé** : `Set-NpsRadiusClient` renvoie
par défaut un objet contenant le secret partagé dans sa sortie. Une telle
sortie a été produite pendant l'activation. Le secret a immédiatement été
remplacé côté NPS et dans le fichier protégé ; la nouvelle valeur n'a pas
été affichée. Toujours terminer les commandes de mutation NPS par
`| Out-Null`, puis relire une sélection explicite de propriétés non sensibles.

État de reprise : **stratégie active, client RADIUS désactivé**, attente de
la recopie du secret renouvelé par l'utilisateur dans SoftEther. Les valeurs
de préparation « désactivée » ci-dessus décrivent l'état initial. Aucun
test de connexion RADIUS/VPN réussi n'est encore établi.

Après confirmation de l'utilisateur, le client RADIUS a été réactivé, sans
sortie de secret (`Set-NpsRadiusClient ... | Out-Null`). L'export protégé
`enabled-after-rotation.xml` conserve cet état. À 21:20 UTC, la comparaison
en mémoire confirme que le secret renouvelé enregistré par SoftEther
correspond au fichier protégé de SRV-21 ; seule la valeur booléenne de cette
comparaison a été affichée. **État actuel : stratégie et client RADIUS
actifs**, Message-Authenticator obligatoire côté NPS, tests réels à faire.

Le formulaire public DEV a été préparé pour la nouvelle identité fictive
**Noé Valbrume**, avec l'alias de test autorisé. Aucune soumission par l'agent :
relais demandé à l'utilisateur pour hCaptcha, envoi et confirmation e-mail.
La session admin du même navigateur a été déconnectée pour accéder au
parcours public ; une connexion admin sera nécessaire pour approuver ensuite
la demande. Aucun mot de passe de client n'a été saisi par l'agent.

## Preuves et suite

- Preuves locales :
  `C:\Users\zhounsah\Backups\Kermaria\access-e2e-20261003-221913`
  (`created-groups.json`, collection/RDU avant et après).
- Sauvegarde de configuration API DEV sur SRV-13 :
  `C:\ProgramData\Kermaria-dev\access-config-backup-20261003-222844`.
- Nouvelle recette à réaliser : identité fictive distincte, validation email
  et mot de passe par l'utilisateur, achat Stripe TEST confirmé par lui,
  quota personnel réel, accès VPN/RDS, retrait après résiliation et
  conservation du dossier. Ne pas présenter les prérequis ci-dessus comme
  la réussite de cette recette.

Références officielles :
[réplication d'un objet AD](https://learn.microsoft.com/en-us/powershell/module/activedirectory/sync-adobject),
[administration NPS](https://learn.microsoft.com/en-us/windows-server/networking/technologies/nps/nps-admintools),
[attributs NPS](https://learn.microsoft.com/en-us/windows/win32/api/sdoias/ne-sdoias-attributeid),
[commandes de hub SoftEther](https://www.softether.org/4-docs/1-manual/6/6.4).
