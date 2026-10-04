# Accès VPN/RDS DEV — reprise du 2026-10-03

État opérationnel consigné, WIP non commité. Revalider avant toute reprise.

Cloture recette Noe : VPN negatif NPS6273/65, RDS NLA4624/type3 puis
RdpCore226/0x80070005, aucune nouvelle session. Ancienne session5 logoff bornee,
admin2 preservee. Quota et temoinSHA conserves. KoXo importmanuel dansIHM/AD
n'avait pas persiste le retrait dans XML, meme apres fermeture normale.
Realignement ponctuel et sauvegarde des seuls2noeuds obsoletes sous mutex,
aucun LDAPwrite. ModeCSVonly pour triggerqualities_changed livre au receiver
DEV depuis3592ba4, pas de lancement KoXo. Nouvelle release2032 en preparation,
ne pas confondre avec preuve de deploiement final. RapportNoe et release note
font foi pour empreintes et limites.

Decision la plus recente : automatisation du remplacement des qualites
differee explicitement par le titulaire ; il effectuera les synchronisations
lui-meme. Ne plus lancer de synchronisation sur son initiative a sa place.
Import dedie avec « Ne conserver que les qualites supplementaires importees »
valide manuellement : capture Noe montrant seulement GG_NO_ACCESS_E2E_DEV.
La synchro generale reste incrementielle. Aucune commande equivalente a cet
import n'est exposee dans l'aide CLI fournie. Retrait AD et refus VPN/RDS apres
cette intervention non revalides par l'agent. Aucun worker ni declencheur
existant n'a ete desactive par cette decision documentaire.

Derniere consigne utilisateur : mode eco-token, agent code/tests uniquement,
utilisateur infra/reglages. StripeTEST Noe canceled confirme apres traitement
normal outbox ; intent2 vide, CSV vide mais KoXo conserve qualites et AD garde
VPN/RDS. Proposition utilisateur groupe neutre : GG_NO_ACCESS_E2E_DEV cree vide
sans imbrication ; config JSON DEV + source horsGit ajoutees avant cette consigne.
Code optionnel EMPTY_QUALITY_GROUP livre en API DEV sur demande explicite du
titulaire : DLL44ECE1C2EDCEBFC77AC10D90BADD928ECA9663595B5A3390774421168A003094,
serviceRunning/readiness200, configpreservee, smoke sur paquet publiePASS.
Rollback C:\apps\api-internal-dev-old-neutral-20261004. PROD inchangee.
Ne pas conclure au retrait sans verifier que KoXo remplace au lieu de cumuler.
RelaisStripe nettoyage programme vers19:24, verification infra au titulaire.

**Lire d'abord la derniere section et docs/DEV_E2E_NOE_VALBRUME.md** : les
paragraphes suivants sont chronologiques. A 17:35 le 04/10, ajout des qualites
DEV applique et prouve, VPN/RDS positifs, mais resiliation non enregistree.
Boites natives de confirmation bloquent le pilotage navigateur (Chrome et
IAB), DNS Stripe SRV13 indisponible. API DEV actuelle DLL 3CB01B085CE087C85C19029846B1F3C971BED48B2EE57A95314E6D5261DBB392,
rollback api-internal-dev-old-compat-20261004 ; readiness200, PROD preservee.
Ne pas publier v2.0.3.2 tant que le retrait reel n'est pas prouve.

Garde local Deploy-KoxoScripts : refuse moduleCSVv3 sur tache8042 ou chemin
partagePROD avant WinRM. Suite KoXo finale202/202PASS. Contrat webKoXo adapte
PRODv2/DEVv3, abonnements et securiteADPASS. Limite statuts itemspending apres
ack : telemetrie uniquement, aucune gate/lectureUI actuelle ; ne pas inventer
deprovisioned. Rapport actualise avec empreintes, rollbacks et limites.

