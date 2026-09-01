# Billing V2 — Checklist de déploiement et lancement contrôlé

Document de préparation. **Aucun déploiement ni changement Stripe production
n'est autorisé avant validation explicite de cette checklist.**

Périmètre gelé : Stripe mensuel uniquement. Le développement fonctionnel
Billing V2 est arrêté.

---

## A. Base de données

### A.1 Sauvegarde préalable — bloquant

```bash
npm run backup:mariadb
```

Ne pas continuer sans un dump vérifié (taille non nulle, restaurable). Ne
jamais versionner le dump.

### A.2 État de `schema_migrations` avant migration

```sql
SELECT migration_id, applied_at
FROM schema_migrations
ORDER BY migration_id DESC
LIMIT 10;
```

Attendu : la dernière ligne est `061_billing_v2_renewal_lifecycle`. Si une
migration ≥ 062 est déjà présente, **arrêter** et rapprocher les
environnements avant toute action.

### A.3 Migrations à appliquer

Seule `062_billing_v2_initial_cycle_integrity` reste à appliquer si l'existant
est à 061. Elle est additive et rejouable.

Compte à utiliser : `kermaria_migrator` (DDL). Charger d'abord
`.local.env.ps1` puis **surcharger** les identifiants, sinon `CREATE command
denied` — le compte applicatif `kermaria_api` n'a pas les droits de schéma.

```powershell
dotnet run --project <chemin absolu>/apps/api-internal/Kermaria.ApiInternal.csproj -- --apply-migrations
```

Passer `--project` en chemin absolu : lancé depuis une autre racine, le runner
applique le mauvais checkout.

### A.4 Vérification post-migration

```sql
-- 1. La migration est enregistrée.
SELECT migration_id FROM schema_migrations WHERE migration_id LIKE '062%';

-- 2. Le rang de cycle est obligatoire : l'unicité par cycle est effective.
SELECT COLUMN_NAME, IS_NULLABLE, COLUMN_DEFAULT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'billing_v2_subscription_documents'
  AND COLUMN_NAME = 'cycle_sequence';
-- attendu : IS_NULLABLE = NO, DEFAULT = 1

-- 3. Aucun document orphelin de BillingEvent.
SELECT COUNT(*) AS documents_sans_evenement
FROM billing_v2_subscription_documents
WHERE billing_event_id IS NULL;
-- attendu : 0

-- 4. Aucune ancre contractuelle manquante sur un abonnement démarré.
SELECT COUNT(*) AS ancres_manquantes
FROM billing_v2_subscriptions
WHERE started_at IS NOT NULL AND billing_anchor_at IS NULL;
-- attendu : 0

-- 5. Cohérence de l'axe documentaire.
SELECT COUNT(*) AS incoherences
FROM billing_v2_billing_events event_row
INNER JOIN billing_v2_subscription_documents doc
    ON doc.billing_event_id = event_row.id
WHERE doc.status IN ('issued', 'paid')
  AND event_row.document_status <> 'issued';
-- attendu : 0
```

### A.5 Rollback

**Il n'y a pas de rollback SQL de 062.** Elle rend `cycle_sequence` non nul et
remplit des colonnes : la défaire ferait perdre le rattachement documentaire.
Le rollback est **applicatif**, par les drapeaux (section D), pas par la base.

Rollback applicatif immédiat, sans redéploiement :

```
BILLING_V2_AUTHORITATIVE_CHECKOUT_ENABLED = false
BILLING_V2_PROVIDER_EXECUTOR_ENABLED      = false
BILLING_V2_RECONCILIATION_WORKER_ENABLED  = false
```

Effet : plus aucun nouveau checkout V2, plus aucun appel provider sortant, plus
aucune transition automatique. Les abonnements déjà réglés restent valides et
facturés ; les cas en cours basculent en revue manuelle. Redémarrage requis sur
SRV-13 (la configuration est lue au démarrage).

