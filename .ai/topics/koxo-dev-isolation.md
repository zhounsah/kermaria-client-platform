---
name: koxo-dev-isolation
description: "Isolation KoXo DEV/PROD : namespace API (CLI-D, DEV-CLI-, CLIENTS DEV), instance SRV-21 isolée (receveur 8043), mutex Global\\Kermaria-KoXoAdm, stockage DEV FS-01. Run 1/2 et E2E identité standard PASS les 2026-09-27/28 ; stockage headless non validé."
metadata:
  type: project
---

Constat du 2026-09-26 : DEV et PROD partagent `clients.home.bzh` et `KoXoAdm.exe`.
La PROD porte déjà `employeeNumber` CLI-000001 à CLI-000003, et le compteur de
`kermaria_dev` repartait de 1. Le lanceur SRV-21 rechargeait sans condition les
variables Machine `KOXO_*`, qui pointent sur l'export PROD.

Fondations logicielles (commit `f15e314`) :

- **API** : `KoxoNamespace` (`KOXO_IDENTIFIER_PREFIX`, `CUSTOMER_REFERENCE_PREFIX`,
  `KOXO_PRIMARY_GROUP_CLIENTS`, `KOXO_PRIMARY_GROUP_DEMO`).
  - Défaut = production au bit près ; tout ou rien.
  - Le garde-fou `APP_ENV` exige un namespace DEV dès que `controlled_write` ou
    `KOXO_SYNC_WEBHOOK_URL`, et interdit tout namespace hors production en PROD.
- **SRV-21** : `Resolve-KoxoSyncLaunchPlan`.
  - Sans définition, c'est le plan PROD historique.
  - Avec une définition JSON, toute variable `KOXO_*` est retirée du processus
    et chaque réglage vient de la définition, jetons lus dans des fichiers.
  - Une définition qui reprend un élément PROD est refusée : groupe, CSV,
    profil, port 8042, répertoire, URL ou jeton.
- **Verrou** : mutex système `Global\Kermaria-KoXoAdm` dans `Invoke-KoxoProcess`.
  Nom non configurable, délai explicite (600 s), libération en `finally`,
  reprise d'un mutex abandonné.

