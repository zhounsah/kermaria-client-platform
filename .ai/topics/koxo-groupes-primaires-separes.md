---
name: koxo-groupes-primaires-separes
description: "Séparer clients payants (CLIENTS) et démos (CLIENTS DÉMO) en deux groupes primaires KoXo : mesuré le 2026-08-06. Parfait pour les identités NEUVES, cassé pour migrer une identité existante (perte définitive du groupe secondaire)."
metadata: 
  node_type: memory
  type: project
  originSessionId: d79dbe2b-9ae3-4e1a-bc48-9b5d713aee39
  modified: 2026-08-06T11:47:50.272Z
---

**LIVRÉ ET EN PRODUCTION le 2026-08-06** (commit `b73045e`) : l'API publie
`groupePrimaire` (schemaVersion 2), `Invoke-KoxoSyncProfiles` enchaîne les deux
profils sur un **unique** appel à l'API, et Roselyne a effectivement migré vers
`OU=DEMO-CLI-67RYTC,OU=CLIENTS DÉMO` — mot de passe, RDS et dossier personnel
intacts, mais **`GG_DEMO_VPN` / `GG_DEMO_RDS` perdus au déplacement** et
réappliqués à la main.

⚠️ **L'ordre des profils compte** : le lanceur fait `CLIENTS` puis
`CLIENTS DÉMO`, correct pour le sens habituel (démo → payant), car la destination
reprend l'identité avant que l'origine ne balaye ses orphelins. En sens inverse,
passer la destination **d'abord**, à la main — sinon l'identité est **supprimée**
(pas désactivée, voir [[koxo-orphelins-supprimes]]) avant d'être reprise.

Décision de ZH (2026-08-06) : un modèle KoXo ne peut être associé qu'à **un
seul groupe primaire**, d'où la séparation `CLIENTS` (payants) /
`CLIENTS DÉMO` (démos), le groupe secondaire des démos reprenant
l'identifiant réservé nommé par l'API (`koxo_group_reference`, ex.
`CLI-67RYTC`) **préfixé `DEMO-`**.

**L'argument le plus fort n'est pas le modèle** : aujourd'hui un seul CSV
pilote un seul groupe primaire avec `DisableOrphanedAccounts=1`, donc une
anomalie d'export côté démo peut désactiver de vrais clients payants. Séparer
cloisonne le rayon d'action de chaque synchro.

## Mesuré en réel sur SRV-21

| Cas | Résultat |
|---|---|
| Profil visant un groupe primaire **inexistant** | **no-op total et silencieux** : journal = « Paramètre accepté » puis « Fin de l'opération », rien d'autre. Aucune création, aucune erreur. |
| Identité **neuve** sous le nouveau groupe primaire | **parfait** : OU créée, groupe secondaire créé et l'utilisateur dedans, mot de passe de la colonne 14 appliqué et authentifiant. |
| Identité **existante** migrée depuis `CLIENTS` | **déplacée mais amputée** (voir ci-dessous). |

### Le détail de la migration

- Il faut **deux passages** : le premier crée l'OU cible sous le nouveau
  groupe primaire sans rien déplacer (ce qui donne l'illusion d'un échec), le
  second déplace l'identité.