Reprise18h : demande utilisateur de finir Noe, profil import non modifie.
Relais CONNECT Stripe temporaire sur SRV13 loopback38844, HTTPS_PROXY du seul
processus API DEV ; TLS integral, DNS DC preserves. Lecture StripeTEST reussie,
abonnement toujoursactif ; confirmationUI attendue. Nettoyage automatique
vers18:34 via Kermaria-Stripe-DEV-Relay-Cleanup-20261004 (retire env proxy,
restartDEV, stop/desactive relais). Dossier ProgramData\Kermaria-dev\stripe-relay-20261004,
Restore-DevTransport.ps1 et cleanup-result.json. Revalider avant reprise.

- Stockage personnel KoXo session 0 validé avec `CLIENTS\svc-koxo-dev$`,
  groupe d'exécution dédié sur FS-01 ; voir `docs/KOXO_DEV_SERVICE_ACCOUNT.md`.
- API DEV raccordée au récepteur de stockage dédié SRV-21 ; ne pas confondre
  le récepteur d'identité SYSTEM et le récepteur stockage sMSA.
- Groupes Global/Security `GG_VPN_E2E_DEV` et `GG_RDS_E2E_DEV` sous
  `CLIENTS DEV`, encore vides ; mappings API DEV et règles catalogue via UI
  VPN ESSENTIAL/RDS configurés.
- Réplication intersites à 180 min : un groupe présent sur le DC enfant peut
  rester introuvable par résolution Windows sur le domaine parent. Vérifier
  chaque GC explicitement. La réplication des seuls objets concernés avec
  `Sync-ADObject` a résolu le défaut ; ne pas élargir les ACL ou changer la
  portée des groupes pour ce symptôme.
- RDS collection Clients sur SRV-27 et RDU local de SRV-30 contiennent le
  groupe DEV, en conservant les trois autorisations antérieures.
  `Set-RDSessionCollectionConfiguration` sous SYSTEM peut modifier le
  courtier puis échouer sur l'hôte distant : relire les deux côtés avant
  de conclure à un échec sans mutation. Tâches ponctuelles désactivées.
- SoftEther SRV-24 est accessible sur son interface serveur 192.168.100.224 ;
  le DNS renvoie aussi une interface d'un autre réseau. Hub Clients sans
  RADIUS, confirmé par l'utilisateur. Aucun changement DNS ni SoftEther.
- NPS existant SRV-21 : client et stratégie dédiés préparés mais désactivés,
  conditions cumulatives groupe DEV/IP SRV-24/NAS-Identifier Clients.
  Secret partagé dans un fichier protégé hors Git ; relais humain SoftEther
  en attente. Aucun secret ni export NPS ne doit entrer dans le dépôt.
- Aucun nouveau client ni achat dans cette passe ; connexions et retrait
  après résiliation restent à prouver. Handoff utilisateur pour email,
  mot de passe et paiement. Aucun commit/push/tag ni déploiement PROD autorisé.

Détails et preuves : `docs/DEV_VPN_RDS_ACCESS_VALIDATION.md`.

## Reprise inscription et SMTP

Le raccordement RADIUS a ensuite été terminé : client NPS et stratégie
actifs, secret renouvelé et correspondance vérifiée, NAS-Identifier Clients
activé. Nouvelle inscription fictive Noé Valbrume créée, ID
`6dc55aab-14f7-4d64-91ef-f94bf140d41d`, encore en attente de validation e-mail.
Après un envoi SMTP annulé et une résolution DNS transitoirement en échec,
le renvoi manquant a été implémenté dans l'UI/BFF/API avec rotation atomique
du jeton et délai d'une minute. Livré uniquement en DEV, tests locaux PASS.
Renvoi via UI enregistré `sent` à 23:43, corrélation
`d3b232ee-8715-44ce-b05e-7cb8df0b639c`. Attendre la confirmation du titulaire
avant approbation ; ne pas extraire de lien ni modifier le statut en base.
Voir `docs/DEV_SIGNUP_VERIFICATION_RECOVERY.md` pour code, preuves et rollback.

## État du 2026-10-04 après reprise nocturne

