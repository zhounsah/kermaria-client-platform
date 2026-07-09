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

Les jalons fonctionnels (V0.20 a V0.31) avancent en respectant ces
bornes. Les jalons d'exploitation finale (V1.0 beta 1, V1.0 RC) sont
**bloques par la livraison du R740xd**.

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

## Jalon V0.23.2 patch harmonisation horodatages

Statut : **implemente dans le depot, en phase de tests**. Documentation
dediee : [`V0.23.2_TIMEZONE_PATCH.md`](V0.23.2_TIMEZONE_PATCH.md). Patch
cross-cutting identifie en recette V0.25 : les logs `api-internal`, les
entrees d'audit et les colonnes `created_at` / `updated_at` affichees
cote portail arrivaient avec deux heures d'avance sur l'heure locale
Europe/Paris (processus en UTC sans conversion a l'affichage).

Livre :

- **Front** : `formatDate` / `formatDateTime`
  (`apps/webportal/lib/formatters.ts`) forcent
  `timeZone: DISPLAY_TIME_ZONE` (`"Europe/Paris"`, IANA — bascule DST
  automatique). Les 33 pages qui consomment ces helpers sont couvertes
  sans modification, aucun `toLocaleString` ne contourne le helper.
- **C#** : helper partage
  `apps/api-internal/Infrastructure/KermariaTimeZone.cs` (IANA +
  fallback Windows `Romance Standard Time`). Utilise pour la date
  fiscale envoyee a BPCE (`InvoiceIssuingService.cs:138` — auparavant
  bug latent : une facture emise entre 00h et 02h Paris ete envoyait la
  date de la veille a BPCE) et pour le rollover / le timestamp
  `FileLoggerProvider`.
- **Console log** : `Program.cs` `AddJsonConsole` bascule en
  `UseUtcTimestamp = false` + format `yyyy-MM-ddTHH:mm:ss.fffzzz`
  (offset ISO 8601 explicite `+02:00` ete / `+01:00` hiver).
- **Templates email** (`invoice_issued`, `payment_reminder`,
  `payment_confirmed`, `contact_form`) : audites, aucun n'injecte de
  date inline dans le corps, rien a patcher.
- **Stockage** : MariaDB reste en UTC, les payloads JSON sortis par
  l'API restent en ISO 8601 `Z` — seule la chaine d'affichage est
  convertie. Zero migration.
- **Tests** : `npm run test:timezone` (nouveau
  `apps/webportal/scripts/verify-timezone-contract.mjs`) combine
  assertions statiques sur les 5 fichiers touches + assertions runtime
  `Intl.DateTimeFormat` sur trois inputs UTC (ete, hiver, bascule
  2026-03-29) validant la conversion DST automatique.

Aucune fonctionnalite metier ajoutee. Aucune dependance hardware.

## Jalon V0.24 stabilisation testable sur SRV-01 et SRV-02

Statut : **recette jouee en DEUX passes divergentes ; l'etat de reference
est la re-verification la plus recente (2026-07-08), plus prudente, qui
reste OUVERTE**. Cadrage detaille :
[`V0.24_STABILISATION.md`](V0.24_STABILISATION.md).
Ex-V0.24a renomme au 2026-06-28 (la phase de validation hardware
ex-V0.24b devient V1.0 beta 1).

Runbook infra : [`DEPLOYMENT_WINDOWS.md`](DEPLOYMENT_WINDOWS.md).
Fichier de suivi vivant des scenarios OK/KO :
[`V0.24_SUIVI.md`](V0.24_SUIVI.md).
Anomalies ouvertes : [`V0.24_ANOMALIES.md`](V0.24_ANOMALIES.md).

> **Note de reconciliation (2026-07-09, revisee).** Il existe DEUX passes de
> recette contradictoires. **Passe 1** (2026-07-03→06, operateur `ZH`,
> manuelle) sur la branche `claude/priceless-driscoll-a6928d` : marque quasi
> tout `[x]`. Elle etait absente de `main` (SUIVI vide), ce qui avait d'abord
> fait croire « recette jamais faite » ; elle est maintenant consolidee
> (audit Brique 2, guides Brique 3, correctif Stripe `V0.29-2` verifie).
> **Passe 2** (2026-07-06→08, operateur `auto (staging)`) : re-verification
> nettement **plus prudente**, elle **conteste plusieurs `[x]` de la passe 1**
> — notamment la **rotation des secrets P04/P05 (mdp AD + `test_web`) marquee
> NON FAITE**, `validate:staging`/backup MariaDB en `[~]`, et la majorite des
> scenarios client V0.17 non re-prouves. **C'est la passe 2 (le `V0.24_SUIVI.md`
> de `main`, + `V0.24_ANOMALIES.md`) qui fait foi pour l'etat reel.** V0.24
> n'est donc **PAS clos** : rotation P04/P05 a executer, scenarios prudents a
> rejouer, puis sign-off. Seul acquis ferme cote code : bloquant `V0.29-2`
> (Stripe `invoice.paid`) corrige dans `main`
> (`StripeWebhookService.ReadStripeInvoiceSubscriptionId` lit desormais
> `parent.subscription_details.subscription`).
Guides utilisateur livres (Brique 3) :
[`GUIDE_CLIENT_PAIEMENT.md`](GUIDE_CLIENT_PAIEMENT.md) (client) et
[`GUIDE_ADMIN.md`](GUIDE_ADMIN.md) (admin : paiements, abonnements, journal
e-mails, inscriptions, Active Directory). Rotation des secrets etendue :
[`SECRET_ROTATION.md`](SECRET_ROTATION.md).