Restauration de base : uniquement depuis le dump A.1, et uniquement si la
migration elle-même a échoué à mi-parcours.

---

## B. Stripe LIVE

| Point | Contrôle | Bloquant |
| --- | --- | --- |
| `STRIPE_MODE` | reste `test` jusqu'à l'instant du lancement, puis `live` | oui |
| `STRIPE_SECRET_KEY` | commence par `sk_live_` | oui |
| `STRIPE_PUBLISHABLE_KEY` | commence par `pk_live_` | oui |
| `STRIPE_WEBHOOK_SECRET` | secret **live** (`whsec_…`), posé sur le **WEBPORTAL** | oui |
| `STRIPE_WEBHOOK_VERIFY` | absent ou `true` — jamais `false` | oui |
| Endpoint webhook | `https://<host-webportal>/api/webhooks/stripe` | oui |
| `/internal/webhooks/stripe` | **non exposé** à Internet | oui |
| Mappings prix | une ligne active par `service_price_id` en `environment='live'` | oui |
| Objets test | aucun `price_…`/`prod_…` de test référencé en live | oui |
| `PAYPAL_MODE` | `disabled` | oui |

Le `STRIPE_MODE` et les clés doivent être cohérents : des clés `sk_test_` avec
`STRIPE_MODE=live` font résoudre au catalogue les `stripePriceIdLive`.

### Événements à sélectionner dans le Dashboard live

`checkout.session.completed`, `invoice.paid`, `invoice.payment_succeeded`,
`invoice.payment_failed`, `invoice.marked_uncollectible`,
`customer.subscription.updated`, `customer.subscription.deleted`,
`customer.subscription.created`.

Ne pas cocher `payment_intent.succeeded` : traité par le service legacy
uniquement.

### Requête de contrôle des mappings live

```sql
SELECT price.id, service.code, mapping.external_price_id
FROM billing_v2_service_prices price
INNER JOIN billing_v2_services service ON service.id = price.service_id
LEFT JOIN billing_v2_provider_price_mappings mapping
       ON mapping.service_price_id = price.id
      AND mapping.provider = 'stripe'
      AND mapping.environment = 'live'
      AND mapping.status = 'active'
WHERE price.status = 'active'
  AND mapping.id IS NULL;
-- attendu : aucune ligne
```

---

## C. Périmètre de lancement

Autorisé : Stripe ; paiement mensuel ; offres mensuelles prévues ; portail ;
facturation et document ; provisioning sous gate.

Interdit : PayPal ; paiement upfront 6/12 mois ; upgrades/downgrades
self-service ; Credit Ledger ; remboursements automatiques ; chargebacks
automatiques ; résiliation self-service ; TVA non nulle.

Ce périmètre n'est pas déclaratif : `BillingV2LaunchScope` le refuse sur le
chemin de dispatch, avec les codes `BILLING_V2_SCOPE_*`.

---

## D. Drapeaux Billing V2

| Variable | Avant lancement | Validation | Premier client | Rollback |
| --- | --- | --- | --- | --- |
| `BILLING_V2_NEW_SUBSCRIPTIONS_ENABLED` | `false` | `true` | `true` | `false` |
| `BILLING_V2_AUTHORITATIVE_CHECKOUT_ENABLED` | `false` | `true` | `true` | `false` |
| `BILLING_V2_FIRST_REAL_SUBSCRIPTION_APPROVED` | `false` | `false` | `true` | `false` |
| `BILLING_V2_PROVIDER_OUTBOX_ENABLED` | `false` | `true` | `true` | `false` |
| `BILLING_V2_PROVIDER_EXECUTOR_ENABLED` | `false` | `true` | `true` | `false` |
| `BILLING_V2_REFUNDS_ENABLED` | `false` | `false` | `false` | `false` |
| `BILLING_V2_RECONCILIATION_WORKER_ENABLED` | `false` | `true` | `true` | `false` |
| `BILLING_V2_RECONCILIATION_INTERVAL_SECONDS` | — | `300` | `300` | — |
| `BILLING_V2_PROVISIONING_ENABLED` | `false` | `false` | `true` | `false` |
| `BILLING_V2_ADDITIONAL_USER_PROVISIONING_ENABLED` | `false` | `false` | `true` | `false` |
| `BILLING_V2_CATALOG_SHADOW_MODE` | `true` | `true` | `true` | `true` |
| `BILLING_V2_PROVISIONING_SHADOW_MODE` | `true` | `true` | `true` | `true` |
| `STRIPE_MODE` | `test` | `test` | `live` | `test` |
| `PAYPAL_MODE` | `disabled` | `disabled` | `disabled` | `disabled` |
| `BPCE_INTEGRATION_MODE` | `mock` | `mock` | `live` | `mock` |
| `STRIPE_WEBHOOK_VERIFY` | `true` | `true` | `true` | `true` |

