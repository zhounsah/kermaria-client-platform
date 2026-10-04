# V0.40 / V0.40.1 - Synchronisation KoXo privee, validee et non destructive

## Objet

### Evolution du 04/10/2026 : qualites supplementaires (activees en DEV)

Retrait réel testé ensuite : une cellule vide n'a pas retiré les qualités
existantes dans KoXo. Option livrée en DEV :
`BILLING_V2_KOXO_EMPTY_QUALITY_GROUP`, groupe DEV neutre explicitement configuré
et mappé dans AD_ALLOWED_ROOTS. Il représente une liste d'accès vide dans le
transport CSV ; il ne doit recevoir aucun accès ni imbrication. La preuve
exige toujours l'absence des anciens groupes, pas seulement sa présence.
Un import non vide reste incrémentiel. Le titulaire réalise l'import dédié
avec « Ne conserver que les qualités supplémentaires importées ». Les preuves
restent nécessaires, notamment la persistance XML après import (une correction
ponctuelle a été nécessaire pour Noé ; voir son rapport).

Le trigger DEV `qualities_changed` prépare désormais le CSV avec
`-PublishCsvOnly`, sans lancer KoXoAdm. Quand le CSV est conforme, le worker
attend les preuves XML/AD sans répéter la publication. Les autres événements
d'identité gardent leur circuit existant. L'import avec remplacement reste
manuel ; son automatisation a été différée explicitement par le titulaire.

En DEV, le contrat JSON passe en version 3 avec `qualitesSupplementaires`,
chaine obligatoire pouvant etre vide. PROD conserve le contrat v2 sans ce
champ pour son recepteur existant. Le recepteur DEV produit 15 colonnes :
`QualitésSupplémentaires` est ajoutee apres `MotDePasse`, qui reste en colonne 14.
Le format est celui du CSV DEV fourni sur SRV-21 : noms de groupes separes
par des virgules, exemple `GG_RDS_E2E_DEV,GG_VPN_E2E_DEV`.

L'API publie le plan valide par identite dans `koxo_quality_intents` apres les
garde-fous et la resolution des identites. L'export lit cette revision durable.
Avant la premiere revision seulement, il conserve les appartenances actives
deja appliquees et suivies dans `billing_v2_provisioning_managed_memberships`.
Elle ne lit pas memberOf et ne distribue jamais l'union des groupes du client
a chacun de ses utilisateurs. Un droit retire du plan ou du suivi ne sort plus ;
une cellule vide est explicite. Une source indisponible
fait echouer l'export, plutot que publier une fausse revocation. Les noms sont
valides avant assemblage pour interdire l'injection de qualites/separateurs.

Le flag DEV `BILLING_V2_KOXO_QUALITIES_ENABLED` remplace les ecritures AD directes
par une demande persistante, puis une preuve CSV + XML KoXo + AD avant acquittement.
Les groupes ajoutes
manuellement hors du suivi Billing V2 ne sont pas automatiquement adoptes.
La colonne constitue une liste complete : ne pas y melanger des qualites
manuelles dont la preservation n'est pas representee dans l'application.

Livraison coordonnee requise : API et scripts de synchronisation, puis profils
KoXo avec champ 15 et separateur interne virgule. Les versions 2 et 3 se
refusent mutuellement avant remplacement du CSV. Suspendre les declencheurs
pendant cette livraison et conserver le CSV/profil de retour arriere hors Git.
Le CSV DEV est regenere par le lanceur de publication autorise. Une cellule
vide n'efface pas les qualites de KoXo : seule la preuve finale CSV/XML/AD
valide le retrait, apres import manuel et controle de la fiche persistante.

Etat verifie le 04/10 a 17:15 : migration098 active, worker DEV actif,
Noe revision1 appliquee a 14:30:14 UTC. Console fermee normalement, deux
metadonnees SAM de qualites corrigees sous mutex puis conservees par reimport.
La recherche LDAP utilise maintenant l'OU client KoXo directement, sans
sous-OU Users inexistante. Preuves, empreintes et rollback dans
`docs/DEV_E2E_NOE_VALBRUME.md` ; les sections historiques ci-dessous decrivent
les etapes successives, pas toutes l'etat actuel.

Limite d'observabilite : apres un nouvel ajout asynchrone, l'intent devient
applique mais le statut historique d'item peut rester pending jusqu'au prochain
rejeu. Aucune surface ou gate ne lit actuellement ce statut. Ne pas inventer
un statut deprovisioned ni acquitter tous les items actifs d'un client depuis
une ancienne revision : une future convergence par item doit persister le lien
revision → items. Apres resiliation, conserver provisioned comme historique
n'autorise aucun acces ; seuls les abonnements actifs alimentent le plan.

Garde de livraison : `Deploy-KoxoScripts.ps1` refuse maintenant le module
v3 si la tache cible est le recepteur PROD 8042 **ou** si le chemin normalise
est le repertoire partage `Data\CSVSynchro`. Refus avant WinRM, meme en
DryRun ; ListOnly reste une lecture locale du manifeste. Les exemples generiques
plus bas decrivent l'ancienne livraison v2 et ne sont pas une procedure pour
livrer ces scripts v3 en PROD. La livraison DEV exige toujours le repertoire
isole et sa definition d'instance ; ne pas utiliser les variables Machine
partagees pour la configurer.

Validations locales du lot : build API, export/projection cibles, suite smoke
API effectivement executee et typecheck shared/web PASS. Contrat CSV autonome
`scripts/koxo/tests/Test-KoxoQualitiesContract.ps1` PASS sous PowerShell 5.1 et 7.
La suite complete est desormais PASS sous Windows PowerShell 5.1 avec une
copie isolee de Pester 4.10.1 obtenue depuis le depot officiel : 193 tests,
aucun echec ni test ignore. Pester 3.4 installe globalement reste inchange.
Un vrai defaut du controle inter-CSV a ete corrige : le decoupage negatif
ignorait les identifiants. TextFieldParser lit maintenant les champs CSV
avec guillemets/retours a la ligne et refuse les entetes ou largeurs incoherentes.
Les nouvelles regressions prouvent la detection des doublons dans ces cas.
Preuves hors Git : `Backups\Kermaria\koxo-qualities-20261004` dans le profil local.

Profil DEV relu sur SRV-21 : `Data\CSVSynchro\CLIENTS-DEV.xml` contient bien
`OtherGroups=Field 15` et `SeparatorInsideField=,`, comme le modele CSV fourni
par le titulaire. Le profil n'est pas dans CSVSynchro-DEV, contrairement au CSV.
Cette lecture ne prouve pas encore la suppression effective des qualites vides.

### Registre des demandes KoXo — implementation partielle, inactif

La migration `098_koxo_quality_intents.sql` (non appliquee) separe l'etat
demande de l'historique des memberships reellement observes. Un document par
client contient la liste complete des qualites par identite, une revision
monotone et la derniere revision appliquee. Aucune donnee de mot de passe.
`MariaDbKoxoQualityIntentRepository` refuse une migration absente sans DDL,
verrouille le client avant publication, verifie l'appartenance de chaque
identite au client et compare la revision attendue. Un accuse ancien ne peut
pas valider une revision plus recente. Ces garanties SQL restent a exercer
sur une base MariaDB de test autorisee ; les tests actuels valident la policy.

Le moteur accepte un depot optionnel de demandes. Quand il est fourni, il
lit la revision avant de calculer le plan, conserve les gates de preparation,
reconcilie le stockage, resout les identites, puis publie la demande a la place
de l'ecriture AD directe. Il retourne KOXO_QUALITIES_PENDING (jamais un succes
sur simple publication). L'export lit alors ce document autorise, sans exiger
que les groupes aient deja ete ajoutes dans AD. Une demande anterieure entre
dans le perimetre de retrait meme si son application n'a pas encore ete accusee.

**Non active :** le depot n'est pas enregistre dans l'injection de dependances.
Il reste a raccorder le traitement/reessai des demandes, declencher la synchro,
verifier la revision CSV + qualites KoXo + memberships AD, accuser uniquement
cette revision, puis verifier la convergence et le comportement apres crash.
Ne pas activer cette branche avant ces etapes et le test MariaDB. Aucun
schema ni runtime DEV/PROD n'a ete modifie par ce chantier local.

### Traitement des demandes et premiere preuve reelle — encore inactif

Le dispatcher et worker locaux prennent une reservation de 120 secondes,
bornent l'execution a 60 secondes et accusent uniquement une preuve concordante
sur client, revision, SHA-256 du document, CSV, XML KoXo et AD. Publication
nouvelle = invalidation de l'ancienne reservation. Echec = code non sensible
et reessai exponentiel borne a cinq minutes. Un crash laisse expirer la
reservation ; un accuse tardif ou une revision remplacee ne peut pas terminer
la nouvelle demande. Tests de politique/dispatcher PASS ; SQL concurrent
et expiration reelle restent a tester sur MariaDB. Aucun worker enregistre en DI.

