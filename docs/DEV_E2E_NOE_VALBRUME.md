# Recette DEV Noé Valbrume — reprise du 2026-10-04

**Actualisation de cloture :** Stripe TEST resilie, groupes VPN/RDS retires
dans AD apres import manuel du titulaire. VPN : refus NPS6273, motif65, profil
TEST-CLIENTS-DEV laisse deconnecte. RDS : ancienne session5 de Noe fermee,
authentification4624/type3 reussie puis refus RdpCoreTS226/0x80070005 a20:36:53,
aucune nouvelle session ; session administrateur preservee. Quota dur64Gio et
SHA256 du fichier temoin C36A85A51FC1D7D32654A5D80EC52CDEAF9E4D4148D74E0700A26B21A9B767B8
conserves. Les etats precedents ci-dessous sont l'historique de la recette.

Le mode qualites prepare maintenant seulement le CSV (-PublishCsvOnly), puis
attend l'import manuel et la preuve. API et scripts testes : 205PesterPASS et
smoke completPASS. Receiver DEV et lanceur mis a jour depuis commit3592ba4,
sans copie sur le receiver PROD ni lancement de synchro.

Limite constatee : apres fermeture normale de la console, la fiche XML de Noe
portait encore les trois qualites alors que l'IHM et AD ne portaient que le
groupe neutre. Realignement ponctuel sous mutex sur l'import valide par le
titulaire : seuls les noeuds GG_RDS_E2E_DEV/GG_VPN_E2E_DEV ont ete retires,
comparaison structurelle garantissant les autres champs inchanges. Aucune
ecriture LDAP directe. Backup sur SRV21 :
`C:\ProgramData\Kermaria\noe-profile-20261004\noe-before-manual-revocation-persistence.xml`.
SHA avant DABC0F4637D4AAE0A8A8667B522E5A93E48A79857B21171CD2C557EE77D8030F,
apres D4ED1166EDC0C513822974BDEC75564288A3F96FDA120DC87EB1A16E15E2C633.
Cette reparation n'est PAS le remplacement automatise reporte par l'utilisateur.

**État au 04/10 à 17:35 : recette incomplète, ajout de qualités opérationnel
en DEV.** Paiement TEST confirmé, stockage personnel 64 Gio et écriture client
validés, session RDS réelle et connexion VPN/NPS réussies. P: est monté
automatiquement à la reconnexion, confirmé par le titulaire. Les qualités
sont maintenant publiées par l'API dans le CSV, persistées par KoXo et
acquittées après vérification CSV/XML/AD (révision 1 appliquée).

La résiliation et le contrôle de conservation après retrait restent à faire :
les confirmations natives bloquent le pilotage de Chrome et du navigateur
intégré. Aucune demande n'est encore enregistrée en base. La résolution DNS
de Stripe échoue aussi depuis SRV-13 via ses DNS habituels. Ne confirmer
qu'une boîte pour Noé TEST et fermer les autres, puis vérifier l'outbox avant
toute reprise. Le service DEV et le site répondent normalement ; PROD n'a pas
été modifiée. **v2.0.3.2 non publiée**, condition de recette non satisfaite.
Les sections suivantes conservent les étapes et leurs preuves horodatées.

## Identité et périmètre

- Demande : `6dc55aab-14f7-4d64-91ef-f94bf140d41d`.
- Client : `DEV-CLI-723NSN` ; identifiant client interne :
  `f8489b1d-aead-42c6-b330-a65f4af89e23`.
- Identité : `CLI-D000002` dans `employeeNumber`, `noe.valbrume`,
  UPN `noe.valbrume@clients.home.bzh`.
- DN vérifié :
  `CN=noe.valbrume,OU=DEV-CLI-723NSN,OU=CLIENTS DEV,OU=Utilisateurs,OU=KoXoAdm,DC=clients,DC=home,DC=bzh`.
- E-mail confirmé par le titulaire puis statut vérifié relu ; demande
  approuvée le 3 octobre à 23:46. Mot de passe défini par l'utilisateur via
  l'action admin normale, état persistant **Oui** relu le 4 octobre.
- Compte AD actif, XML KoXo présent sous le client secondaire DEV. Session
  portail client observée : « Bonjour Noé », bonne référence client.

L'e-mail de mot de passe avait échoué deux fois par délai SMTP ; voir
`DEV_SIGNUP_VERIFICATION_RECOVERY.md`. Aucun secret n'est à copier ici.

## Panier et tentative de paiement

- Panier : `255b6cb4-d013-4331-94bc-f42778957767`.
- Souscription créée : `d0f9f554-1465-40a2-9f1e-668919b58502`.
- Configuration : socle, **64 Go personnels**, VPN **ESSENTIAL**, RDS,
  sans espace partagé, sans sauvegarde additionnelle, sans utilisateur
  supplémentaire ; mensuel sans engagement.
- Le configurateur permettait de sélectionner 16 Go, mais le panier
  conservait aussi une ligne obligatoire de 64 Go. Observation UI validée ;
  cause de cet écart à diagnostiquer. La ligne optionnelle de 16 Go a été
  retirée par l'UI avant checkout, laissant un seul stockage personnel.
- Panier anonyme ensuite rattaché au bon client (`CART_CLAIMED`).
- Première tentative Stripe interrompue par la résolution de `api.stripe.com`.
  Tentative persistée et reprise automatiquement par l'outbox : aucune
  seconde commande créée manuellement.
