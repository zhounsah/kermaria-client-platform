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

Les jalons fonctionnels (V0.20 a V0.27) avancent en respectant ces bornes.
Les jalons d'exploitation finale (V1.0 beta 1, V1.0 RC) sont **bloques
par la livraison du R740xd**.

## Jalon V0.20 facturation reelle BPCE controlee

Statut : **implemente dans le depot, en phase de tests (mode live non active
par defaut)**. Documentation dediee : [`V0.20_BPCE_INVOICING.md`](V0.20_BPCE_INVOICING.md).

Integration avec l'API de gestion de factures de la Banque Populaire
(`https://www.gestion-factures.banquepopulaire.fr/inv/api/v5`) :

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
- portail client : affichage du statut `issued` sans texte "informatif".

La V0.20 ne declenche aucun envoi e-mail, aucune action AD, aucun paiement
en ligne. Le mode `live` est desactive par defaut, conforme au principe
phase-de-tests (R740xd non encore livre).

### V0.20.1 import du catalogue de services

Statut : **implemente dans le depot**.

- migration `009_catalog_articles` : ajoute `tax_rate_basis_points` et
  `external_reference` (unique) sur `commercial_offers` ;
- import de 17 articles reels (audit, VPN, RDS, support N1/N2, etc.) a 20 %
  TVA, prix en centimes, `external_reference` reutilisable en cle d'import ;
- formulaire de ligne admin : selection d'une offre auto-remplit libelle,
  unite, prix et taux indicatif. Aucun calcul de TVA legal a ce stade.

## Jalon V0.21 canaux de paiement client

Statut : **implemente dans le depot, en phase de tests (e-mail `live` et
PayPal `live` desactives par defaut)**. Documentation dediee :
[`V0.21_PAYMENT_CHANNELS.md`](V0.21_PAYMENT_CHANNELS.md).

Acquis livres :

- section `Reglement` affichee sur le portail client uniquement pour les
  factures `issued` ou `paid` ;
- virement bancaire : IBAN, BIC, libelle beneficiaire et reference a indiquer
  presentes via les variables `BILLING_*` ;
- paiement carte / PayPal en ligne (`PAYPAL_MODE=sandbox|live`,
  `PAYPAL_CLIENT_ID`, `PAYPAL_CLIENT_SECRET`) :
  - OAuth2 client credentials avec cache de token jusqu'a expiration ;
  - creation d'ordre PayPal `intent: CAPTURE` (one-shot, jamais recurrent) ;
  - capture lors du retour utilisateur, propagation a BPCE via
    `POST /invoices/{id}/mark_as_paid/` et a `commercial_documents.status` ;
  - statut local `paid` ajoute aux types partages et au formatter ;
  - bouton PayPal masque apres paiement, message "facture reglee" affiche ;
- **telechargement PDF cote portail client** : endpoint dedie
  `GET /internal/portal/commercial-documents/{id}/invoice/pdf` cote
  `API-INTERNAL` avec controle d'ownership par session client
  (`GetClientDocumentAsync` -> 404 `PORTAL_DATA_NOT_FOUND` si non
  proprietaire). PDF servi depuis le cache local, bouton "Telecharger la
  facture (PDF)" dans le bloc Reglement ;
- **vue admin de suivi des paiements** : page `/admin/payments` avec
  totaux a regler / regle, filtre statut (Toutes / A regler / Reglees),
  table des factures emises. Marquage manuel d'un encaissement hors
  PayPal via `POST .../mark-as-paid` qui reutilise `ConfirmPaymentAsync`
  (meme flow BPCE `mark_as_paid` + statut local que le rail PayPal) ;
  bouton "Marquer paye (hors PayPal)" sur la fiche document admin ;
- **canal e-mail transactionnel sortant**, **premier vrai canal externe**,
  via SMTP configurable mais non cable sur un SMTP de production :
  - `EMAIL_INTEGRATION_MODE=disabled` (defaut) | `mock` | `live`,
    `System.Net.Mail.SmtpClient` avec STARTTLS ;
  - destinataire = `customers.billing_email` uniquement, statut
    `no_recipient` trace sans envoi si vide ;
  - 3 templates texte (fr) : `invoice_issued`, `payment_reminder`,
    `payment_confirmed` ;
  - declenchements automatiques : `IssueInvoiceAsync(sendEmail=true)` ->
    `invoice_issued` ; `ConfirmPaymentAsync` (PayPal ou manuel) ->
    `payment_confirmed` (best-effort, n'echoue pas le flow BPCE) ;
  - declenchement manuel : `POST .../send-reminder` -> `payment_reminder` ;
  - journal d'envoi isole : table `email_messages` (migration
    `010_email_log`), page `/admin/email-log` (200 derniers envois).