Le module en lecture seule `KoxoQualities.Common.psm1` compare, pour une
identite CLI-D dans CLIENTS DEV, le CSV et les AdditionalQuality/SAMAccountName
de l'unique fiche XML portant son UniqueID. Aucune donnee de mot de passe
ne sort de cette lecture. Copie diagnostique hors runtime deposee sur SRV-21,
dans ProgramData\Kermaria\noe-profile-20261004, puis executee en lecture seule.

**Constat reel nouveau :** CSV de Noe conforme et memberships AD presents,
mais SAMAccountName des deux AdditionalQuality vaut actuellement
` membership managed by Billing V2` au lieu des noms GG_RDS_E2E_DEV et
GG_VPN_E2E_DEV. XML modifie a 11:11:47 le 04/10. Les champs Group et FQDN
restent corrects. La description AD contient exactement cette chaine apres
un point-virgule : anomalie de decoupage a l'import suspectee, non demontree.
Le verifier retourne CsvVerified=true, KoxoVerified=false. Ne pas accepter
Group comme substitut silencieux au SAMAccountName pour contourner ce constat.
Regression de cette anomalie ajoutee ; cinq tests de preuve PASS sous PS5.1.
Console KoXo toujours ouverte lors du dernier controle. Aucune qualite,
description AD ni configuration active n'a ete modifiee pendant ce diagnostic.

Restent : implementation de l'executant HTTP/relecture AD, raccordement de
la preuve au recepteur sous verrou KoXo, diagnostic de cette corruption,
tests MariaDB de la migration 098 (toujours non appliquee), activation DEV,
ajout/retrait/rejeu reels puis resiliation et conservation des donnees.

### Executant HTTP et corrections de revue — non active

`HttpKoxoQualityIntentExecutor` est implemente : candidats exportables DEV,
lien unique de la bonne identite/client, relecture AD par employeeNumber et
objectGUID, POST authentifie sur `/internal/koxo/qualities/proof/`, puis
comparaison des groupes AD (nom ET DN configure). Le recepteur expose cette
route uniquement pour une instance DEV contenant le profil CLIENTS DEV,
hors StorageOnly. Lecture CSV/XML sous mutex commun, refus si KoXo est ouvert,
reponse 409 sans declenchement automatique. Une route inconnue/202 ne vaut
jamais preuve ; le chemin de preuve ne tombe jamais dans la synchro globale.
Le corps de preuve est borne a 128 Ki caracteres et 128 identites.

Preuve discordante mais lisible : declenchement du webhook de synchronisation
existant, puis maintien pending jusqu'a une verification ulterieure. Le nouveau
module fait partie du manifeste Deploy-KoxoScripts. Tests HTTP sans reseau reel
PASS : trois preuves, refus 202/404, occupation 409, revision incorrecte,
absence de droits AD et declenchement uniquement apres preuve lisible non conforme.

Revue independante autorisee : deux findings VALIDE corriges. La publication
refuse plus de 128 identites ou un document canonique UTF-8 de plus de 64 KiB,
avant toute persistance, pour ne pas creer une demande structurellement
inexecutable. L'export n'interroge plus le plan catalogue : intent autorise
s'il existe, sinon dernier etat actif effectivement applique/suivi par l'API.
Une anomalie de catalogue ne bloque donc plus la synchronisation des autres
clients. La decision nouvelle reste dans le provisioning et ses gates.
Cette regle remplace l'intersection opportuniste plan/memberships de la premiere
version locale de ce lot, decrite historiquement plus haut.

Activation DI et tests transactionnels toujours en attente. Aucune connexion
MariaDB de test configuree ; autorisation demandee pour une base distincte
`kermaria_koxo_quality_test_dev` sur KERMARIA-SRV-06.home.bzh, sans toucher
kermaria_dev/PROD. Pas de connexion SQL ni de migration tant que cette cible
n'est pas approuvee. Console KoXo toujours ouverte au dernier controle.

### Tests MariaDB prepares ; cible approuvee, acces DDL manquant

L'accord « Tu peux continuer » a ete pris comme autorisation pour la base
dediee explicitement proposee `kermaria_koxo_quality_test_dev` sur SRV-06.
Lecture autorisee de l'instance via SSH : KERMARIA-SRV-06, MariaDB 11.8.6,
base de test absente. Le compte enregistre `/root/.mariadb-backup.cnf` se
connecte comme mariadb_backup@localhost mais n'a pas CREATE DATABASE. Root
sans mot de passe et la configuration Debian ne permettent pas la connexion.
Aucun contournement d'authentification, aucune creation de base executee.

Script administrateur prepare : scripts/dev-env/create-koxo-quality-test-database.sql.
Il cree cette seule base et accorde SELECT/INSERT/UPDATE/CREATE/REFERENCES
au compte existant kermaria_dev_migrator@192.168.100.213. Aucun secret ni
modification de compte ; NO_AUTO_CREATE_USER interdit une creation implicite.
Creation demandee au titulaire via son acces SQL administrateur.

Runner `--koxo-quality-mariadb` : exige BILLING_V2_TEST_MARIADB_CONNECTION
avec hote SRV-06 exact, base dediee exacte, port 3306, et
RUN_KOXO_QUALITY_SQL_TESTS=true. Execution prevue depuis SRV-13 pour respecter
la source du compte. La migration 098 est incorporee au binaire de tests.
Le runner refuse une base non vide, ne cree que les deux tables parentes
minimales et la table de demandes, et conserve les fixtures pour inspection.
Il ne contient aucun DROP/DELETE. Cas : schema absent sans DDL implicite,
publication concurrente, identite d'un autre client, rejeu, lease exclusif,
retrait contre accuse ancien, backoff, expiration/reprise et isolation client.
Compilation et tests du garde de cible PASS ; invocation sans opt-in refusee
avant connexion. Les transactions reelles ne sont PAS encore validees.

### Validation transactionnelle obtenue en instance locale isolee

Apres instruction explicite d'autonomie complete a 13:45, tentative du script
sur SRV-06 : erreur 1044 au CREATE DATABASE, arret sans creation ni GRANT.
Alternative utilisee : binaire MariaDB 11.8.9 deja installe localement,
nouveau datadir protege sous Backups\Kermaria\koxo-sql-isolated-20261004,
liaison 127.0.0.1:33398 exclusivement, aucun service Windows cree. Secret
ephemere de cette instance conserve seulement dans ce dossier protege.

Le runner accepte aussi cette cible locale precise, toujours avec opt-in et
base exacte, et exige KOXO_QUALITY_TEST_EXPECTED_DATADIR. Il compare @@datadir
avant les DDL pour refuser une autre instance. Tests transactionnels reels
PASS sur MariaDB 11.8.9 : concurrence publication/lease, rejet interclient,
rejeu, retrait contre ancien accuse, backoff, expiration/reprise, isolation.
Etat final du cas de retrait : revision 2, applied_revision 2, trois tentatives,
KOXO_QUALITIES_APPLIED. Tables fictives conservees pour inspection. Serveur
temporaire arrete proprement par mariadb-admin ; PID 53436 termine et port
33398 ferme. Aucune base operationnelle touchee. La demande de creation
manuelle sur SRV-06 n'est plus necessaire pour ces tests ; l'application de
la migration 098 a kermaria_dev reste une etape distincte non realisee.

### Activation explicite preparee

BILLING_V2_KOXO_QUALITIES_ENABLED=false par defaut. A true, le resolver exige
APP_ENV=Development, MariaDB kermaria_dev, provisioning active et recepteur
KoXo sur 8043/internal/koxo/sync avec jeton configure. Refus en production.
L'injection enregistre ensemble depot, executant, dispatcher, garde de schema
au demarrage et worker. Aucun reglage runtime n'a encore ete active. Les tests
de configuration et les tests cibles de l'export passent. La version 2.0.3.2
reste conditionnee a la recette complete, notamment retrait KoXo et donnees.

### Preparation du test de separateur KoXo

Sauvegarde privee des fichiers DEV avant fermeture sous
ProgramData\Kermaria\koxo-dev-before-close-20261004-1400 sur SRV-21.
Demande de fermeture normale du PID 5876 refusee par Windows (force requise).
Arret force non realise ; ne pas perdre un eventuel etat UI non enregistre.