Les paragraphes précédents sont des étapes historiques. L'état courant est
dans `docs/DEV_E2E_NOE_VALBRUME.md` : paiement TEST confirmé, souscription
`d0f9f554-1465-40a2-9f1e-668919b58502` active, identité `CLI-D000002` /
`noe.valbrume`, quota dur 64 Go et deux groupes DEV présents. Une délégation
member sans héritage sur les deux groupes a été ajoutée à
`HOME\svc-kermaria-ad-dev`, puis le moteur API a appliqué les appartenances.
Le modèle KoXo DEV contient maintenant un partage personnel `DEV-%USER_ID%$`,
sans partage de groupe. Écriture/relecture réussies comme Noé ; fichier témoin
à conserver jusqu'au contrôle de résiliation.

Les tests VPN ont identifié la vraie source RADIUS de SRV-24 : `10.35.62.253`,
confirmée par Hyper-V. Client et condition NPS corrigés sur cette seule IP,
avec NAS-Identifier Clients et groupe DEV inchangés. Refus actuel précis :
NPS événement 17, Message-Authenticator absent côté SoftEther, protection
NPS conservée. Aucun profil VPN de test ne reste configuré. RDS réel validé
le 04/10 à 03:13 avec FreeRDP officiel, certificat épinglé et rendu SDL dummy :
session 3 de Noé active, événement Security 4624 type 10 et événements LSM
21/22. Seule cette session de test a ensuite été fermée. Aucun test de passerelle
RDS externe ni d'ergonomie graphique. Ne pas résilier avant preuve VPN positive.

Le code appelle maintenant le provisioning existant après règlement vérifié
par réconciliation Stripe, en préservant le workflow technique VPS et les
gates de readiness. Tests ciblés, contrat et smoke API PASS. API DEV finale
DLL SHA-256 `748A9BF9C0556A1981EB27F455C2B115B84922B0712F23E5A4BF615E17CD627C`,
readiness 200, configuration DEV et binaire PROD conservés. Ce changement
n'ajoute pas de file durable de reprise du provisioning après crash.

## CSV des qualités supplémentaires — WIP local du 04/10, non déployé

Le titulaire a validé l'import KoXo et fourni le CSV DEV à 15 colonnes :
QualitésSupplémentaires après MotDePasse, noms séparés par virgule. Noé contient
GG_RDS_E2E_DEV,GG_VPN_E2E_DEV ; l'autre identité a une cellule vide.
L'API et le convertisseur local prennent maintenant en charge ce format,
avec contrat JSON v3 incompatible volontairement avec le récepteur v2.
La projection réutilise le plan par identité et l'intersection avec le suivi
des memberships actifs déjà provisionnés. Ne pas la confondre avec une
migration complète du moteur AD vers un provider KoXo : cela reste à faire.
La révocation du plan enlève le groupe exporté sans le reprendre depuis AD.
Les groupes manuels non suivis ne sont pas adoptés implicitement.
Voir docs/koxo-sync.md pour limites et livraison coordonnée nécessaire.

Validation : build et smoke API réellement exécutés PASS, tests d'export et
projection PASS, typecheck shared/web PASS, contrat CSV autonome PASS sous
Windows PowerShell 5.1 et PowerShell 7. La suite Pester 3.4 locale présente
des échecs d'assertions. Résolu ensuite avec une copie isolée officielle de
Pester 4.10.1 : 193/193 PASS sous Windows PowerShell 5.1. Le contrôle des
identifiants inter-CSV avait aussi un vrai défaut de découpage ; corrigé avec
TextFieldParser et regressions champs cités/multilignes. Profil KoXo DEV relu :
Data\CSVSynchro\CLIENTS-DEV.xml référence OtherGroups=Field 15 et séparateur
interne virgule. Aucune écriture serveur,
aucun remplacement du CSV de référence ni déploiement dans cette passe.

### Suite : demandes de qualités persistantes, non activées

