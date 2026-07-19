# V0.38 - Dossier KoXo, AD et R740xd

Statut : **cadrage implementation-ready + runbooks differes**. Ce dossier
ne decrit pas le comportement actuellement livre ; il prepare la future
integration Kermaria -> Active Directory -> KoXo Administrator. Depuis le
2026-07-18, le domaine enfant cible `clients.home.bzh` existe deja ; ce
qui reste a confirmer est surtout les comptes de service, les ACL
detaillees et le rollout d'activation.

## Objet

V0.38 fige une cible dans laquelle :

- **Kermaria** reste la source de verite metier pour les clients et les
  utilisateurs crees via le portail ;
- **Active Directory** devient le systeme d'identite technique cible des
  comptes clients dans le domaine `clients.home.bzh` ;
- **KoXo Administrator** reprend les comptes crees par Kermaria pour
  l'administration quotidienne, sans devenir la source de verite ;
- la reprise KoXo est **obligatoire mais asynchrone**, par artefacts
  `CSV/XML` et tache planifiee.

## Ordre de lecture

1. [V0.38_KOXO_SIGNUP_INTEGRATION.md](V0.38_KOXO_SIGNUP_INTEGRATION.md)
   : spec fonctionnelle et technique de la future feature.
2. [V0.38_SITE_AD_ALIGNMENT.md](V0.38_SITE_AD_ALIGNMENT.md)
   : ecart entre le modele actuel du site et la cible AD `clients.home.bzh`.
3. [V0.38_KOXO_DATA_CONTRACTS.md](V0.38_KOXO_DATA_CONTRACTS.md)
   : contrats de donnees, mapping Kermaria/AD/KoXo, formats d'artefacts.
4. [V0.38_KOXO_AUTOMATION_RUNBOOK.md](V0.38_KOXO_AUTOMATION_RUNBOOK.md)
   : chaine asynchrone de reprise KoXo par `CSV/XML` et tache planifiee.
5. [V0.38_R740XD_CUTOVER_CHECKLIST.md](V0.38_R740XD_CUTOVER_CHECKLIST.md)
   : prerequis et checklist d'activation lorsque le R740xd arrivera.

## Relations avec la documentation existante

Ce dossier prolonge ou contredit explicitement plusieurs hypotheses plus
anciennes :

- [../V0.26_USER_GUIDE_SIGNUP.md](../V0.26_USER_GUIDE_SIGNUP.md)
  documente aujourd'hui qu'aucun compte AD n'est cree a l'approbation d'un
  signup. V0.38 prepare le futur comportement dans lequel la creation AD
  intervient au `set-password`.
- [../AD_PRODUCTION_MIGRATION.md](../AD_PRODUCTION_MIGRATION.md)
  decrit maintenant la sortie de `OU=TEST_SITE_WEB` vers
  `clients.home.bzh` pour les operations AD admin actuelles. V0.38 va plus
  loin en preparant l'alignement du modele de donnees et du signup.
- [../PRODUCTION_DEPLOYMENT.md](../PRODUCTION_DEPLOYMENT.md)
  reste la procedure de bascule infra vers R740xd. V0.38 ajoute les
  prerequis identite/KoXo qui n'y figurent pas encore.

## Decisions figees par V0.38

- domaine AD cible : `clients.home.bzh`
- domaine enfant cree le 2026-07-18
- OU cible : `OU=Clients,DC=clients,DC=home,DC=bzh`
- arborescence cible : `OU=<CUSTOMER_REFERENCE>/Users|Disabled`
- groupes de securite centralises dans
  `OU=SecurityGroups,OU=Kermaria,DC=home,DC=bzh`
- creation du compte AD au moment du `set-password`
- synchronisation continue du mot de passe portail -> AD
- particulier : `1 signup = 1 customer + 1 portal_user + 1 user AD`
- pro/association : signup multi-utilisateur avec un compte portail et un
  compte AD par utilisateur
- KoXo en administration quotidienne uniquement
- reprise KoXo asynchrone obligatoire, non bloquante pour la fin du signup

## Prerequis encore ouverts

Les points suivants restent a confirmer par l'infrastructure avant
implementation et avant activation sur R740xd :

- hote exact qui portera la tache planifiee de reprise KoXo
- comptes de service, droits AD et secrets d'execution
- delegations exactes sur `OU=Clients` et sur les groupes `GG_*`
- chemins SMB/locaux de depot des artefacts `CSV/XML`
- politique de supervision et d'archivage des journaux KoXo