`BILLING_V2_FIRST_REAL_SUBSCRIPTION_APPROVED` est le dernier verrou : il ne
passe à `true` qu'au moment d'accepter le premier vrai client.
`BILLING_V2_REFUNDS_ENABLED` reste fermé tant que le refund Stripe n'a pas été
validé sur MariaDB/Stripe et qu'un avoir BPCE canonique n'est pas disponible
pour les événements déjà documentés. Il n'est jamais un droit client.
Le tarif de validation du premier abonnement reel est un mecanisme temporaire,
ferme par defaut, qui ne modifie jamais le catalogue. Il est controle par :

- `BILLING_V2_FIRST_REAL_TEST_PRICING_ENABLED` ;
- `BILLING_V2_FIRST_REAL_TEST_CUSTOMER_ID` ;
- `BILLING_V2_FIRST_REAL_TEST_PRESET_CODE` ;
- `BILLING_V2_FIRST_REAL_TEST_SELECTION_FINGERPRINT` ;
- `BILLING_V2_FIRST_REAL_TEST_DISCOUNT_BPS` ;
- `BILLING_V2_FIRST_REAL_TEST_EXPECTED_TOTAL_CENTS`.

Quand le gate vaut `true`, l override ne peut s appliquer qu au client cible,
sur Stripe, en selection native `pack-pro-association`/`FLEX`/`monthly`. Pour le
client cible, toute divergence de scope, de configuration ou de total attendu
echoue avant la creation de l abonnement. Le `discount_basis_points_snapshot`
et le price lock portent ensuite le meme prix contractuel pour Stripe, les
documents et les renouvellements. Couper le gate juste apres l ancrage du
checkout de validation ; le snapshot de l abonnement reste autoritaire.

Apres validation du premier abonnement reel, remettre
`BILLING_V2_FIRST_REAL_TEST_PRICING_ENABLED=false` puis retirer de la
configuration runtime les six parametres `BILLING_V2_FIRST_REAL_TEST_*`.
Le snapshot de prix deja persiste reste autoritaire pour cet abonnement ; la
suppression des parametres runtime empeche toute reutilisation accidentelle du
tarif de validation.


Le gate `BILLING_V2_ADDITIONAL_USER_PROVISIONING_ENABLED` est volontairement
independant du provisioning general. Lorsqu'il vaut `true` avec
`BILLING_V2_PROVISIONING_ENABLED=false`, seules les mutations USER-ADDITIONAL
et leur worker de convergence `koxo_pending`/`directory_ready` sont ouverts.
Le provisioning general reste ferme. En rollback, remettre ce gate a `false`
et redemarrer API-INTERNAL.

Rappel SRV-13 : corriger un réglage **aux deux endroits** —
`C:\ProgramData\Kermaria\api-internal.config.json` et
`<repo-parent>/kermaria-client-platform.local.env.ps1` — sinon la régénération
l'annule. Redémarrage du service après toute modification.

---

## E. Ordre de déploiement

