# Stockage personnel DEV et accès VPN/RDS — exécution du 2026-10-03

> **État le plus récent : service de stockage autonome opérationnel.**
> Compte managé dédié et groupe de sécurité approuvés ; commande KoXo en
> session 0 validée après correction d'un accès manquant à sa sauvegarde
> applicative. La route de stockage isolée a appliqué puis vérifié un quota
> technique de 201 MiB sur demande depuis SRV-13. Raccordement API permanent
> et E2E commercial encore à réaliser. Voir [le bilan du service](KOXO_DEV_SERVICE_ACCOUNT.md).

> **Dernier état : quota personnel réel validé ; identité de service bloquante.**
> La continuation a créé puis relu un quota FSRM contraignant de 200 MiB sur
> le compte technique. Le rejeu retourne `noop / fully_verified` sous
> `HOME\zhounsah`. La même lecture sous SYSTEM/session 0 reçoit « Accès refusé ».
> La route stockage DEV et les droits VPN/RDS restent inchangés.

## Continuation : quota réel et lecture depuis le compte de service

- Nouvelle réparation ciblée interactive : PASS en 10,06 s le 2026-10-03 à
  19:42, journal `20261003_194234.log`. L'ancien timeout intermittent reste
  inexpliqué ; ce succès ne suffit pas à prouver une fiabilité permanente.
- Dossier technique vide avant activation ; sa fiche existante prévoyait
  déjà `FolderQuota=200`, désactivé. L'appel diagnostic de
  `Invoke-KoxoStorageReconcile` a activé ces **200 MiB** via KoXo, sans création
  directe de quota par l'agent sur FS-01. Aucun contrat commercial n'est créé.
- Relecture indépendante sur FS-01 : `Size=209715200`, `Disabled=False`,
  `SoftLimit=False`. KoXo a donc réellement appliqué le quota, et pas seulement
  son intention XML.
- SRV-21 ne possède ni FSRM ni RSAT-FSRM. Le vérificateur local a été étendu
  pour interroger la classe CIM `MSFT_FSRMQuota` du serveur explicitement
  configuré si `Get-FsrmQuota` est absent. Chemin exact filtré et contrôlé,
  résultat unique exigé, timeout d'opération 15 s ; aucun serveur deviné,
  installation de rôle ou écriture CIM.
- Rejeu avec ce module en copie diagnostique isolée sur SRV-21 :
  **`noop / fully_verified`**, session utilisateur 2. Aucune nouvelle
  réparation nécessaire. Cela valide le chemin de vérification réelle via CIM.
- Test séparé, strictement en lecture seule, sous l'identité du receveur :
  **SYSTEM, session 0, `Attempted=True`, `Verified=False`, accès refusé**.
  Aucun KoXo lancé par ce test et aucune élévation/délégation supplémentaire.

Le compte SYSTEM ne dispose donc pas actuellement du droit nécessaire à cette
lecture distante. Ce constat n'explique pas à lui seul le blocage historique
de KoXo et ne prouve pas que toutes ses écritures distantes échouent. Aucun
compte de service KoXo dédié n'a été identifié dans les recherches ciblées.
Le choix entre préparation d'une identité de service dédiée et poursuite de
la recette supervisée a été demandé à l'utilisateur ; aucun ajout du serveur
aux administrateurs de FS-01 n'est effectué implicitement.

Les trois tâches de diagnostic sont désactivées, sans automatisme permanent.
Le dossier et le quota de 200 MiB sont conservés sur le seul compte technique.
Les scripts courants ont été copiés uniquement dans le dossier diagnostique
protégé `C:\ProgramData\Kermaria\koxo-dev\storage-quota-20261003` ; les
receveurs existants et l'API n'ont pas été redéployés ni reconfigurés.

Validation locale de la continuation : **180/180 tests Pester PASS**, dont
**67/67 stockage**. Tests spécifiques : CIM sur serveur explicite, refus sans
serveur, refus d'accès et rejet d'une réponse concernant un autre dossier.
Les tests locaux sont distincts des preuves réelles XML/KoXo/FSRM ci-dessus.