**Livre au 2026-07-03** :
- Infra deployee sur KERMARIA-SRV-01 (Dell Optiplex 5070, WEBPORTAL
  + IIS front), KERMARIA-SRV-02 (ASUS FX753VD, API-INTERNAL) et
  KERMARIA-SRV-07 (MariaDB 192.168.100.207). Windows Server 2022,
  sans VM ni Docker.
- Compte de service AD partage `HOME\svc_api_portal_ad` pour les
  deux services Windows.
- Config unifiee dans un fichier JSON par app
  (`C:\ProgramData\Kermaria\{api-internal,webportal}.config.json`)
  au lieu des variables Machine — patch Program.cs charge
  `KERMARIA_CONFIG_PATH`, wrapper Node lit et injecte les env de
  session. Zero pollution systeme.
- Split IIS : `kermaria-vitrine` (`www.home.bzh` + `www.zacharyhounsa.ovh`,
  X-Robots-Tag strippe pour indexation) et `kermaria-portal`
  (`portail.*` + `dashboard.*` sur les deux domaines, `/` → `/login`),
  wildcard Let's Encrypt reutilise.
- `PUBLIC_VITRINE_ENABLED=true` — vitrine V0.27 accessible sur `www.*`,
  backoffice reserve aux hostnames `portail`/`dashboard`.
- Bootstrap 1er admin via nouveau flag CLI `--seed-admin` (Program.cs
  + `MariaDbAdminSeeder`), usable hors Development, prompt masque,
  hash PBKDF2, sentinel customer `INTERNAL` si aucun customer.
- Scripts de deploiement livres : `build-api-config.ps1`,
  `build-webportal-config.ps1`, `start-webportal.ps1`.

**Brique 1 — recette staging jouee, re-verification prudente OUVERTE**
(etat de reference = passe 2 dans `V0.24_SUIVI.md`) :
- passe 1 (`ZH`) a couvert V0.17, V0.20 BPCE mock, V0.21 PayPal sandbox,
  V0.22 souscriptions, V0.25 AD par reference, V0.26 signup, V0.27 vitrine,
  V0.29 Stripe `test`, V0.30 allowlist SMTP, V0.23.2 timezone, T-1..T-4 ;
- **mais** la passe 2 (`auto`, plus recente) laisse plusieurs de ces
  scenarios en `[ ]`/`[~]` (validate:staging, backup MariaDB, majorite des
  cas client V0.17) : a rejouer/prouver avant de clore.

**Brique 2 — audit securite execute** :
- audit securite interne : dependances, secrets (couvre
  `BPCE_REFRESH_TOKEN`, `SMTP_PASSWORD`, `PAYPAL_CLIENT_SECRET`,
  `STRIPE_SECRET_KEY`, `STRIPE_WEBHOOK_SECRET`, `SERVICE_AUTH_TOKEN`,
  `SQL_PASSWORD`, `HCAPTCHA_SECRET_KEY`), headers, rate limiting ;
- rotation des mots de passe faibles utilises pendant l'installation
  (compte AD, user MariaDB) — le validator `IsPlaceholderSecret`
  refuse deja tout secret commencant par "test", donc a traiter
  avant sortie de recette ;
- revue accessibilite WCAG AA des parcours critiques (scan axe : 0 violation
  critique) ; matrice des 8 secrets renseignee. **Rotation P04/P05 (mdp AD
  `svc_api_portal_ad` + user MariaDB `test_web`) : marquee FAITE par la
  passe 1, mais NON FAITE par la passe 2 (07-08) — a executer/confirmer
  avant sortie de recette.**

**Brique 3 — documentation redigee** :
- **procedure formelle de mise en production redigee 2026-07-03**
  dans [`docs/PRODUCTION_DEPLOYMENT.md`](PRODUCTION_DEPLOYMENT.md),
  a relire et signer off, **non executee** avant V1.0 beta 1 ;
