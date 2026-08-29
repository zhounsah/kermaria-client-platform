# Securite

> Navigation 1.0.0 : lire
> [`V1.0.0_DOCUMENTATION.md`](V1.0.0_DOCUMENTATION.md) pour la carte complete
> de la documentation. Ce document decrit les frontieres, garde-fous et
> secrets de la plateforme.

## Modele de menace

Actifs principaux :

- identites et sessions portail ;
- profils clients et references de contrat ;
- demandes support et service ;
- documents commerciaux informatifs et factures fiscales BPCE ;
- journaux d'audit ;
- secrets applicatifs (incluant `BPCE_REFRESH_TOKEN` et `PAYPAL_CLIENT_SECRET`) ;
- MariaDB, Active Directory, API BPCE et API PayPal.

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

## Durcissement V0.19

La V0.19 prolonge la V0.18, conserve l'architecture et cumule les garde-fous
suivants :

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
- `BPCE_REFRESH_TOKEN` est un JWT long-lived bancaire : jamais commit,
  jamais log, jamais retourne dans une reponse API. Il est echange contre
  un access token court mis en cache memoire avec verrou et invalide sur
  401. Voir `docs/V0.20_BPCE_INVOICING.md`.
- `PAYPAL_CLIENT_SECRET` est traite avec le meme niveau de protection. Le
  module `apps/webportal/lib/paypal.ts` ne le journalise jamais et ne le
  renvoie jamais cote navigateur.
- Les anciens secrets de test deja exposes doivent etre consideres compromis et
  ne jamais etre repetes. Une rotation effective de tous les secrets cites
  est prevue en V1.0 beta 1 sur la cible R740xd.
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

Politique cookie V0.19 :

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
  notification et document commercial ; les V0.18 et V0.19 etendent la
  surface admin avec la fiche client, les flux AD controles et leurs
  mutations protegees par CSRF.

## Autorisation

- `client_user` accede uniquement a ses vues metier.
- `internal_admin` accede aux vues globales et aux mutations deja bornees du
  workflow.
- Le controle de role est execute cote BFF puis repete dans API-INTERNAL.
- Les mutations admin restent bornees et auditees.
- Les actions AD reelles ne sont autorisees qu'en `controlled_write` dans l'OU
  de test validee.
- Aucune suppression client destructive, aucun hard delete AD, aucun
  provisioning complet, aucun e-mail automatique, SMS, push ou WebSocket
  n'est introduit.
- L'emission de facture BPCE (`POST .../issue`) et la confirmation de
  paiement PayPal sont reservees respectivement aux roles `internal_admin`
  et au flux de retour PayPal authentifie par session client. Le
  basculement `BPCE_INTEGRATION_MODE=live` ou `PAYPAL_MODE=live` requiert
  une validation explicite (V1.0 beta 1).

### Centre de configuration

- Le registre de parametres et les registres de gabarits sont **fermes cote
  code** : ni l'API ni l'interface ne peuvent creer une cle. Une cle presente
  en base mais inconnue du code est ignoree sans erreur.
- Les valeurs classees `secret` ne sont jamais renvoyees ; l'API n'expose
  qu'un etat « Configure » / « Non configure ».
- Les gabarits de communication n'acceptent que du texte brut et des
  placeholders `{{variable}}` d'une whitelist fermee par modele. Aucun moteur
  d'expression, aucun acces environnement, aucune reflection, aucun include :
  une variable inconnue fait echouer la sauvegarde.
- Toute mutation de gabarit exige la permission `settings.templates.write`,
  distincte de `settings.write` : un texte envoye a des clients n'a pas le
  meme niveau de risque qu'un reglage interne.
- L'envoi de test d'un e-mail ne peut viser que l'adresse de
  l'administrateur connecte ; la route refuse toute autre destination, et
  l'allowlist SMTP de `EMAIL_LIVE_ALLOWLIST` continue de s'appliquer.
- La configuration du diagnostic est une **DSL declarative fermee** : les
  operateurs sont definis dans le code, la charge est reserialisee a partir du
  modele, et aucun script ni expression arbitraire ne peut etre stocke. Elle
  est validee a l'enregistrement **et** de nouveau avant publication.
- Le parcours public ne lit que l'etat `published` du diagnostic ; le
  brouillon reste invisible des visiteurs. La publication est atomique.
- Toute mutation du diagnostic exige la permission
  `settings.diagnostic.write` : un parcours mal configure orienterait de vrais
  clients vers une mauvaise formule.
- Le kill switch d'inscription est verifie **cote API-INTERNAL** a chaque
  soumission : masquer le parcours dans le portail ne suffit pas a fermer
  l'inscription.