- Page Stripe ouverte via « Poursuivre le paiement », affichage confirmé
  **Environnement de test**, abonnement mensuel **31,70 EUR**, bon destinataire.
  Confirmation financière laissée à l'utilisateur ; aucun résultat de
  paiement ni webhook réussi n'est encore établi à ce point de reprise.
- Le bandeau de `/souscription` affichait « STRIPE DISABLED » alors que la
  session Stripe préparée était de test : écart d'affichage observé, à traiter
  séparément ; ne pas confondre ce bandeau avec le mode réel du provider.

## État initial avant confirmation du paiement

Lecture AD : `noe.valbrume` actif, non membre de `GG_VPN_E2E_DEV` et
`GG_RDS_E2E_DEV`. Son seul groupe explicite est celui du client secondaire.
Sur FS-01, le dossier
`F:\KoXoDATA\CLIENTS DEV\DEV-CLI-723NSN\noe.valbrume` n'existe pas encore
et aucun quota FSRM ne correspond à ce chemin. Le récepteur stockage dédié
est démarré. Ces constats sont le point de comparaison pour le provisioning.

## Incident WAN signalé par l'utilisateur

WAN principale indisponible. Route de secours décrite par l'utilisateur :
UniFi → `192.168.100.202` / `KERMARIA-SRV-02.home.bzh` → iPhone USB partagé.
U5G commandée. Les serveurs continuent d'utiliser les DNS AD :
SRV-17/18/19 (`192.168.100.217/218/219`), eux-mêmes configurés avec les
redirecteurs `8.8.8.8` et `8.8.4.4`.

Des requêtes Stripe/SMTP ont expiré ; sur un contrôle explicite, SRV-17
expirait pour Stripe, SRV-18/19 répondaient, puis la résolution standard
sur SRV-13 a de nouveau réussi. Aucun changement DNS, NAT, routage ou
partage USB effectué. Inspection distante SRV-02 impossible (WinRM/DCOM
en échec). L'accès entrant depuis Internet n'est pas prouvé : l'outil web
externe ne pouvait pas accéder à la readiness, résultat non conclusif.

## Suite attendue

### Paiement TEST confirmé côté Stripe

Le titulaire a payé puis a été renvoyé vers « Votre paiement est prêt ».
Vérification en lecture seule de la session Checkout exacte :
`livemode=false`, `status=complete`, `payment_status=paid`, abonnement
provider `sub_1UMbLvPjVmQIehZaWnzXVxbu`.

Le contrôle normal HTTPS depuis SRV-13 échouait sur la résolution de
`api.stripe.com`. La lecture diagnostique a utilisé l'adresse issue de la
résolution précédente avec `curl --resolve` pour cette seule requête,
nom TLS et vérification de certificat conservés. Aucun changement hosts,
DNS, routage ou écriture provider ; clé TEST transmise uniquement par stdin,
réponse limitée aux états non sensibles. Le connecteur Stripe disponible
nécessitait une réauthentification et n'a pas été utilisé pour lire le compte.

À ce point, le portail reste en attente : le worker de réconciliation avait
échoué à 00:22 sur le DNS Stripe, tentative
`2d0a1bf6-33b0-443e-bd07-3c7321c4cbc1`. La livraison du webhook et
l'activation locale ne sont pas établies. **Ne pas refaire le paiement.**
La suite doit passer par les mécanismes normaux de webhook/réconciliation,
puis prouver les droits et le quota ; aucune activation manuelle de statut.

### Réconciliation financière réussie, provisioning non déclenché

À **00:28:03**, le worker journalise `reconciled=1`, `failed=0`.
Le portail affiche ensuite **Paiement confirmé** puis une souscription
**Stripe Active**, démarrée à 00:27, un cycle payé. L'e-mail
`payment_confirmed` a échoué par délai SMTP ; cela n'annule pas le traitement
financier. Le compte AD reste sans `GG_VPN_E2E_DEV`/`GG_RDS_E2E_DEV` lors du
contrôle qui suit.

Constat **VALIDE**, borné au code actuel :
`BillingV2StripeReconciliationService.ReconcileOneAsync` appelle
`VerifyAndSettleAsync` et l'émission documentaire après règlement, sans
appeler `IBillingV2ProvisioningService`. Le chemin
`BillingV2ProviderInboundEventService.TryTriggerProvisioningAsync` appelle,
lui, `TryReconcileActivatedSubscriptionAsync`. La convergence financière
après un webhook absent ne prouve donc pas la convergence des ressources.
Il reste un défaut de déclenchement autonome à traiter ; ne pas présenter
une reprise manuelle comme une preuve d'autonomie de ce parcours.

La reprise d'exploitation normale existe déjà :
`POST /internal/admin/billing-v2/subscriptions/{id}/provisioning/reconcile`,
BFF homologue, `AdminReconcileProvisioningButton` en mode Billing V2.
Elle appelle bien le moteur V2 courant et est réservée à une session admin,
avec CSRF et audit. Elle se distingue de l'ancienne route
`/internal/admin/subscriptions/...`. Session courante cliente : relais
de connexion admin demandé, aucune action de reprise encore exécutée.

### Reprise admin et revue de préparation

L'utilisateur a ouvert une session admin dans Chrome, distincte du compte
client du navigateur intégré. Une reprise V2 ciblée à **00:36:34** a été
refusée : `BILLING_V2_PROVISIONING_REVIEW_NOT_PASSED`, aucune action externe.
Audit corrélé `53444aca-7584-4be6-b1a9-453840e0cf0c`.