- documentation utilisateur admin et client redigee
  ([`GUIDE_ADMIN.md`](GUIDE_ADMIN.md), [`GUIDE_CLIENT_PAIEMENT.md`](GUIDE_CLIENT_PAIEMENT.md)) ;
- plan de continuite minimal (integre a la section 11 de
  PRODUCTION_DEPLOYMENT.md).

La V0.24 n'ajoute aucune fonctionnalite metier (sauf
`--seed-admin` qui est un outil de bootstrap) et ne s'execute pas
sur l'infrastructure definitive.

## Jalon V0.25 finalisation Active Directory

Statut : **livre et valide en recette utilisateur 2026-06-30** sur AD
reel (`home.bzh`, mode `controlled_write`). Cadrage 2026-06-28, voir
[`V0.25_AD_FINALISATION.md`](V0.25_AD_FINALISATION.md) (section
"Recette utilisateur" pour le detail des cas et les correctifs
UX/diagnostic apportes pendant la recette).

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
- **brique 1 livree 2026-06-30** : changement de mot de passe AD cote
  client (page `/password`), derriere flag
  `AD_PASSWORD_CHANGE_ENABLED=true|false` (defaut `false`), policy AD
  du domaine = seule source de verite, rate limit 3 echecs / 15 min
  avec blocage 15 min, audit `ad.password_change.*`, aucun mot de passe
  en log ni en cache. Endpoint API-INTERNAL
  `POST /internal/profile/password`, BFF
  `POST /api/profile/password`. Liaison portal_user -> AD via
  convention `customer_ad_links.user_principal_name == portal_users.email` ;
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
  `window.confirm()` cross-client.

La V0.25 n'ouvre pas encore les ecritures hors OU de test ni n'active
le mode `live` AD : tout reste sur SRV-01/02. La sortie effective de
`OU=TEST_SITE_WEB` est portee par le jalon V0.31.

## Jalon V0.26 creation de compte self-service

Statut : **livre et valide en recette utilisateur le 2026-07-02**,
`SIGNUP_ENABLED=false` par defaut (kill switch). Recette rejouee en
`EMAIL_INTEGRATION_MODE=live` avec SMTP OVH reel : parcours complet
inscription -> verification -> approbation -> definition mot de passe
-> connexion boucle sur un nouveau compte reel. Doc detaillee :
[docs/V0.26_SELF_SERVICE_SIGNUP.md](V0.26_SELF_SERVICE_SIGNUP.md) ; guide
utilisateur : [docs/V0.26_USER_GUIDE_SIGNUP.md](V0.26_USER_GUIDE_SIGNUP.md).

Permet a un visiteur d'initier la creation d'un compte client sans
intervention manuelle prealable :

- formulaire d'inscription public (entreprise, contact, e-mail) avec
  validation, honeypot + timing anti-bot et **hCaptcha** verifie cote
  serveur (`siteverify`) avant toute insertion en base ;
- verification e-mail via token aleatoire 32 octets, **stocke uniquement
  en hash SHA-256**, TTL 24h, one-shot ;
- creation du compte cote portail **uniquement a l'approbation admin**
  (customer + portal_user, statut `active` mais sans mot de passe), sans
  provisioning AD automatique ;
- workflow admin `/admin/signups` (liste + detail + approuver/refuser)
  avec audit a chaque etape (`signup.submit` / `.verify_success` /
  `.verify_failed` / `.approved` / `.rejected` / `.password_set`) ;
- **definition du mot de passe par lien** : l'e-mail `account_approved`
  contient un lien one-shot (token hash SHA-256, TTL 24h) vers
  `/set-password` ; aucun mot de passe en clair ne transite ni n'est
  journalise ;
- e-mails transactionnels `signup_verification` / `account_approved` /
  `account_rejected` (texte, mode courant `disabled`/`mock`/`live`) ;
- isolation stricte : aucun acces au portail authentifie tant que le mot
  de passe n'est pas defini, aucune creation de compte AD (acte admin
  separe via V0.18/V0.25).

Migration `020_signup_pending.sql` (renumerotee depuis le `017` du
cadrage : les `017`/`018`/`019` ont ete consommes par V0.29 Stripe ;
aucune migration de templates e-mail necessaire, `email_messages.template`
etant un VARCHAR libre). Tests contrat : `npm run test:signup`.

La V0.26 prepare le canal de conversion sans bypasser la qualification
commerciale manuelle. **Ouverture des inscriptions (`SIGNUP_ENABLED=true`)
reservee au test interne** tant que `EMAIL_INTEGRATION_MODE=mock`, et a la
validation juridique pour la production (cf. V1.0 RC).

## Jalon V0.27 site vitrine public