Descriptions de GG_RDS_E2E_DEV et GG_VPN_E2E_DEV sauvegardees dans
ProgramData\Kermaria\noe-profile-20261004\group-descriptions-before-separator-test.json,
SHA-256 92BD3FC22B6735753051598EF30F1B994C6237116B78284A48D3F2E74C516BFA.
Les deux points-virgules de chaque description ont ete remplaces par des
tirets, sans toucher noms/SID/memberships. Hypothese de decoupage a verifier
au prochain import ; cette modification seule ne prouve PAS la correction
des SAMAccountName deja enregistres dans les fiches KoXo.

### Migration 098 appliquee en DEV, fonctionnalite toujours inactive

Preflight depuis SRV-13 avec kermaria_dev_migrator : serveur SRV-06,
base kermaria_dev, migrations jusqu'a 097, table 098 absente. Le compte de
sauvegarde du serveur ne peut pas lire les metadonnees DEV : un resultat
information_schema vide sous ce compte ne prouve pas une absence de table.

Le dump logique a echoue sur SHOW VIEW manquant ; il n'est pas une sauvegarde
validee. Sauvegarde physique filtree realisee avec mariadb-backup 11.8.6 et
le compte de sauvegarde existant : --backup --databases=kermaria_dev puis
--prepare --export. Deux sorties 0 et marqueurs completed OK. Repertoire
protege /var/backups/kermaria-dev-before098-20261004-1410 sur SRV-06,
seul sous-repertoire de base kermaria_dev, 345 fichiers, 273828234 octets.
Manifest SHA-256 :
7f393c04839d45ebf1f42ea479daaf44966b57ad406d532eaf04c6cd4220fcf2.
Il s'agit d'une sauvegarde physique partielle preparee pour restauration
par tables ; aucune restauration n'a ete simulee sur la base operationnelle.

Seule migration 098 executee, par le migrator DEV depuis SRV-13, apres
controle de l'empreinte du fichier et des preconditions. SHA-256 migration :
C735025477E4398B8C21BC9E0920E3C67CFA79D3E1E1724A9BC8122BC0F0B79C.
Ligne schema_migrations presente ; table vide a 11 colonnes, cle primaire,
cle etrangere customer et deux CHECK verifies. API DEV Running, readiness 200.
Ni binaire API ni recepteur remplace dans cette passe ; flag toujours inactif.
Retour arriere applicatif : conserver la table additive, pas de DROP automatique.
Preuve locale : Backups\Kermaria\koxo-qualities-20261004\dev-migration-098-proof.json.

### Livraison DEV coordonnee, flag inactif et export reel valide

API DEV livree, SHA-256 DLL
4207D95A3499B7D4FE802310CE328114DA32E49082AC7710851EEEEA01BCD2FF.
Archive 603D247B1F68305F9B7D6FC371D6F05EAEFBAC1DB66CC703569732BB2D8CB5C2.
Rollback SRV-13 : C:\apps\api-internal-dev-old-qualities-20261004.
Configuration externe conservee octet pour octet, authoritative=true,
service Running et readiness 200. Binaire PROD inchange, empreinte
4B07BB0A764F01BC6E0F9F9B8DB2BC6F22497C9566A4B99504F8FED94FEB254F.
Flag qualites absent du JSON DEV et des variables Machine, donc false.

Le recepteur DEV partageait ses fichiers avec PROD. Nouveau code isole sous
C:\ProgramData\Kermaria\koxo-dev\app-qualities-20261004 sur SRV-21.
Seule l'action de Kermaria-KoXoWebhookReceiver-DEV-8043 a ete redirigee vers
ces six fichiers verifies par hash, puis relancee. Taches PROD 8042 et stockage
sMSA DEV restees Running. XML de retour arriere de la tache sous
ProgramData\Kermaria\noe-profile-20261004\receiver-task-before-qualities.xml.
Le recepteur DEV refuse aussi un lancement de synchro si une console KoXo
interactive est ouverte : pas de reecriture concurrente d'un etat GUI ancien.

Route de preuve reelle : 401 sans bearer, 409 avec bearer valide pendant
l'ouverture de KoXo (aucune execution). Export API via BFF de l'instance :
schema v3, deux utilisateurs, CLI-D000002 avec GG_RDS_E2E_DEV,GG_VPN_E2E_DEV,
CLI-D000001 avec cellule vide. Deux DryRun donnent le meme hash CSV
006ab14fbed93bd8c13571fff1110f4070b6a4ab995acc67ea1ef6eaaa701018,
LaunchRequested=false, not_requested, CSV actif inchange. Preuves privees
sous ProgramData\Kermaria\noe-profile-20261004\dryrun-api-v3 sur SRV-21.

Rejeu d'un intent deja accuse : resultat KOXO_QUALITIES_ALREADY_VERIFIED,
sans nouvelle ecriture groupe ; le code ne pretend pas avoir relu AD a cet
instant, il reconnait la preuve persistante de la meme revision. Les statuts
items peuvent etre remis en conformite lors de ce rejeu. Message UI pending
prepare localement, mais WebPortal non relivre dans cette passe.
Restent l'import reel, preuve de retrait, activation du worker et resiliation.

### WebPortal DEV livre et controle navigateur

Release /opt/kermaria/releases-dev/webportal-dev-koxo-qualities-20261004 sur
SRV-12, service actif et readiness privee OK. Archive SHA-256
2BFDAE7887562052D19B7F5D24ACBC55CF6159D3CC1FEFDC78D378303907C9E5.
Rollback : /opt/kermaria/releases-dev/webportal-dev-readiness-20261004.
Fichiers /etc/kermaria/webportal-dev.env et webportal.env conserves par hash,
symlink PROD inchange. Ne pas reutiliser Install-WebportalDev.ps1 tel quel
pour cette mise a jour : ses valeurs hCaptcha de demonstration remplaceraient
la configuration active. Aucun secret ni unite systemd n'a ete reecrit ici.

Build Windows : telechargement des polices via tunnel CONNECT local limite
aux deux domaines Google Fonts, resolution ponctuelle et TLS de bout en bout
conserve ; DNS systeme inchanges, auxiliaire arrete et port 38843 ferme ensuite.
Ajout au paquet des deux dependances Linux de Sharp verrouillees par
package-lock.json, SHA-512 controles. Test de rendu PNG sous kermaria-web-dev
sur SRV-12 reussi avant bascule. Le paquet precedent ne portait que Sharp
Windows ; pour les prochaines livraisons Linux, conserver cet ajout explicite.

Session admin DEV retablie via formulaire normal avec autorisation existante,
fiche Noe rechargee avec succes : abonnement actif, deux groupes DEV, bouton
de resiliation immediate present. Aucune resiliation executee. La notice de
demande KoXo en cours est livree, mais ne sera exercee qu'a l'activation du flag.
Manifeste local : Backups\Kermaria\koxo-qualities-20261004\webportal-delivery-proof.json.

V0.40 ajoute une chaine privee `webportal -> api-internal -> PowerShell -> CSV -> KoXo`
sans SMB cote site, sans secret reel dans le depot, sans execution KoXo cote site,
et sans creation automatique de la vraie tache planifiee.

> **Instances KoXo (2026-09-26).** Le lanceur accepte desormais une instance
> ISOLEE (DEV) decrite par un fichier JSON, et tout lancement de `KoXoAdm.exe`
> passe par le verrou systeme `Global\Kermaria-KoXoAdm`. Le comportement de
> l'instance de production est inchange. L'infrastructure DEV n'est **pas**
> deployee : voir [KOXO_DEV_ISOLATION.md](KOXO_DEV_ISOLATION.md).

> **La regle mot de passe de la V0.40.1 est REVOQUEE depuis le 2026-08-06.**
> Elle disait « aucun mot de passe n'est exporte vers KoXo » et confiait
> l'alignement a un flux portail -> AD direct. Mesures a l'appui, cette voie ne
> tient pas : avec `ForcePasswords=1`, KoXo **reecrit** le mot de passe de
> l'annuaire a chaque synchronisation depuis la colonne 14, donc tout mot de
> passe pose par LDAP serait ecrase au passage suivant. Les deux mecanismes sont
> exclusifs.

Regle en vigueur : **KoXo est maitre du mot de passe.**

- le mot de passe voyage dans la colonne 14 du CSV, champ JSON `motDePasse` ;
- `password_hash` en base SQL reste un hash local non reversible, sans rapport ;
- l'API ne peut publier le mot de passe qu'a l'instant ou le client le saisit,
  puisqu'elle n'en conserve pas de forme reversible : le champ est donc
  **facultatif**, et son absence laisse KoXo conserver ce qu'il connait ;
- l'API n'ecrit plus le mot de passe dans l'annuaire par LDAP quand KoXo fait
  autorite.

Consequence assumee : le mot de passe transite en clair par `clients.csv`, ses
sauvegardes et la base XML de KoXo. Ces emplacements doivent etre traites comme
un magasin de secrets.