La review API existait, mais son action UI/BFF manquait. Raccordement local :
`AdminProvisioningReadinessReview` sur le détail d'abonnement, ciblé avec
`subscription.customerId` ; BFF POST vers la route API existante, utilisant
`handleAdminMutation` (session admin, CSRF). Le navigateur ne fournit aucun
verdict : l'API calcule et persiste sa décision, sans exécuter le provisioning.
Le succès affiché exige `ready && persisted`; les motifs de refus restent
visibles. La réconciliation demeure une action distincte.

Autre défaut UI corrigé dans ce raccordement : la reprise V2 renvoie un HTTP
200 même quand `succeeded=false`. Le bouton ne doit plus masquer ce refus
par un simple rafraîchissement ; il affiche désormais son `resultCode`.
Types shared/web, lint sans erreur et contrat preview/review validés.
Livraison DEV et résultat de la revue à consigner après leur exécution.

### Provisioning et accès personnel validés

- WebPortal DEV livré dans
  `/opt/kermaria/releases-dev/webportal-dev-readiness-20261004` ; archive
  SHA-256 `B3639BDC7CE03138A6BC52AC6733FA2B53A614BCE2F6B8EFDEF631879368C7B8`.
  Readiness healthy, fichiers d'environnement DEV/PROD et symlink PROD
  inchangés. Build, types, lint et contrat preview/review PASS.
- Revue à 00:42:03 : `BILLING_V2_PROVISIONING_READINESS_REVIEW_READY`, deux
  groupes, une cible de stockage ; corrélation
  `fbe9fa14-9d4d-4dd8-8fde-b9b644e6cf8e`.
- Première reprise : stockage appliqué en environ 4,2 s par le récepteur,
  puis refus AD. Constat VALIDE : `HOME\svc-kermaria-ad-dev` n'avait la
  délégation member que sur l'ancien groupe de recette. Ajout de la seule
  ACE `WriteProperty` sur l'attribut `member`, sans héritage, sur les deux
  groupes E2E DEV. Aucun groupe administrateur ajouté au compte.
  Sauvegarde sur SRV-21 :
  `C:\ProgramData\Kermaria\ad-dev-group-delegation-20261004\before.json`,
  SHA-256 `BA867EB51E12B8B017C835CEBE79244DE1C1E7D580AF9ACB468B328925F90096`.
- Reprise à 00:44:28 : **PROVISIONING_APPLIED**, corrélation
  `cd60e995-5fa3-4c4b-831b-7f06134b9376`. Deux appartenances vérifiées sur le
  compte `noe.valbrume` : `GG_RDS_E2E_DEV`, `GG_VPN_E2E_DEV`.
- Rejeu à 00:50:56 : **PROVISIONING_UNCHANGED**, corrélation
  `e2527caf-b6dd-46a6-b40c-141299d0ec29`.
- Quota personnel FSRM : **68719476736 octets**, `Disabled=false`,
  `SoftLimit=false`. Le SID utilisateur `...-1693` porte Modify/Synchronize
  sur son dossier et un refus de suppression du dossier racine ; ACL gérées
  par KoXo, pas réécrites par l'API.

### Partage personnel manquant et preuve d'écriture

Le premier essai SMB sous l'identité de Noé authentifie bien en Kerberos
(événement FS-01 4624, type 3), mais refuse l'écriture via `KoXoDATA$`, dont
la permission de partage est Read. Le modèle primaire `CLIENTS DEV` ne
contenait aucun élément Share. Le point de montage « Sans partage » est
normal pour la racine de stockage et **n'a pas été modifié**.

Correction KoXo DEV : ajout uniquement du Share personnel standard dans
`PrimaryGroupModel/User/Root/Contents/Folder`, avec nom **`DEV-%USER_ID%$`**.
Aucun partage de groupe ajouté. Modèle source CLIENTS lu uniquement.
Sauvegarde et script ciblé :
`C:\ProgramData\Kermaria\noe-personal-share-20261004` sur SRV-21.
Hash modèle DEV avant :
`13B1DD3B960B3F0C56E5C065B89DC75780DEB34F35B3D0C848292E44EE6D3CF6` ;
après : `7DADFB0DFE970E58DB52F65D8F0094CE2C84E6FA764773E096F70D492A8BB132`.

Réparation **du seul utilisateur Noé**, native `/RepairUser ... Type="Storage"`,
terminée en session 0 sous `CLIENTS\svc-koxo-dev$` en environ 4 s. Quota
65536 MiB dans la fiche préservé. Tâche ponctuelle ensuite désactivée.
Partage créé par KoXo : `\\KERMARIA-FS-01.home.bzh\DEV-noe.valbrume$` ;
Noé a Change, Administrateurs et CLIENTS-KOXO-ADM ont Full.

Une session réseau Windows isolée (`runas /netonly`) évite le conflit SMB
avec les connexions d'administration existantes. Avec les identifiants
autorisés de Noé, création et relecture réussies du fichier
`recette-conservation-20261004.txt`, SHA-256
`C36A85A51FC1D7D32654A5D80EC52CDEAF9E4D4148D74E0700A26B21A9B767B8`.
Mot de passe saisi au prompt masqué, aucun secret dans les scripts ou Git.
Ce fichier est volontairement conservé pour le contrôle après résiliation.
`homeDirectory` AD reste vide et `homeDrive=P:` ; le montage automatique P:
en session RDS n'est pas prouvé par cet accès UNC.

### Limites de tests de connexion