Statut : **livre et valide le 2026-06-30** (squelette + flag desactive
par defaut), finalisation du contenu et bascule
`PUBLIC_VITRINE_ENABLED=true` reportees a la recette pre-V1.0 RC. Doc
detaillee : [docs/V0.27_PUBLIC_VITRINE.md](V0.27_PUBLIC_VITRINE.md).

Page d'accueil publique non authentifiee, en amont du backend client et
admin :

- branche racine `/` selon la session : client authentifie ->
  `/dashboard`, admin -> `/admin`, anonyme +
  `PUBLIC_VITRINE_ENABLED=false` -> `/login` (comportement V0.23),
  anonyme + `PUBLIC_VITRINE_ENABLED=true` -> landing vitrine ;
- bascule `PublicShell` / shell securise par route. Etat actuel :
  `AppShell.tsx` choisit selon `usePathname()` + `public-route-config.ts`
  ; `proxy.ts` n'est plus la source de verite fonctionnelle de ce split ;
- landing avec contenu et ton calques sur
  [zacharyhounsa.ovh](https://zacharyhounsa.ovh/) : hero "Informatique
  claire et utile", section Methode (3 etapes), section Services (6
  prestations : hebergement de dossiers, sauvegarde, acces distant,
  VPN prive, maintenance, reseau & infrastructure), section "Pour qui"
  (particuliers / associations / petites structures), CTA finale ;
- portfolio **embarque statique** : copie integrale du portfolio Astro
  (21 fichiers HTML/CSS/JS + sous-projets) dans
  `apps/webportal/public/portfolio/`. Lien dans la nav vers
  `/portfolio/index.html`. Aucune modification du contenu : la
  canonical pointe toujours vers `zacharyhounsa.ovh/portfolio/`, ce
  qui evite la duplication SEO ;
- page `/offres` lecture seule reutilisant le catalogue V0.15
  (`/internal/portal/catalog` rendu anonyme cote api-internal, toujours
  protege par `X-Service-Auth`), tri par `displayOrder`, filtre des
  offres `monthly` sans plan PayPal pour le mode actif, cache 5 min
  (`revalidate = 300`) ;
- page `/contact` avec formulaire POST `/api/contact` (rate limit 5
  req/5 min par IP), forward vers `/internal/public/contact-message`
  api-internal, template email `contact_form` respectant
  `EMAIL_INTEGRATION_MODE` (recipient = `CONTACT_FORM_RECIPIENT`).
  Lien de retour en haut de page (vers `/offres` si `?offer=` present,
  sinon vers `/`) ;
- pages publiques legales/editoriales initialement posees en statique
  en V0.27 ; etat actuel : `/a-propos`, `/mentions-legales` et `/cgv`
  sont admin-editables via V0.33, `/politique-confidentialite` restant
  hors module ;
- SEO : `sitemap.ts` dynamique (les 7 pages Next ; portfolio non
  inclus car canonical pointe ailleurs), `robots.ts` etendu (allow
  tout sauf routes privees, gate sur `PUBLIC_VITRINE_ENABLED`),
  JSON-LD `Organization` sur `/`, `metadataBase` + `openGraph` defaut,
  titre override par page ;
- conformite : aucun analytics, aucun pixel tiers, aucune banniere de
  consentement cookies (decision 2026-06-28).

Variables d'environnement nouvelles : `PUBLIC_VITRINE_ENABLED` (defaut
`false`), `CONTACT_FORM_RECIPIENT` (destinataire interne du formulaire
contact).

A traiter avant V1.0 RC : raffiner le header
`X-Robots-Tag: noindex, nofollow` defini dans `next.config.ts`
(actuellement applique a toutes les routes, heritage V0.23) pour qu'il
ne couvre que les routes privees une fois la vitrine activee.

La V0.27 separe clairement le public anonyme (vitrine) et l'espace
authentifie (portail + admin). Elle est realisee avant la bascule
hardware pour permettre une presentation publique des l'arrivee du
R740xd. Le contenu redactionnel des pages legales et `/a-propos` reste
a finaliser avant V1.0 RC.

## Jalon V0.28 catalogue packs et offres groupees

Statut : **a cadrer, faisable sans la cible R740xd**.

Le catalogue actuel (V0.15 + V0.20.1) ne g\xE8re que des offres unitaires.
La V0.28 ajoute la notion de **pack** : un produit commercial unique
compose de plusieurs offres existantes, avec un prix ou un remise
specifique au pack (ex. "Pack PME = VPN + Sauvegarde + 5h support").