WIP local : migration 098_koxo_quality_intents.sql et dépôt transactionnel,
policy de révisions et tests (canonicalisation, rejeu, concurrence optimiste,
retrait, accusé obsolète). Branche optionnelle du moteur après gates/stockage/
résolution qui publie une demande au lieu d'écrire AD ; résultat pending.
Export capable de lire ce document sans dépendre d'un ajout AD préalable.
Le dépôt n'est PAS enregistré en DI : pas de modification du runtime existant.
Restent traitement/retry, déclenchement, preuve KoXo+AD, accusé de révision,
tests MariaDB (migration non appliquée), activation et recette résiliation.
Console KoXo SRV-21 observée ouverte PID 5876/session 2 : fermeture demandée
avant les essais réels, réponse encore attendue à ce point de reprise.

Dispatcher local ajouté : lease 120 s, timeout 60 s, reprise exponentielle,
accusé conditionné à client/révision/hash et preuves CSV+KoXo+AD. Worker et
dépôt toujours non enregistrés, migration 098 non appliquée. Tests ciblés PASS.
Lecture réelle par nouveau module KoxoQualities.Common.psm1 : CSV Noé conforme,
mais AdditionalQuality/SAMAccountName corrompus en « membership managed by
Billing V2 » (description AD contenant des points-virgules). Group et FQDN
corrects, memberships AD présents. Cause import suspectée, à démontrer.
Ne pas contourner ce refus de preuve. Voir docs/koxo-sync.md pour état précis.

Executant HTTP + route de preuve DEV implementes localement, encore non
actives : route distincte, bearer, lecture sous mutex, 409 si console ouverte,
verification identite AD et noms/DN des groupes, synchro puis relecture.
Revue du seul sous-agent autorise GPT-5.6 Terra Medium : capacite bornee avant
publication (128 identites / 64 KiB canonique), export decouple du catalogue
(intent ou dernier baseline actif applique) pour isoler les erreurs metier.
Tests cibles API PASS, suite KoXo 199 PASS avant la derniere borne du corps HTTP.
Demande en attente : autoriser base jetable kermaria_koxo_quality_test_dev sur
SRV-06 pour les tests SQL ; fermeture console KoXo egalement en attente.
Utilisateur absent jusqu'a environ 18h ; gere lui-meme le reset d'utilisation.
Un seul sous-agent autorise utilise. Deploiement v2.0.3.2 autorise uniquement
si tout est fonctionnel et verifie ; condition NON satisfaite actuellement.

Base de test dediee approuvee par « Tu peux continuer », accord reformule.
SSH SRV-06 confirme MariaDB 11.8.6 et absence de cette base. Compte SQL enregistre
mariadb_backup@localhost sans CREATE DATABASE ; root sans mot de passe refuse.
Script create-koxo-quality-test-database.sql prepare, execution admin demandee.
Runner --koxo-quality-mariadb compile, migration embarquee, refuse toute cible
sauf SRV-06:3306/kermaria_koxo_quality_test_dev avec opt-in explicite ; execution
prevue depuis SRV-13 avec le migrator DEV existant. Tests transactionnels
prepares, non executes. Aucun schema operationnel ou test n'a ete cree.

### Suite apres autonomie explicite a 13:45

Script CREATE DATABASE execute sur SRV-06 : refuse 1044, aucun effet schema.
Alternative locale permise par l'autonomie : instance MariaDB 11.8.9 existante
en nouveau datadir prive, 127.0.0.1:33398, aucun service installe. Runner borne
a cette adresse/base et verification @@datadir. Tous les tests transactionnels
reels PASS ; instance arretee proprement, PID 53436 termine, port ferme.
Fixtures/preuves protegees sous Backups\Kermaria\koxo-sql-isolated-20261004.
Creation manuelle de la base test SRV-06 devenue inutile. La migration sur
kermaria_dev reste a appliquer apres sauvegarde, aucune base active modifiee.
Activation DI maintenant implementee sous BILLING_V2_KOXO_QUALITIES_ENABLED,
false par defaut, garde DEV/DB/recepteur/provisioning et schema au demarrage.
Non activee. Restent anomalie SAMAccountName KoXo, tests reel ajout/retrait,
deploiement DEV coordonne, resiliation et preservation des donnees.

