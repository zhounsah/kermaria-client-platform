---
name: koxo-ad-password-mastery
description: "Qui maîtrise le mot de passe AD : c'est ForcePasswords (Config.xml) qui décide. À 1 KoXo réécrit le MDP à CHAQUE synchro depuis la colonne 14, y compris sur un compte existant ; à 0 il ne touche jamais l'AD. Décision 2026-08-06 : KoXo est maître."
metadata: 
  node_type: memory
  type: project
  originSessionId: d79dbe2b-9ae3-4e1a-bc48-9b5d713aee39
  modified: 2026-08-06T07:24:50.388Z
---

**TRANCHÉ le 2026-08-06 : KoXo est maître de tout, mot de passe [REDACTED]
Le mot de passe [REDACTED] dans la colonne 14 du CSV (champ JSON `motDePasse`,
facultatif). L'API n'écrit plus le mot de passe par LDAP quand KoXo fait
autorité — les deux mécanismes sont **exclusifs**, KoXo écraserait.

## LE paramètre : `ForcePasswords` (Config.xml)

C'est lui qui décide, et il explique tous les résultats contradictoires du
2026-08-05 :

| `ForcePasswords` | Comportement sur un compte **existant** |
|---|---|
| `0` (valeur pendant tous les essais du 08-05) | KoXo lit la colonne 14, met à jour **sa propre base**, mais **n'écrit rien dans l'AD**. Aucune ligne `Mot de passe forcé` au journal. |
| `1` (activé par ZH le 08-06) | KoXo **réécrit le mot de passe AD à chaque synchro**. Le journal ajoute `Mot de passe forcé pour "<login>"`. Vérifié par authentification. |

**Leçon de méthode** : j'avais `<ForcePasswords>0</ForcePasswords>` sous les yeux
dès le premier relevé de `Config.xml` et je ne l'ai pas fait varier. J'ai donc
conclu « KoXo n'écrit qu'à la création » alors que la conclusion correcte était
« KoXo n'écrit pas tant que ForcePasswords vaut 0 ». Devant un comportement
absent, faire varier les drapeaux qui portent son nom **avant** de généraliser.

Réglages requis pour que la chaîne fonctionne, tous dans `Config.xml` et non
dans le profil de synchro : `ForcePasswords=1`,
`PurifyImportedPassword=0`, `DoNotWritePasswordsInActiveDirectory=0`.

## État mesuré sur SRV-21 (2026-08-05)

- `CLIENTS.xml` : `<UserId>Generated</UserId>` + `<Password>Generated</Password>`,
  `<FixedPassword/>` vide → KoXo fabrique login **et** mot de passe.
- KoXo **stocke le mot de passe en clair réversible** (base64) dans sa propre
  base : `Data\Users\CLIENTS\<OU>\<login>.xml` → `<Password>[REDACTED]</Password>`
  = `[REDACTED]`. Encodage piloté par `<PasswordEncodingMethod>1</PasswordEncodingMethod>`.