Infrastructure DEV en place au 2026-09-27 (état détaillé en tête de
`docs/KOXO_DEV_ISOLATION.md`) :
- scripts `f15e314` sur SRV-21 ; lanceur d'instance corrigé dans `8336cc1`
  (non redéployé, la tâche 8043 n'en dépend pas) ;
- groupe primaire, modèle et profil `CLIENTS DEV` ; fiche `AllowRDS=0`,
  `AllowDialin=0`, quotas désactivés ;
- lieux de stockage `Espaces mutuels "Clients DEV"` et `Espaces personnels
  "Clients DEV"` sur `KERMARIA-FS-01 F:\KoXoDATA\CLIENTS DEV`, « Sans
  partage », sans quota ;
- modèle `CLIENTS DEV` : seules les primitives `Group/Root` et `User/Root`
  (dossier `%USER_ID%`) de `CLIENTS`, à l'identique, sans partage, contenu,
  profil ni quota ;
- définition et jetons dans `C:\ProgramData\Kermaria\koxo-dev\` ; receveur
  `:8043` en tâche planifiée ;
- réseau : `:3100` réservé à SRV-11, `X-Forwarded-For $remote_addr` sur le
  vhost DEV (SRV-12 voit `192.168.100.221` pour SRV-21).

**Tests du 2026-09-27 : PASS.** KoXoAdm lancé en SYSTEM (tâche planifiée à
usage unique) via `Invoke-KoxoProcess`, sous le mutex :
- Run 1 : `/Synchro=CLIENTS-DEV.xml` crée OU et groupe `DEV-CLI-TST001` et
  `devkoxo.testisola` (`CLI-D999901`), sans rien toucher hors `CLIENTS DEV` ;
- Run 2 : la ligne remplacée par `CLI-D999902` ; l'ancien compte est
  **désactivé** (non supprimé, appartenance conservée), le nouveau créé actif.

## E2E identité standard DEV — PASS le 2026-09-28

L'E2E manuel a exercé le parcours normal sans toucher à PROD : signup,
vérification e-mail, approbation, définition du mot de passe, publication
KoXo, synchronisation et convergence du bootstrap.

- L'approbation a créé le client `DEV-CLI-PDXVX6`, l'utilisateur portail et
  `CLI-D000001`, avec un PIB `awaiting_password`.
- Le webhook `signup_approved` a répondu `202`, mais l'export ne publiait
  encore aucune identité avant la définition du mot de passe : le profil
  `CLIENTS DEV` a été ignoré sans CSV ni lancement KoXoAdm.
- Après le password setup normal, le PIB est passé par `koxo_pending`, le
  webhook `password_set` a répondu `202`, et `clients-dev.csv` a contenu une
  seule identité DEV. Aucun secret, hash ou colonne mot de passe n'est
  documenté ici.
- `/Synchro=CLIENTS-DEV.xml` a pris le mutex
  `Global\Kermaria-KoXoAdm`, lancé KoXoAdm et terminé avec le marqueur de
  succès et le code de sortie `0` ; aucune opération de réparation n'a été
  demandée.
- KoXo a créé `DEV-CLI-PDXVX6` sous `CLIENTS DEV`, puis Melis Rochedune active
  avec `employeeNumber=CLI-D000001`. L'utilisateur n'a reçu ni `GG_VPN` ni
  `GG_RDS`.
- La convergence a adopté l'objet strictement par `employeeNumber`, créé
  `customer_ad_links`, acquitté le secret KoXo et terminé le PIB à `completed`
  (`PRIMARY_IDENTITY_COMPLETED`).
- Le stockage est hors périmètre de cette preuve et la matérialisation
  headless reste non validée.

### Dettes connues après l'E2E

1. `scripts/dev-env/Install-ApiInternalDev.ps1` régénère la configuration DEV
   depuis une source qui contient encore des valeurs LIVE/PROD : ne pas
   l'utiliser avant assainissement.
2. Le fichier `dev.env.ps1` / la source DEV associée ne doit pas devenir une
   source globale tant que les valeurs LIVE/PROD n'en sont pas retirées.
3. `RepairSecondaryGroup Type=Storage` en headless n'est pas validé.
4. `CLIENTS DEV DEMO` n'existe pas encore ; le profil démonstration DEV reste
   refusé de façon sûre.
5. Les objets des Run 1/2 (`DEV-CLI-TST001`, `CLI-D999901`, `CLI-D999902`)
   sont volontairement toujours présents.

Mesuré :
- **`/Synchro` ne crée aucun dossier.** L'arborescence vient d'une réparation
  de stockage. Une réparation complète interactive (IHM) l'a produite
  correctement. En SYSTEM, `/RepairSecondaryGroup … Type="Storage"` s'est
  bloqué sans écrire (0 % CPU, threads `UserRequest`, probable boîte de dialogue
  invisible) jusqu'au délai de 1800 s, en tenant le mutex : **stockage headless
  non validé**.
- À la création d'un groupe primaire, l'IHM ajoute le groupe AD à
  `Utilisateurs de KoXo Administrator CLIENTS` (`CLIENTS-KOXO-USERS`) ; ce
  groupe ne donne aucun droit de stockage.
- **La fiche, pas le modèle, porte les droits effectifs** : l'IHM avait posé
  `AllowRDS=1` malgré `AllowRDS=0` dans le modèle (même écart en PROD).
- **Ne jamais créer un lieu de stockage sur la racine `KoXoDATA`.** Le
  2026-09-27, sa suppression a fait tenter à KoXo d'effacer tout
  `F:\KoXoDATA\` (échec, aucune perte). Il reste à corriger : l'ACE HOME
  remplacée par `CLIENTS-KOXO-ADM` sur `F:\KoXoDATA`, et `CLIENTS` qui n'est
  plus protégé.
- Lancer KoXoAdm depuis une session WinRM échoue sur FS-01 (double saut) :
  passer par une tâche SYSTEM, comme le receveur.

Pièges mesurés :
- Sous Windows PowerShell 5.1, `-File` n'évalue pas `$PSScriptRoot` dans les
  valeurs par défaut des paramètres. Lancer les scripts avec
  `-Command "& '…'"`.
- Une tâche planifiée qui lance un `.cmd` dont le chemin contient un espace ne
  doit pas recevoir d'argument entre guillemets : `cmd /c` les retire.
- `RandomNumberGenerator.GetBytes(int)` n'existe pas sous .NET Framework
  (SRV-21) ; utiliser `RNGCryptoServiceProvider`.
- `sudo` sur SRV-11 est interactif.
- SRV-21 est aussi contrôleur de domaine de `clients.home.bzh` et porte le rôle
  DHCP. Le 2026-09-27, `Utilisateurs DHCP` et `Administrateurs DHCP` y ont été
  créés dans `CN=Users` 31 s avant le groupe `CLIENTS DEV`. Ils sont hors
  `OU=KoXoAdm`, vides et sans droit. Origine non établie ; ne pas y toucher.
- Un lieu de stockage KoXo, c'est :
  - un fichier `Data\Storages\<nom>.xml` : `Name`, `Server` (pris dans la liste
    `Servers` de `Config.xml`, aujourd'hui SRV-21 et FS-01), `Drive`, `Path`,
    `MountingPointModel`, quota ;
  - le modèle de point de montage « Sans partage », qui crée le dossier avec
    des ACE Administrateurs, SYSTEM et `CLIENTS-KOXO-ADM`, sans partage.

  FSRM n'est installé que sur FS-01.
- Pour prouver ce que SRV-12 reçoit de SRV-11, `tcpdump` sur `:3100`
  (`kermaria_ai_admin`, sudo sans mot de passe), filtré sur un marqueur de
  requête. N'afficher que les en-têtes voulus, jamais les cookies.

La prochaine étape n'est pas couverte par cette note : l'E2E identité standard
est terminé et validé. Toute extension doit repartir des dettes ci-dessus et
d'une autorisation explicite.

Piège de test mesuré : un mutex nommé disparaît avec sa dernière poignée. Pour
prouver la reprise d'un mutex abandonné, un autre processus doit garder une
poignée ouverte. Et `Copy-Item` conserve la date de modification : après la
restauration d'un fichier C#, MSBuild peut garder la DLL mutée, donc il faut
toucher le fichier.

Voir aussi [[dev-environment]], [[koxo-api-ne-cree-plus]], [[primary-identity-bootstrap]].