Trois reglages KoXo conditionnent le fonctionnement, tous dans `Config.xml` et
non dans le profil de synchro :

| Reglage | Valeur requise | Effet si mal regle |
|---|---|---|
| `ForcePasswords` | `1` | a `0`, KoXo lit la colonne 14 et met a jour sa propre base, mais **n'ecrit rien dans l'AD** |
| `PurifyImportedPassword` | `0` | a `1`, les caracteres speciaux sont **silencieusement supprimes** : `Ker-maria!2026#xY` devient `Kermaria2026xY` |
| `DoNotWritePasswordsInActiveDirectory` | `0` | a `1`, le compte est cree desactive avec `pwdLastSet = 0` |

`DoNotUpdateNotMovedUsers` n'a **aucun effet** sur le mot de passe : teste le
2026-08-06 a `0` dans le profil puis dans les defauts globaux, sans changement.

## Architecture retenue

1. `apps/webportal/app/api/internal/koxo/users/route.ts`
   expose un endpoint BFF prive protege par bearer token, HTTPS et allowlist IP optionnelle.
2. `apps/api-internal/Program.cs` expose `GET /internal/koxo/users` et les routes admin
   `GET /internal/admin/koxo` + `POST /internal/admin/koxo/validate`.
3. `apps/api-internal/Services/KoxoExportService.cs` charge, trie, valide et audite le
   payload JSON KoXo sans reparation silencieuse.
4. `scripts/koxo/Sync-KoXoClients.ps1` consomme le JSON prive, applique les garde-fous,
   genere un CSV 14 colonnes, calcule le hash, valide la relecture, remplace la cible
   de facon sure, puis peut lancer `KoXoAdm.exe /Synchro=CLIENTS.xml`. Il ne pilote
   **qu'un profil** : `-PrimaryGroup` dit lequel.
4bis. `Invoke-KoxoSyncProfiles` (dans `KoxoSync.Common.psm1`) enchaine les deux
   profils — `CLIENTS` et `CLIENTS DÉMO` — sur un **unique** appel a l'API. C'est ce
   que declenche `Invoke-KoxoSyncFromWebhook.ps1`.
5. `scripts/koxo/Install-KoXoScheduledTask.ps1` documente et simule la tache planifiee ;
   aucune creation reelle n'est effectuee depuis le depot.

## Deux operations, a ne surtout pas confondre

Le recepteur de SRV-21 sert **deux routes** sur le meme port et le meme
mecanisme d'authentification. Elles n'ont pas la meme portee et ne sont pas
interchangeables.

| Route | Portee | Effet |
|---|---|---|
| `/internal/koxo/sync/` | **globale** | Rejoue la synchronisation CSV de tous les profils. Avec `DisableOrphanedAccounts`, une ligne absente du CSV **desactive** le compte correspondant. |
| `/internal/koxo/storage/reconcile/` | **un seul objet** | Pose un quota sur une fiche utilisateur ou une fiche de groupe secondaire. Ne touche a rien d'autre, ne lance aucune synchronisation CSV. |

**Ne jamais appeler `/internal/koxo/sync/` pour poser un quota.** La portee est
sans commune mesure avec l'intention, et une desactivation de masse ne se
rattrape pas apres coup.

### Reconciliation ciblee d'un quota

Implementee dans `scripts/koxo/KoxoStorage.Common.psm1`, appelee par
`HttpBillingV2KoxoStorageProvider` cote API-INTERNAL. Elle est **idempotente**
et **fermee par defaut**.

La requete porte tout ce qui est necessaire, pour que le recepteur n'ait rien
a deviner : `correlationId`, `targetKind` (`user` ou `secondary_group`),
`userId` exact pour une fiche personnelle, `primaryGroup` et `secondaryGroup`
exacts, `desiredQuotaMib`, plus `targetKey` et `subscriptionItemId` pour
l'audit. Aucun mot de passe ne circule. Un champ incoherent avec le type de
cible est refuse, pas neutralise.

La fiche est cherchee a son emplacement exact — `Data\Users\<PRIMAIRE>\<SECONDAIRE>\<userId>.xml`
pour une personne, `Data\Users\<PRIMAIRE>\<SECONDAIRE>.xml` pour un groupe — et
**jamais par balayage de nom** : le `sAMAccountName` est derive par KoXo et le
nom est translittere, donc aucun des deux n'est predictible cote application.
Le `userId` transmis est celui **lu** dans `customer_ad_links`.

Etat constate puis decision, avant toute ecriture :

| Constat | Decision |
|---|---|
| fiche absente | `not_materialized` — bloquant, jamais de creation |
| quota applique == desire | `noop` |
| quota applique < desire | augmentation appliquee |
| quota applique > desire | `blocked_reduction` — **jamais applique** |
| fiche illisible ou ambigue | `failed` |

Une reduction n'est pas l'inverse d'une augmentation : abaisser un quota sous
l'occupation reelle bloque l'utilisateur sans rien liberer. Cette phase ne
reduit donc jamais.

Pour une augmentation : verrou partage avec la synchronisation globale (KoXoAdm
ne supporte pas deux instances), relecture sous verrou, sauvegarde locale,
remplacement atomique, puis
`/RepairUser UserId="…" Type="Storage"` ou
`/RepairSecondaryGroup Group="…" PrimaryGroup="…" Type="Storage"`. Seul le type
`Storage` est demande : une reparation complete reappliquerait aussi groupes,
mot de passe et acces.

La fiche est relue **apres** la reparation. L'ecriture n'est pas sa propre
preuve : une reparation qui reecrirait la fiche depuis la base KoXo annulerait
silencieusement la modification.

Si la reparation ne peut pas etre prouvee (exception ou timeout sans marqueur de fin, relecture XML non concluante, ou verification FSRM demandee mais en echec), la fiche **pre-repair est restauree atomiquement avant de rendre l'echec**. Un retry ne peut donc pas devenir un faux `NOOP` simplement parce que l'intention etait restee ecrite dans le XML.

Le niveau de preuve est explicite dans la reponse :

- `xml_verified` — la fiche porte bien `EnableFolderQuota=1` et le quota
  demande, relue apres la reparation ;
- `fully_verified` — le quota **effectif** a en plus ete constate cote FSRM.

FSRM n'est verifie que si `KOXO_STORAGE_FSRM_ENABLED=true` et qu'un gabarit de
chemin est fourni ; la verification demandee mais non concluante **ferme** le
resultat. Par defaut, la reponse s'arrete honnetement a `xml_verified`.

La verification FSRM est aussi executee sur un rejeu dont la fiche XML est
deja conforme. Le quota doit etre actif, contraignant (`SoftLimit=false`) et
de taille exacte. Si les outils `Get-FsrmQuota` sont absents du receveur,
`KOXO_STORAGE_FSRM_SERVER` permet une lecture CIM distante de
`Root/Microsoft/Windows/FSRM:MSFT_FSRMQuota`, bornee au chemin attendu et a une
reponse unique. Aucun role local n'est installe. Une erreur d'authentification
ou de lecture ferme le resultat ; le compte d'execution doit disposer des
droits distants requis. Un succes sous un administrateur connecte ne prouve
pas ceux de SYSTEM. Voir le [bilan DEV](DEV_STORAGE_ACCESS_VALIDATION.md).

## Donnees exportees

Chaque utilisateur exporte contient exactement 8 champs JSON :

- `civilite`
- `nom`
- `prenom`
- `dateNaissance`
- `identifiantUnique`
- `groupeSecondaire`
- `email`
- `groupePrimaire` — **n'alimente aucune colonne du CSV.** Le groupe primaire est
  porte par le profil KoXo (le XML), pas par le fichier. Ce champ sert uniquement
  a aiguiller chaque identite vers le bon profil, donc vers le bon CSV. Voir
  « Separation des groupes primaires » ci-dessous.

Un neuvieme champ **facultatif** peut s'y ajouter :

- `motDePasse` — alimente la colonne 14. Publie, KoXo l'applique a l'annuaire ;
  **absent, KoXo reapplique le mot de passe qu'il detient dans sa propre base**.
  Nuance mesuree le 2026-08-06, et elle compte : avec `ForcePasswords=1` le
  journal affiche « Mot de passe force » et `pwdLastSet` change **meme quand la
  colonne 14 est vide** — KoXo ne s'abstient pas, il reecrit ce qu'il sait. Le
  compte reste donc authentifiable avec son mot de passe courant, mais c'est
  KoXo, et non l'annuaire, qui fait autorite. Une valeur perdue de sa base
  serait perdue tout court. Il n'est jamais
  journalise : `Write-KoxoSyncLog` ecarte toute cle nommee `token`, `password`,
  `motdepasse` ou `secret`.