- Sont **préservés** : `sAMAccountName`, `employeeNumber`, compte actif, mot de
  passe (réappliqué depuis la colonne 14), et la fiche KoXo suit dans
  `Data\Users\<GROUPE PRIMAIRE>\<groupe secondaire>\`.

### LA condition : des noms de groupes secondaires DISTINCTS entre branches

Cause identifiée grâce à une hypothèse de ChatGPT, puis **vérifiée** :

| Nom du groupe secondaire | Résultat de la migration |
|---|---|
| **identique** dans les deux branches (`CLI-TEST` / `CLI-TEST`) | l'utilisateur se déplace mais **perd son groupe, définitivement**. 3 passages et la suppression de la coquille de l'ancienne branche n'ont rien rétabli. |
| **distinct** (`CLI-TEST` → `DEMO-CLI-TEST`) | **tout fonctionne** : OU créée, groupe créé, utilisateur membre. |

KoXo ne recrée un groupe secondaire dans l'AD que s'il est **nouveau pour sa
propre base**. Avec le même nom des deux côtés, il le croit déjà existant et ne
crée jamais l'objet AD dans la nouvelle branche.

**Conclusion** : nommer les groupes secondaires différemment selon la branche,
p. ex. `DEMO-CLI-67RYTC` côté démo et `CLI-67RYTC` côté définitif. À cette
condition, la migration démo → payant par CSV fonctionne proprement.

## Le groupe primaire ne se crée pas par le CSV

Contrairement au groupe **secondaire**, que KoXo crée à la volée, le groupe
**primaire** doit préexister — création via l'IHM. Graphie à contrôler au bit
près : `CLIENTS DÉMO` = `43 4c 49 45 4e 54 53 20 44 c3 89 4d 4f`. Un accent ou
une casse différente entre l'IHM et le profil donne le no-op silencieux.

## Garde-fous livrés (commit `a4c1f4c`, déployés sur SRV-21)

- `Test-KoxoIdentifierOwnership` : refuse la synchro si un `IdentifiantUnique`
  figure aussi dans un autre CSV, et nomme le fichier fautif. Les autres CSV se
  déclarent dans `KOXO_OTHER_CSV_PATHS` (posée sur SRV-21 vers
  `clients-demo.csv`). Vérifié en réel : cas nominal passant, cas fautif bloqué.
- `Test-KoxoLogOutcome` compte désormais les identités traitées et **échoue si
  ce compte est nul alors que le CSV en publie** — c'était le no-op silencieux.
  Volontairement pas de comparaison exacte : un déplacement entre groupes
  primaires ne journalise personne au premier passage, et ce passage est
  légitime.

## Si on met la séparation en place

- deux profils KoXo (deux XML), chacun son `<PrimaryGroup>` et son `<File>` ;
- `Sync-KoXoClients.ps1` est déjà paramétrable (`-CsvTargetPath`,
  `-KoxoSyncArgument`) : deux invocations suffisent ;
- **piège** : `koxo-sync.state.json` est unique et sert de référence au
  garde-fou de volumétrie. Deux profils alternant 2 puis 1 utilisateurs
  verraient une chute de 50 % et **bloqueraient**. Il faut un état par profil ;
- côté API, `KoxoExportService.ResolveGroupeSecondaire` doit rendre
  `koxo_group_reference` pour les démos au lieu de la constante `CLI-DEMO`, et
  l'export doit être scindé par groupe primaire.

## Ce qu'on ne peut PAS automatiser en ligne de commande

`/AddSecondaryGroup Group="X" PrimaryGroup="Y"` est **accepté** par KoXo
(« Paramètre accepté » au journal) mais **ne crée rien** — ni dans l'AD, ni dans
sa propre base. La variante `/AddSecondaryGroup=X` est mal découpée sur l'espace
et rejetée. Ne pas bâtir de procédure de promotion dessus : passer par le CSV,
qui crée bien les groupes secondaires à la volée, ou par l'IHM.

## Le modèle `CLIENTS DÉMO` (fiche `Data\Users\CLIENTS DÉMO.xml`)

`UserFolderQuota = 5120` (5 Go, contre 32768 pour CLIENTS), `AllowDialin = 2`,
`PasswordNeverExpires = 1`.

**`AllowRDS` était à l'envers** (`1` en démo, `0` en payant) : non voulu,
**corrigé le 2026-08-06**, `CLIENTS` passé à `1` (sauvegarde
`CLIENTS.xml.avant-rds.bak`).

⚠️ **CORRIGÉ le 2026-08-06 (fin de journée)** : ce n'est pas « appliqué
seulement à la création ». KoXo garde une **fiche par utilisateur** et la
réapplique à chaque synchro — `allowLogon` posé par ADSI est donc **écrasé**.
Le seul correctif qui tient est d'éditer la fiche. Voir
[[koxo-fiche-utilisateur-maitre]], qui remplace ce paragraphe.

Le drapeau se lit et s'écrit par ADSI, pas par `Get-ADUser` : KoXo l'écrit dans
le blob hérité `userParameters`, `msTSAllowLogon` restant vide.
```powershell
$de = New-Object System.DirectoryServices.DirectoryEntry("LDAP://$dn")
$de.InvokeGet('allowLogon'); $de.InvokeSet('allowLogon', 1); $de.CommitChanges()
```
Les trois comptes existants ont été remis à `1` ainsi le 2026-08-06.

**`UserCannotChangePassword` : corrigé par ZH le 2026-08-06 à 12:29/12:30** sur
les deux modèles (le drapeau, c'est la case « Le mot de passe est fixe » de
l'onglet Compte). Bonne nouvelle mesurée dans la foulée : **un changement de
modèle se propage aux comptes existants à la synchro suivante** — `jean.dupont`
est passé de `True` à `False` sans intervention, groupes intacts. Corriger le
modèle suffit donc, inutile de reprendre les comptes un par un.

Stratégie de nommage du login, visible dans le même onglet :
`%FIRST_NAME[10]%.%NAME[9]%`. Le `sAMAccountName` est donc *techniquement*
prédictible — mais s'y fier resterait fragile (translittération, collisions, et
la règle se change dans l'IHM) : le rattachement par `employeeNumber` reste la
bonne clé.

Stockage provisionné au passage : `HomeDrive P:`,
`HomeDirectory \\KERMARIA-FS-01.home.bzh\<login>$`.

## Détail d'exploitation

`KoXoAdm.exe /Synchro=...` aboutit en quelques secondes ; **20 s d'attente
suffisent** (préférence de ZH). Il n'en reste pas moins qu'il ne se termine pas
toujours de lui-même — prévoir le kill.

Voir [[koxo-ad-password-mastery]] et [[custom-demo-accounts]].