- Les limites de debit d'inscription sont comptees en base par API-INTERNAL.
  Le limiteur en memoire du BFF reste utile en premiere barriere, mais il est
  par processus et disparait au redeploiement.
- Le depassement de la limite par e-mail, comme un compte deja existant,
  renvoie une reponse indiscernable d'un succes : l'API ne revele jamais qu'une
  adresse est connue. Seule la limite par adresse IP donne un refus explicite.
- L'approbation automatique des inscriptions est verrouillee dans le code : la
  cle est visible, non editable, et une ligne posee directement en base ne la
  reactive pas.
- Une valeur de configuration relue depuis MariaDB repasse par la validation
  d'ecriture : les bornes du registre ne peuvent pas etre contournees en
  ecrivant directement en base.
- La console d'integrations ne transporte aucun secret : un mot de passe SMTP,
  une cle Stripe, un secret PayPal, un jeton BPCE ou KoXo n'y apparaissent que
  par leur presence. Un test de non-regression serialise la reponse et echoue si
  une valeur secrete configuree y figure.
- Aucun mode d'integration n'est modifiable depuis le Centre de configuration :
  ils commandent des appels reels chez des tiers et se changent sur la machine.
- L'envoi de test SMTP est borne par l'allowlist d'envoi, pas par le mode : une
  page d'administration ne peut donc pas ecrire a un vrai client.
- Stripe, PayPal, BPCE et KoXo n'exposent pas de test de connectivite : une
  verification authentifiee serait un appel sortant reel, un quota consomme, ou
  une operation trop large. L'absence est affichee avec sa raison.
- Les modeles de demonstration ne peuvent pas introduire un type de service
  inconnu du code : le registre ferme `ServiceTypeRegistry` refuse la valeur,
  ce qui evite de contourner les validations de provisionnement et d'affichage.
- Un modele reference par un profil de demonstration n'est pas supprimable :
  sa disparition creerait des comptes sans aucun service, sans erreur visible.
- La destination AD de conversion reste en lecture seule dans le Centre de
  configuration et son appartenance a `AD_ALLOWED_ROOTS` est affichee : elle
  deplace de vraies identites, et le deplacement est de toute facon revalide au
  moment de la conversion.
- Les mutations des modeles de demonstration exigent `settings.demo.write`,
  distincte de `settings.write`.
- La fiscalite n'est **pas** scriptable : le regime et le calcul restent dans
  le code, seule la formulation de la mention est administrable, et uniquement
  pour un regime deja connu.
- Une mention fiscale ne peut pas etre antidatee, et la resolution se fait a la
  date de la ligne de document : un document deja etabli ne peut donc pas etre
  modifie retroactivement depuis l'administration.
- Une mention deja en vigueur n'est pas supprimable ; elle documente ce qui a
  ete imprime sur de vrais documents.
- Les mutations fiscales exigent `settings.billing.write`, distincte de
  `settings.write`.
- Les drapeaux Billing V2 restent en lecture seule dans le Centre de
  configuration : ils commandent des appels sortants reels chez un prestataire
  de paiement et des ecritures d'infrastructure. Leur modification passe par la
  machine puis un redemarrage, jamais par une page web.
- Les replis sont fail-closed au sens metier : en cas de panne SQL ou de
  ligne absente, le runtime utilise le gabarit integre au code, jamais un
  texte vide, et jamais une configuration plus permissive que l'absence de
  configuration.

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
- `Cross-Origin-Resource-Policy: same-site`.

### Source de verite unique : l'application

**L'application est seule source de verite pour les en-tetes de securite.**
Le reverse proxy nginx (SRV-11) ne doit emettre **aucun** `add_header` de
securite sur les vhosts kermaria : il relaie ceux de l'amont sans y toucher.

Motifs :

- le tableau `SECURITY_HEADERS` de `apps/webportal/next.config.ts` est deja
  exhaustif (7 en-tetes, contre 4 cote nginx) et versionne avec le code ;
- il est couvert par `npm run test:operations` ;
- `nginx` ne remplace jamais un en-tete amont : `add_header` **ajoute** une
  seconde ligne. Toute valeur definie des deux cotes produit un doublon.

Regle d'ecriture nginx : si une valeur doit un jour etre imposee par le proxy,
utiliser `proxy_hide_header <nom>;` **puis** `add_header <nom> <valeur> always;`
dans le meme bloc — jamais `add_header` seul. Attention aussi a l'heritage :
un `add_header` dans un bloc `location` annule tous ceux herites de `server`
et `http`.