### Comment le mot de passe atteint l'export

L'export est un instantane complet regenere a la demande, alors que le mot de
passe n'existe en clair qu'a l'instant ou le client le saisit. Il faut donc le
retenir entre les deux, et c'est le role de
`Services/KoxoPendingPasswordStore.cs` :

- **en memoire**, pas en base. Persister le mot de passe en clair dans MariaDB
  creerait un magasin de secrets durable pour un besoin qui dure quelques
  secondes ;
- **a usage unique**. Un second export ne republie pas le mot de passe, sinon
  KoXo le reappliquerait a chaque synchronisation et annulerait tout changement
  ulterieur ;
- **a duree bornee** (15 min), et une entree qui expire sans etre consommee est
  journalisee en avertissement — la divergence portail/annuaire ne doit pas
  etre silencieuse.

Seul l'export **reel** consomme l'entree. Le tableau de bord admin et la
validation rejouent la preparation a la demande : ils passent
`consumePendingPasswords: false`, faute de quoi un simple affichage ferait
disparaitre le mot de passe avant KoXo, et l'exposerait dans l'apercu.

**Limite assumee** : un redemarrage de l'API, ou un deploiement multi-instances
sans affinite de session, perd l'entree. Le mot de passe du portail reste
correct, seul l'alignement annuaire est manque, et le client peut redefinir son
mot de passe pour relancer le cycle.

Le CSV genere 14 colonnes avec `;` comme separateur :

1. `civilite`
2. `nom`
3. `prenom`
4. `dateNaissance`
5. `identifiantUnique`
6. `groupeSecondaire`
7. `email`
8. vide
9. vide
10. vide
11. vide
12. vide
13. vide
14. `motDePasse` (vide si non publie)

La premiere ligne contient l'en-tete exact KoXo :

`Civilite;Nom;Prenom;DateNaissance;IdentifiantUnique;GroupeSecondaire;Email;Telephone;TelephoneMobile;Fax;PageWeb;ChampLibre;Fonction;MotDePasse`

### La largeur est constante, et ce n'est pas cosmetique

`Test-KoxoCsvFile` exige **exactement 14 champs sur chaque ligne**, en-tete
comprise, et nomme la ligne fautive. Motif : KoXo rapproche les lignes par
l'`IdentifiantUnique` de la **colonne 5** (`UseUniqueIDFirst=1`). Un champ
manquant decale cette colonne, et KoXo ecrit alors l'identite **et le mot de
passe** d'un client sur le compte d'un autre.

Ce n'est pas theorique : le 2026-08-06, un `clients.csv` assemble a la main
melangeait des lignes a 13 et 14 champs. Le journal KoXo porte la trace de
`Ajout/Modification de Jean DUPONT (zachary.hounsahou)` suivi de
`Mot de passe force pour "zachary.hounsahou"` — l'identite de test appliquee
sur un compte reel, mot de passe compris.

## Ce que KoXo fait de ces champs (verifie en reel le 2026-08-03)

### `groupeSecondaire` pilote l'OU — et la cree si besoin

KoXo place l'identite dans l'OU nommee d'apres ce champ, **et cree cette OU si
elle n'existe pas**. C'est le seul levier de placement annuaire : l'application
ne deplace aucune identite elle-meme.

| Cas | Valeur publiee |
|---|---|
| Essai de demonstration en cours | `DEMO-` + le code `CLI-XXXXXX` reserve a la creation |
| Essai anterieur a la reservation systematique | `DEMO-CLI-DEMO`, l'OU commune historique |
| Compte converti en client reel | le meme code, **sans prefixe** |
| Client reel ordinaire | sa reference client, qui nomme deja son OU |

Le prefixe n'est pas cosmetique. **KoXo ne cree un groupe secondaire dans
l'annuaire que s'il est nouveau pour sa propre base.** Si les deux cotes de la
separation portent le meme nom, il croit le groupe deja existant, ne le cree
jamais dans la nouvelle branche, et l'identite migree **perd son groupe
definitivement** — mesure en reel le 2026-08-06, ni un troisieme passage ni la
suppression de la coquille d'origine ne l'ont retabli. Le changement de nom entre
l'essai et le compte definitif est donc ce qui rend la conversion possible.

### Separation des groupes primaires

Un modele KoXo ne s'associe qu'a **un seul groupe primaire**, et c'est lui qui
porte le quota (32 Go pour un client payant, 5 Go pour un essai) et le modele de
compte. La population est donc scindee en deux profils, chacun avec son XML, son
CSV et son fichier d'etat :

| Groupe primaire | CSV | Profil |
|---|---|---|
| `CLIENTS` | `clients.csv` | `/Synchro=CLIENTS.xml` |
| `CLIENTS DÉMO` | `clients-demo.csv` | `/Synchro=CLIENTS-DEMO.xml` |

L'argument le plus fort n'est pas le quota : avec un CSV unique et
`DisableOrphanedAccounts` actif, **une anomalie d'export cote demonstration
desactive de vrais clients payants**. Separer cloisonne le rayon d'action de
chaque synchronisation.

**L'ordre des profils compte.** Le lanceur passe `CLIENTS` puis `CLIENTS DÉMO`,
ce qui est correct pour le sens habituel (conversion demo -> payant) : le profil
de destination reprend l'identite avant que celui d'origine ne balaye ses
orphelins. Pour une migration en sens inverse, il faut passer le profil de
destination **d'abord**, a la main, sinon l'identite est supprimee par le premier
passage avant d'avoir ete reprise par le second.

**Ce qu'une migration entre groupes primaires conserve et ce qu'elle perd**
(mesure sur un compte reel le 2026-08-06) :

| Conserve | Perdu |
|---|---|
| mot de passe (authentification verifiee apres coup) | **appartenance aux groupes `GG_*`** — a reappliquer |
| `allowLogon` (RDS), `sAMAccountName`, `employeeNumber` | |
| compte actif, `HomeDirectory` / `HomeDrive` | |

La perte des `GG_*` corrige une note anterieure : une synchronisation ordinaire
ne les touche pas — verifie — mais **un deplacement entre groupes primaires les
efface**.

Points de vigilance verifies en reel :

- le **groupe primaire doit preexister** — l'IHM le cree, le CSV non. Un profil
  qui vise un groupe inexistant sort en trois lignes de journal, avec les deux
  marqueurs de succes, **sans toucher personne**. C'est ce que detecte le
  garde-fou « zero traite » ;
- la graphie doit correspondre **au bit pres** : `CLIENTS DÉMO` vaut
  `43 4c 49 45 4e 54 53 20 44 c3 89 4d 4f` en UTF-8. Cote code, le nom s'ecrit
  toujours par sequence d'echappement (`É` en C#, `[char]0x00C9` en
  PowerShell), jamais litteralement : les `.ps1` du depot n'ont pas de marque
  d'ordre d'octets et PowerShell 5.1 les relirait en ANSI ;
- le **fichier d'etat est par profil** (`koxo-sync.state.<csv>.json`). Un etat
  partage ferait alterner deux volumetries differentes, et le garde-fou de chute
  se declencherait a chaque passage sur une variation inexistante ;
- le **verrou reste commun** : `KoXoAdm.exe` ne supporte pas deux instances, les
  profils doivent s'attendre.

`Invoke-KoxoSyncFromWebhook.ps1` appelle `Invoke-KoxoSyncProfiles`, qui
**n'interroge l'API qu'une fois** et sert les deux profils depuis le meme
payload. Ce n'est pas une optimisation : l'export **consomme** les mots de passe
en attente, un second appel rendrait un payload sans colonne 14 et le profil
servi en second laisserait ses comptes sur un mot de passe obsolete.

Avant d'ecrire quoi que ce soit, `Test-KoxoProfileRouting` verifie qu'aucun
groupe primaire publie par l'API n'est laisse sans profil. Une identite qui
n'atteint aucun CSV n'est pas « ignoree » : elle devient orpheline pour le profil
qui la portait, donc desactivee.

### `identifiantUnique` revient dans `employeeNumber`

KoXo reporte l'identifiant du CSV (`CLI-NNNNNN`) dans l'attribut AD
**`employeeNumber`**. C'est la **seule cle de rattachement fiable** entre une
identite creee par KoXo et l'utilisateur portail : le nom subit une
translitteration et le `sAMAccountName` est derive par KoXo, donc aucun des deux
n'est predictible cote application. `DemoProvisioningService` s'en sert pour
ecrire le lien `customer_ad_links` manquant.

### Qui part dans le CSV sans lien AD

La regle reste fail-closed (`KoxoExportCandidateQuery`) : un compte reel sans
`customer_ad_links(user)` n'est exporte que s'il est **designe explicitement**
par un cycle de vie. Trois cas, pas un de plus :

