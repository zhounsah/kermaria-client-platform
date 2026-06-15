# Sécurité

## Modèle de menace

Actifs : identités, profils clients, demandes, secrets applicatifs, journaux
d'audit, MariaDB et Active Directory.

Menaces principales :

- vol d'identifiants ou de session ;
- accès aux données d'un autre client ;
- injection et entrées non validées ;
- appel direct de l'API privée ;
- fuite de secret dans le code, les logs ou les sauvegardes ;
- abus d'un compte technique trop privilégié ;
- mouvements latéraux vers SQL ou AD.

La barrière principale est la séparation entre `WEBPORTAL`, public, et
`API-INTERNAL`, privée.

## Séparation

- Le navigateur appelle uniquement `WEBPORTAL` et ses routes BFF.
- `INTERNAL_API_URL` est strictement serveur et ne doit jamais être préfixée
  par `PUBLIC_` ou `NEXT_PUBLIC_`.
- `API-INTERNAL` n'est jamais exposée à Internet.
- MariaDB est accessible uniquement depuis `API-INTERNAL`.
- AD est accessible uniquement depuis `API-INTERNAL`.
- `WEBPORTAL` ne contient aucun pilote SQL ou annuaire.
- Tout flux non documenté est refusé par défaut.

## Secrets

- Secrets uniquement dans des variables d'environnement ou un gestionnaire
  dédié.
- Aucun secret dans Git, les exemples, les images, les logs ou les erreurs.
- La chaîne MariaDB est assemblée en mémoire à partir des variables `SQL_*`.
- La chaîne de connexion et les mots de passe ne sont jamais journalisés.
- Les erreurs de configuration nomment uniquement les variables concernées,
  jamais leur valeur.
- La rotation suit [SECRET_ROTATION.md](SECRET_ROTATION.md).
- Aucun compte `Domain Admin` n'est autorisé.

En Production, API-INTERNAL refuse :

- une configuration MariaDB incomplète ;
- `SQL_PASSWORD` ou `SERVICE_AUTH_TOKEN` absent ou manifestement factice ;
- `SESSION_COOKIE_SECURE=false` ;
- des variables de mot de passe `DEMO_*` ;
- `AD_INTEGRATION_MODE=enabled`.

WEBPORTAL refuse ses appels internes si `INTERNAL_API_URL` est absente,
invalide ou locale sans dérogation explicite. Aucun nom de variable interne
n'utilise `NEXT_PUBLIC_*`.

## MariaDB

- Compte applicatif dédié et privilèges limités aux tables nécessaires.
- Paramètres SQL centralisés dans le dépôt de données et requêtes paramétrées.
- Migrations appliquées manuellement, jamais automatiquement en production.
- Seed uniquement fictif et uniquement sur commande en développement.
- `SQL_CONFIG_MISSING` bloque le démarrage hors développement.
- `SQL_UNAVAILABLE` masque les détails techniques au client.
- Sauvegardes et restaurations doivent être testées avant données réelles.

## Active Directory

`AD_INTEGRATION_MODE=disabled` est la valeur par défaut et la procédure de
rollback immédiate. Les modes `test` et `enabled` ne réalisent encore aucune
mutation dans la V0.9.

Garde-fous :

- compte de service dédié, jamais administrateur du domaine ;
- OU configurée côté serveur, jamais fournie librement par le navigateur ;
- cible obligatoirement sous `AD_CLIENTS_OU_DN` ;
- groupes limités à `AD_ALLOWED_GROUPS` ;
- groupes administratifs refusés ;
- aucune erreur LDAP détaillée dans les réponses ;
- audit limité aux métadonnées et au résultat ;
- aucun mot de passe lu en modes `disabled` ou `mock` ;
- aucun mot de passe persisté ou loggé dans aucun mode.

L'OU `OU=KoXoAdm,DC=home,DC=bzh` appartient à la production et est hors
périmètre. Elle est explicitement refusée par la configuration V0.9.

L'OU de test actuelle est `OU=TEST_SITE_WEB,DC=home,DC=bzh`. Avant tout essai
AD réel, une OU encore plus isolée est recommandée, par exemple
`OU=PortalTest,OU=TEST_SITE_WEB,DC=home,DC=bzh`. Elle ne doit contenir aucun
compte administratif ou sensible.

Rollback AD :