Preuves et sauvegardes avant modification des fichiers déjà modifiés :
`C:\Users\zhounsah\Backups\Kermaria\storage-continuation-20261003-194537`.
Fichiers clés : `quota-applied-xml-only.json`, `result.json` (rejeu vérifié),
`system-readonly-result.json`, `fsrm-real.json`, `pester-final.xml`.

> **Complément du 2026-10-03 à 18:38 : commande interactive testée avec succès.**
> L'utilisateur a ensuite autorisé l'essai depuis une session Windows
> utilisateur, sans manipulation de l'interface KoXo, et la réutilisation d'un
> compte technique existant pour ce diagnostic. Voir la section suivante.
> Le bilan initial plus bas reste historique : il précédait ces essais.

## Complément : réparation ciblée depuis une session utilisateur

Cible vérifiée dans AD : `devkoxo2.testisola`, `employeeNumber=CLI-D999902`,
sous `CLIENTS DEV / DEV-CLI-TST001`. Dossier personnel absent avant essai :
`F:\KoXoDATA\CLIENTS DEV\DEV-CLI-TST001\devkoxo2.testisola`.

Commande exécutée :

```text
KoXoAdm.exe /RepairUser UserId="devkoxo2.testisola" Type="Storage"
```

Une tâche ponctuelle avec `LogonType=Interactive`, sous `HOME\zhounsah`, a
exécuté le wrapper existant avec le mutex `Global\Kermaria-KoXoAdm`. Session
Windows 2, déjà ouverte puis déconnectée ; aucune saisie de mot de passe et
aucune manipulation graphique par l'agent. Pas de changement de quota,
d'annuaire, de mapping, de règle VPN/RDS ou de route du receveur.

| Passage | Résultat |
|---|---|
| Préparation | Import direct du module manquant ; échec avant tout lancement KoXo, puis correction du script ponctuel |
| Création, 18:34–18:35 | PASS : 41,12 s, code 0, marqueurs de journal valides, dossier créé à 18:35:01 |
| Premier rejeu, 18:35–18:36 | ÉCHEC : timeout à 45 s, sans nouveau journal ; seul le processus du test a été arrêté par le wrapper |
| Dernier rejeu, 18:37 | PASS : borne 90 s, durée réelle 3,52 s, code 0, nouveau journal valide, ACL du dossier inchangée |

Journaux de réussite : `Data\Logs\20261003_183422.log` et
`Data\Logs\20261003_183740.log` sur SRV-21. La fiche utilisateur conserve son
SHA-256 `FF770257EE723C135459A60B51F245A8E31DB43367B9B7E3B66E4C8440DFD44A`
avant/après chaque passage. Les huit ACL racine/témoins relevées lors de la
correction FS-01 sont également inchangées après la création.

Le dossier porte les ACE de modification de `CLIENTS\devkoxo2.testisola`,
avec refus de suppression du dossier racine personnel, et les droits
d'administration/SYSTEM attendus. Ce relevé n'est pas une connexion effective
du client. **Aucun quota FSRM n'existe sur ce dossier** : le test ne prouve
ni quota, ni accès VPN/RDS. L'échec intermédiaire reste inexpliqué ; relever
la borne à 90 s ne démontre pas que le timeout était la cause.

**Conclusion :** la réparation ciblée par commande peut fonctionner depuis
une session utilisateur. Le fonctionnement sous SYSTEM/session 0 et la
fiabilité d'un worker permanent ne sont pas démontrés. Aucun worker interactif
permanent n'a été installé. La tâche ponctuelle
`Kermaria-Koxo-Interactive-Storage-20261003` est désactivée, sans déclencheur ;
aucun processus KoXoAdm du test ne reste actif. Le dossier est conservé.

Preuves locales :
`C:\Users\zhounsah\Backups\Kermaria\storage-interactive-20261003`.
Sur SRV-21 : `C:\ProgramData\Kermaria\koxo-dev\interactive-storage-20261003`,
sous les ACL Administrateurs/SYSTEM du répertoire DEV ; sauvegarde XML de la
fiche conservée uniquement dans ce dossier protégé, jamais affichée ou ajoutée
au dépôt. Les fichiers `attempt-2-success.json`, `attempt-3-timeout45.json`,
`final-result.json` et `fs01-final.json` séparent les preuves de chaque essai.