1. l'essai de demonstration (`demo_kind = 'trial'`) ;
2. l'utilisateur additionnel Billing V2 (`billing_v2_user_identity_provisioning`
   en `koxo_pending` / `directory_ready`) ;
3. le **compte client principal** (`portal_user_identity_bootstrap`,
   migration 096) en `koxo_pending` / `directory_ready`, e-mail verifie quand le
   parcours l'exige (Cart, VPS) et secret KoXo non expire.

Avant le 3e cas, la boucle etait fermee pour tout compte principal en
`controlled_write` : pas de lien, donc pas d'export, donc pas d'identite, donc
pas de lien. Detail du cycle et procedure de reprise des comptes existants :
[PRIMARY_IDENTITY_BOOTSTRAP.md](PRIMARY_IDENTITY_BOOTSTRAP.md).

### Le CSV fait autorite, mais ne porte pas les permissions

Une synchronisation reconcilie l'annuaire sur le CSV. **Retirer une ligne est
donc une instruction** : KoXo desactive les comptes absents du fichier. En
revanche l'appartenance aux groupes `GG_*` n'est **pas** pilotee par le CSV,
elle reste du ressort de l'API — une synchronisation ne peut donc pas defaire
une revocation d'essai echu.

> ### Un compte retire du CSV est SUPPRIME, pas desactive
>
> Mesure en reel le 2026-08-06 : deux identites absentes du CSV ont disparu de
> l'annuaire, introuvables par `sAMAccountName`. Les profils portent
> `<SyncDoNotDeleteUsers>0</SyncDoNotDeleteUsers>`, qui l'emporte sur le nom
> rassurant de `DisableOrphanedAccounts`. `BackupDeletedUsersData=1` sauvegarde
> les donnees, **pas le compte**.
>
> La suppression est irreversible et le compte recree recevra un **SID
> different** : les ACL de fichiers, les acces RDS et tout ce qui reference le
> SID sont perdus. Partout ou ce document dit « desactive », lire « supprime ».
> C'est ce qui donne leur portee reelle aux garde-fous ci-dessous.
>
> **Applique le 2026-08-06** : `SyncDoNotDeleteUsers` vaut desormais `1` sur les
> deux profils. Un orphelin est **desactive**, plus supprime. Ce qui precede
> reste ecrit ici parce que le drapeau peut se defaire, et que son nom ne dit pas
> ce qu'il fait.

#### Le cycle desabonnement / reabonnement

Mesure de bout en bout le 2026-08-06, avec les profils reels :

| Passage | CSV | Etat du compte |
|---|---|---|
| A | identite presente | cree, actif |
| B | identite retiree | **desactive**, toujours present |
| C | identite de retour | **reactive automatiquement** |

Le compte qui revient conserve son `SID`, son `sAMAccountName`, son
`employeeNumber`, son dossier personnel et **son mot de passe** — verifie par
authentification reelle. Rien n'est a refaire cote annuaire, et l'adoption par
`employeeNumber` cote API retrouve le compte tel quel.

C'est ce qui rend le reabonnement possible sans renommer ni recreer : une
suppression aurait donne un SID different, donc des ACL de fichiers et des acces
RDS perdus.

#### Garde-fou de volumetrie

Consequence directe : un export **partiel mais valide** — requete interrompue,
client filtre par erreur — coupe l'acces de vrais clients sans lever la moindre
erreur. La validation du CSV protege d'un fichier *corrompu*, pas d'un fichier
*incomplet*.

`KOXO_MAX_USER_DROP_PERCENT` (defaut `20`) refuse donc la synchronisation quand
le nombre de lignes chute de plus de ce pourcentage par rapport au dernier
export reussi, memorise dans `koxo-sync.state.<csv>.json` — **un fichier par
profil**. Le premier passage, qui n'a pas de reference, ne bloque jamais.

C'est justement ce trou du premier passage que couvre `KOXO_ALLOW_EMPTY_CSV`
(defaut `false`) : un CSV **vide** vaut ordre de desactivation de toute la
branche, et une reference a zero ne pourrait pas s'y opposer. Par defaut, un
profil sans identite a publier est donc **saute**, son fichier laisse intact et
KoXo pas lance — perime vaut mieux que destructeur.

> Attention : le defaut du module ne fait pas foi en exploitation. Une variable
> Machine `KOXO_*` posee sur SRV-21 prime, et `KOXO_MAX_USER_DROP_PERCENT` y est
> restee a `100` apres la correction du defaut, laissant le garde-fou inoperant
> en production jusqu'au 2026-08-06. Corriger le code ne suffit pas : il faut
> repasser par `Deploy-KoxoScripts.ps1 -Settings`.

Pour une baisse legitime, `KOXO_ALLOW_USER_DROP=true` laisse passer et marque le
passage `bypassed` dans le journal. Le releve (`user_count`,
`baseline_user_count`, `drop_percent`) est ecrit a **chaque** execution, y
compris quand le controle passe : un garde-fou muet ne se distingue pas d'un
garde-fou absent le jour ou l'on cherche a comprendre une desactivation en
masse.

> Le defaut valait `100` jusqu'a la V0.41, ce qui le rendait inoperant : la
> comparaison est strictement superieure et une chute ne peut pas depasser
> 100 %.

#### Un identifiant n'appartient qu'a un seul CSV

Des qu'il y a plusieurs profils de synchronisation — typiquement `CLIENTS` et
`CLIENTS DEMO` —, un meme `IdentifiantUnique` present dans deux CSV est
revendique par deux moteurs de reconciliation : la derniere synchro executee
reprend l'identite, et le retrait du premier fichier la fait passer pour
orpheline, donc **desactivee**.

`KOXO_OTHER_CSV_PATHS` (chemins separes par `;`, vide par defaut) liste les
autres CSV de l'installation. Avant d'ecrire quoi que ce soit,
`Test-KoxoIdentifierOwnership` refuse la synchronisation si un identifiant y
figure deja, et nomme le fichier fautif.

**Ce controle ne vaut que pour une invocation isolee**, typiquement une synchro
manuelle d'un seul profil. `Invoke-KoxoSyncProfiles` le neutralise
deliberement : il relit les CSV **sur disque**, or ils sont perimes tant que
leur profil n'est pas passe. Un client qui vient de changer de branche figurerait
dans le nouveau fichier *et* encore dans l'ancien, et serait signale comme un
conflit alors qu'il n'en est pas un — bloquant exactement la conversion qu'on
cherche a rendre possible. L'orchestrateur verifie la meme propriete sur la
**source**, ou elle est exacte : un export decoupe par groupe primaire produit
des sous-ensembles disjoints par construction.

#### Une synchro qui ne traite personne n'est pas un succes

Un profil visant un groupe primaire **inexistant** sort en trois lignes :
parametre accepte, fin de l'operation. Les deux marqueurs de succes sont donc
presents alors que KoXo n'a touche personne — mesure en reel le 2026-08-06.

`Test-KoxoLogOutcome` compte desormais les identites traitees dans le journal
KoXo et **echoue si ce compte est nul alors que le CSV en publie**. Le controle
ne compare pas les nombres exacts : un deplacement d'identite entre groupes
primaires ne journalise aucun utilisateur au premier passage, et ce passage est
legitime. Seul le zero absolu est traite comme un echec.

### Autres attributs renseignes

`sn`, `givenName`, `displayName`, `mail`, `userPrincipalName`,
`personalTitle` (`Mme` / `M.`), `pager` (date de naissance),
`physicalDeliveryOfficeName` / `division` / `department` (groupe secondaire),
`homeDirectory`, `homeDrive`, `scriptPath`, et l'appartenance au groupe portant
le nom du groupe secondaire.

### Code de sortie et fin de processus

`KoXoAdm.exe` renvoie **1 meme en cas de succes** (defaut connu, non corrige ;
en interactif il faut valider deux ou trois fois). **Ne pas se fier au code de
sortie** : le script s'appuie sur les marqueurs `LogSuccessful`,
`LogAcceptedMarker`, `LogCompletionMarker` et `LogBlockingError` du journal KoXo.

**Ne pas se fier davantage a la fin du processus.** `KoXoAdm.exe` peut terminer
son travail — journal complet, `Fin de l'operation` ecrite — puis **ne jamais
rendre la main**. Constate sur SRV-21 le 2026-08-04 a 21:32 : le journal portait
`Parametre accepte`, l'`Ajout/Modification` des deux utilisateurs et
`Fin de l'operation` a 21:32:27, mais le processus tournait toujours ; le script
l'a tue au bout de 90 s et a journalise `KoXo sync failed` alors que la
synchronisation avait reussi. Le receveur webhook remontait donc un echec pour
une synchronisation correcte.