Le client SoftEther Windows est installé. Une interface de test VPN124 a
été créée, mais la protection de ses routes demandait des droits Windows
indisponibles (session locale non élevée, WinRM local refusé). Elle a été
supprimée sans connexion ; route par défaut Wi-Fi conservée vers
`172.16.90.254`, métrique 500. Aucun profil VPN contenant un mot de passe n'a
été créé. Les profils existants n'ont pas été modifiés.

Alternative examinée : WSL Ubuntu-26.04 déjà présent. L'installation OpenVPN
a échoué pendant les téléchargements (sortie WAN/DNS instable) ; aucun
paquet OpenVPN installé. Les essais TCP bornés depuis WSL vers SRV-24:443 et
SRV-30:3389 n'ont pas abouti. Aucun changement de routage/DNS WSL ou Windows.
Les connexions VPN/RDS effectives et leur refus après résiliation restent
donc à prouver. Ne pas résilier avant d'avoir pu établir la preuve positive.

### Essais VPN natifs supplémentaires et refus NPS précis

Une interface déjà inutilisée, VPN8, a une métrique IPv4 de 9999. Un profil
temporaire a été essayé avec certificat serveur individuel vérifié depuis
le serveur, contrôle de certificat activé, reconnexion désactivée, durée
bornée et surveillance des routes. Le profil est supprimé à la fin de
chaque essai. La route Windows Wi-Fi vers `172.16.90.254`, métrique 500,
est conservée ; aucune protection TLS/NPS n'a été désactivée.

Premier refus : NPS événement **13**, requête reçue de **10.35.62.253**,
adresse non déclarée. Cette adresse et 192.168.100.224 sont confirmées par
Hyper-V sur `KERMARIA-SRV-24.home.bzh`. Correction du seul client RADIUS DEV
et de sa condition d'IP source vers **10.35.62.253** ; NAS-Identifier
`^Clients$`, groupe SID `...-1690`, secret et Message-Authenticator requis
conservés. Exports NPS protégés avant/après dans le dossier déjà documenté.
La correspondance du secret NPS avec le fichier protégé a été revalidée
par booléen, sans afficher sa valeur.

Second refus : NPS événement **17**, attribut **Message-Authenticator absent**.
SoftEther conserve `RadiusRequireMessageAuthenticator=false` dans le hub
Clients. C'est désormais le blocage d'authentification VPN observé. Il reste
à activer le mécanisme côté SoftEther par une procédure d'administration
prise en charge et à revalider. Aucun redémarrage global du serveur VPN
mutualisé ni assouplissement de NPS n'a été effectué.

Preuves locales : `vpn-native-result.json` et `vpn-source-fixed-result.json`
dans `C:\Users\zhounsah\Backups\Kermaria\vpn-client-e2e-20261004`.
Ces refus antérieurs à l'authentification utilisateur ne prouvent **pas**
le retrait des droits après résiliation.

### Préflight RDS et limites d'outillage

Certificat RDP de SRV-30 récupéré en lecture seule par l'administration
authentifiée : CN=KERMARIA-SRV-30.clients.home.bzh, empreinte
`6F35871363B149998B49CFD451079B774262E3A6`. Aucune nouvelle session RDP n'a
été lancée par ces essais automatisés, ni aucune session existante interrompue.
Le client Python headless essayé dans des environnements virtuels privés
nécessite un compilateur Rust absent (et C++ pour certaines dépendances avec
Python 3.14). Le téléchargement officiel FreeRDP a échoué sur la vérification
de révocation inaccessible ; le navigateur a aussi échoué en DNS. Aucun
binaire de cette tentative n'a été exécuté et aucun contrôle de certificat
n'a été désactivé.

Lecture des groupes d'autorisation AD par recherche de base : Noé n'est pas
verrouillé, possède les deux groupes DEV et n'appartient ni à GG_RDS_CLIENTS
ni à GG_DEMO_RDS. Le fichier témoin a été relu sur FS-01 : propriétaire
`CLIENTS\noe.valbrume`, empreinte SHA-256 inchangée.

### RDS réel validé le 04/10 à 03:13

Le téléchargement officiel FreeRDP est redevenu accessible avec vérification
TLS normale. Client 3.32.2-dev0 (9ae75235a), SHA-256 du binaire
`D42E0879E2C38CF33B02E33F29D342FF9FF7B0FFDD28318AE30CCD4ADAAD0224`.
Certificat RDP épinglé par SHA-256 depuis le certificat public obtenu par
administration authentifiée :
`3A54F2B7DA174B4FC9F1652A87C644F59B24B1C32988AAB058477EC1AB9779F6`.
Mot de passe transmis uniquement à l'invite masquée, aucune ligne de commande
ni fichier de preuve ne le conserve.

Après une authentification NLA seule réussie à 03:11, une vraie connexion
RDP a été établie à 03:13 avec rendu SDL dummy, sans presse-papiers ni lecteurs
redirigés. SRV-30 confirme `CLIENTS\noe.valbrume`, session 3 active,
Security 4624 type 10 à 03:13:32, LSM 21/22 à 03:13:38 depuis 172.16.90.4.
Cette preuve valide l'ouverture de session RDS sur le LAN ; elle ne valide
ni la passerelle externe ni l'utilisation graphique du bureau.
Seule la session 3 de Noé, vérifiée avant action, a été fermée avec logoff.
La session administrateur existante n'a pas été interrompue.
Preuve locale : `C:\Users\zhounsah\Backups\Kermaria\rdp-e2e-20261004\rds-session-proof.json`.

Le blocage VPN Message-Authenticator demeure. La souscription reste active
pour permettre cette dernière preuve positive avant résiliation et contrôles
de refus des nouvelles connexions/conservation des données.