1. définir `AD_INTEGRATION_MODE=disabled` ;
2. redémarrer `API-INTERNAL` ;
3. révoquer ou désactiver le compte de service si nécessaire ;
4. vérifier les audits corrélés sans consulter de contenu sensible.

## Audit et erreurs

Les connexions, déconnexions, créations de demandes, tentatives AD, refus
interservice et erreurs contrôlées portent :

- `correlation_id` ;
- action et résultat ;
- code de raison ;
- référence cible non sensible ;
- date UTC et source utile.

Sont interdits dans les audits : mots de passe, jetons, chaînes de connexion,
documents, descriptions complètes et secrets.

Les erreurs publiques contiennent uniquement `code`, `message` et
`correlation_id`. Les traces et détails SQL/AD restent internes.

Les logs applicatifs ne doivent pas contenir les headers ou payloads complets.
Le script `npm run check:secrets` détecte quelques motifs évidents avant
validation. Ce garde-fou ne remplace pas un scanner de secrets côté forge.

## Authentification et sessions

La V0.9 conserve l'authentification locale contrôlée de V0.8 :

- mot de passe hashé par le `PasswordHasher` ASP.NET Core, fondé sur PBKDF2
  avec sel et paramètres versionnés ;
- message public identique pour utilisateur inconnu, mot de passe incorrect ou
  compte désactivé ;
- token de session aléatoire de 256 bits généré par API-INTERNAL ;
- token brut renvoyé uniquement au BFF et conservé dans un cookie `HttpOnly` ;
- cookie `SameSite=Lax`, `Secure` en production, chemin `/` ;
- SHA-256 du token stocké dans `portal_sessions`, jamais le token brut ;
- expiration et révocation vérifiées à chaque appel ;
- rôle `client_user` ou `internal_admin` porté par la session validée ;
- verrouillage temporaire après un seuil configurable d'échecs consécutifs ;
- compteur de connexion remis à zéro après une authentification réussie ;
- révocation optionnelle des autres sessions du même utilisateur ;
- `last_seen_at` mis à jour périodiquement, pas à chaque requête ;
- aucun token dans `localStorage`, `sessionStorage`, les logs ou les réponses
  publiques du BFF.

Le BFF transmet le token à API-INTERNAL avec `X-Portal-Session` sur le réseau
privé. Ce header n'est jamais construit par le JavaScript navigateur.

En Production, le BFF ajoute aussi `X-Service-Auth` depuis
`SERVICE_AUTH_TOKEN`. API-INTERNAL compare cette identité avant toute route
`/internal/*`. Les endpoints health restent disponibles au réseau privé pour
la supervision. Cette protection complète le pare-feu et HTTPS ; elle ne les
remplace pas.

L'isolation suit exclusivement :

```text
cookie HttpOnly -> BFF -> session API-INTERNAL -> user_id -> customer_id
```

API-INTERNAL filtre les services, factures, demandes et profils par ce
`customer_id`. Aucun `customerId` fourni dans un payload navigateur n'est
accepté comme autorité.

## Autorisation interne

- `client_user` accède uniquement aux routes métier du client issu de sa
  session.
- `internal_admin` accède aux vues globales et aux seules mutations de workflow
  V0.11 : statut, note interne et message public d'une demande.
- Le contrôle du rôle est exécuté dans le BFF et répété dans API-INTERNAL.
- L'interface admin ne permet ni changement de rôle, ni création/suppression
  de compte, ni mutation de client, service, facture ou intégration.
- Les sessions admin n'exposent ni référence client, ni token, ni hash.
- Les adresses réseau sont masquées et les User-Agent tronqués dans les vues.
- Les accès admin autorisés et refusés sont audités.

## Notes et messages des demandes

Les notes internes et les messages publics utilisent des tables, contrats et
formulaires séparés. Cette séparation est une frontière de confidentialité :

- une note interne n'est jamais incluse dans une réponse client ;
- un message public n'affiche pas l'identité technique de son auteur ;
- les contenus sont limités à 2 000 caractères et rendus comme texte brut ;
- aucun HTML ou Markdown interprété n'est accepté ;
- aucune édition ou suppression silencieuse n'est prévue ;
- l'audit conserve l'action et l'identifiant de demande, pas le contenu ;
- l'interface rappelle de ne jamais saisir mot de passe, token ou secret.