La V0.21 n'introduit aucun prelevement SEPA recurrent, aucun rapprochement
bancaire automatique, aucun SMS, aucun push, aucun WebSocket, aucun envoi
HTML enrichi (texte uniquement). Les abonnements recurrents font l'objet
du jalon V0.22 separe. Le SMTP reel et PayPal `live` restent branchables
uniquement en preprod cible.

## Jalon V0.22 abonnements recurrents PayPal

Statut : **implemente dans le depot** (V0.22 le 2026-06-25, V0.22.1 le
2026-06-26). Documentation d'implementation :
[`V0.22_SUBSCRIPTIONS.md`](V0.22_SUBSCRIPTIONS.md).

V0.22.1 ajoute la creation automatique des Plans PayPal depuis le
formulaire admin (bouton "Créer le plan PayPal") et stocke les IDs en
deux colonnes distinctes `paypal_plan_id_sandbox` et `paypal_plan_id_live`
(migration 016) — un plan PayPal sandbox n'existe pas en live, donc
chaque mode a son propre identifiant. Le prix d'une offre devient
immutable des qu'un plan PayPal existe pour eviter de desynchroniser les
souscriptions actives.

Objectif : permettre la facturation automatique mensuelle des services
recurrents (acces VPN, RDS, support continu) sans saisie manuelle
d'encaissement. S'appuie sur l'API PayPal Subscriptions, distincte de
l'API Orders utilisee en V0.21.

Perimetre vise :

- distinction `one_time` vs `recurring` au catalogue (`commercial_offers`),
  via une colonne additionnelle `billing_cadence` ;
- creation manuelle des Plans PayPal cote dashboard developpeur pour chaque
  service recurrent, avec stockage de l'identifiant retourne dans le
  catalogue local ;
- flux de souscription cote portail : redirection vers l'approbation du
  mandat PayPal, callback de confirmation, persistance de la souscription
  dans une table dediee `subscriptions` (id PayPal, plan, statut, dates) ;
- webhooks PayPal (`BILLING.SUBSCRIPTION.ACTIVATED`,
  `PAYMENT.SALE.COMPLETED`, `BILLING.SUBSCRIPTION.CANCELLED`...) :
  authentification du webhook, idempotence sur l'event id, generation
  automatique de facture BPCE et marquage `paid` ;
- interface admin de suivi : liste des abonnements actifs, lien vers la
  facture mensuelle generee, suspension et annulation cote PayPal.

Garde-fous :

- aucun cycle de prelevement declenche tant que `PAYPAL_MODE=sandbox` ;
- aucune souscription active en environnement de tests sur un buyer reel ;
- l'admin reste capable de marquer une periode comme `unpaid` si la banque
  refuse le prelevement (statut PayPal `SUSPENDED` ou `EXPIRED`).

La V0.22 n'ajoute ni rapprochement bancaire SEPA hors PayPal, ni SMS, ni
push, ni provisioning AD automatique declenche par un encaissement.

## Jalon V0.23 harmonisation UX portail et admin

Statut : **implemente dans le depot** (2026-06-26). Aucune dependance
materielle. Refonte purement frontend, contrats API inchanges.

- navigation laterale gauche unifiee (sidebar) pour le portail client et
  l'administration interne, exposant **toutes** les pages disponibles
  (les pages auparavant non listees dans le bandeau superieur sont
  desormais accessibles depuis la sidebar) ;