Console KoXo : fermeture normale refusee (taskkill sans /F), sauvegarde DEV
privee realisee, pas d'arret force. Pour tester la corruption a l'import,
descriptions des deux GG_*_E2E_DEV sauvegardees puis points-virgules remplaces
par tirets ; aucun changement de droit. Reimport et preuve SAM restent a faire.

Migration098 maintenant appliquee a kermaria_dev via migrator depuis SRV-13.
Backup logique refuse faute SHOW VIEW ; backup physique filtre DEV + prepare
--export reussi sur SRV-06 sous /var/backups/kermaria-dev-before098-20261004-1410.
345 fichiers, 273828234 octets, manifeste hash dans docs/koxo-sync.md. Table
098 vide, 11 colonnes et contraintes verifiees ; service DEV Running/ready200.
Flag toujours inactif, aucun binaire remplace ; rollback conserve table additive.

Livraison suivante realisee : API DEV DLL 4207D95A3499B7D4FE802310CE328114DA32E49082AC7710851EEEEA01BCD2FF,
config preservee/ready200, rollback api-internal-dev-old-qualities-20261004.
Recepteur DEV separe de PROD sous ProgramData\Kermaria\koxo-dev\app-qualities-20261004,
seule tache DEV redirigee/restart. Flag absent=false. Export reel via BFF
v3 deux utilisateurs avec bonnes qualites ; deux DryRun deterministes,
CSV actif inchange et KoXo non lance. Route preuve 401 sans auth, 409 avec
console ouverte. Worker pas encore active, WebPortal notice pending seulement
local. Retrait reponsecachedack reconnu mais pas de nouvelle preuve AD.

WebPortal DEV ensuite livre sous releases-dev/webportal-dev-koxo-qualities-20261004,
archive 2BFDAE7887562052D19B7F5D24ACBC55CF6159D3CC1FEFDC78D378303907C9E5,
env DEV/PROD et lien PROD preserves. Dependances Sharp Linux ajoutees depuis
le lock avec SHA512 verifiees, rendu PNG serveur PASS. Build polices reussi
via auxiliaire reseau local limite, ensuite arrete. Session Chrome admin
retablie et fiche Noe rechargee (qualityPwTab, onglet 476082170). Abonnement
actif, resiliation immediate disponible ; pas encore executee.

### Etat actualise a 17:15 le 4 octobre

Les points ci-dessus sont historiques. Console KoXo fermee normalement par
CloseMainWindow dans sa session, tache ponctuelle desactivee. Deux SAMAccountName
corrompus dans les qualites XML Noe corriges sous mutex, apres backup et preuve
AD ; reimport natif conserve la correction. Cause point-virgule suspectee,
pas totalement demontree. Aucun changement de groupe par cette reparation XML.

Flag qualites actif dans JSON API DEV et source locale. Correction de recherche
AD KoXo : utilisateur directement sous OU client, pas sous OU Users.
DLL DEV 4DF683287E20304F8B3D7A88C795A479CC9893AE891700C325E01A052268794F,
rollback api-internal-dev-old-quality-search-20261004. Noe intent revision1
applied_revision1 a 14:30:14 UTC, CSV+XML+AD prouves. VPN reconnecte16:38,
NPS6272 ; NLA RDS positif16:34 (pas une nouvelle session complete).
Titulaire a confirme P: automatiquement monte et temoin lisible.

Resiliation non enregistree : Chrome bloque sur confirmation ; DB reste active.
DNS Stripe depuis SRV13 indisponible, DNS DC preserves. Correction compat PROD
v2 locale et tests complets API PASS. Pas de tag/commit/push/PROD. v2.0.3.2 reste
conditionnee au retrait reel VPN/RDS et a la conservation des donnees.