## Bilan initial avant les essais interactifs

**Lot partiellement exécuté ; E2E arrêté au prérequis KoXo non interactif.**
Base Git : `2059a98`, branche `main`, dépôt initialement propre.

Le périmètre retenu est un nouveau client fictif DEV, stockage personnel,
VPN et RDS réels via des groupes dédiés DEV. Aucun worker interactif de repli
n'est accepté. Aucun paiement, création d'identité, changement AD, lancement
KoXo ou ouverture VPN/RDS n'a été effectué dans cette passe. Aucune connexion
MariaDB ni migration. Aucun déploiement applicatif, commit, tag ou push.

Les ACL de FS-01 ont en revanche été modifiées sur l'infrastructure partagée,
selon le détail et les limites ci-dessous. Les corrections de scripts restent
locales et non déployées.

## Préflight et constat fournisseur

- SRV-21 : `KoXoAdm.exe` installé en **4.0.0.3** ; receveurs PROD 8042 et DEV
  8043 actifs. Définition DEV : `identifierPrefix=CLI-D`, profil `CLIENTS DEV`,
  `storageRouteEnabled=false`, timeout 90 secondes. Configuration inchangée.
- SRV-13 : service `KermariaApiInternalDev` actif. Lecture sélective du JSON
  DEV : `APP_ENV=Development`, base `kermaria_dev`, Stripe `test`, OU et racines
  AD sous `CLIENTS DEV`, URL de stockage absente. Ce relevé du fichier ne vaut
  pas interrogation de la configuration effective du processus ou preuve SQL.
- FS-01 : rôle FSRM installé. Aucun quota créé ou modifié.
- Manuel installé `KoXoAdm_FR.pdf`, 270 pages : section 37, pages 217–219,
  commandes ciblées `/RepairUser ... Type="Storage"` et
  `/RepairSecondaryGroup ... Type="Storage"`. Pas de garantie non interactive
  établie pour ces commandes. `/RepairAll` mentionne l'absence de console mais
  sa portée globale l'exclut du lot.
- Vérification complémentaire de l'[historique officiel V4](https://www.koxo.net/produits/koxo-administrator/versions/versions-4)
  et de l'[historique V3](https://www.koxo.net/produits/koxo-administrator/versions/versions-3) :
  aucune solution ciblée non interactive identifiée dans les passages consultés.

**INCERTAIN :** l'ancienne attente `UserRequest` peut provenir d'une interaction
native invisible ; cette cause n'est pas démontrée. Aucun nouvel essai SYSTEM
n'a été lancé. L'absence de solution identifiée n'est pas une preuve que le
produit ne sait jamais fonctionner sans interaction.

**Arrêt :** obtenir une procédure fournisseur pour une réparation ciblée du
stockage en session 0, avec identité d'exécution et prérequis documentés, puis
la tester sur une nouvelle identité technique DEV. Ne pas lancer `RepairAll`,
détourner la synchronisation CSV ou créer les dossiers directement pour lever
ce blocage. Aucun message n'a été envoyé au fournisseur.

## ACL FS-01 : modification et incident de propagation

**VALIDE avant correction :** `CLIENTS` et `CLIENTS DEV` n'étaient pas protégés
de l'héritage ; la racine portait l'ACE de `CLIENTS-KOXO-ADM` au lieu du groupe
HOME attendu. Les SID ont été résolus et contrôlés avant mutation.

Opérations réalisées :
1. Export des ACL racine et des sept répertoires enfants immédiats.
2. Protection de `CLIENTS` et `CLIENTS DEV`, en conservant leurs ACE effectives.
3. Substitution ciblée à la racine de l'ACE `CLIENTS-KOXO-ADM` par l'ACE
   `HOME\Administrateurs de KoXo Administrator` avec les mêmes droits et flags.