### Correctif du déclenchement après réconciliation Stripe

`BillingV2StripeReconciliationService` appelle désormais le moteur V2 après
`VerifyAndSettleAsync` lorsque le règlement est confirmé. Le garde VPS
technique existant reste appliqué ; le moteur conserve tous ses contrôles
de préparation, identité, stockage et appartenances gérées. Un refus ou
une exception n'annule pas le règlement financier. Une exception laisse
l'état externe indéterminé : le résultat ne prétend pas qu'aucune écriture
partielle n'a eu lieu. Les annulations d'exécution restent propagées.

Tests : aucun appel pour un règlement non vérifié, exclusion VPS, délégation
exacte à la souscription ciblée, conservation des refus, erreur technique,
garde VPS indisponible, readiness non exécutée et annulation. Contrat de
raccordement et suite complète de smoke tests API PASS.

Livraison finale uniquement API DEV : DLL SHA-256
`748A9BF9C0556A1981EB27F455C2B115B84922B0712F23E5A4BF615E17CD627C`,
service Running et readiness 200. Configuration DEV conservée, binaire PROD
inchangé. Le transfert WinRM fichier par fichier a été arrêté volontairement
avant bascule pour lenteur constatée ; staging partiel conservé. Même paquet
transféré en ZIP par SMB, empreinte vérifiée et extraction/bascule avec
validation DEV et retour arrière prévu. Archive SHA-256
`186C44B15F953AFDEE5FCF4450DFB2332DCDE4B4060561FB28C31FE60903C832` ;
rollback `C:\apps\api-internal-dev-old-archive-20261004-022936` sur SRV-13.

Limites : le nouveau déclenchement est testé localement et livré, mais n'a
pas été éprouvé par un second paiement réel TEST. Il ne crée pas une outbox
durable pour reprendre une exception/crash de provisioning après clôture de
la tentative financière. Une readiness non validée demeure bloquante et
requiert la reprise normale après correction. Aucune migration SQL ni
aucun nouveau paiement n'ont été réalisés pour simuler cette preuve.

1. Confirmation humaine du paiement TEST, puis preuve provider et webhook
   réellement traité ; le retour navigateur seul ne suffit pas.
2. Stockage personnel 64 Go créé/réconcilié par KoXo, quota FSRM dur vérifié,
   appartenance aux deux groupes DEV du même utilisateur.
3. Connexions VPN et RDS réelles avec les identifiants saisis par le titulaire.
4. Résiliation normale, retrait des appartenances gérées, échecs de nouvelles
   connexions et conservation du dossier/données. Ne pas prétendre qu'un
   retrait de groupe termine à lui seul une session déjà ouverte.

Conserver cette souscription et ces identifiants de corrélation à la reprise.
Ne pas rejouer un paiement, simuler un webhook ou forcer des droits pour
compenser l'indisponibilité WAN. Aucun commit/push/tag ni livraison PROD.

## Reprise du 04/10 au matin : profil de connexion et VPN

Le titulaire confirme une connexion RDS interactive réussie. Lecture AD :
`homeDrive=P:`, mais `homeDirectory` et `scriptPath` vides. Cause confirmée :
les champs `User/Directory/HomePath` et `Script` du modèle CLIENTS DEV sont
vides, ainsi que son contenu de script. Le partage personnel existe déjà.

Proposition préparée, **non appliquée**, sur SRV-21 dans
`C:\ProgramData\Kermaria\noe-profile-20261004` : sauvegarde du modèle et
`CLIENTS-DEV.proposed.xml` (SHA-256
`9A4CF56565CAC99D73EA55E3B04B56912C54E7CFBA63DBFA2B5D99B316892B5A`).
Elle raccorde `HomePath` à `\\%USER_SERVER%\DEV-%USER_ID%$`, utilise le nom
`DEV-%USER_ID%.VBS` et un script qui ne raccorde que le lecteur personnel.
Console KoXo ouverte, PID 5884 session 2 : fermeture demandée avant application
pour éviter une réécriture depuis son état en mémoire. La réparation annuaire
doit préserver le mot de passe : manuel KoXo, section 38.B, page 220,
et réglage actuel `DoNotWritePasswordsInActiveDirectory=0`. Ne pas lancer
aveuglément une réparation Directory sous cette configuration.

L'utilisateur a modifié NPS/SoftEther. État constaté avant intervention :
client RADIUS référencé par FQDN, Message-Authenticator non obligatoire,
stratégie DEV filtrant le nom convivial avec le FQDN serveur, sans condition
NAS-Identifier. Test 09:07 : événement 6273, mauvaise stratégie sélectionnée.
Sauvegarde NPS protégée `before-rule-match-20261004-0915.xml` sur SRV-21.
Conditions corrigées : SID du groupe DEV, nom convivial exact
`^SoftEther-Clients-E2E-DEV$`, NAS-Identifier `^Clients$`. Adresse du client
fixée à `192.168.100.224`, source mesurée des requêtes. L'exigence
Message-Authenticator désactivée par l'utilisateur est laissée inchangée.

Dernier test 09:20 : aucune session VPN établie, événement NPS 13
« client RADIUS non valide » malgré l'adresse correcte dans la configuration
relue. Application effective de cette configuration à diagnostiquer ; aucun
redémarrage global IAS/SoftEther effectué. Profils temporaires retirés,
route par défaut Wi-Fi conservée. Les preuves `vpn-after-user-fix.json`,
`vpn-rule-corrected-result.json` et `vpn-address-rule-corrected-result.json`
sont dans le dossier local de recette VPN. Souscription toujours active.

