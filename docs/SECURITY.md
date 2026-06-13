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
- Rotation et révocation doivent être prévues avant production.
- Aucun compte `Domain Admin` n'est autorisé.

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
mutation dans la V0.8.

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
périmètre. Elle est explicitement refusée par la configuration V0.8.

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

Les connexions, déconnexions, créations de demandes, tentatives AD, refus et
erreurs contrôlées portent :

- `correlation_id` ;
- action et résultat ;
- code de raison ;
- référence cible non sensible ;
- date UTC et source utile.

Sont interdits dans les audits : mots de passe, jetons, chaînes de connexion,
documents, descriptions complètes et secrets.

Les erreurs publiques contiennent uniquement `code`, `message` et
`correlation_id`. Les traces et détails SQL/AD restent internes.

## Authentification et sessions

La V0.8 utilise une authentification locale contrôlée :

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
- `internal_admin` accède uniquement aux vues globales en lecture seule.
- Le contrôle du rôle est exécuté dans le BFF et répété dans API-INTERNAL.
- L'interface admin ne permet ni changement de rôle, ni création/suppression
  de compte, ni mutation métier.
- Les sessions admin n'exposent ni référence client, ni token, ni hash.
- Les adresses réseau sont masquées et les User-Agent tronqués dans les vues.
- Les accès admin autorisés et refusés sont audités.

La protection anti-brute-force V0.8 est volontairement simple et centrée sur
le compte : `LOGIN_MAX_FAILURES` définit le seuil et
`LOGIN_LOCKOUT_MINUTES` la durée du verrouillage. Elle ne remplace pas un
rate limiting réseau au reverse proxy.

## Headers WEBPORTAL

WEBPORTAL ajoute `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`,
une `Referrer-Policy` restrictive et une CSP limitée à `frame-ancestors`,
`base-uri` et `form-action`. Cette CSP évite de bloquer les scripts Next.js
tout en empêchant le cadrage et les formulaires vers une origine tierce.

Avant production restent requis : fournisseur compatible MFA, rate limiting,
protection CSRF complémentaire selon les flux, identité service-à-service entre
VM, rotation opérationnelle et revue de sécurité.

## Données de démonstration

Les seeds et mocks sont fictifs, utilisent des références `DEMO` ou `MOCK` et
des domaines `.invalid`. Ils ne doivent jamais être remplacés par de vraies
données client dans le dépôt.
