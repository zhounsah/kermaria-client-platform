# Feuille de route

Chaque phase conserve les contraintes de `AGENTS.md` et `docs/SECURITY.md`.
L'avancement d'une phase n'autorise jamais implicitement une intégration réelle.

## 1. Documentation et architecture

Statut : **complétée pour le périmètre initial**.

- Responsabilités de `WEBPORTAL` et `API-INTERNAL` documentées.
- Flux, sécurité, contrats et modèle de données documentés.
- Choix de déploiement initial décrits.

Une validation d'exploitation restera requise avant la production.

## 2. Squelette technique

Statut : **complétée**.

- Monorepo créé avec `apps/webportal`, `apps/api-internal` et `packages/shared`.
- Health checks, `correlation_id`, erreurs structurées et tests minimaux ajoutés.
- Scripts racine de build et validation opérationnels.
- Aucune dépendance à SQL ou Active Directory pour compiler et tester.

## 3. Portail client sans AD réel

Statut : **V0.7 authentifiée complétée côté interface**.

- Navigation responsive et pages principales disponibles.
- Services, factures, support, profil et catalogue fictifs.
- États vides, indisponibles, suspendus et désactivés.
- Formulaires via routes BFF avec indication `persisted: true|false`.
- Branding visible aligné avec Zachary HOUNSA-HOUNKPA EI.

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

Statut : **V0.7 locale implémentée**.

- Connexion locale par mot de passe hashé avec message d'échec générique.
- Token aléatoire conservé uniquement dans un cookie `HttpOnly`.
- Hash du token stocké dans `portal_sessions`, avec expiration et révocation.
- Pages privées protégées et données filtrées par `customer_id` de session.
- Audit minimal des connexions, refus et déconnexions.

Restent à faire : fournisseur standard compatible MFA, SSO éventuel,
rate limiting renforcé, protections CSRF complémentaires et revue de sécurité
avant production.

## 6. Connexion SQL

Statut : **V0.7 implémentée, validation MariaDB réelle opt-in disponible**.

- Moteur MariaDB confirmé pour l'environnement de test.
- Configuration construite en mémoire à partir de variables séparées.
- Dépôt MariaDB, transactions, audits et migrations versionnées ajoutés.
- Seed fictif contrôlé disponible uniquement en développement.
- Tests MariaDB conditionnels derrière `RUN_MARIADB_TESTS=true`.

Restent à faire : injecter les secrets localement, exécuter les migrations sur
`TEST_WEB`, valider les sauvegardes et réduire précisément les privilèges SQL.

`WEBPORTAL` ne devra jamais accéder directement à SQL.

## 7. Factures et services réels

Statut : **non commencée**.

- Connecter les sources existantes uniquement par `API-INTERNAL`.
- Garantir l'isolation des données du client authentifié.
- Définir un téléchargement contrôlé des documents si nécessaire.

Aucun paiement n'est prévu dans la V0.7.

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