### VPN confirmé et correction du profil à 09:48

Après résolution NPS par le titulaire : profil `TEST-CLIENTS-DEV` connecté
sur VPN124, adresse `10.35.63.1`, session SoftEther établie et chiffrement
TLS_AES_256_GCM_SHA384. NPS événement 6272 à 09:45:05 pour Noé, stratégie
`VPN-CLIENTS-E2E-DEV`, NAS-Identifier Clients, source 192.168.100.224.
Connexion existante laissée ouverte ; aucun test du trafic RDS via cette
interface seule n'a encore été réalisé.

Modèle DEV appliqué puis réparation ciblée Directory de Noé en session 0,
sous SYSTEM, verrou KoXo tenu et `DoNotWritePasswordsInActiveDirectory=1`
uniquement pendant l'opération, puis valeur précédente 0 restaurée.
Résultat : `homeDirectory=\\KERMARIA-FS-01.HOME.BZH\DEV-noe.valbrume$`,
`homeDrive=P:` ; `pwdLastSet=134355388077148111` inchangé. Le journal annonce
« Mot de passe forcé » alors que ce marqueur AD est inchangé : ne pas utiliser
cette seule ligne comme preuve d'un changement de mot de passe.

**Incident borné et réparé :** RepairUser Directory a retiré les appartenances
GG_VPN_E2E_DEV et GG_RDS_E2E_DEV. Elles ont immédiatement été rétablies depuis
le relevé AD antérieur, exclusivement pour Noé et ces deux groupes. Ne pas
intégrer cette réparation annuaire au moteur automatique de stockage.
Tâche ponctuelle désactivée. Quota dur 64 Gio et empreinte du fichier témoin
inchangés. La connexion VPN et la session RDS du titulaire n'ont pas été fermées.

Le script reste absent sur le compte existant. Manuel KoXo section 22.A :
scripts interprétés au niveau du groupe secondaire, héritage/propagation via
la console. Modèle corrigé pour un script nommé selon le groupe secondaire,
avec P: raccordé à `\\KERMARIA-FS-01.home.bzh\DEV-` + utilisateur courant + `$`,
sans lecteur partagé. SHA-256 du modèle final :
`41E860F3BB1882AB34ACF569658F05F6819CB40C2DE327DCEAE3608DE7591D93`.
Il reste à faire hériter/générer ce script pour DEV-CLI-723NSN dans KoXo,
puis vérifier scriptPath, fichier NETLOGON et reconnexion RDS. Ne pas lancer
une propagation globale ni une nouvelle réparation Directory non maîtrisée.
Sauvegardes et résultat : `C:\ProgramData\Kermaria\noe-profile-20261004` sur SRV-21.

### Qualités supplémentaires et script confirmés après intervention du titulaire

Lecture de la fiche XML Noé : entrées `AdditionalQuality` avec `Group` et
`SAMAccountName` pour GG_RDS_E2E_DEV et GG_VPN_E2E_DEV. Les appartenances
sont présentes dans AD. La décision utilisateur remplace le principe antérieur
d'écriture des seuls groupes dans AD : les droits calculés par l'API doivent
désormais être persistés dans KoXo puis appliqués/vérifiés dans AD. Cette
adaptation du provisioning et de la résiliation reste à implémenter/tester.

`scriptPath=CLIENTS\DEV-CLI-723NSN.VBS`, fichier correspondant présent dans
SYSVOL scripts\CLIENTS, 2457 octets, modification 10:17:47. Le script raccorde
P: depuis homeDrive/homeDirectory et tente aussi Q: vers DEV-CLI-723NSN$.
Ce partage commun n'existe pas ; seul DEV-noe.valbrume$ est présent. Pour la
recette personnelle, neutraliser GroupPath dans KoXo, sans créer un partage
commun hors souscription. Le montage effectif à la reconnexion reste à vérifier.

Le marqueur pwdLastSet a évolué depuis le relevé précédent :
134355756663759411, soit 2026-10-04T08:21:06.3759411Z. Ceci prouve une nouvelle
écriture du mot de passe, pas que sa valeur diffère ; la conservation du marqueur
constatée à 09:48 ne décrit plus l'état courant. Aucun secret lu ou enregistré.

### Reprise autonome du 4 octobre — preuves à 17:15

L'autorisation d'autonomie a permis de fermer normalement la console KoXo
dans la session existante, via `CloseMainWindow` (sans arrêt forcé ni fermeture
de session RDS). La tâche ponctuelle a ensuite été désactivée.

Les deux qualités de Noé avaient des champs `SAMAccountName` incorrects,
contenant une partie de la description AD séparée par un point-virgule.
Les descriptions des seuls groupes DEV ont été sauvegardées et normalisées.
Une réimportation seule ne corrigeait pas ce cache. Sous le verrou KoXo,
les deux champs SAM ont été corrigés après vérification des noms et DN AD,
avec sauvegarde et comparaison structurelle du XML. Aucun autre champ XML
n'a été modifié. La synchronisation native suivante a conservé les valeurs
correctes. Le lien causal exact avec le parseur KoXo reste une hypothèse.

La recherche AD visait à tort une sous-OU `Users` absente de l'arborescence
KoXo. Le correctif recherche maintenant sous l'OU client lorsque KoXo possède
l'annuaire, en conservant le contrôle d'appartenance au client. Livré en DEV :
DLL `4DF683287E20304F8B3D7A88C795A479CC9893AE891700C325E01A052268794F`.
Rollback : `C:\apps\api-internal-dev-old-quality-search-20261004`.