Le depassement de `KOXO_SYNC_TIMEOUT_SECONDS` est donc traite **exactement comme
un code de sortie non nul** : le processus est tue, puis le journal KoXo recent
est consulte.

| Journal recent | Resultat |
|---|---|
| prouve le succes (marqueurs attendus, pas d'erreur bloquante) | statut `completed_after_timeout`, `TimedOut = $true`, journalisation en niveau `warning`, la synchronisation est un succes |
| ne prouve rien (absent, incomplet ou erreur bloquante) | erreur `KoXo process timed out after N seconds.` apres une journalisation de niveau `error` |

## Variables d'environnement

### Webportal / BFF

- `KOXO_EXPORT_API_TOKEN`
- `KOXO_EXPORT_ALLOWED_IPS` optionnelle, liste separee par `;`
- `KOXO_EXPORT_REQUIRE_HTTPS` par defaut `true` hors local

### Script PowerShell

- `KOXO_API_URL`
- `KOXO_API_TOKEN`
- `KOXO_ALLOW_INSECURE_HTTP` optionnelle, `false` par defaut, reservee a la recette technique hors HTTPS
- `KOXO_CSV_ENCODING` optionnelle, `utf8bom` par defaut ; toute autre valeur
  expose a une perte d'accents, voir la section « Encodage »
- `KOXO_MIN_USER_COUNT`
- `KOXO_MAX_USER_DROP_PERCENT` optionnelle, **`20` par defaut**
- `KOXO_ALLOW_USER_DROP` optionnelle, `false` par defaut
- `KOXO_ALLOW_EMPTY_CSV` optionnelle, `false` par defaut ; sans elle, un profil
  sans identite a publier est **saute**, son CSV laisse en l'etat
- `KOXO_OTHER_CSV_PATHS` optionnelle, vide par defaut ; chemins separes par `;`.
  **Sans effet depuis le lanceur `Invoke-KoxoSyncFromWebhook.ps1`**, qui la vide
  volontairement : l'exclusivite des identifiants s'y verifie sur l'export, pas
  sur des fichiers voisins encore perimes en cours de passage
- `KOXO_SYNC_TIMEOUT_SECONDS`
- `KOXO_LOG_DIRECTORY`
- `KOXO_KOXO_LOG_GLOB`
- `KOXO_BACKUP_RETENTION_COUNT`

### Reconciliation ciblee de quota

Cote API-INTERNAL (SRV-13) :

- `BILLING_V2_KOXO_STORAGE_URL` — vise **la route ciblee**, jamais
  `/internal/koxo/sync/`
- `BILLING_V2_KOXO_STORAGE_TOKEN`
- `BILLING_V2_KOXO_STORAGE_TIMEOUT_SECONDS` optionnelle, `180` par defaut
- `BILLING_V2_KOXO_STORAGE_ALLOW_INSECURE_HTTP` optionnelle, `false` par defaut

Les deux premieres absentes laissent le provider **dormant** : tout quota
Billing V2 est alors bloque, sans repli silencieux. Une seule des deux fait
echouer le demarrage — une configuration a moitie posee est une erreur
d'exploitation, pas une intention.

Cote SRV-21 :

- `KOXO_STORAGE_WEBHOOK_TOKEN` optionnelle. Posee, elle devient le **seul**
  jeton accepte sur la route de stockage, de sorte qu'un secret qui fuiterait
  cote facturation ne puisse pas declencher la synchronisation globale. Absente,
  la route retombe sur `KOXO_SYNC_WEBHOOK_TOKEN`.
- `KOXO_STORAGE_DATA_ROOT` optionnelle, `C:\Program Files\KoXo Dev\KoXoAdm\Data`
- `KOXO_STORAGE_FSRM_ENABLED` optionnelle, `false` par defaut
- `KOXO_STORAGE_FSRM_SERVER` optionnelle — hote portant le role FSRM
- `KOXO_STORAGE_FSRM_USER_PATH_TEMPLATE` /
  `KOXO_STORAGE_FSRM_GROUP_PATH_TEMPLATE` — gabarits acceptant `{primaryGroup}`,
  `{secondaryGroup}` et `{userId}`

Valeur validee en recette SRV-21 pour les journaux KoXo :

- `KOXO_KOXO_LOG_GLOB=C:\Program Files\KoXo Dev\KoXoAdm\Data\Logs\*.log`

## Utilisation locale / simulation

### Validation admin sans KoXo

1. Ouvrir `/admin/koxo`
2. verifier les compteurs et l'aperÃ§u JSON
3. lancer `Tester la validation`
4. corriger les erreurs listees tant que le statut reste `validation_failed`

### DryRun PowerShell

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\koxo\Sync-KoXoClients.ps1 `
  -CsvTargetPath C:\Temp\koxo\users.csv `
  -WorkingDirectory C:\Temp\koxo\work `
  -DryRun
```

Le mode `DryRun` :

- prend le verrou
- appelle l'API HTTPS ou consomme la charge injectee en test
- valide le JSON
- genere le CSV temporaire
- relit et reverifie le CSV
- journalise l'operation
- n'ecrase jamais le fichier cible
- n'actualise pas l'etat precedent

### Execution cible SRV-21

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\koxo\Sync-KoXoClients.ps1 `
  -CsvTargetPath "C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\clients.csv" `
  -WorkingDirectory "C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\work" `
  -LaunchKoxo `
  -KoxoExecutablePath "C:\Program Files\KoXo Dev\KoXoAdm\KoXoAdm.exe" `
  -KoxoWorkingDirectory "C:\Program Files\KoXo Dev\KoXoAdm" `
  -KoxoSyncArgument "/Synchro=CLIENTS.xml"
```

Quand `-LaunchKoxo` est active :

- le CSV est ecrit et relu avant tout lancement KoXo
- `KoXoAdm.exe` est lance localement sur SRV-21
- le script attend la fin du processus avec le timeout configure
- un code retour non nul reste tolere si le journal KoXo recent prouve une
  fin d'operation correcte avec les marqueurs attendus
- un depassement du timeout reste tolere aux memes conditions : le processus est
  tue, puis le journal tranche (`completed_after_timeout` en niveau `warning`)
- les journaux KoXo recents peuvent etre relus via `KOXO_KOXO_LOG_GLOB`

## Procedure cible SRV-21

1. definir les variables d'environnement KoXo sur SRV-21
2. verifier que `KOXO_API_URL` pointe vers le BFF HTTPS prive
3. tester le script en `-DryRun`
4. verifier les logs locaux et le hash produit
5. verifier la lecture des journaux KoXo via `KOXO_KOXO_LOG_GLOB`
6. executer ensuite sans `-DryRun` et avec `-LaunchKoxo`
7. seulement apres recette exploitable, utiliser `Install-KoXoScheduledTask.ps1`
   en dehors du depot, avec confirmation explicite

Pour une recette technique ponctuelle avant exposition HTTPS, `KOXO_ALLOW_INSECURE_HTTP=true`
peut etre active explicitement sur SRV-21. Cette option ne doit pas rester active
en cible durable.

Recette reelle confirmee le 2026-07-30 sur SRV-21 :

- appel prive BFF KoXo via bearer token ;
- generation locale du CSV `clients.csv` ;
- backup automatique du CSV precedent ;
- lancement de `KoXoAdm.exe /Synchro=CLIENTS.xml` ;
- code retour KoXo `1` tolere si le journal recent confirme :
  `Parametre accepte`, `Ajout/Modification`, `Fin de l'operation`.

## Remplacement sur disque et rollback

- ecriture dans un fichier temporaire
- validation immediate de relecture
- remplacement sur la cible avec backup
- retention bornee des backups
- rollback manuel = remettre en place le dernier `.bak`

## Encodage

Valeurs supportees par le module :

- `utf8`
- `utf8bom`
- `unicode`
- `ascii`
- `latin1`

**Valeur par defaut du module : `utf8bom`** (2026-08-04). Le defaut precedent
`utf8` sans marque d'ordre d'octets est relu en ANSI par KoXo : `LAUMAILLÉ`
arrive alors dans l'annuaire sous la forme `LAUMAILLÃ‰`. Le defaut est
desormais sur par lui-meme : `KOXO_CSV_ENCODING` absente de l'environnement
d'execution ne peut plus reintroduire la corruption.

Deux garde-fous accompagnent ce defaut :

- `Write-KoxoTextFile` relit le fichier avec le meme encodage et **echoue** si
  un caractere a ete perdu — `ascii` et `latin1` remplacent silencieusement par
  `?` ce qu'ils ne savent pas representer ;
- l'encodage effectivement utilise est journalise (`csv_encoding`) et renvoye
  dans le resultat de `Invoke-KoxoSync` (`CsvEncoding`).

### Diagnostiquer une majuscule accentuee perdue

Deux causes distinctes, qui **se cumulent** et se distinguent a la signature :

| Constat | `LAUMAILLÉ` devient | Cause | Correction |
|---|---|---|---|
| `corrompu_encodage` | `LAUMAILLÃ‰` | CSV relu en ANSI par KoXo | `KOXO_CSV_ENCODING=utf8bom` cote machine de synchronisation |
| `translittere` | `LAUMAILLE` | KoXo normalise le caractere | reglage KoXo, ou reprise de `sn` apres synchronisation |
| `corrompu_puis_translittere` | `LAUMAILLA‰` | les deux : ANSI donne `Ã‰`, puis KoXo retire l'accent du `Ã` | corriger l'encodage **d'abord**, le reste ne se mesure qu'ensuite |

Mesure sur SRV-21 le 2026-08-04, avant puis apres correction de l'encodage :

| | `KOXO_CSV_ENCODING` | journal KoXo | `sn` | constat |
|---|---|---|---|---|
| 21:08 | `utf8` | `Roselyne LAUMAILLA‰` | `4c … 41 e2 80 b0` | `corrompu_puis_translittere` |
| 21:32 | `utf8bom` | `Roselyne LAUMAILLE` | `LAUMAILLE` | `translittere` |

La valeur `utf8bom` documentee le 2026-08-03 n'avait jamais ete appliquee sur le
serveur : `KOXO_CSV_ENCODING` y valait toujours `utf8` en variable **Machine**,
qui prime sur le defaut du module.

**Conclusion, desormais mesuree et non plus supposee** : encodage corrige, KoXo
translittere les majuscules accentuees (`LAUMAILLÉ` → `sn=LAUMAILLE`). Aucune
option de `CLIENTS.xml` ne pilote ce comportement.

### Aucun reglage du CSV ne conserve un accent

Six essais reels le 2026-08-04, tous aboutissant a `sn=LAUMAILLE` :

| CSV envoye | encodage | octets de l'accent | `sn` obtenu |
|---|---|---|---|
| `LAUMAILLÉ` | `utf8bom` | `c3 89` | `LAUMAILLE` |
| `LAUMAILLÉ` | `latin1` | `c9` (ANSI natif) | `LAUMAILLE` |
| `LAUMAILLÉ` | `unicode` | `c9 00` (UTF-16LE) | `LAUMAILLE` |
| `Laumaillé` | `utf8bom` | `c3 a9` | `LAUMAILLE` |

Deux enseignements :

- **l'encodage est hors de cause** : `latin1` ne fait intervenir aucune
  conversion UTF-8, l'accent y est un caractere natif du jeu ANSI, et il est
  rabote quand meme. La translitteration est **en aval du decodage**, dans le
  traitement du nom par KoXo ;
- **KoXo force la majuscule sur le champ `Nom`** : envoye en casse normale,
  `Laumaillé` ressort en `LAUMAILLE`. C'est cette mise en capitales qui
  desaccentue. Le contournement « saisir les noms en casse normale », propose
  dans les versions precedentes de ce document, **ne fonctionne pas** — il n'a
  jamais ete teste.

Conserver une majuscule accentuee dans l'annuaire ne peut donc **pas** se jouer
sur le contenu ni sur l'encodage du CSV. Le seul levier restant est de reprendre
`sn` / `displayName` **apres** la synchronisation, l'identite etant retrouvable
par son `employeeNumber`.

Le `sAMAccountName` reste de toute facon translittere en ASCII par KoXo
(`roselyne.laumaille`), ce qui est le comportement voulu et ne prejuge pas de la
valeur de `sn`.

Pour trancher sur donnees reelles, sans rien ecrire ni dans le CSV ni dans
l'annuaire :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\koxo\Test-KoxoAccentHandling.ps1 `
  -CsvPath "C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\clients.csv"
```

Le script relit le CSV consomme par KoXo, retrouve chaque identite par son
`employeeNumber`, affiche les octets reellement ecrits et rend un constat par
ligne. Une identite creee avant une correction d'encodage conserve son
`sAMAccountName` d'origine : la comparer sans rejouer une synchronisation donne
un faux positif.

## Deploiement des scripts sur la machine de synchronisation

`scripts/koxo/Deploy-KoxoScripts.ps1` remplace la copie manuelle fichier par
fichier, qui avait laisse le 2026-08-04 un module vieux de cinq jours sur SRV-21
pendant que la documentation decrivait l'etat du depot.

```powershell
# Voir ce qui divergerait, sans rien ecrire
.\scripts\koxo\Deploy-KoxoScripts.ps1 -DryRun

# Deployer, poser les variables et relancer le receveur
.\scripts\koxo\Deploy-KoxoScripts.ps1 `
  -Settings @{ KOXO_CSV_ENCODING = 'utf8bom' } `
  -RestartReceiver
```

Ce que le script garantit :

- **liste explicite** de fichiers deployes. Le dossier cible heberge aussi
  `CLIENTS.xml` (configuration de KoXo), `koxo-webhook-token.txt` (le secret),
  `clients.csv`, `backups\`, `Logs\` et `work\` : une copie en bloc les
  detruirait. Ces noms sont **proteges**, le script refuse de demarrer si la
  liste a deployer en contient un ;
- **comparaison insensible aux fins de ligne**. `*.ps1` n'est pas couvert par
  `.gitattributes` : git rend du CRLF a la sortie alors que la cible peut porter
  du LF. Comparer les octets bruts signalerait une derive permanente sur des
  fichiers identiques ;
- **sauvegarde horodatee** dans `backups\deploy-<horodatage>\` avant tout
  ecrasement ;
- **verification apres copie** : empreinte exacte **et** analyse syntaxique du
  fichier arrive, pour attraper une copie tronquee ;
- **variables Machine `KOXO_*`** posees et verifiees, car elles priment sur les
  defauts du module — deployer le module sans corriger la variable ne change
  rien au comportement. Les valeurs dont le nom contient `TOKEN`, `SECRET` ou
  `PASSWORD` ne sont jamais affichees ;
- **redemarrage du receveur** via `-RestartReceiver`. Sans lui, un changement de
  variable reste sans effet : le processus garde son bloc d'environnement, et le
  script emet un avertissement explicite dans ce cas ;
- **validation finale** par une synchronisation `-DryRun` qui prouve que le
  deploiement est vivant, et rend l'encodage et la presence du BOM ;
- **inventaire de la derive** : les scripts presents sur la cible mais absents du
  depot sont listes. C'est ainsi qu'a ete repere
  `Start-KoxoSyncWebhookReceiver-8042.cmd`, depuis rapatrie.

### `Start-KoxoSyncWebhookReceiver-8042.cmd`

Lanceur manuel du receveur : il lit `koxo-webhook-token.txt` place a cote et
demarre `Start-KoxoSyncWebhookReceiver.ps1`. Le port se passe en premier
argument, `8042` par defaut. Les chemins viennent de `%~dp0`, il fonctionne donc
aussi bien depuis le depot que depuis le dossier cible.

> La tache planifiee `Kermaria-KoXoWebhookReceiver-8042` appelle ce fichier
> avec l'argument `8042`. Le lanceur lit le jeton local depuis
> `koxo-webhook-token.txt`, puis demarre le receveur PowerShell.
>
> La tache est un service long-lived : `ExecutionTimeLimit=PT0S`, trois
> tentatives de redemarrage espacees d'une minute, `AtStartup`, `SYSTEM`,
> `Highest` et `MultipleInstances=IgnoreNew`.

La version qui trainait sur SRV-21 etait **inoperante** : elle portait `` `$t ``
au lieu de `$t`, fuite d'echappement PowerShell de l'outil qui l'avait generee.
`` `$ `` etant un dollar litteral, la variable n'etait jamais creee et le jeton
transmis valait la chaine « $t ». La version du depot est corrigee et un test
Pester interdit la reapparition de cet echappement.

## Permissions minimales

- lecture HTTPS sur le BFF prive
- ecriture sur le dossier cible CSV
- ecriture sur le dossier de logs locaux
- lecture sur le glob de journaux KoXo si active
- eventuellement droit de creation de tache planifiee si l'installation est
  finalement confirmee hors depot

## Mise en production controlee

Avant une vraie activation :

- renseigner un vrai `KOXO_EXPORT_API_TOKEN` hors depot
- confirmer l'encodage attendu par KoXo
- confirmer le chemin reel du CSV cible
- confirmer le compte d'execution SRV-21
- confirmer l'intervalle de planification
- valider la retention des backups et des logs
- faire une premiere execution `DryRun`
- faire une premiere execution manuelle hors heures sensibles
