# Feuille de route

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