- dashboard admin nettoye : seules les metriques et l'etat AD restent
  inline, le flux d'activite publique est extrait dans `/admin/activity`
  (le journal d'audit reste a `/admin/audit-logs`) ;
- catalogue admin refondu : `/admin/catalog` devient une liste tabulaire,
  `/admin/catalog/[id]` ouvre la fiche complete d'edition, creation via
  `/admin/catalog/new` (apres creation, redirection vers la fiche) ;
- pages admin Paiements et Abonnements harmonisees : meme bandeau
  `metrics-grid-three` + meme bloc filtres `content-panel > admin-filters` ;
- page client `/invoices` : table elargie, colonne action droite reste
  visible sans defilement horizontal ;
- page client `/support` : formulaire de nouvelle demande en zone
  centrale en haut, demandes existantes empilees en liste dessous.

La V0.23 n'ajoute aucune fonctionnalite metier ni mutation cote API ; elle
ne deplace pas la frontiere hardware (V1.0 beta 1 reste bloquee par la
livraison R740xd).

## Jalon V0.24 stabilisation testable sur SRV-01 et SRV-02

Statut : **a faire, faisable sans la cible R740xd**. Ex-V0.24a renomme
au 2026-06-28 (la phase de validation hardware ex-V0.24b devient
V1.0 beta 1).

- recette complete executee sur le staging interne (couvre V0.16, V0.17,
  V0.20 BPCE mock et V0.21 PayPal sandbox + e-mail mock) ;
- restauration MariaDB testee sur instance distincte ;
- audit securite interne : dependances, secrets (couvre
  `BPCE_REFRESH_TOKEN`, `SMTP_PASSWORD`, `PAYPAL_CLIENT_SECRET`,
  `SERVICE_AUTH_TOKEN`), headers, rate limiting ;
- revue accessibilite WCAG AA des parcours client critiques ;
- documentation utilisateur admin et client ;
- procedure formelle de mise en production redigee, **non executee** ;
- plan de continuite minimal documente.

La V0.24 n'ajoute aucune fonctionnalite metier et ne s'execute pas sur
l'infrastructure definitive.

## Jalon V0.25 finalisation Active Directory

Statut : **en cours, faisable sans la cible R740xd**. Cadrage ajoute au
2026-06-28, voir [`V0.25_AD_FINALISATION.md`](V0.25_AD_FINALISATION.md).

Apporte les briques AD restantes preparees mais desactivees ou hors
sequence depuis V0.9 / V0.18, sans encore sortir de l'OU de test :

- **brique 3 livree 2026-06-30** : procedure documentee de sortie
  progressive de `OU=TEST_SITE_WEB`, prerequis a la mise en prod
  V1.0 RC, voir
  [`AD_PRODUCTION_MIGRATION.md`](AD_PRODUCTION_MIGRATION.md). Point cle
  identifie : la sortie d'OU exige une PR de code (levee du
  `RequiredTestOuRoot` hardcode dans
  `apps/api-internal/Data/Configuration/AdRuntimeConfiguration.cs`),
  pas seulement une reconfiguration env. Procedure executee en V1.0 RC,
  pas en V0.25 ;
- **brique 2 livree 2026-06-30** : provisioning AD etendu, toujours
  borne a `OU=TEST_SITE_WEB`. Sous-brique 2a : lecture des groupes
  effectifs (directs + transitifs via `LDAP_MATCHING_RULE_IN_CHAIN`),
  endpoint `GET /internal/admin/customers/{ref}/ad/users/{sam}/groups`,
  section UI "Groupes effectifs". Sous-brique 2b : renommage
  utilisateur (CN + sAMAccountName + displayName + UPN en un appel),
  endpoint `POST .../rename`, audit `admin.customers.ad_users.rename`,
  `customer_ad_links` mis a jour automatiquement. Sous-brique 2c :
  deplacement utilisateur Users <-> Disabled meme client et cross-client
  (rare), endpoint `POST .../move` avec `targetCustomerReference` +
  `targetContainer` ("Users"|"Disabled"), refus early si client cible
  inexistant, `customer_ad_links` migre vers le nouveau client si
  cross-client. UI : SectionCards Renommer / Deplacer avec
  `window.confirm()` cross-client ;
- **brique 1 a venir** : changement de mot de passe AD cote client,
  reactiver le flux prepare en V0.9 derriere
  `AD_PASSWORD_CHANGE_ENABLED` (defaut `false`), exige re-auth recente,
  audit ecrit, sans bypass, policy AD du domaine seule source de
  verite.

La V0.25 n'ouvre pas encore les ecritures hors OU de test ni n'active
le mode `live` AD : tout reste sur SRV-01/02.

## Jalon V0.26 creation de compte self-service

Statut : **a cadrer, faisable sans la cible R740xd**. Ajoute au
2026-06-28.