Un changement de statut ne déclenche aucune action externe. Il ne provisionne
pas de service, ne contacte pas AD, n'envoie pas d'e-mail et ne lance ni
facturation ni paiement.

## Notifications portail

Les notifications V0.12 restent dans MariaDB et ne quittent jamais le flux
`Navigateur -> BFF -> API-INTERNAL -> MariaDB`.

- le `customer_id` vient exclusivement de la session validée ;
- lecture et marquage appliquent toujours ce filtre côté API-INTERNAL ;
- une notification étrangère est traitée comme introuvable ;
- les notes internes ne déclenchent aucune notification ;
- le contenu complet des messages publics n'est pas dupliqué ;
- les liens sont des chemins internes support/service et sont à nouveau
  filtrés avant rendu par WEBPORTAL ;
- aucun e-mail, SMS, push, WebSocket ou worker n'est utilisé.

Le navigateur ne reçoit jamais `INTERNAL_API_URL`, `SERVICE_AUTH_TOKEN`, le
token de session ou une information d'administration dans ces contrats.

## Conversation publique V0.13

- une réponse client passe uniquement par le BFF puis API-INTERNAL ;
- le client propriétaire est résolu depuis la session, jamais depuis le JSON ;
- l'écriture vérifie la correspondance avec le `customer_id` de la demande ;
- une demande étrangère est traitée comme introuvable ;
- les messages sont limités à 2 000 caractères et rendus comme texte brut ;
- aucune balise HTML ou syntaxe Markdown n'est interprétée ;
- les notes internes utilisent une table et un DTO distincts ;
- l'audit conserve l'action, l'acteur et la référence, jamais le message ;
- une réponse client ne génère ni e-mail, ni notification externe, ni
  automatisation métier.

## Centre d'activité admin V0.14

- les routes d'activité exigent `internal_admin` côté BFF et API-INTERNAL ;
- les réponses contiennent uniquement des métadonnées de suivi ;
- le contenu des messages publics et des notes internes est exclu ;
- les filtres sont validés par allowlist dans le BFF puis dans API-INTERNAL ;
- le dernier auteur est résolu depuis `portal_users`, jamais depuis le
  navigateur ;
- aucune lecture n'entraîne de changement de statut ou d'action externe ;
- aucune URL interne, identité interservice ou session n'est exposée au
  JavaScript navigateur.

La protection anti-brute-force V0.9 est volontairement simple et centrée sur
le compte : `LOGIN_MAX_FAILURES` définit le seuil et
`LOGIN_LOCKOUT_MINUTES` la durée du verrouillage. Elle ne remplace pas un
rate limiting réseau au reverse proxy.

## Headers WEBPORTAL

WEBPORTAL ajoute `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`,
une `Referrer-Policy` restrictive et une CSP limitée à `frame-ancestors`,
`base-uri` et `form-action`. Cette CSP évite de bloquer les scripts Next.js
tout en empêchant le cadrage et les formulaires vers une origine tierce.

Le portail privé ajoute `X-Robots-Tag: noindex, nofollow` et un `robots.txt`
interdisant toute exploration. Ces directives limitent l'indexation mais ne
constituent pas un contrôle d'accès.

Avant production restent requis : fournisseur compatible MFA, rate limiting,
protection CSRF complémentaire selon les flux, gestionnaire de secrets et
revue de sécurité.

## Health checks

- `/health/live` et `/api/health/live` n'accèdent à aucun secret ni système
  interne.
- `/health/ready` vérifie la configuration et MariaDB par `SELECT 1`.
- `/api/health/ready` appelle la readiness API côté serveur.
- Les réponses n'affichent ni URL, ni host SQL, ni token, ni stacktrace.
- HTTP 503 doit retirer l'instance du trafic sans la redémarrer en boucle si
  la dépendance est simplement indisponible.

## Sauvegardes

Les dumps peuvent contenir des données personnelles et des hashes. Ils doivent
être chiffrés, stockés hors Git, soumis à rétention et testés par restauration.
Voir [BACKUP_RESTORE.md](BACKUP_RESTORE.md).

## Données de démonstration

Les seeds et mocks sont fictifs, utilisent des références `DEMO` ou `MOCK` et
des domaines `.invalid`. Ils ne doivent jamais être remplacés par de vraies
données client dans le dépôt.