- **KoXo n'écrit le mot de passe QU'À LA CRÉATION.** Mesuré par A/B le
  2026-08-05, quatre essais, drapeau à 0 (défaut) :
  | Essai | Changement | `pwdLastSet` |
  |---|---|---|
  | CSV inchangé | rien | inchangé |
  | `Fonction` → TEST-A puis TEST-B (`title` écrit dans l'AD) | attribut ordinaire | **inchangé** |
  | `Nom` → LAUMAILLE-TEST (`sn` écrit dans l'AD) | champ d'identité | **inchangé** |
  | mot de passe posé de l'extérieur puis synchro modifiante | — | **préservé** |
  Autrement dit : un mot de passe posé par l'API **survit** aux synchros
  suivantes, et la divergence est assumée par KoXo (sa base gardait
  `[REDACTED]` pendant que l'AD portait le mot de passe [REDACTED]
  **Correction d'une affirmation antérieure du même jour** : j'avais conclu
  « KoXo réécrit à chaque synchro » depuis un seul relevé (`whenCreated` 22:03
  vs `pwdLastSet` 23:43:46 le 04/08). C'était une inférence, pas une mesure, et
  elle est fausse. Ce qui a déplacé `pwdLastSet` le 04/08 à 23:43:46 **reste
  inexpliqué** — ne pas le rationaliser sans nouvelle mesure.
- Les comptes portent `CannotChangePassword=True` et `PasswordNeverExpires=True`
  (posés par le modèle KoXo, `<UserCannotChangePassword>1</UserCannotChangePassword>`
  dans le XML utilisateur).

## Les leviers sont dans Config.xml, pas dans CLIENTS.xml

`C:\Program Files\KoXo Dev\KoXoAdm\Config.xml` :

- `<DoNotWritePasswordsInActiveDirectory>` : **testé le 2026-08-05 sur une
  identité jetable `CLI-000099`**. À 1, KoXo crée bien le compte
  (`sAMAccountName` dérivé, `sn` posé) mais **`pwdLastSet = 0` — aucun mot de
  passe n'est jamais écrit — et le compte reste désactivé**. C'est exactement
  le contrat que `LdapActiveDirectoryService` attend déjà : il crée en
  `userAccountControl 514` puis `SetUserPasswordAsync` pose le mot de passe et
  repasse à `512`. Effet secondaire observé : KoXo n'a pas persisté de fiche
  utilisateur dans sa propre base pour ce compte (aucun `test.drapeau.xml`),
  donc aucun mot de passe [REDACTED] en base64 — bénéfice sécurité en prime.
  **Remis à 0 le 2026-08-05 en fin de test** : ne le passer à 1 qu'AU MOMENT du
  déploiement de la modification API, sinon toute inscription réelle crée un
  compte désactivé sans mot de passe que personne ne vient activer.
- **`<Password>Field 14</Password>` testé en réel le 2026-08-06** (ZH avait fait
  le mapping dans l'IHM). Verdict : **le mot de passe du CSV n'est appliqué qu'à
  la CRÉATION**.
  | Cas | Résultat |
  |---|---|
  | compte **existant**, mot de passe en colonne 14 | `pwdLastSet` **inchangé**, authentification **False** |
  | compte **créé** par la synchro, mot de passe en colonne 14 | compte **activé**, authentification **True** |
  ⚠️ **Ce tableau a été mesuré avec `ForcePasswords=0` et ne vaut que dans ce
  cas.** Avec `ForcePasswords=1`, la colonne 14 s'applique aussi aux comptes
  existants et un changement de mot de passe se propage bien. Voir ci-dessus.
  **`DoNotUpdateNotMovedUsers` (« Ne pas mettre à jour les utilisateurs non
  déplacés ») n'y est pour rien** — hypothèse de ZH testée et écartée le
  2026-08-06, profil à 0, puis profil ET défauts globaux à 0 : `pwdLastSet`
  reste inchangé alors que `title` passe bien à la valeur du CSV, ce qui prouve
  que la ligne est traitée comme une modification.
  Détail éclairant : KoXo **lit** pourtant la colonne 14 sur une modification et
  met à jour le mot de passe dans **sa propre base** (`Users\...\<login>.xml`
  passe à la valeur du CSV) — il refuse seulement de l'écrire dans l'AD. Sa base
  et l'annuaire divergent donc en silence, et ses états/listes PDF imprimeraient
  un mot de passe qui n'est pas le vrai.
  Piège d'IHM : décocher l'option dans KoXo l'écrit dans la section
  `<CSVSynchronization>` de `Config.xml` (les défauts des futurs profils), pas
  dans `CLIENTS.xml` — le profil garde sa valeur. Toujours vérifier le profil.
  Le passage par le CSV écrit en plus le mot de passe [REDACTED] en clair dans
  `clients.csv`, ses `backups\clients.csv.*.bak` et la base XML KoXo (base64).
- **`<PurifyImportedPassword>1</PurifyImportedPassword>` mutile silencieusement
  le mot de passe [REDACTED] Mesuré : `[REDACTED]` arrive dans l'AD sous
  la forme `Kermaria2026xY` (tirets, `!` et `#` supprimés) — l'authentification
  échoue avec le mot de passe [REDACTED] et réussit avec l'épuré. À passer à 0 si
  on retient quand même la voie CSV, sinon le client ne peut pas se connecter
  avec le mot de passe qu'il a choisi, et un mot de passe plus faible est posé
  à son insu.
- Le générateur du dépôt produit **13 colonnes en dur** et `Test-KoxoCsvFile`
  **rejette** toute ligne qui n'en a pas exactement 13 : la colonne 14 n'existe
  pas côté code, et l'y ajouter demande de toucher aux deux.
- Voir aussi `[[koxo-accents-majuscules]]` : `ProcessCapitalsForNameAndFirstname`
  et `NameAndFirstnameWithoutAccents` sont dans le même fichier.

## Chaîne livrée (commits `be2cbc8` puis `53beb1c`)

`set-password` → `KoxoPendingPasswordStore` (mémoire, usage unique, TTL 15 min)
→ champ JSON `motDePasse` → colonne 14 du CSV → KoXo → AD.

- L'API n'écrit **plus** le mot de passe par LDAP en `ControlledWrite` ; elle le
  fait toujours en `Mock`, où aucun KoXo ne tourne.
- Le magasin est **en mémoire et pas en base** : le clair ne doit pas devenir
  durable dans MariaDB pour un besoin de quelques secondes. Limite assumée :
  redémarrage de l'API ou multi-instances = entrée perdue (journalisée).
- **Piège** : `KoxoExportService.PrepareAsync` sert aussi au tableau de bord
  admin, qui la rejoue à la demande. Seul l'export réel passe
  `consumePendingPasswords: true` — sinon un simple affichage consommait le mot
  de passe avant KoXo et l'exposait dans l'aperçu.

## Côté application

`SignupService.ApplyPasswordAsync` fait, dans cet ordre : pose du mot de passe
AD (`Invoke("SetPassword")` + activation `uac 512`), puis hash local, puis
`TriggerKoxoSyncWebhookAsync`. **Cet ordre est en réalité sûr** (la synchro
finale ne réécrit pas le mot de passe), contrairement à ce que j'avais dit.

Le vrai défaut est ailleurs : le `sAMAccountName` dérivé par l'API
(`initiale + 6 lettres du nom`, ex. `zhounsa`) diffère de celui dérivé par KoXo
(`prenom.nom`) → **deux comptes distincts** si les deux créent. Au 2026-08-05
l'annuaire ne contient QUE des comptes KoXo, dans `OU=KoXoAdm` : le chemin de
création de l'API n'a jamais produit de compte en recette.

**Why:** on est tenté de conclure d'un seul relevé `pwdLastSet` que KoXo écrase
le mot de passe, et de bâtir dessus une refonte inutile ; la mesure A/B dit
l'inverse. Le vrai risque est la double création, pas l'écrasement.

**How to apply:** pour tout doute sur le mot de passe, faire l'A/B (poser un
mot de passe de l'extérieur, modifier un attribut dans le CSV, resynchroniser,
relire `pwdLastSet`) plutôt que d'inférer d'un horodatage isolé — **et faire
varier `ForcePasswords`, pas seulement le CSV**. Le journal KoXo tranche seul :
la ligne `Mot de passe forcé pour "<login>"` est présente ou absente.

## Incident du 2026-08-06 à retenir

Un `clients.csv` assemblé à la main mélangeait des lignes à 13 et 14 champs.
KoXo rapprochant les lignes par l'`IdentifiantUnique` de la **colonne 5**
(`UseUniqueIDFirst=1`), le décalage a fait appliquer l'identité **et le mot de
passe** de l'identité de test `Jean DUPONT` sur le compte réel
`zachary.hounsahou` — journal : `Ajout/Modification de Jean DUPONT
(zachary.hounsahou)` puis `Mot de passe forcé pour "zachary.hounsahou"`.
D'où `Test-KoxoCsvFile` qui exige désormais exactement 14 champs par ligne.
Une largeur de CSV variable n'est pas un défaut cosmétique : elle repointe des
identités.

Note sécurité relevée au passage : `Config.xml` contient un
`<SMTPUserPassword>` en clair et un secret Azure AD en clair (fonctions
désactivées, mode SMTP 0 et Office365 0).