Le flag `BILLING_V2_KOXO_QUALITIES_ENABLED=true` est enregistré dans les deux
sources DEV autorisées. La migration 098 et le worker sont actifs. Pour Noé,
`revision=applied_revision=1`, résultat `KOXO_QUALITIES_APPLIED`, acquittement
du 04/10 à 14:30:14 UTC : CSV, XML KoXo et groupes AD ont été vérifiés ensemble.
La relecture à 17:15 confirme cet état. La production est restée inchangée.

Preuves d'accès renouvelées : authentification NLA RDS réussie à 16:34
(événement 4624 type 3, à distinguer de la session RDS complète déjà prouvée),
puis déconnexion/reconnexion du seul profil VPN `TEST-CLIENTS-DEV` à 16:38,
avec NPS 6272 à 16:38:31. Le titulaire a également confirmé le montage
automatique de P: après déconnexion/reconnexion et la lecture du témoin.

La résiliation TEST a été préparée via le bouton normal de l'administration,
mais Chrome ne répond plus depuis la boîte de confirmation. La lecture SQL
confirme que l'abonnement reste `active` et qu'aucune demande de résiliation
n'a été enregistrée. Ne pas conclure à une résiliation ni à un retrait d'accès.
La résolution DNS de `api.stripe.com` échoue encore depuis SRV-13 via ses DNS
habituels ; aucune configuration DNS des DC n'a été modifiée.

La compatibilité de l'export PROD avec son récepteur v2 a été corrigée
localement : v2 sans qualités en PROD, v3 avec qualités explicites en DEV.
Tests ciblés et suite complète API exécutée : PASS. La convergence automatique
des statuts d'items après acquittement asynchrone reste en revue. La recette
de retrait VPN/RDS et conservation après résiliation n'est pas encore prouvée ;
la condition de livraison v2.0.3.2 n'est donc pas satisfaite.

La revue des statuts a ensuite distingue deux sujets : le maintien historique
de `provisioned` après résiliation est normal (aucun lecteur applicatif de ce
champ, droits calculés uniquement sur les abonnements actifs). Pour un nouvel
ajout asynchrone, `pending` peut subsister jusqu'au rejeu : limite de télémétrie,
sans blocage d'accès actuel. Une correction automatique robuste nécessiterait
un lien durable révision → items, et ne doit pas acquitter globalement des
achats plus récents à partir d'une ancienne preuve.

### Livraison DEV du correctif de compatibilité

API DEV redémarrée et readiness 200 après livraison du correctif de contrat
PROD v2 / DEV v3. DLL actuelle :
`3CB01B085CE087C85C19029846B1F3C971BED48B2EE57A95314E6D5261DBB392`.
Archive : `0182DEFDAB020A7223F1C444884FC779280F39DFBED5BC3F58172C84A0457055`.
Rollback : `C:\apps\api-internal-dev-old-compat-20261004`.
Config DEV inchangée :
`2090F18AF740F7CF7886C38FE41531913C6446365D9472AA085EDFE8E13264AB`.
DLL PROD inchangée :
`4B07BB0A764F01BC6E0F9F9B8DB2BC6F22497C9566A4B99504F8FED94FEB254F`.

Contrôles après livraison : CSV et XML KoXo conformes pour les deux qualités,
groupes AD présents, homeDrive/homeDirectory/scriptPath corrects, quota dur
68719476736 octets actif et témoin SHA-256 inchangé. Site DEV public HTTP 200
avec bannières DEV / Stripe TEST, récepteur DEV en cours et tâche de fermeture
ponctuelle désactivée. Aucun processus KoXo interactif restant au relevé.

Tests : smoke API complet, export ciblé, types shared/web, lint web sans erreur,
contrats KoXo/abonnements/sécurité AD, contrôle secrets et mémoire : PASS.
Le contrat statique KoXo attendait encore une version globale 2 ; il vérifie
maintenant explicitement la séparation PROD v2 / DEV v3. Le lint conserve un
avertissement préexistant `isBillingV2` inutilisé dans la page abonnements.
Les tests SQL isolés et les 199 tests PowerShell KoXo étaient déjà PASS dans
les étapes précédentes ; ils ne remplacent pas la résiliation réelle restante.

Revue de déploiement : risque VALIDE d'envoyer le module CSV v3 au récepteur
PROD v2 avec les valeurs par défaut du script générique. Garde ajouté avant
WinRM pour la tâche PROD 8042 ou le chemin partagé normalisé, avec tests de
refus et contrôle qu'aucune session distante n'est ouverte. Le récepteur
PROD actuel n'a pas été modifié ; la copie DEV isolée reste celle déjà vérifiée.

Validation finale de ce garde à 17:43 : **202/202 tests PowerShell PASS** sous
Windows PowerShell 5.1 / Pester 4.10.1, dont les trois nouveaux tests de cible.
`git diff --check` et contrôle de secrets de la mémoire partagée PASS.

### Reprise ciblée après 18 h : transport Stripe disponible

Priorité demandée : terminer le cycle Noé. Profil d'import KoXo non modifié.
La confirmation UI reste attendue ; souscription active et aucune outbox de
résiliation au dernier contrôle. Intent1 toujours appliqué. Test VPN négatif
préparé hors Git, non exécuté ; il refuse de démarrer si les groupes sont présents.

