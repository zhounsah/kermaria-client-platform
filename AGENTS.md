# Règles permanentes du projet

Ce fichier s'applique à l'ensemble du dépôt `kermaria-client-platform`.

## Priorités

1. La sécurité prévaut sur la rapidité de livraison et la commodité.
2. Préserver la séparation entre le portail public et les systèmes internes.
3. Produire du code clair, maintenable, testable et documenté.
4. Limiter chaque changement au besoin exprimé et éviter les dépendances inutiles.

## Architecture obligatoire

- `WEBPORTAL` héberge le portail client public et son backend orienté utilisateur.
- `API-INTERNAL` porte toutes les opérations sensibles et toutes les intégrations avec l'infrastructure interne.
- Le navigateur et `WEBPORTAL` ne doivent jamais accéder directement à Active Directory, au NAS, à RDS, au VPN, au serveur SQL ou aux systèmes de facturation internes.
- Toute action sensible passe par l'API privée d'`API-INTERNAL`.
- `API-INTERNAL` ne doit jamais être publiée directement sur Internet.
- Le serveur SQL existant doit être utilisé. Ne pas prévoir ni créer de VM SQL supplémentaire.
- L'architecture applicative est limitée à deux VM : `WEBPORTAL` et `API-INTERNAL`.

## Sécurité

- Ne jamais stocker de secret, mot de passe, jeton, certificat privé ou chaîne de connexion dans le code, les exemples ou l'historique Git.
- Utiliser des variables d'environnement ou un gestionnaire de secrets adapté à l'environnement cible.
- Ne jamais utiliser un compte `Domain Admin` pour l'application, les déploiements ou les automatisations.
- Toute future intégration AD utilisera un compte de service dédié, limité à l'OU Clients et aux seules opérations nécessaires.
- Ne pas implémenter de connexion ou d'action Active Directory réelle sans validation explicite de l'architecture, des permissions et des tests.
- Journaliser les actions sensibles dans des logs d'audit structurés, sans enregistrer de secret ni de mot de passe.
- Appliquer le moindre privilège aux identités, flux réseau, comptes de base de données et droits applicatifs.
- Valider toutes les entrées côté serveur et ne jamais faire confiance au navigateur.

## Qualité

- Respecter les conventions et outils déjà présents dans le dépôt.
- Favoriser des composants simples, des contrats explicites et des dépendances maintenues.
- Ajouter des tests proportionnés au risque de chaque changement.
- Documenter les décisions d'architecture et les comportements de sécurité importants.
- Ne pas laisser de code mort, de contournement de sécurité ou de valeur sensible en dur.

## Langues et conventions

- Rédiger en français la documentation destinée aux utilisateurs, exploitants et administrateurs.
- L'anglais est accepté pour les noms techniques, routes, variables, types, classes et conventions de code.
- Utiliser des noms non ambigus et une terminologie cohérente avec les documents du dossier `docs/`.

## Avant toute livraison

- Vérifier qu'aucun secret ou identifiant réel n'a été ajouté.
- Vérifier qu'aucun accès direct depuis `WEBPORTAL` vers l'infrastructure interne n'a été introduit.
- Vérifier que les opérations sensibles sont authentifiées, autorisées et auditables.
- Mettre à jour la documentation lorsque le comportement, les flux ou les responsabilités changent.