1. Sauvegarde MariaDB (A.1) et relevé de `schema_migrations` (A.2).
2. Migration 062 avec `kermaria_migrator` (A.3), puis vérifications (A.4).
3. **SRV-13** (API-INTERNAL) : publier avec `-p:UseAppHost=true`, sauvegarder
   le dossier existant en `api-internal-old-<horodatage>`, poser la
   configuration, redémarrer `KermariaApiInternal`, contrôler `/health`.
4. **SRV-12** (WEBPORTAL) : livrer en `.tar.gz` — **jamais** `.zip` —, créer
   `.next/cache` propriétaire `kermaria-web`, basculer le symlink, redémarrer
   `kermaria-webportal.service`, contrôler la page publique réelle.
5. Poser le webhook Stripe **live** et son secret sur le WEBPORTAL.
6. Passer les drapeaux en colonne « Validation », vérifier la readiness admin.
7. Basculer `STRIPE_MODE=live` puis
   `BILLING_V2_FIRST_REAL_SUBSCRIPTION_APPROVED=true` — dernier geste.

Le séquencement SRV-13 puis SRV-12 est impératif.

---

## F. Runbook — premier vrai client

Ordre imposé. Ne pas passer à l'étape suivante sans la preuve.

### 1. Client

- **Preuve** : compte portail actif, `customers.is_demo = false`.
- **Où** : table `customers`, table `portal_users`.
- **Si échec** : ne pas créer le client à la main en base ; passer par le
  parcours d'inscription et l'approbation admin.

### 2. Checkout

- **Preuve** : réponse `BILLING_V2_AUTHORITATIVE_CHECKOUT_READY`, un
  `billing_v2_subscriptions` en `pending_approval`, un BillingEvent
  `cycle_sequence = 1` au montant attendu.
- **Où** : `billing_v2_subscriptions`, `billing_v2_billing_events`, journal
  `BillingV2CheckoutReadinessService`.
- **Si échec** : lire le code `BILLING_V2_*`. `PROVIDER_PRICE_MAPPING_INCOMPLETE`
  = mappings live manquants (B). `SCOPE_*` = hors périmètre, ne pas contourner.

### 3. Paiement Stripe

- **Preuve** : Checkout Session `status=complete` **et**
  `payment_status=paid`, montant et devise identiques au BillingEvent.
- **Où** : Dashboard Stripe live, `billing_v2_provider_checkout_sessions`.
- **Si échec** : ne rien forcer localement. Une session `complete` sans `paid`
  ne prouve rien.

### 4. Settlement local

- **Preuve** : `billing_v2_payment_attempts.status = succeeded`,
  `settled_amount_cents` = `expected_amount_cents`, devise identique ;
  BillingEvent en `settlement_status = settled`.
