# Securite

## Modele de menace

Actifs principaux :

- identites et sessions portail ;
- profils clients et references de contrat ;
- demandes support et service ;
- documents commerciaux informatifs ;
- journaux d'audit ;
- secrets applicatifs ;
- MariaDB et Active Directory.

Menaces principales :

- vol d'identifiants ou de cookie de session ;
- acces croise aux donnees d'un autre client ;
- exposition de secret dans Git, les logs ou les erreurs ;
- appel direct de l'API privee ;
- payloads invalides ou injectes ;
- confusion entre staging et production ;
- mouvement lateral de `WEBPORTAL` vers SQL ou AD.

La barriere principale reste la separation stricte :

```text
navigateur -> WEBPORTAL / BFF -> API-INTERNAL -> MariaDB
```

`WEBPORTAL` ne contacte jamais MariaDB ni Active Directory directement.

## Frontieres techniques

- Le navigateur appelle uniquement `WEBPORTAL` et ses routes `/api/*`.
- `INTERNAL_API_URL` et `SERVICE_AUTH_TOKEN` sont strictement server-only.
- `API-INTERNAL` n'est jamais exposee a Internet.
- MariaDB est accessible uniquement depuis `API-INTERNAL`.
- Active Directory reste desactivee par defaut et n'est joignable que depuis
  `API-INTERNAL` lorsqu'un mode de test controle est explicitement prepare.
- Tout flux non documente est refuse par defaut.

## Durcissement V0.18

La V0.18 conserve l'architecture et ajoute des garde-fous supplementaires :

- la fiche client admin consolide identite, statut, services, demandes,
  documents commerciaux, factures, activite recente et audits via
  `API-INTERNAL` uniquement ;
- les routes admin de detail refusent les identifiants invalides avant l'appel
  interne ;
- `API-INTERNAL` applique `X-Service-Auth` sur `/internal/*` dans tout
  environnement non `Development` ;
- `RUN_MARIADB_TESTS=true` est refuse hors `Development` ;
- les mutations BFF admin sensibles exigent un jeton CSRF sans stockage en
  `localStorage` ou `sessionStorage` ;
- `controlled_write` reste strictement borne a l'OU de test
  `OU=TEST_SITE_WEB,DC=home,DC=bzh` ;
- la readiness WEBPORTAL valide aussi la configuration du cookie de session ;
- `Permissions-Policy`, `Cross-Origin-Opener-Policy` et
  `Cross-Origin-Resource-Policy` completent les headers existants.

## Secrets

- Les secrets proviennent uniquement de l'environnement ou d'un gestionnaire
  dedie.
- Aucun secret ne doit etre committe, affiche dans une erreur, copie dans une
  capture ou journalise.
- La connexion MariaDB est assemblee en memoire a partir des variables
  `SQL_*`. Aucune chaine complete ne doit etre stockee ni affichee.
- Les anciens secrets de test deja exposes doivent etre consideres compromis et
  ne jamais etre repetes.
- `npm run check:secrets` reste un garde-fou local, pas un remplacement de
  scanner cote forge.

API-INTERNAL refuse tout environnement non `Development` si :

- `SQL_PROVIDER` n'est pas `mariadb` ;
- `SQL_PASSWORD` ou `SERVICE_AUTH_TOKEN` est absent ou manifestement factice ;
- `SESSION_COOKIE_SECURE=false` ;
- une variable `DEMO_*` reste definie ;
- `RUN_MARIADB_TESTS=true` ;
- `AD_INTEGRATION_MODE=enabled`.

WEBPORTAL refuse ses appels internes si `INTERNAL_API_URL` est absente,
invalide ou locale sans derogation explicite `ALLOW_LOCAL_INTERNAL_API_URL=true`.

## Authentification et sessions

- Authentification locale controlee uniquement a ce stade.
- Mot de passe hashe par ASP.NET Core `PasswordHasher` (PBKDF2 + sel).
- Message public identique pour utilisateur inconnu, mot de passe incorrect ou
  compte desactive.
- Token de session aleatoire genere par `API-INTERNAL`.
- Token brut renvoye uniquement au BFF puis conserve dans un cookie `HttpOnly`.
- SHA-256 du token stocke dans `portal_sessions`, jamais le token brut.
- Aucun token de session en `localStorage` ou `sessionStorage`.
- Aucun token, cookie, mot de passe, chaine de connexion ou secret dans les
  logs, audits ou vues admin.