⚠️ Ne jamais ajouter `add_header X-Robots-Tag ...` cote nginx : la vitrine
publique doit rester indexable et l'application gere seule cet en-tete par
prefixe (voir plus bas).

### Anomalie du 2026-08-04 — en-tetes dupliques par SRV-11

Tracee en `v1.1.10.2` ([`ROADMAP.md`](ROADMAP.md)).

Constatee sur `https://www.zacharyhounsa.ovh/` : quatre en-tetes emis en double,
dont deux valeurs **contradictoires**.

| En-tete | Application | nginx SRV-11 |
|---|---|---|
| `X-Frame-Options` | `DENY` | `SAMEORIGIN` — **contradictoire** |
| `X-Content-Type-Options` | `nosniff` | `nosniff` — identique |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | identique |
| `Permissions-Policy` | `camera=(), geolocation=(), microphone=()` | memes directives, ordre different |

Impact reel evalue : **aucune perte de protection anti-clickjacking.**

1. `Content-Security-Policy: frame-ancestors 'none'` est emis par
   l'application et prime sur `X-Frame-Options` dans tous les navigateurs
   modernes ;
2. verification empirique (Chromium, 2026-08-04, reproduction locale d'une
   reponse portant `DENY` **et** `SAMEORIGIN` sans CSP) : le navigateur
   **bloque** le cadrage. Un `X-Frame-Options` multivalue et incoherent est
   traite en echec ferme, pas ignore. Le temoin sans `X-Frame-Options`
   chargeait bien dans le meme cadre.

Le risque residuel est donc limite aux navigateurs anciens sans support de
`frame-ancestors`, hors perimetre supporte. La correction reste requise au
titre de la coherence de configuration : deux sources de verite divergentes
sont un piege de maintenance, et le prochain ecart pourrait, lui, etre
ouvrant.

Correction retenue : **retirer les `add_header` de securite des vhosts
kermaria sur SRV-11**, conformement a la regle ci-dessus. Procedure et
verification : `docs/OPERATIONS.md`, section « En-tetes de securite ».

**Applique et verifie le 2026-08-05.** Les huit `add_header` de
`/etc/nginx/sites-available/kermaria` (quatre par bloc TLS) ont ete retires sur
SRV-11, sauvegarde `kermaria.bak-20260804T230719Z`, `nginx -t` puis rechargement.
Controle : `npm run assert:security:headers` passe, les sept en-tetes sont
servis une seule fois, `X-Frame-Options: DENY` sans contradiction.

Non-regression relevee dans la foulee : `portfolio.zacharyhounsa.ovh` et
`dashboard.zachary-it.fr` en `200`, `noindex` conserve sur `/login`, aucun
`X-Robots-Tag` sur `/offres`.

Cote depot, `scripts/r740xd-vm/srv11/kermaria-nginx.conf` ne porte plus aucun
`add_header` non plus. Ce fichier dormait sur la branche
`codex/r740xd-automation` — d'ou l'affirmation erronee, en `v1.1.10.2`, que la
configuration nginx n'etait pas versionnee : seul `main` avait ete regarde. Il
reste toutefois un **gabarit perime**, non deployable tel quel (voir l'entete du
fichier et `OPERATIONS.md`).

Reste ouvert : `kermaria-tls.pending`, inactif, porte encore les quatre
directives.

Garde-fou : `npm run assert:security:headers -- --url https://zachary-it.fr/`
compare la reponse **livree** au contrat de `next.config.ts` et echoue sur tout
doublon. Les tests `test:operations` et `test:seo` lisent le code source et ne
voient pas le proxy : seul ce controle en ligne couvre SRV-11.

`X-Robots-Tag: noindex, nofollow` n'est **pas** global : il est servi
uniquement sur les prefixes prives listes par `NOINDEX_ROUTE_PREFIXES`
(`next.config.ts`) — espaces authentifies, `/api` et pages
transactionnelles a jeton. Les pages de la vitrine publique n'en
portent aucun, sinon elles sortent de l'index des moteurs.

`robots.txt` bloque aussi l'indexation du portail prive, mais l'en-tete
HTTP prime sur `robots.txt` et sur les metadonnees `robots` des pages.
Garde-fou automatise : `npm run test:seo`.

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

## Recette securite V0.19

Verifier au minimum :

1. absence de secret cote client ;
2. cookie `HttpOnly`, `Secure` et `SameSite` conformes ;
3. refus des roles croises (`client_user` vers `/admin`) ;
4. absence de log sensible sur un login, une erreur et une lecture admin ;
5. refus des identifiants invalides sur les routes admin de detail ;
6. maintien de l'isolation entre deux clients fictifs.