**Incident détecté et corrigé :** l'application de l'ACL racine via `Set-Acl`
a déprotégé `CLASSES`, `ELEVES`, `ESPACES_PARTAGES` et `Journaux des logs`, avec
ajout d'ACE héritées. Chacun de ces quatre témoins a été restauré séparément
depuis son descripteur initial, puis reprotégé. Pas de restauration globale.

Contrôle final : les huit chemins sont protégés. Pour les sept enfants,
propriétaire et tuples identité/droits/type/flags correspondent au relevé
initial ; `CLIENTS` et `CLIENTS DEV` ont désormais leurs ACE explicites. À la
racine, seule la substitution prévue est conservée. Le flag de contrôle
Windows `AI` et l'ordre canonique peuvent différer : ne pas confondre cela avec
un changement de droits. Une comparaison SDDL brute ne suffit pas.

**Limite :** aucun inventaire exhaustif des descendants ni test d'ouverture
de session client n'a été réalisé. La preuve porte sur les huit ACL relevées,
pas sur l'intégralité des droits effectifs de l'arborescence. Aucun autre
`Set-Acl` racine ne doit être rejoué sans tenir compte de cette propagation.

## Corrections locales et compatibilité

- Préflight : paramètre **obligatoire** `-Environment Development|Production`,
  base `*_dev` en DEV, refus des incohérences avant appel SQL, recherche LDAP
  DEV bornée à son OU, identifiant `CLI-DNNNNNN` et groupe `DEV-CLI-*` en DEV.
  Les démos DEV restent refusées. Le lien AD exact et la recherche par
  `employeeNumber` restent obligatoires ; aucune identité n'est fabriquée.
- Réconciliation : un XML déjà conforme ne court-circuite plus la lecture
  FSRM lorsque celle-ci est configurée. Un quota absent, divergent, désactivé,
  souple ou sans métadonnées de contrainte empêche le succès. La distinction
  [`Disabled` / `SoftLimit`](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/fsrm/msft-fsrmquota)
  suit le contrat Microsoft.
- En cas de rejeu refusé, aucune réparation KoXo ni écriture XML. Les refus
  de diminution et la restauration XML après échec initial sont conservés.
- Sans FSRM configuré, `xml_verified` reste compatible avec l'existant et ne
  vaut jamais preuve du quota réel. Pour l'E2E futur, FSRM sera obligatoire.
- Aucun contrat API public, type partagé ou schéma SQL modifié.

## Validation et preuves

Tests locaux Windows PowerShell 5.1 : suite stockage **63/63 PASS**, incluant
les nouveaux cas de namespace, refus avant SQL, rejeu FSRM et quotas non
contraignants. Les doubles FSRM/KoXo ne constituent aucune preuve fournisseur.
Ensemble des suites `scripts/koxo/tests` : **176/176 PASS**, aucun test ignoré.

Suites API Release PASS : provider KoXo, cibles, résolution, sémantique des
règles, isolation par utilisateur et réconciliation des memberships.
Build avec avertissements CA1416 et CS8620 ; aucun changement C# dans ce lot.

Preuves et sauvegardes hors Git :
`C:\Users\zhounsah\Backups\Kermaria\storage-access-20261003-175837`.
Ce dossier contient le manuel installé, les scripts avant modification et leurs
diffs HEAD, les hashes, l'export Pester et les relevés de configuration expurgés.

| Fichier | SHA-256 |
|---|---|
| `acl-before.json` | `24533980A3F58D3BA99B9731E1B4582E856F836A412994A3CFD7914240090563` |
| `acl-after.json` | `B1FE5F7AF5AA8E5E59E2F574CE5F922514D02EE221A6F772FF040906FF53D760` |

Reprise : preuve fournisseur d'abord ; ensuite manifeste exact des groupes et
règles VPN/RDS, accès au dossier via KoXo, nouveau client E2E, paiement TEST
confirmé par l'utilisateur, accès positifs/négatifs, rejeu et résiliation.
Les cibles VPN/RDS n'ont pas été inventoriées à ce stade et ne sont pas
autorisées par simple déduction de documents historiques.
