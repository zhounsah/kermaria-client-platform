# Compte de service KoXo DEV — 2026-10-03

> **Mise à jour après raccordement manuel et reprise de l'API, 22:06 :** les
> quatre paramètres de stockage sont désormais présents dans le JSON DEV.
> Le service a échoué après l'édition manuelle à cause d'une virgule manquante
> après `BILLING_V2_KOXO_STORAGE_TIMEOUT_SECONDS`, avant `PROVISIONING_ENABLED`.
> Une correction strictement syntaxique (une virgule, aucune valeur modifiée)
> a été appliquée après sauvegarde protégée. Service Running, readiness 200
> et en-tête Development confirmés ; jeton identique à celui du receveur.
> Le blocage de raccordement décrit plus bas est donc historique.

## Reprise après erreur JSON

- Sauvegardes sur SRV-13 :
  `C:\ProgramData\Kermaria-dev\json-syntax-repair-20261003-220653`.
- Hash avant correction :
  `0D9404FE971419B88DA8D23CADAC4AA7DDCB43E10ABD772A5F36BE7D56DEE868`.
- Hash après correction :
  `CB60514A3648461B9359DF0AFE6460F1374DDB58B29132FDA483B56663212A4E`.
- JSON valide, sans clé dupliquée, valeurs en chaînes, garde-fous DEV validés.
  URL attendue, jeton comparé sans affichage, HTTP explicitement autorisé et
  timeout 180 s. Seul le service DEV a été démarré.
- La source locale hors Git contient deux affectations pour chacune des quatre
  clés ; les dernières, au niveau principal du script, sont correctes. Aucun
  nettoyage de ces doublons ni exécution du script complet n'a été effectué.
- Après actualisation, l'UI conserve `BILLING_V2_KOXO_STORAGE_PROVIDER_NOT_READY`.
  **Constat source VALIDE :** `BillingV2AdminOperationalLimitations.Default`,
  dans `BillingV2AdminReadinessService.cs`, inclut ce message statiquement.
  Il ne constitue pas une sonde du provider actuellement configuré.
- La reprise et la configuration ne prouvent pas encore le parcours commercial
  complet ; restent les groupes DEV d'accès, la nouvelle identité, le paiement
  TEST, les connexions VPN/RDS et la résiliation.

## Autorisations et périmètre

L'utilisateur a autorisé un compte de service dédié, puis son rattachement à
un groupe de sécurité donnant le droit administrateur sur l'hyperviseur. Le
compte ne figure pas directement dans les administrateurs locaux. **KoXo
reste l'unique outil appliquant les ACL et quotas des dossiers clients.**

Vérification actuelle : `KERMARIA-FS-01.home.bzh` désigne
`KERMARIA-SRV-01`, PowerEdge R740xd, domaine `home.bzh`, rôles Hyper-V et FSRM.
Le privilège approuvé couvre donc tout cet hôte, pas seulement `CLIENTS DEV`.
Le suffixe DEV du groupe ne réduit pas les droits Windows hérités.

## Identité et droits effectivement posés

- sMSA `CLIENTS\svc-koxo-dev$`, SID
  `S-1-5-21-2041662347-3856961323-164109739-1688`.
- Objet dans `CN=Managed Service Accounts,DC=clients,DC=home,DC=bzh`, lié
  uniquement à l'ordinateur `KERMARIA-SRV-21`, installé et testé sur cet hôte.
  Mot de passe géré par Windows ; aucune clé KDS forestière créée.
- AES128/AES256 ; `AccountNotDelegated=True`, `TrustedForDelegation=False`.
- Seule appartenance explicite : `CLIENTS\GG_KOXO_FSRM_ADM_DEV`, groupe de
  sécurité global, SID `S-1-5-21-2041662347-3856961323-164109739-1689`.
  Ce groupe a pour seul membre le sMSA et est membre des administrateurs locaux
  de `KERMARIA-SRV-01`. Aucun ajout aux administrateurs de domaine.
- Le groupe existant `CLIENTS-KOXO-ADM` n'est pas utilisé : il appartient aussi
  aux Opérateurs de serveur et Utilisateurs du Bureau à distance du domaine.
- SRV-21, accès applicatifs : lecture de la fiche du groupe primaire DEV,
  modification de la branche XML `Data\Users\CLIENTS DEV`, des journaux KoXo
  et de `Data\AutoBackup`. Ces ACL concernent les fichiers de l'application,
  pas les dossiers clients de FS-01. Les sauvegardes SDDL précèdent les changements.