Politique cookie V0.18 :

- `HttpOnly` obligatoire ;
- `Secure` obligatoire hors developpement local ;
- `SameSite=Lax` par defaut ;
- `SESSION_COOKIE_SAME_SITE` peut etre force a `strict` si le parcours reste
  strictement same-site ;
- `SESSION_COOKIE_SAME_SITE=none` est refuse sans `Secure=true`.

Le flux d'autorite reste :

```text
cookie HttpOnly -> BFF -> session API-INTERNAL -> user_id -> customer_id
```

## Isolation client

- Le `customer_id` vient uniquement de la session validee par `API-INTERNAL`.
- Les services, factures, demandes, notifications et documents commerciaux sont
  filtres par ce `customer_id`.
- Un identifiant navigateur etranger est traite comme introuvable ou invalide.
- Les validations BFF/API refusent les identifiants mal formes avant
  interpretation metier.
- Les tests MariaDB opt-in couvrent deja des cas d'isolation support,
  notification et document commercial ; la V0.18 etend la surface admin avec la
  fiche client et les flux AD controles.

## Autorisation

- `client_user` accede uniquement a ses vues metier.
- `internal_admin` accede aux vues globales et aux mutations deja bornees du
  workflow.
- Le controle de role est execute cote BFF puis repete dans API-INTERNAL.
- Les mutations admin restent bornees et auditees.
- Les actions AD reelles ne sont autorisees qu'en `controlled_write` dans l'OU
  de test validee.
- Aucune suppression client destructive, aucun hard delete AD, aucun
  provisioning complet, aucun paiement, e-mail, SMS, push ou WebSocket n'est
  introduit.

## Logs, audits et erreurs

Les audits conservent uniquement :

- `correlation_id` ;
- action et resultat ;
- code de raison ;
- reference cible non sensible ;
- date UTC et source utile ;
- reference client lorsque c'est pertinent.

Les audits ne doivent jamais contenir :

- mot de passe ;
- token ;
- cookie ;
- chaine de connexion ;
- payload sensible complet ;
- contenu integral d'un document commercial.

Les erreurs publiques restent neutres :

- `code` ;
- `message` ;
- `correlation_id`.

Les traces SQL, AD et details d'exception restent internes.

## Headers WEBPORTAL

WEBPORTAL applique :

- `X-Content-Type-Options: nosniff` ;
- `X-Frame-Options: DENY` ;
- `Content-Security-Policy` limitee a `frame-ancestors`, `base-uri` et
  `form-action` ;
- `Referrer-Policy: strict-origin-when-cross-origin` ;
- `Permissions-Policy: camera=(), geolocation=(), microphone=()` ;
- `Cross-Origin-Opener-Policy: same-origin` ;
- `Cross-Origin-Resource-Policy: same-site` ;
- `X-Robots-Tag: noindex, nofollow`.

`robots.txt` bloque aussi l'indexation du portail prive.

## MariaDB

- Compte applicatif dedie avec privileges minimaux.
- Migrations appliquees manuellement, jamais automatiquement au demarrage.
- Seed uniquement fictif et uniquement sur commande explicite en
  `Development`.
- Sauvegardes et restaurations doivent etre testees avant toute recette
  preproduction.
- Le mode local connu `--skip-ssl` reste accepte dans cet environnement si le
  serveur MariaDB ne supporte pas TLS, sans modifier l'architecture.

## Active Directory

- `AD_INTEGRATION_MODE=disabled` reste le mode le plus restrictif.
- `mock` reste reserve aux tests et ne doit realiser aucune mutation reelle.
- `read_only` autorise uniquement les lectures AD.
- `controlled_write` n'autorise que des actions bornees a
  `OU=TEST_SITE_WEB,DC=home,DC=bzh`.
- Aucun hard delete AD, reset de mot de passe ou authentification portail
  contre AD n'est expose.
- Toute OU de production reste hors perimetre et explicitement refusee.

## Recette securite V0.18

Verifier au minimum :

1. absence de secret cote client ;
2. cookie `HttpOnly`, `Secure` et `SameSite` conformes ;
3. refus des roles croises (`client_user` vers `/admin`) ;
4. absence de log sensible sur un login, une erreur et une lecture admin ;
5. refus des identifiants invalides sur les routes admin de detail ;
6. maintien de l'isolation entre deux clients fictifs.