Permet a un visiteur d'initier la creation d'un compte client sans
intervention manuelle prealable :

- formulaire d'inscription public (entreprise, contact, e-mail) avec
  validation et anti-bot ;
- verification e-mail (token signe, expiration courte) ;
- creation du compte en statut `pending` cote portail, sans
  provisioning AD automatique ;
- workflow admin de validation/refus avec audit ;
- e-mail transactionnel `account_pending` et `account_approved` (mode
  `mock` jusqu'a V1.0 RC) ;
- isolation stricte : aucun acces aux services tant que l'admin n'a pas
  valide, aucune creation de compte AD avant V0.25.

La V0.26 prepare le canal de conversion sans bypasser la qualification
commerciale manuelle.

## Jalon V0.27 site vitrine public

Statut : **a cadrer, faisable sans la cible R740xd**. Ajoute au
2026-06-28.

Page d'accueil publique non authentifiee, en amont du backend client et
admin :

- landing page `/` avec presentation des offres (catalogue V0.15
  reutilise en lecture seule), proposition de valeur, contact ;
- pages legales : mentions legales, politique de confidentialite, CGV
  (prerequis a V1.0 RC) ;
- redirection vers `/login` pour les clients existants et vers
  l'inscription V0.26 pour les nouveaux ;
- SEO de base (sitemap, meta description, robots.txt deja present) ;
- aucun appel a l'API interne depuis le site vitrine : donnees servies
  par le BFF en mode statique ou cache court.

La V0.27 separe clairement le public anonyme (vitrine) et l'espace
authentifie (portail + admin). Elle est realisee avant la bascule
hardware pour permettre une presentation publique des l'arrivee du
R740xd.

## Jalon V1.0 beta 1 test de deploiement sur la cible R740xd

Statut : **bloque, declenche a la livraison du R740xd**. Ex-V0.24b
renomme au 2026-06-28. Premier deploiement complet sur l'infrastructure
definitive, en mode beta interne (pas encore de client reel).

- bascule des services sur l'hote cible ;
- execution de la procedure de mise en production redigee en V0.24 ;
- restauration MariaDB testee sur la cible reelle ;
- supervision, sauvegardes et alertes cables sur l'infrastructure
  definitive ;
- rotation effective des secrets precedemment exposes (`BPCE_REFRESH_TOKEN`,
  `SMTP_PASSWORD`, `PAYPAL_CLIENT_SECRET`, `SERVICE_AUTH_TOKEN`) ;
- certificats et regles pare-feu actifs sur la cible ;
- bascule `BPCE_INTEGRATION_MODE=live`, `EMAIL_INTEGRATION_MODE=live` et
  `PAYPAL_MODE=live` apres validation explicite ;
- recette UX et fonctionnelle re-executee dans l'environnement cible.

La V1.0 beta 1 n'ajoute aucune fonctionnalite metier ; elle valide la
chaine de deploiement bout en bout sans premier client reel.

## Jalon V1.0 RC deploiement reel et mise en production

Statut : **bloque, materiel**. Prerequis : V1.0 beta 1 realisee et
recettee sur le R740xd. Ex-V1.0 renomme au 2026-06-28.

- exposition publique avec domaine, TLS et supervision actifs ;
- CGV, mentions legales et politique de confidentialite (V0.27)
  publiees et acceptees ;
- tarification publique alignee avec le catalogue V0.15 ;
- premier client reel integre, **sortie de `OU=TEST_SITE_WEB`** vers
  une OU de production validee (procedure V0.25) ;
- SLA documente et procedure d'incident formelle ;
- ouverture des inscriptions self-service V0.26 si validation
  juridique OK.

La V1.0 RC ne marque pas la fin du produit. Toute fonctionnalite
supplementaire identifiee pendant V0.27 ou V1.0 beta 1 est isolee en
V1.1 ou plus tard, jamais ajoutee en derniere minute a V1.0 RC.

## Hors sequence

Reserves, non programmes (ni dans V0.24, ni dans V1.0 RC) :

- prelevement SEPA hors PayPal ;
- integration comptable automatique ;
- automatisation NAS, RDS, VPN declenchee par un encaissement ;
- HTML enrichi dans les e-mails (texte uniquement depuis V0.21) ;
- application mobile native.

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
