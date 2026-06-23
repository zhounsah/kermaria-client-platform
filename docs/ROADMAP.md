# Feuille de route

## Phase de tests : principe

Tant que le serveur cible **R740xd** n'est pas livre, le projet reste en
**phase de tests** sur les hotes existants **SRV-01** et **SRV-02**. Aucune
preprod jetable ne sera deployee sur l'infra actuelle.

Pendant cette phase :

- aucune mutation AD hors `OU=TEST_SITE_WEB,DC=home,DC=bzh` ;
- aucun e-mail effectivement envoye a un destinataire externe ;
- aucune numerotation fiscale revendiquee comme legale ;
- aucun client reel integre.

Les jalons fonctionnels (V0.20, V0.21) avancent en respectant ces bornes.
Les jalons d'exploitation finale (V0.22b, V1.0) sont **bloques par la
livraison du R740xd**.

## Jalon V0.20 facturation reelle BPCE controlee

Statut : **implemente dans le depot, en phase de tests (mode live non active
par defaut)**.

Integration avec l'API de gestion de factures de la Banque Populaire
(https://www.gestion-factures.banquepopulaire.fr/inv/api/v5) :

- `BPCE_INTEGRATION_MODE` : `disabled` (defaut) | `mock` | `live` ;
  le mode `live` emet de vraies factures cote banque, ne l'activer qu'apres
  validation explicite ;
- `--verify-bpce-sender` : commande CLI pour verifier la connexion JWT et
  lister les profils de facturation BPCE (lecture seule, aucune ecriture) ;
- synchronisation client BPCE (`/customers/`) via `external_id` = reference
  externe client, idempotente ;
- creation de brouillon et validation (`/invoices/` + `validate/`) :
  numerotation fiscale allouee par BPCE, facture immuable apres validation ;
- generation PDF archivee cote `API-INTERNAL` en LONGBLOB avec hash SHA-256 ;
- tables `bpce_customers` et `bpce_invoices` (migration `008_bpce_invoicing`)
  pour la double persistance locale independamment de la disponibilite BPCE ;
- endpoint admin `POST .../issue` et `GET .../invoice/pdf` servi depuis le
  cache local (jamais d'acces BPCE direct depuis le navigateur) ;
- interface admin : bouton d'emission avec confirmation, affichage numero
  fiscal, lien PDF ;
- portail client : affichage du statut `issued` sans texte "informatif" ;
  telechargement PDF client prevu en V0.21 (necessite endpoint portail dedie).

La V0.20 ne declenche aucun envoi e-mail, aucune action AD, aucun paiement
en ligne. Le mode `live` est desactive par defaut, conforme au principe
phase-de-tests (R740xd non encore livre).

## Jalon V0.21 suivi paiement manuel et premier canal e-mail

Statut : **a faire, e-mail desactive par defaut**.

- statut paiement `unpaid`, `partial`, `paid`, `overdue` ;
- enregistrement manuel admin d'un encaissement ;
- relances depuis l'admin avec notifications portail (s'appuie sur V0.12) ;
- canal e-mail transactionnel sortant, **premier vrai canal externe**, via
  SMTP configurable mais non cable sur un SMTP de production ;
- templates minimaux : facture emise, relance, confirmation encaissement ;
- journal d'envoi e-mail isole.

La V0.21 n'ajoute aucun paiement en ligne, aucun prelevement SEPA, aucun
rapprochement bancaire, aucun SMS, aucun push, aucun WebSocket. Le SMTP
reel reste branchable uniquement en preprod cible.

## Jalon V0.22a stabilisation testable sur SRV-01 et SRV-02

Statut : **a faire, faisable sans la cible R740xd**.

- recette complete executee sur le staging interne (couvre V0.16 et V0.17) ;
- restauration MariaDB testee sur instance distincte ;
- revue accessibilite WCAG AA des parcours client critiques ;
- audit securite interne : dependances, secrets, headers, rate limiting ;
- documentation utilisateur admin et client ;
- procedure formelle de mise en production redigee, **non executee** ;
- plan de continuite minimal documente.

La V0.22a n'ajoute aucune fonctionnalite metier et ne s'execute pas sur
l'infrastructure definitive.

## Jalon V0.22b validation cible R740xd

Statut : **bloque, declenche a la livraison du R740xd**.

- bascule des services sur l'hote cible ;
- execution de la procedure de mise en production redigee en V0.22a ;
- restauration MariaDB testee sur la cible reelle ;
- supervision, sauvegardes et alertes cables sur l'infrastructure
  definitive ;
- rotation effective des secrets precedemment exposes ;
- certificats et regles pare-feu actifs sur la cible.

La V0.22b n'ajoute aucune fonctionnalite metier.

## Jalon V1.0 produit commercialisable minimal

Statut : **bloque, materiel**. Prerequis : V0.22b realisee sur le R740xd.

- deploiement sur l'infrastructure cible avec domaine, TLS et supervision
  actifs ;
- CGV, mentions legales et politique de confidentialite publiees ;
- tarification publique alignee avec le catalogue V0.15 ;
- premier client reel integre, **sortie de `OU=TEST_SITE_WEB`** vers une OU
  de production validee ;
- SLA documente et procedure d'incident formelle.

La V1.0 ne marque pas la fin du produit. Toute fonctionnalite supplementaire
identifiee pendant V0.22 est isolee en V0.23 ou plus tard, jamais ajoutee
en derniere minute a V1.0.

## Hors sequence

Reserves, non programmes :

- changement de mot de passe AD cote client (prepare mais desactive depuis V0.9) ;
- provisioning AD etendu hors OU de test ;
- paiement en ligne, prelevement SEPA, integration comptable ;
- automatisation NAS, RDS, VPN.

## Jalon V0.19 durcissement securite et coherence AD

Statut : **implemente dans le depot, prolonge le jalon V0.18**.

- mutations BFF admin sensibles protegees par un jeton CSRF cote serveur,
  sans stockage en `localStorage` ou `sessionStorage` ;
- `X-Service-Auth` exige sur `/internal/*` dans tout environnement non
  `Development`, et plus seulement en Production ;
- `RUN_MARIADB_TESTS=true` explicitement refuse hors `Development` ;
- validateur d'entrees Active Directory strict cote `API-INTERNAL` : DN
  normalises et scope client verifie avant toute action ;
- routes admin BFF reorganisees autour de mutations bornees auditables
  plutot qu'une lecture seule ;
- suite de tests `API-INTERNAL` etendue sur les flux AD controles.

La V0.19 ne change pas l'architecture `browser -> WEBPORTAL/BFF ->
API-INTERNAL -> MariaDB`, n'ouvre aucun nouveau perimetre metier, n'active
aucune OU AD de production, n'ajoute aucun paiement, e-mail, SMS, push,
WebSocket, provisioning complet ou suppression destructive.

## Jalon V0.18 Active Directory controlled_write borne

Statut : **implémente dans le dépôt, borné à l'OU de test validée**.

- modes `disabled`, `mock`, `read_only` et `controlled_write` ;
- endpoints admin AD via `WEBPORTAL / BFF -> API-INTERNAL` ;
- liaisons `customer_ad_links` en MariaDB ;
- recherche, création, membership, désactivation et déplacement bornés au
  scope client sous `OU=TEST_SITE_WEB,DC=home,DC=bzh` ;
- aucune suppression AD définitive exposée ;
- aucun périmètre AD de production activé.

La V0.18 ne change pas l'architecture `browser -> WEBPORTAL/BFF ->
API-INTERNAL -> MariaDB`, n'ajoute aucune connexion SQL/AD directe dans
`WEBPORTAL`, n'ajoute aucun paiement, e-mail, SMS, push, WebSocket,
provisioning complet ou suppression destructive.

## Jalon V0.17 consolidation client, staging et recette

Statut : **implémenté dans le dépôt, recette préproduction à exécuter**.

- fiche client admin consolidée avec identité, statut, services, demandes,
  documents commerciaux, factures, activité récente et audits associés ;
- nouveau contrat admin `customer detail` et route dédiée
  `/internal/admin/customers/{customerReference}` ;
- contrôles d'identifiants renforcés côté BFF et API pour limiter les accès
  croisés ou invalides ;
- headers WEBPORTAL complétés (`Permissions-Policy`,
  `Cross-Origin-Opener-Policy`, `Cross-Origin-Resource-Policy`) ;
- validation dédiée `npm run validate:staging` pour distinguer staging et
  production ;
- readiness WEBPORTAL étendue à la validation de la configuration cookie
  serveur ;
- document de recette dédié `docs/V0.17_RECETTE_PREPRODUCTION.md`.

La V0.17 ne change pas l'architecture `browser -> WEBPORTAL/BFF ->
API-INTERNAL -> MariaDB`, n'ajoute aucune connexion SQL directe dans
`WEBPORTAL`, n'active pas l'AD réelle, n'ajoute aucun paiement, e-mail, SMS,
push, WebSocket, provisioning ou suppression client destructive.

## Jalon V0.16 preproduction technique

Statut : **implémenté dans le dépôt, validation hôte cible encore requise**.

- documentation dédiée `docs/V0.16_PREPRODUCTION_TECHNIQUE.md` ;
- checklist de prédéploiement et diagnostic d'incident ;
- script `validate:preprod` pour les variables et garde-fous d'architecture ;
- script `check:health` pour WEBPORTAL et API-INTERNAL ;
- alias API `GET /ready` ;
- logs de requête et de readiness renforcés avec corrélation exploitable ;
- scripts PowerShell `backup:mariadb` et `restore:mariadb`.

La V0.16 n'ajoute aucune intégration AD réelle, aucun paiement, aucune
facturation légale, aucun e-mail, aucun SMS, aucun push, aucun WebSocket,
aucun provisioning et aucune action admin destructive.

## Jalon V0.15 socle commercial informatif

Statut : **implémenté et validé pour publication `v0.15`**.

- Catalogue d'offres administrable côté admin.
- Documents commerciaux informatifs avec statuts `draft`,
  `pending_review`, `shared_with_customer` et `cancelled`.
- Lignes de document et calcul des montants en centimes côté API-INTERNAL.
- Affichage client des documents sur `/invoices` et `/commercial-documents/[id]`.
- Pages admin `/admin/catalog` et `/admin/commercial-documents`.
- Contrat BFF/webportal vérifié par `test:commercial`.
- Migration additive `006_commercial_foundation.sql`.

La V0.15 n'ajoute aucune facture officielle, aucune numérotation fiscale
définitive, aucun paiement, aucun PDF légal, aucun e-mail réel, aucune TVA
automatisée validée, aucune action AD et aucun provisioning.

Chaque phase conserve les contraintes de `AGENTS.md` et `docs/SECURITY.md`.
L'avancement d'une phase n'autorise jamais implicitement une intégration réelle.

## Jalon V0.14 centre d'activité admin

Statut : **implémenté et validé pour publication `v0.14`**.

- Compteurs support et service à traiter.
- Mise en avant des demandes dont le dernier message vient du client.
- Liste limitée aux dix dernières activités publiques.
- Filtres admin `to_handle` et `client_reply`.
- Indicateurs de suivi sur les listes et détails.
- Aucun contenu de message ou note interne dans le contrat d'activité.
- Aucun changement de schéma MariaDB.

La V0.14 ne change aucun statut automatiquement et n'ajoute ni e-mail,
WebSocket, worker, pièce jointe, AD, provisioning, paiement ou facturation.

## Jalon V0.13 réponses client

Statut : **validé, commité et tagué `v0.13`**.

- Réponse client sur une demande support ou de service lui appartenant.
- Conversation publique distinguant `admin` et `client`.
- Affichage chronologique partagé entre les vues client et admin.
- Validation serveur et BFF de 3 à 2 000 caractères.
- Audit de l'ajout sans journalisation du contenu.
- Aucun changement de schéma : `author_user_id` existant est réutilisé.

La V0.13 n'ajoute aucun temps réel, e-mail, pièce jointe, AD, provisioning,
paiement ou facturation réelle.

## Jalon V0.12 notifications portail

Statut : **validé, commité et tagué `v0.12.1`**.

- Notifications créées lors des changements réels de statut.
- Notifications créées lors des messages publics admin.
- Aucune notification pour les notes internes.
- Centre d'activité `/notifications` isolé par client.
- Marquage individuel et global comme lu.
- Activité récente ajoutée au dashboard.
- Contrat BFF et tests MariaDB opt-in étendus.

La V0.12 n'ajoute aucun e-mail, SMS, push, worker, file de messages,
provisioning, AD, paiement ou facturation réelle.

## Jalon V0.11 workflow demandes

Statut : **validé, commité et tagué `v0.11`**.

- Statuts contrôlés pour les demandes support et de service.
- Historique append-only des créations et changements de statut.
- Notes internes réservées aux administrateurs.
- Messages publics séparés, visibles par le client dans le portail.
- Détails client isolés par la session et sans note interne.
- Détails admin avec changement de statut et formulaires distincts.
- Filtres simples par statut, priorité et ordre.
- Audit des mutations sans contenu de note ou de message.

La V0.11 ne déclenche aucun provisioning, AD, e-mail, paiement ou facturation.
Elle ne permet aucune suppression ni modification sensible d'un client ou d'un
service actif.

## Jalon V0.10 UX client

Statut : **validé, commité et tagué `v0.10`**.

- États loading, erreur et vide standardisés.
- Formulaires client validés et protégés contre les doubles soumissions.
- Appels BFF navigateur avec timeout et parsing JSON contrôlé.
- Réponses API internes illisibles transformées en erreurs non sensibles.
- Dashboard, services, support, demandes, profil et facturation informative
  clarifiés.
- Factures rendues lisibles sur mobile.
- Navigation et accessibilité basique renforcées.

La V0.10 n'ajoute aucune migration, fonction de paiement, facturation réelle,
action AD, provisioning ou écriture admin.

## Jalon V0.9 exploitation

Statut : **implémenté, validation pré-production à exécuter sur l'hôte cible**.

- Health checks live/ready API-INTERNAL et WEBPORTAL.
- Readiness MariaDB réelle et statut AD sans activation.
- Validation stricte des variables en Production.
- Authentification interservice minimale sur `/internal/*` en Production.
- Commande globale `npm run validate` et garde-fou secrets.
- Règles `.gitattributes` et exclusions de dumps/backups.
- Runbooks déploiement, opérations, sauvegarde/restauration et rotation.
- Portail privé marqué `noindex, nofollow`.

Restent à valider sur l'infrastructure : supervision, services système,
certificats, restauration réelle de test, pare-feu et rotation effective des
secrets précédemment exposés.

## 1. Documentation et architecture

Statut : **complétée pour le périmètre initial**.

- Responsabilités de `WEBPORTAL` et `API-INTERNAL` documentées.
- Flux, sécurité, contrats et modèle de données documentés.
- Choix de déploiement initial décrits.

Les procédures V0.9 sont disponibles ; leur exécution sur la pré-production
reste requise avant la production.

## 2. Squelette technique

Statut : **complétée**.

- Monorepo créé avec `apps/webportal`, `apps/api-internal` et `packages/shared`.
- Health checks live/ready, `correlation_id`, erreurs structurées et tests
  minimaux ajoutés.
- Scripts racine de build et validation opérationnels.
- Aucune dépendance à SQL ou Active Directory pour compiler et tester.

## 3. Portail client sans AD réel

Statut : **V0.9 exploitable côté interface**.

- Navigation responsive et pages principales disponibles.
- Services, factures, support, profil et catalogue fictifs.
- États vides, indisponibles, suspendus et désactivés.
- Formulaires via routes BFF avec indication `persisted: true|false`.
- Branding visible aligné avec Zachary HOUNSA-HOUNKPA EI.
- Zone `/admin` interne avec vues globales en lecture seule et mutations de
  workflow strictement bornées.

Restent à faire avant production : accessibilité approfondie, revue UX,
durcissement proxy/rate limiting et validation d'exploitation.

## 4. API interne en mode mock

Statut : **complétée, fallback conservé pour le développement**.

- Endpoints `/internal/portal/*` ajoutés.
- Réponses métier fictives et POST non persistants lorsque SQL est absent.
- Corrélation, erreurs structurées et smoke tests ajoutés.
- Refus AD `AD_INTEGRATION_DISABLED` conservé.
- Consommation par le BFF de `WEBPORTAL` ajoutée.

Restent à faire : authentification service-à-service réelle, autorisations,
rate limiting et stratégie d'audit complète.

## 5. Authentification

Statut : **V0.9 locale durcie et préparée pour pré-production**.

- Connexion locale par mot de passe hashé avec message d'échec générique.
- Token aléatoire conservé uniquement dans un cookie `HttpOnly`.
- Hash du token stocké dans `portal_sessions`, avec expiration et révocation.
- Pages privées protégées et données filtrées par `customer_id` de session.
- Audit minimal des connexions, refus et déconnexions.
- Rôles `client_user` et `internal_admin` sans RBAC complexe.
- Verrouillage temporaire configurable après plusieurs échecs.
- Révocation des autres sessions du même utilisateur.
- Vues admin globales protégées côté BFF et API-INTERNAL.

Restent à faire : fournisseur standard compatible MFA, SSO éventuel,
rate limiting renforcé, protections CSRF complémentaires et revue de sécurité
avant production.

## 6. Connexion SQL

Statut : **V0.9 implémentée, readiness et validation MariaDB opt-in disponibles**.

- Moteur MariaDB confirmé pour l'environnement de test.
- Configuration construite en mémoire à partir de variables séparées.
- Dépôt MariaDB, transactions, audits et migrations versionnées ajoutés.
- Seed fictif contrôlé disponible uniquement en développement.
- Tests MariaDB conditionnels derrière `RUN_MARIADB_TESTS=true`.
- Migration `003_admin_and_auth_hardening.sql` additive.

Restent à faire : tourner puis injecter les secrets localement, tester une
restauration sur une base distincte et réduire précisément les privilèges SQL.

`WEBPORTAL` ne devra jamais accéder directement à SQL.

## 7. Factures et services réels

Statut : **non commencée**.

- Connecter les sources existantes uniquement par `API-INTERNAL`.
- Garantir l'isolation des données du client authentifié.
- Définir un téléchargement contrôlé des documents si nécessaire.

Aucun paiement n'est prévu dans la V0.9.

## 8. Changement de mot de passe AD

Statut : **préparé mais désactivé**.

- Valider la délégation AD minimale et les règles réseau.
- Exiger l'ancien mot de passe.
- Ne jamais persister ni journaliser les mots de passe.
- Tester d'abord dans une OU dédiée sous `TEST_SITE_WEB`.
- Conserver `AD_INTEGRATION_MODE=disabled` tant que la validation n'est pas
  formalisée.

## 9. Provisioning AD contrôlé

Statut : **contrats et garde-fous préparés, mutations non commencées**.

- Limiter les actions à l'OU de test configurée et aux groupes approuvés.
- Ajouter approbation, idempotence et audit.
- Interdire tout compte Domain Admin.

## 10. Automatisation avancée

Statut : **non commencée**.

- Orchestrer plus tard les workflows NAS, RDS, VPN et facturation.
- Ajouter reprises, alertes, approbations et supervision.
- Réévaluer le modèle de menace avant chaque activation.