Le choix sMSA et son association à un seul hôte suivent la
[procédure Microsoft](https://learn.microsoft.com/en-us/services-hub/unified/health/kb-running-assessments-with-msas).
La forêt ne possédait aucune clé KDS ; le sMSA évite une création de clé
forestière pour un service sur un seul hôte.

## Blocage natif identifié puis corrigé

Deux commandes ciblées en session 0 avaient expiré à 90 secondes. Les lectures
FSRM par CIM et COM réussissaient pourtant sous le sMSA. Une instrumentation
en lecture seule, dans le contexte du processus de test, a relevé le message :
impossibilité de créer une archive dans `Data\AutoBackup`, accès refusé.

L'accès à ce répertoire applicatif a été ajouté sans désactiver les sauvegardes.
La commande `/RepairUser UserId="devkoxo2.testisola" Type="Storage"` a ensuite
réussi sous le sMSA/session 0 en 3,25 secondes, avec les marqueurs de journal
attendus (`20261003_205638.log`). Le quota et la fiche ont été relus.

La copie diagnostique instrumentée avait perdu le BOM UTF-8 du module et mal
reconnu les marqueurs accentués sous PowerShell 5.1. Elle a été remplacée par
la copie originale avec BOM avant la validation finale. Cette instrumentation
ne fait pas partie des modules déployés du receveur.

## Service de stockage isolé

- Tâche `Kermaria-KoxoStorageReceiver-DEV-8043`, principal sMSA, session 0,
  démarrage système, relance sur erreur, aucune session utilisateur requise.
- Code et configuration privés dans
  `C:\ProgramData\Kermaria\koxo-dev-storage` ; code/configuration en lecture
  pour le service, écriture limitée aux répertoires de travail et de journaux.
- Route unique : `http://+:8043/internal/koxo/storage/reconcile/`.
  Réservation URL limitée à cette route et à ce compte.
- `-StorageOnly` impose une instance isolée, un jeton dédié, une vérification
  FSRM effective et un serveur explicite. Le secret d'export CSV n'est pas lu.
- Les requêtes de stockage sont bornées aux profils de l'instance. Le mutex
  KoXo commun couvre la lecture/modification XML, le processus natif et la
  vérification. Sa prise interne par le lanceur reste réentrante.
- Le receveur d'identité DEV et le receveur PROD restent inchangés sous SYSTEM.
  Le service de stockage n'a pas de route permettant de lancer la synchro CSV.

## Preuves courantes et limites

- 188 tests Pester locaux PASS avant mise en service.
- GET refusé 405, mauvais jeton refusé 401, corps invalide refusé 400,
  profil `CLIENTS` de production refusé 400.
- Relecture du quota technique : HTTP 200, `noop / fully_verified`.
- Appel diagnostique depuis **SRV-13**, augmentation 200 → **201 MiB** :
  HTTP 200, `applied / fully_verified`. KoXo réalise l'opération en session 0.
  Ce test utilise des références explicitement diagnostiques, sans souscription.
- Rejeu 201 MiB : `noop / fully_verified`. Demande de réduction à 200 MiB :
  `blocked_reduction / BILLING_V2_KOXO_STORAGE_QUOTA_DECREASE_REFUSED`.
- La configuration permanente de l'API DEV n'est pas encore raccordée à ce
  stade. Ce n'est donc pas une preuve E2E navigateur/Billing/paiement.
- L'action groupée de modification des quatre clés `BILLING_V2_KOXO_STORAGE_*`
  du JSON API DEV et de sa source hors Git, puis redémarrage DEV, a été rejetée
  avant exécution par le contrôle d'approbation automatique, sans motif détaillé.
  Aucune de ces écritures n'a eu lieu ; `StorageConfigured=False` a été relu.
  L'utilisateur a confirmé précisément les quatre clés et le redémarrage DEV.
  Une nouvelle tentative a néanmoins été rejetée avant exécution, toujours sans
  motif détaillé. Aucun autre chemin n'a été utilisé pour contourner le rejet ;
  le raccordement reste à effectuer par un opérateur ou après levée du blocage.
  Revalidation lors de la continuation automatique : URL et jeton toujours
  absents, hash du JSON inchangé
  `4FE8E6EE618F3B7F0B65EB2928A817C5321BF277342D59A9D9B5389E3DAF820A`.
  L'API DEV est active et sa readiness vaut 200 sur
  `http://192.168.100.213:5100/health/ready`, avec l'en-tête Development.
  Elle écoute cette adresse précise, pas `127.0.0.1` : un échec sur le loopback
  ne doit pas être interprété comme une panne. Le service stockage reste actif.
- L'utilisateur s'est connecté à l'administration DEV. Lecture UI confirmée :
  `BILLING_V2_KOXO_STORAGE_PROVIDER_NOT_READY`, cohérent avec le JSON non raccordé.
  Aucun changement de catalogue n'a été effectué.
- Préflight catalogue : `STORAGE-PERSONAL` propose six capacités, de 16 à
  512 Go ; l'écran de provisioning n'affiche que les règles de groupe AD, donc
  son absence de lignes ne démontre pas l'absence de règles de quota. RDS vise
  encore `GG_RDS` ; les cinq paliers VPN visent encore `GG_VPN`. Les groupes DEV
  dédiés restent à préparer avant toute modification de ces règles.
- Préflight infrastructure, lecture seule : SRV-30 est dans **clients.home.bzh**,
  son rôle RDS et `TermService` sont actifs. Ses groupes d'accès locaux incluent
  `GG_DEMO_RDS` et `GG_RDS_CLIENTS`. SRV-27 possède trois collections, dont
  `Clients`, prouvées par WMI RDMS ; une erreur des cmdlets RD dans une session
  WinRM ne doit pas être interprétée comme une absence de déploiement.
  `TSGateway` y est arrêté. SRV-24 est une VM active mais WinRM n'est pas joignable.
  Ces observations ne prouvent pas les connexions VPN/RDS client et n'ont donné
  lieu à aucun redémarrage ou changement de politique sur ces hôtes.
- Aucun compte client nouveau, paiement, mapping ou accès VPN/RDS créé dans
  cette phase. Aucun commit, tag, push ni livraison applicative PROD.

Preuves, sauvegardes avant changement et XML de la tâche d'identité initiale :
`C:\Users\zhounsah\Backups\Kermaria\koxo-service-account-20261003-202911`.

Retour arrière ciblé : arrêter/désactiver seulement la tâche de stockage,
retirer sa réservation URL et le groupe dédié des administrateurs locaux si
l'autorisation est révoquée ; restaurer uniquement les ACL applicatives
explicitement sauvegardées. Ne pas supprimer de dossier client ni réduire son
quota. Le compte et le groupe peuvent être conservés pour diagnostic, sans
suppression AD ni restauration globale.