- **Où** : `billing_v2_payment_attempts`, `billing_v2_billing_events`.
- **Si échec** : si le webhook n'est pas arrivé, laisser le réconciliateur
  converger (il relit l'état réel chez Stripe). En cas de
  `reconciliation_required`, **ne pas** marquer payé à la main : rapprocher
  d'abord montant, devise, client et abonnement.

### 5. BillingEvent

- **Preuve** : un seul événement pour `cycle_sequence = 1`, `finalized`,
  lignes cohérentes avec l'offre.
- **Où** : `billing_v2_billing_events`, `billing_v2_billing_event_lines`.
- **Si échec** : un doublon de cycle est un incident bloquant. Ne pas
  supprimer ; ouvrir une revue manuelle.

### 6. Document / facture

- **Preuve** : `billing_v2_subscription_documents` en `issued` avec
  `cycle_sequence = 1` et `billing_event_id` renseigné ; BillingEvent en
  `document_status = issued` ; une seule ligne dans
  `billing_v2_document_issuance_attempts`.
- **Où** : ces trois tables, plus `commercial_documents`.
- **Si échec** : l'émission est idempotente et rejouable. En cas de blocage
  BPCE, l'état reste indéterminé → revue manuelle, jamais une seconde facture.

### 7. Portail

- **Preuve** : abonnement visible, `billingSystem = billing_v2`,
  `rail = stripe`, prix et statut corrects, `paidCyclesCount = 1`, aucun
  doublon.
- **Où** : `/internal/portal/subscriptions` via le BFF, écran client.
- **Si échec** : vérifier la projection avant de toucher aux données.

### 8. Droits

- **Preuve** : services attendus actifs, rattachés à l'abonnement, sans
  doublon.
- **Où** : `/internal/portal/services`.

### 9. Provisioning

- **Preuve** : apres settlement et activation, les items techniques convergent
  automatiquement ; `billing_v2_subscription_item_provisioning` doit refleter
  `provisioned` sans `last_error`, les quotas KoXo doivent etre verifies sur le
  stockage effectif et les appartenances AD doivent correspondre aux droits V2.
- **Ou** : `billing_v2_subscription_item_provisioning`, readiness client,
  journaux API-INTERNAL/KoXo, FSRM et AD. L'endpoint admin natif V2 de reconcile
  sert uniquement au replay controle et idempotent d'un abonnement deja actif.
- **Si echec** : rester fail-closed ; ne jamais marquer un item `provisioned` a
  la main. Corriger readiness/provider KoXo/AD puis rejouer la reconciliation.
  Un echec KoXo non prouve restaure la fiche pre-repair afin qu'un retry ne
  puisse pas devenir un faux `NOOP`.

---

## G. Runbook — premier renouvellement réel

1. **Avant l'échéance** : vérifier `billing_anchor_at` non nul et
   `renews_at` cohérent.
2. **Signal** : `invoice.paid` reçu sur le webhook live. La metadata
   d'abonnement est portée par `parent.subscription_details.metadata` — si le
   rail V2 ne s'active pas, c'est le premier point à contrôler.
3. **Cycle** : un BillingEvent `cycle_sequence = 2` doit apparaître, au prix
   **contractuel**, jamais au prix catalogue courant.
4. **Règlement** : tentative propre au cycle 2, montant et devise vérifiés.
5. **Document** : `renewal_subscription_invoice` avec `cycle_sequence = 2`.
6. **Portail** : `paidCyclesCount = 2`.

Contrôle de non-duplication après coup :

```sql
SELECT subscription_id, cycle_sequence, COUNT(*) AS n
FROM billing_v2_billing_events
WHERE event_type = 'renewal_charge'
GROUP BY subscription_id, cycle_sequence
HAVING n > 1;
-- attendu : aucune ligne
```

En cas d'échec de paiement : l'abonnement passe en
`payment_state = payment_attention`, reste `active`, aucun document n'est
marqué payé, aucun déprovisionnement. La relance est **manuelle** pour cette
version.

---

## H. Contrôles manuels du lancement

À exécuter quotidiennement pendant la phase de lancement.

```sql
-- 1. Abonnements en défaut de paiement.
SELECT id, payment_state_reason_code, payment_state_changed_at
FROM billing_v2_subscriptions
WHERE payment_state <> 'current';

-- 2. Réconciliation Stripe en revue manuelle.
SELECT id, billing_event_id, failure_reason_code, reconciliation_attempts
FROM billing_v2_payment_attempts
WHERE status = 'reconciliation_required';

-- 3. Émission documentaire bloquée (BPCE indéterminé).
SELECT commercial_document_id, status, attempt_count
FROM billing_v2_document_issuance_attempts
WHERE status NOT IN ('succeeded');

-- 4. Charge réglée sans document émis.
SELECT event_row.id, event_row.subscription_id, event_row.cycle_sequence
FROM billing_v2_billing_events event_row
WHERE event_row.settlement_status = 'settled'
  AND event_row.document_status <> 'issued';

-- 5. Tentative anormalement longue en vol (> 24 h).
SELECT id, billing_event_id, status, created_at, reconciliation_attempts
FROM billing_v2_payment_attempts
WHERE status IN ('created', 'in_flight')
  AND created_at < DATE_SUB(UTC_TIMESTAMP(6), INTERVAL 24 HOUR);
```

Le journal du réconciliateur donne la métrique d'exploitation directe :

```
Billing V2 reconciliation run: pending=… reconciled=… failed=… reconciliation_required=…
```

Sur SRV-13, filtrer les journaux sur `"LogLevel":"(Error|Warning|Critical)"` —
jamais sur `Error|Exception`, chaque ligne contenant `"Exception":null`. La
« Référence » affichée côté interface est le `correlation_id`.

Ne jamais journaliser jeton, cookie, chaîne de connexion ni montant complet de
facture.

---

## I. Etat production apres ouverture du 2026-08-19

Etat valide en production apres le premier abonnement Billing V2 reel et la recette USER-ADDITIONAL :

- API-INTERNAL deployee : `89ae2fff38697a1438e162b64afb2c0ada7a2226` ;
- release publique webportal : `5102618da865abc2931bc6ad5486204877f7cdac` ;
- `BILLING_V2_NEW_SUBSCRIPTIONS_ENABLED=true` ;
- `BILLING_V2_AUTHORITATIVE_CHECKOUT_ENABLED=true` ;
- `BILLING_V2_FIRST_REAL_SUBSCRIPTION_APPROVED=true` ;
- `BILLING_V2_PROVIDER_OUTBOX_ENABLED=true` ;
- `BILLING_V2_PROVIDER_EXECUTOR_ENABLED=true` ;
- `BILLING_V2_PROVISIONING_ENABLED=true` ;
- `BILLING_V2_ADDITIONAL_USER_PROVISIONING_ENABLED=true` ;
- le tarif de recette first-real reste desactive : `BILLING_V2_FIRST_REAL_TEST_PRICING_ENABLED` absent ou `false` ;
- readiness admin : `BILLING_V2_ADMIN_READY_FOR_FIRST_SUBSCRIPTION` ;
- compteur de souscriptions legacy reelles bloquantes : `0`, verifie contre MariaDB persistante ;
- readiness provisioning du client de recette : `ready_for_v2_provisioning=1`, `add_only_mode=1`, shadow `success`, match legacy `1`, mismatch non resolu `0` ;
- les huit items actifs du premier abonnement V2 reel sont `provisioned` sans `last_error` ;
- le replay admin natif du premier abonnement est idempotent : `PROVISIONING_UNCHANGED`, avec le droit VPN deja present ;
- un cycle USER-ADDITIONAL reel a converge jusqu'a `ready` avec lien AD durable ;
- le devis non mutant de la configuration Pro/FLEX de recette retourne `4850` centimes, remise `0`, `checkoutAvailable=true`, `checkoutMode=native` ;
- le mode initial de provisioning reste strictement add-only. Cette ouverture ne doit pas etre utilisee pour automatiser des retraits ou suppressions.

Le packager webportal doit toujours inclure le repertoire vide `apps/webportal/.next/cache/` dans l'archive standalone. Sans ce repertoire, le service systemd peut echouer au demarrage avant que Next.js puisse initialiser son cache runtime.

Tunnel public Billing V2 valide :
`vitrine -> choix formule/options -> inscription -> activation du compte -> connexion/session -> paiement -> provisioning de la formule`.

La selection Billing V2 complete est persistee comme codes catalogue/options dans le snapshot JSON du signup, jamais comme montant client. Elle est revalidee/rechiffree par le serveur avant inscription puis avant checkout. `/formules/reprendre` exige une session client et conserve `next=/formules/reprendre` si la session est absente ou expiree.

Le lifecycle de suppression/remplacement d'utilisateur et toute migration ulterieure restent hors de ce runbook tant qu'ils ne sont pas valides separement.