- modele : table `commercial_offer_bundles` + table de liaison
  `commercial_offer_bundle_items` (offre fille, quantite, ordre
  d'affichage), `external_reference` unique partage avec
  `commercial_offers` ;
- prix : strategie au choix par pack (`sum_of_lines` reproduit la somme,
  `fixed_total` impose un total HT, `percent_discount` applique un
  pourcentage sur la somme) ; rendu cote portail et `/admin/payments`
  inchange ;
- creation document : selectionner un pack en ligne explose le pack en
  N lignes filles taggees `bundle_parent_id` cote
  `commercial_document_lines`, idempotent ;
- compatibilite PayPal (V0.22) : un pack ne peut etre `recurring` que
  si toutes ses offres filles ont la meme cadence `monthly`. La creation
  du plan PayPal du pack utilise le prix calcule et garde son propre
  `paypal_plan_id_sandbox` / `paypal_plan_id_live` ;
- admin `/admin/catalog` : section dediee "Packs" en plus de "Offres
  simples", fiche d'edition `/admin/catalog/bundles/{id}` avec drag and
  drop des items, prix calcule en direct ;
- portail client `/services` et `/offres` : packs mis en avant en haut
  de liste, badge "Pack", detail des prestations incluses sur la fiche
  de l'offre.

La V0.28 n'introduit aucun nouveau rail de paiement et aucune
fonctionnalite hardware-gated.

## Jalon V0.29 Stripe comme rail de paiement parallele

Statut : **implemente dans le depot** (2026-07-02). Documentation
d'implementation : [`V0.29_STRIPE_PAYMENTS.md`](V0.29_STRIPE_PAYMENTS.md).

Ajoute Stripe en parallele de PayPal (V0.21 one-shot, V0.22
abonnements), sans remplacer PayPal. Le client choisit son rail au
moment de regler une facture ou souscrire un service.

- variables : `STRIPE_MODE=disabled` (defaut) | `test` | `live`,
  `STRIPE_SECRET_KEY`, `STRIPE_PUBLISHABLE_KEY`,
  `STRIPE_WEBHOOK_SECRET` ; mode `live` reste interdit avant V1.0 beta
  1 ;
- one-shot : `POST /api/payments/stripe/create-intent`
  ([PaymentIntent](https://stripe.com/docs/api/payment_intents)),
  capture automatique au confirm front, propagation BPCE
  `mark_as_paid` + statut local `paid` identique au flow PayPal ;
- abonnements : `Stripe.Subscriptions` avec un `Stripe.Product` +
  `Stripe.Price` par offre `monthly`, miroir des deux colonnes
  `paypal_plan_id_*` -> `stripe_price_id_test` + `stripe_price_id_live`
  (migration dediee) ;
- webhook `POST /api/webhooks/stripe` : verification de signature
  `Stripe-Signature`, idempotence sur `event.id` dans
  `stripe_webhook_events` (miroir de `paypal_webhook_events`), switch
  sur `payment_intent.succeeded` / `invoice.paid` /
  `customer.subscription.deleted` ;
- admin : `/admin/payments` et `/admin/subscriptions` ajoutent une
  colonne "Rail" (PayPal vs Stripe), filtres etendus, MRR HT agrege
  cross-rail ;
- portail client : le bouton "Payer" propose un radio rail si les deux
  sont actifs, defaut Stripe si `STRIPE_MODE != disabled`, sinon
  PayPal ;
- tests : `npm run test:payments-stripe`
  (`scripts/verify-stripe-contract.mjs`) couvre intent + webhook +
  idempotence en mode `test`.

Garde-fou : aucune ouverture du mode `live` Stripe avant V1.0 beta 1
sur le R740xd, identique a PayPal et BPCE — **et code en dur cette fois**
dans `RuntimeConfigurationValidator` (contrairement au garde-fou PayPal,
jamais implemente, qui reste une simple discipline de process).

**Ecarts d'implementation vs cadrage (2026-07-02)** : (a) "PaymentIntent"
est realise via Stripe Checkout Sessions (`mode=payment`/`mode=subscription`),
qui crée un PaymentIntent/une Subscription en interne — choix qui
preserve la convention "raw fetch, pas de SDK" et l'UX 100% redirect
deja en place pour PayPal, plutot que PaymentIntent+Stripe.js Elements
cote client ; (b) la table `subscriptions` est generalisee par colonne
`rail ENUM('paypal','stripe')` plutot que dupliquee, pour garder le MRR
et les listes admin cross-rail en une seule requete ; (c) migrations
`017_stripe_webhook_events.sql`, `018_subscriptions_stripe_rail.sql`,
`019_stripe_offers_and_payment_method.sql` — cette derniere ferme une
lacune V0.21 preexistante (`commercial_documents` n'avait jamais de
colonne pour savoir quel rail avait regle une facture, le endpoint
`payment-confirm` ignorait silencieusement le corps de la requete).

## Jalon V0.30 test envoi e-mail automatique reel

Statut : **partiellement livre et valide en recette utilisateur le
2026-07-02** (uniquement la brique allowlist ; envoi SMTP OVH reel
verifie via signup live + formulaire contact). Doc :
[`V0.30_EMAIL_LIVE_TEST.md`](V0.30_EMAIL_LIVE_TEST.md).
Restent a faire avant V1.0 RC : statuts `email_messages` etendus,
sous-domaine dedie `tests-mail.*`, SPF/DKIM/DMARC documentes, recette
guidee Gmail/Outlook/interne.

### Livre : garde-fou allowlist (2026-07-02)

- variables `EMAIL_LIVE_ALLOWLIST_ONLY=true` (defaut fail-closed) et
  `EMAIL_LIVE_ALLOWLIST` (adresses completes ou motifs `@domaine`),
  parse insensible a la casse ;
- gate dans `LiveEmailService.SendAsync` **avant** toute construction
  `SmtpClient` : destinataire hors allowlist -> retour
  `EmailDeliveryResult(false, "blocked_allowlist", ...)`, log `Warning`,
  status persiste dans `email_messages.status = "blocked_allowlist"` ;
- allowlist vide + `AllowlistOnly=true` -> tout envoi live refuse
  (defense en profondeur) ;
- test contrat : `npm run test:email-live`.

### Reste a faire (cadrage initial)

Premiere bascule controlee de `EMAIL_INTEGRATION_MODE` vers `live` sur
un SMTP reel **sans attendre le R740xd**, dans le cadre limite de la
phase de tests. Objectif : valider la chaine SMTP de bout en bout
(authentification, STARTTLS, reputation, deliverabilite) avant d'engager
la V1.0 beta 1.

- choix d'un SMTP de transit dedie phase-de-tests (compte applicatif
  separe de la prod, From `noreply@zacharyhounsa.ovh` ou sous-domaine
  reserve `tests-mail.zacharyhounsa.ovh`) ;
- destinataire **liste blanche** uniquement (boites internes
  `@home.bzh` + boite personnelle de l'editeur), refus en dur de tout
  destinataire externe tant que `EMAIL_LIVE_ALLOWLIST_ONLY=true`
  (**LIVRE**) ;
- 4 templates couverts : `invoice_issued`, `payment_reminder`,
  `payment_confirmed`, `contact_form` ;
- enregistrement `email_messages.status` etendu : succes SMTP =
  `live_sent`, erreur classifiee (`smtp_auth`, `smtp_relay_refused`,
  `smtp_timeout`, `smtp_other`) ;
- check SPF / DKIM / DMARC sur le sous-domaine emetteur, documente dans
  `docs/V0.30_EMAIL_LIVE_TEST.md` (a creer au demarrage du jalon) ;
- recette guidee : declencher manuellement les 4 templates et verifier
  reception dans Inbox (pas Spam) sur Gmail + Outlook + serveur interne ;
- rollback : retour immediat a `EMAIL_INTEGRATION_MODE=mock` documente.

La V0.30 n'eteint pas PayPal/BPCE en mode sandbox/mock : elle isole le
canal e-mail comme premier vrai canal externe production-grade.

## Jalon V0.31 provisioning AD reel hors OU de test

Statut : **a cadrer, faisable sans la cible R740xd** (depend de la
disponibilite de l'AD `home.bzh`, deja utilise en recette V0.25).

Execution effective de la procedure documentee en V0.25 brique 3
(`docs/AD_PRODUCTION_MIGRATION.md`). Sort definitivement de
`OU=TEST_SITE_WEB,DC=home,DC=bzh` pour cibler une **OU de production**
validee, sur le meme AD que la recette V0.25.

- **PR code de levee** : remplacer
  `AdRuntimeConfiguration.RequiredTestOuRoot` (`const string` hardcode
  ligne 41 de `apps/api-internal/Data/Configuration/AdRuntimeConfiguration.cs`)
  par une variable d'environnement `AD_REQUIRED_OU_ROOT` + allowlist
  `AD_ALLOWED_ROOTS` (liste blanche de DN racines acceptees) ;
- **option A** (recommandee par la procedure brique 3) : bascule
  franche par client, re-provisioning sous nouvelle OU prod,
  invalidation du `customer_ad_links.distinguished_name` ancien,
  audit `admin.customers.ad_users.migrate_root` ;
- **option B** : per-customer `ad_ou_override`, non implementee en
  V0.31 sauf besoin avere ;
- tests des 4 modes AD (`disabled`, `mock`, `read_only`,
  `controlled_write`) sous la nouvelle OU ;
- recette utilisateur sur 1 client temoin (`CLI-DEMO-0060` ou
  equivalent) avec rejouage des cas V0.25 (search / create / rename /
  move / groups / password) ;
- mise a jour du seed mock pour refleter la nouvelle racine sans
  casser les tests `read_only` en developpement ;
- mode `live` AD n'est toujours pas necessaire :
  `controlled_write` reste le mode cible. La "vraie" production AD est
  livree quand V0.31 valide la nouvelle OU racine et que V1.0 RC
  ouvre l'OU au premier client reel.

La V0.31 ne couvre pas le hardware R740xd. Elle valide uniquement que
le code et la procedure tiennent quand on quitte l'OU de test.

## Jalon V0.33 contenus administrables

Statut : **implante dans le depot**. Documentation dediee :
[`V0.33_CONTENUS_ADMINISTRABLES.md`](V0.33_CONTENUS_ADMINISTRABLES.md).

La V0.33 introduit un module ferme de **contenus administrables**,
persiste en MariaDB et edite depuis le back-office, pour eviter les
edits manuels de fichiers applicatifs sur les contenus legaux et les
fiches techniques packs.

- table `managed_content_entries` (`content_key`, `content_type`,
  `title`, `public_path`, `body_markdown`, `version_label`,
  `created_at`, `updated_at`) ;
- registre ferme partage pour :
  `legal:cgv`, `legal:mentions-legales`, `page:a-propos` et
  `pack-sheet:<publicPackCode>` ;
- seed idempotent applicatif dans `api-internal` :
  fichiers UTF-8 pour `cgv`, `mentions-legales`, `a-propos` et
  generation applicative pour les fiches packs ;
- back-office `/admin/content` + `/admin/content/[key]` avec textarea
  Markdown, champ de version, apercu rendu, sauvegarde persistante ;
- pages publiques `/cgv`, `/mentions-legales`, `/a-propos` branchees sur
  le contenu admin ;
- ajout de `/offres/[slug]` comme fiche technique pack publique,
  complementaire a la vitrine `/offres` ;
- rendu Markdown via `react-markdown`, sans HTML brut, avec affichage
  de `versionLabel` et `updatedAt`.

La V0.33 reste volontairement simple : pas de CMS libre, pas de
creation/suppression admin, pas de WYSIWYG, pas de duplication des
composants techniques dans le texte libre. La structure est toutefois
prete a accueillir d'autres contenus plus tard sans refaire le schema.

## Jalon V0.35 panier / commande groupee a la carte

Statut : **implemente dans le depot (2026-07-08)**, faisable sans la cible
R740xd. Documentation dediee :
[`V0.35_CART_ALACARTE.md`](V0.35_CART_ALACARTE.md). Dependance a V0.28
(packs) **conceptuelle et non bloquante** (V0.35 reutilise directement
`commercial_document_lines`), donc livre avant V0.28.

Avant V0.35, la page `/souscrire` gerait deux flux **mono-article** : les
packs grand public via `PublicPackCard` -> `SubscribeButton` (rail
Stripe/PayPal deja en place), et les options a la carte renvoyant chacune
vers `/request-service` (demande etudiee, sans paiement direct). La V0.35
introduit un **panier** client self-service pour regrouper N options a la
carte en une seule commande, **sans approbation admin prealable** :

- table `cart_items` rattachee au client (migration
  `028_alacarte_cart.sql`) + colonne `commercial_documents.origin` ;
  selection multiple depuis `/souscrire`, synthese sur `/panier` ;
- panier strictement **one-shot** : seules les offres `one_time` actives a
  prix > 0 sont eligibles ; toute offre `recurring` est refusee
  (`CART_OFFER_NOT_ELIGIBLE`), coherent avec le garde-fou cadence V0.28 ;
- la confirmation materialise **un** document commercial multi-lignes
  (`origin = 'client_cart'`, reutilise `commercial_document_lines`) puis
  l'emet aussitot (BPCE mock en phase de tests) pour le rendre payable ;
- **reglement via les rails existants** : Stripe, PayPal (`PayButton`) et
  virement bancaire (bloc V0.21) sur le document genere — **aucun nouveau
  rail ni code de paiement** ;
- **provisioning automatique « le cas echeant »** au reglement (tous rails
  via `ConfirmPaymentAsync`) : reconcilie le provisioning AD existant du
  client. **Revise le garde-fou de cadrage** « aucun provisioning declenche
  par le panier ». Inerte pour le catalogue one-shot courant (aucun mapping),
  mais cable pour l'avenir ;
- tests : `npm run test:cart` (contrat statique de bout en bout).

Le choix du rail Stripe vs PayPal est **deja livre** (V0.29) et n'est pas un
objectif de la V0.35. La V0.35 n'ajoute ni prelevement recurrent
multi-articles, ni checkout Stripe/PayPal multi-panier natif, ni
fonctionnalite hardware-gated. Elle reste bornee a la phase de tests (mode
`live` interdit avant V1.0 beta 1) ; recette MariaDB du chemin confirm ->
emission -> reglement -> provisioning a rejouer.

### V0.35.1 correctif horodatages UTC (NOW → UTC_TIMESTAMP)

Statut : **livre (2026-07-09)**. Documentation dediee :
[`V0.35.1_TIMEZONE_UTC_FIX.md`](V0.35.1_TIMEZONE_UTC_FIX.md).

3e recidive de la famille « heure locale serveur stockee comme UTC »
(apres V0.20 BPCE et V0.21 email log) : `MarkDocumentIssued/PaidAsync`
ecrivaient `updated_at = NOW(6)` (heure de Paris sur nos serveurs) →
« Mise a jour » affichee +2h sur le portail. Ratissage complet de la
chaine : `NOW(6)` elimine partout (`commercial_documents`, `cart_items`,
seeds 006/009), serialisations sans suffixe Z corrigees (managed content,
pack catalog), titres « Echeance yyyy-MM » et numero mock BPCE passes en
mois/jour de Paris, defauts `CURRENT_TIMESTAMP` de `signup_pending`
supprimes (migration `031`), logger fichier a offset Paris explicite.
`npm run test:timezone` interdit desormais statiquement toute fonction
SQL d'heure locale dans `Data`/`Services`/`Migrations`. Donnees de la
recette SRV-07 reparees (-2h sur les lignes concernees).

## Jalon V0.36 panier unifie et abonnements factures

Statut : **implante dans le depot (2026-07-09)**. Documentation dediee :
[`V0.36_PANIER_UNIFIE_ABONNEMENTS_FACTURES.md`](V0.36_PANIER_UNIFIE_ABONNEMENTS_FACTURES.md).

Cette V0.36 prolonge la V0.35 sans la remplacer :

- `/souscrire` devient l'entree de selection commune pour les achats
  ponctuels et les packs recurrents ;
- le header client authentifie affiche un mini-panier avec drawer
  survol/clic et recapitulatif des deux tunnels ;
- `/panier` presente un recapitulatif unique, mais conserve deux boutons
  de confirmation distincts :
  - commande one-shot pour `cart_items` ;
  - confirmation recurrente facturee pour `recurring_checkout_items`.

Livraison metier :

- nouvelles selections persistantes `recurring_checkout_items` par
  client + offre ;
- nouveau statut local `subscriptions.status = pending_payment` pour les
  souscriptions facturees avant leur premier reglement ;
- creation d'une **facture initiale groupee** pour les lignes
  recurrentes, avec liaison `commercial_document_line_subscriptions` entre
  les lignes du document et les souscriptions locales ;
- choix explicite Stripe / PayPal / **virement bancaire** sur la page
  document client ;
- au paiement d'un document recurrent :
  `pending_payment -> pending_activation -> active`, calcul de
  `started_at`, `next_billing_at`, `commitment_ends_at`, puis
  provisioning automatique si necessaire ;
- le flux admin existant **Marquer comme paye** declenche le meme
  pipeline pour les virements recus ;
- un worker periodique emet les renouvellements des souscriptions
  `rail='billing'` et suspend en cas d'impaye prolonge.

Correctifs inclus dans le meme jalon :

- lecture MariaDB du panier rendue tolerante au type reel renvoye pour
  `offer_id`, ce qui supprime l'`InvalidCastException` observee sur
  `MariaDbCartRepository` ;
- les erreurs portail sur panier / paiement / resiliation propagent des
  messages plus utiles et la reference de correlation, au lieu du
  message generique uniforme.

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
- CGV et mentions legales publiees via `/admin/content` (V0.33) ;
- politique de confidentialite publiee et acceptee ;
- tarification publique alignee avec le catalogue V0.15 + packs V0.28 ;
- premier client reel integre dans l'OU de production validee en V0.31
  (la procedure de sortie de `OU=TEST_SITE_WEB` est rejouee une fois
  pour le premier client reel) ;
- SLA documente et procedure d'incident formelle ;
- ouverture des inscriptions self-service V0.26 si validation
  juridique OK.

La V1.0 RC ne marque pas la fin du produit. Toute fonctionnalite
supplementaire identifiee pendant V0.27 ou V1.0 beta 1 est isolee en
V1.1 ou plus tard, jamais ajoutee en derniere minute a V1.0 RC.

## Hors sequence

Reserves, non programmes (ni dans V0.24, ni dans V0.28-V0.31, ni dans
V1.0 RC) :

- prelevement SEPA hors PayPal/Stripe ;
- integration comptable automatique ;
- automatisation NAS, RDS, VPN declenchee par un encaissement ;
- HTML enrichi dans les e-mails (texte uniquement depuis V0.21) ;
- application mobile native.

Note : Stripe est desormais **dans la sequence** (V0.29), les packs et
offres groupees egalement (V0.28).

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