DNS habituel SRV-13 en échec, DNS TCP direct disponible. Relais CONNECT
temporaire `127.0.0.1:38844`, limité à `api.stripe.com:443`, transport TLS de
bout en bout sans déchiffrement, sans journal de contenu ni désactivation du
certificat. Réponse401 sans authentification puis lecture du seul abonnement
TEST réussie : livemode=false, status=active, cancel_at_period_end=false.
Aucune mutation Stripe directe ; la résiliation doit passer par l'application.

Seul l'environnement de processus de KermariaApiInternalDev reçoit HTTPS_PROXY
et NO_PROXY temporaires, sauvegardé avant changement ; service redémarré,
readiness200. Aucun DNS/hosts/paramètre Machine global/PROD modifié.
Artefacts : `C:\ProgramData\Kermaria-dev\stripe-relay-20261004` sur SRV-13.
Tâche `Kermaria-Stripe-DEV-Relay-20261004`, durée intrinsèque30min.
Tâche `Kermaria-Stripe-DEV-Relay-Cleanup-20261004` prévue vers18:34 : retrait
des deux valeurs temporaires exactes, redémarrage API DEV et arrêt/désactivation
du relais. Preuve de nettoyage dans `cleanup-result.json`. Exécuter le script
`Restore-DevTransport.ps1` plus tôt après traitement si possible. Revalider
l'état du relais avant toute reprise, ne pas supposer qu'il est encore actif.

### Résiliation confirmée et relais infrastructure au titulaire

Le titulaire a confirmé la résiliation. Après remise en service temporaire du
relais Stripe, l'outbox normale a été traitée : Stripe TEST retourne `canceled`
(canceled_at=1791133310), et l'API a publié la révision2 avec qualités vides.
CSV vide confirmé, mais XML KoXo et groupes AD conservés après import natif.
Le journal natif confirme la mise à jour de Noé ; les deux erreurs concernent
les chemins de stockage sur FS-01. Les deux comptes techniques d'isolation
déjà désactivés sont signalés comme orphelins ; Noé et Melis restent activés.
Le retrait VPN/RDS n'est donc pas prouvé et les tests négatifs n'ont pas été lancés.

Proposition du titulaire : remplacer une liste vide par un groupe sans accès.
Création effectuée avant le changement de répartition des tâches :
`GG_NO_ACCESS_E2E_DEV`, groupe Global/Security vide, sans imbrication, sous
CLIENTS DEV. Deux clés ajoutées au JSON API DEV et à sa source locale hors Git :
`BILLING_V2_KOXO_EMPTY_QUALITY_GROUP=GG_NO_ACCESS_E2E_DEV` et son mapping
`AD_PROVISIONING_GROUP_DNS__GG_NO_ACCESS_E2E_DEV`. Sauvegardes conservées.

Adaptation locale : une cible sans accès désiré utilise la qualité neutre
configurée ; une cible avec des accès réels ne la reçoit pas. Le groupe passe
par le contrôle de mapping/DN et AD_ALLOWED_ROOTS avant publication. Aucun
ajout direct AD dans le code. **Ce nouveau binaire n'est pas déployé.** Il faut
encore prouver si KoXo remplace ses qualités avec une cellule non vide ou les
cumule : l'ajout du groupe neutre ne vaut pas révocation des anciens accès.

Nouvelle consigne : **agent = code/tests ; titulaire = infrastructure/réglages**,
retours courts. Aucun déploiement supplémentaire par l'agent. Le relais Stripe
avait un nettoyage automatique reprogrammé vers19:24 lors de la reprise de18:59 ;
son exécution est à contrôler par le titulaire (Restore-DevTransport.ps1,
cleanup-result.json). Le profil d'import KoXo n'a pas été changé dans cette reprise.

### Déploiement du groupe neutre autorisé par le titulaire

Nouveau binaire API DEV livré après smoke complet sur le paquet publié : PASS.
Service KermariaApiInternalDev Running, readiness HTTP200, configuration préservée.
DLL : `44ECE1C2EDCEBFC77AC10D90BADD928ECA9663595B5A3390774421168A003094`.
Archive : `45C6BD7816E17F022879F192358B25C19FD90F81D3FC53FD60718345A1F1A4FC`.
Config : `EDA93A0F9F9A8B29BE654D0EF7D8CD0A3A1E71F75DEB86FE53A82DD964E8C2AC`.
Rollback : `C:\apps\api-internal-dev-old-neutral-20261004`.
DLL PROD inchangée. Relais temporaire absent de l'environnement du service
avant déploiement, nettoyage automatique confirmé sur ce point.
Une nouvelle réconciliation de Noé reste nécessaire pour publier la qualité
neutre ; ce déploiement ne transforme pas à lui seul la révision2 existante.
Le remplacement effectif des anciennes qualités par KoXo reste à vérifier.

### Décision finale du titulaire : synchronisations manuelles

Le titulaire confirme le caractère incrémentiel de la synchronisation générale.
L'import dédié, avec « Ne conserver que les qualités supplémentaires importées »,
a remplacé les qualités de Noé : sa capture ne montre plus que GG_NO_ACCESS_E2E_DEV.
Cette preuve concerne la fiche KoXo ; les accès VPN/RDS après retrait n'ont pas
été recontrôlés par l'agent. L'aide CLI fournie ne liste pas de commande pour
cet import dédié. L'automatisation de ce remplacement est explicitement différée.
Le titulaire prend en charge les synchronisations. Aucun changement de service,
worker ou déclencheur existant n'a été exécuté à cette occasion ; cette décision
ne constitue pas une preuve de désactivation des automatismes déjà déployés.
