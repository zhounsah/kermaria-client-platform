# Billing V2 — Zachary IT

> Current status - 2026-08-26: Billing V2 / V2.1 is the sole commercial authority in production. Migrations 070/071 removed the legacy commercial model in v2.0.0.0. The admin catalog redesign is deployed in v2.0.0.2. Read `../CURRENT_STATE.md` and `../BILLING_V2_ONLY.md` before using the implementation diary below.
>
> Sections describing dormant flags, shadow adapters, legacy mappings or legacy checkout are historical implementation notes unless the current code/docs explicitly say otherwise.

## V2.1

V2.1 introduit le droit contractuel unique avec composantes tarifaires, le
catalogue Zachary IT et le fulfillment distinct du provisioning. Voir
[V2.1.md](V2.1.md). Les migrations 066–069 restent dormantes tant que les flags
et readiness V2.1 ne sont pas explicitement ouverts.

## Objectif

Faire évoluer la facturation actuelle, centrée sur des packs figés, vers un moteur modulaire :

- un socle obligatoire ;
- des services récurrents indépendants ;
- des paliers explicites de stockage et de VPN ;
- des utilisateurs et espaces partagés ;
- des presets commerciaux simples ;
- des engagements et modes de règlement séparés du produit ;
- un moteur de prix indépendant de Stripe et PayPal ;
- un provisioning piloté par les services ;
- une migration legacy éventuelle seulement si la preuve read-only trouve de vrais contrats actifs.

## Principe central

Un preset commercial n'est pas un contrat.

Le contrat réel est un abonnement composé de `subscription_items`.

Le prix récurrent est calculé à partir des services réellement souscrits, puis la réduction contractuelle est appliquée globalement.

## État

- Schéma SQL V2 : intégré comme migration applicative additive et dormante (`047_billing_v2_schema_dormant.sql`).
- Catalogue V2 : défini et seedé en migration dormante (`048_billing_v2_catalog_seed.sql`).
- Prix candidats : définis et seedés comme prix versionnés V1.
- Presets V2 : définis et seedés.
- Mapping des 20 offres legacy : défini.
- Shadow catalogue V2 : disponible via `V2BillingCatalogAdapter` et `ShadowBillingCatalogAdapter`, désactivé par défaut et activable par `BILLING_V2_CATALOG_SHADOW_MODE=true`.
- Moteur pricing V2 : disponible comme composant pur `BillingV2PricingEngine`, testé, non branché aux flux legacy.
- Shadow provisioning V2 : disponible via `BillingV2ProvisioningShadowService`, désactivé par défaut et activable par `BILLING_V2_PROVISIONING_SHADOW_MODE=true`; il compare uniquement les groupes AD calculés et ne déclenche aucune action AD/Nextcloud.
- Nouveaux abonnements V2 : matérialisation locale disponible via `BillingV2NewSubscriptionService`, désactivée par défaut et activable par `BILLING_V2_NEW_SUBSCRIPTIONS_ENABLED=true`. Le checkout V2 autoritaire n'est tentable depuis le BFF public que si `BILLING_V2_AUTHORITATIVE_CHECKOUT_BFF_ENABLED=true`; sinon le checkout legacy reste le chemin utilisé.
- Couche provider V2 : premier jalon dédié via `BillingV2ProviderAgreementService`, `BillingV2ProviderCheckoutCommandService`, `BillingV2ProviderInboundEventService` et l'outbox provider. Les mappings `billing_v2_provider_price_mappings` sont vérifiables en readiness. Les requêtes Stripe/PayPal sont préparées avec idempotence, mais l'executor réel est remplacé par `DisabledBillingV2ProviderCheckoutExecutor` par défaut.
- Readiness checkout V2 autoritaire : préparée via `BillingV2CheckoutReadinessService` et exposée en lecture seule dans l'administration par `/admin/billing-v2` (`/api/admin/billing-v2/readiness` côté BFF, `/internal/admin/billing-v2/readiness` côté API interne). Elle exige une preuve read-only qu'aucun vrai abonnement client actif n'existe, une validation humaine explicite, un schéma V2 complet, des mappings provider complets, un chemin document/facture V2 prêt et des flags provider cohérents avant d'autoriser le premier vrai nouvel abonnement V2. Un simple flag global n'est jamais suffisant. Si des abonnements réels bloquants existent, le snapshot admin expose leurs lignes SQL de revue sans mutation.
- Documents/factures V2 : `056_billing_v2_document_issuance.sql` ajoute les liens documentaires V2 et les snapshots financiers de lignes. `BillingV2DocumentIssuerService` crée des documents `origin='billing_v2'` sans `offer_id` et sans `subscriptions` legacy, puis réutilise `InvoiceIssuingService`/BPCE après commit local. Les retries retrouvent le document existant et ne créent pas de double facture.
- Visibilité portail du premier abonnement V2 : `BillingV2PortalSubscriptionProjection` fusionne en lecture seule les souscriptions V2 issues de `billing_v2_authoritative_checkout_requests` dans `/internal/portal/subscriptions`, sans créer de ligne `subscriptions` legacy. Les lignes V2 déjà matérialisées en shadow pour un abonnement legacy existant sont exclues par présence d'une ligne legacy homonyme, afin d'éviter tout doublon.
- Catalogue de services client : `BillingV2ClientServiceEntitlementProjection` ajoute en lecture seule les droits issus des `billing_v2_subscription_items` autoritaires à `/internal/portal/service-catalog`. Les références techniques legacy mappées restent préférées pour conserver les libellés et regroupements existants ; en absence de mapping, le code service V2 reste visible comme fallback explicite.
- Téléchargements client : `BillingV2DownloadAccessProjection` complète en lecture seule le scope d'accès avec les cibles équivalentes du premier abonnement V2 actif (`public_pack_code`, référence offre et groupes AD issus des règles V2). Les ressources ciblées par pack/offre/groupe restent donc disponibles sans créer de subscription legacy ni élargir la visibilité à tous les clients.
- Administration Billing V2 : `/admin/billing-v2` expose les souscriptions V2 autoritaires via `/internal/admin/billing-v2/subscriptions`, en lecture seule. Cette vue ne réutilise pas `ISubscriptionService.GetAdminSubscriptionsAsync`, afin que les workers legacy de renouvellement et d'annulation ne traitent jamais une souscription V2 comme une ligne legacy.
- Résiliation V2 : les routes BFF client/admin délèguent à API-INTERNAL, seul détenteur des identifiants fournisseur. L'ancre provider est résolue sur les trois sources autoritaires (`billing_v2_payment_agreements`, `billing_v2_provider_checkout_sessions`, `billing_v2_payment_attempts` réglées) et échoue en fermé sur conflit ou sur absence d'ancre pour un contrat à cadence `monthly`. Un statut local `cancelled` n'est posé qu'après acceptation d'un geste terminal par l'opérateur ; une fin de terme PayPal suspend immédiatement puis résilie réellement au terme via un second événement d'outbox daté de `current_period_ends_at`. Aucun appel ne part si l'environnement persisté diffère de celui réellement chargé dans le processus.
- Outbox checkout provider V2 : préparée via `BillingV2ProviderCheckoutCommandService` et `052_billing_v2_outbox_idempotency.sql`. Une future demande de checkout V2 pourra créer un événement `billing_v2.provider_checkout.create_requested` et un audit dans la même transaction, avec clé d'idempotence stable. Le worker `BillingV2ProviderOutboxWorker` est enregistré uniquement si `BILLING_V2_PROVIDER_OUTBOX_ENABLED=true` et reste fail-closed tant que `BILLING_V2_PROVIDER_EXECUTOR_ENABLED=false`. `BillingV2ProviderCheckoutExecutor` prépare les requêtes Stripe/PayPal idempotentes, mais l'executor réel est remplacé par `DisabledBillingV2ProviderCheckoutExecutor` par défaut.
- Refund core Stripe V2 : `082_billing_v2_refund_core.sql` ajoute `BillingV2Refund`, relié à un `BillingEvent` settled et à sa `PaymentAttempt`. L'intention et l'outbox sont transactionnelles ; le worker relit Stripe avant toute confirmation et ne passe l'événement à `refunded` qu'après preuve montant/devise/PaymentIntent. `BILLING_V2_REFUNDS_ENABLED=false` par défaut garde toute exécution externe fermée et n'accorde aucun droit client. Les événements déjà documentés restent refusés tant qu'un avoir BPCE canonique n'est pas livré.
- Durcissement documentaire du refund : [REFUND-CORE-HARDENING.md](REFUND-CORE-HARDENING.md) décrit l'intention d'avoir durable, sa clé d'idempotence refund/document original, ses reprises BPCE et les preuves qui conditionnent toute activation.
- Cœur financier V2 (Phase 1) : `057_billing_v2_financial_core.sql` introduit
  `billing_v2_billing_events`, `billing_v2_billing_event_lines`,
  `billing_v2_payment_attempts`, l'optimistic locking sur
  `billing_v2_subscriptions.version` et l'évolution de
  `billing_v2_subscription_changes` en intention idempotente persistante.
  Spécification complète dans `FINANCIAL-CORE.md`. Ces tables sont **dormantes** :
  contraintes et testées, écrites par aucun flux de production. Le branchement
  des flux provider/documentaire sur ce cœur est l'objet de la Phase 2.
- Politiques financières pures (Phase 1) : `BillingV2BillingEventPolicy`,
  `BillingV2BillingEventStateMachine`, `BillingV2SettlementPolicy`,
  `BillingV2PaymentAttemptPolicy`, `BillingV2SubscriptionVersionPolicy`,
  `BillingV2ServicePriceResolutionPolicy`, `BillingV2CommitmentFloorPolicy`.
  Elles portent les invariants que MariaDB ne peut pas exprimer en `CHECK`.
- Rail Stripe V2 (Phase 2) : `058_billing_v2_stripe_financial_rail.sql` +
  `BillingV2StripeRail`, `BillingV2StripeGateway`, `BillingV2StripeRailService`,
  `BillingV2FinancialCoreStore`. Le checkout Stripe V2 suit désormais
  SubscriptionChange → BillingEvent finalized → PaymentAttempt → Stripe →
  refetch → settlement vérifié → activation. Le montant est transmis en
  `price_data` inline : aucun `price_id` externe ne détermine plus le total.
  Spécification dans `STRIPE-RAIL.md`. Reste fail-closed par défaut
  (`BILLING_V2_PROVIDER_EXECUTOR_ENABLED=false` ⇒ passerelle désactivée).
  PayPal V2 n'est pas branché.
- Customer Credit Ledger : **non implémenté**. Prérequis de la phase suivante
  pour les downgrades mensuels avec avoir.
- Migration de production : NON exécutée.
- Audit du code applicatif : réalisé avant intégration.
- Tests legacy : renforcés et à conserver verts pendant toute la migration.
- Frontière de lecture catalogue : `IBillingCatalog`, avec `LegacyBillingCatalogAdapter` autoritaire.
- Table legacy réelle : `commercial_offers`.
- Renouvellement legacy : protégé par `subscription_billing_price_locks` (`049_subscription_billing_price_locks.sql`) ; les documents de renouvellement utilisent le lock contractuel actif et ne fallbackent plus sur `commercial_offers.price_amount_cents`. Sans lock actif, le cas est inscrit dans `subscription_billing_price_lock_review_required` et la facture n'est pas créée silencieusement.
- Hypothèse de migration : aucun vrai client production avec abonnement actif n'est présumé à migrer. Avant toute décision d'activation, vérifier en lecture seule `READINESS-CHECKS.sql`; les comptes `is_demo=true` ne sont pas des contrats clients réels, mais un client non-démo reste compté comme réel même s'il porte une trace historique de conversion.
- Readiness données : `real_customer_subscription_count = 0` n'est utilisable que si le snapshot porte `verifiedAgainstPersistentSql=true`. Une absence de configuration SQL ou une lecture non vérifiée ne vaut jamais preuve d'absence de contrats réels.
- Provisioning V2 réel : préparé uniquement derrière `BILLING_V2_PROVISIONING_ENABLED=false` et une readiness explicite par client (`billing_v2_provisioning_client_readiness`). Le mode initial est add-only et le legacy reste autoritaire si une condition échoue. Après activation locale par provider inbound, le provisioning V2 peut être retenté uniquement via `BillingV2ProvisioningService`, la gate de readiness et `ProvisioningService`; aucun appel AD direct n'est ajouté.
- USER-ADDITIONAL : le cycle identite dispose d'un gate dedie `BILLING_V2_ADDITIONAL_USER_PROVISIONING_ENABLED`, independant du provisioning general. Quand seul ce gate est ouvert, un worker borne reprend uniquement les cycles `koxo_pending` et `directory_ready` jusqu'a `ready`; `BILLING_V2_PROVISIONING_ENABLED` peut rester `false`.

## Fichiers SQL

- `001_schema.sql` : proposition de schéma complet.
- `002_presets_legacy_mapping.sql` : proposition de seed catalogue, presets et mappings.
- `apps/api-internal/Migrations/MariaDb/047_billing_v2_schema_dormant.sql` : migration applicative additive, sans seed et sans activation V2.
- `apps/api-internal/Migrations/MariaDb/048_billing_v2_catalog_seed.sql` : seed V2 dormant, sans lecture ni mutation de `commercial_offers`.
- `apps/api-internal/Migrations/MariaDb/049_subscription_billing_price_locks.sql` : verrou contractuel legacy additif pour empêcher le repricing silencieux au renouvellement.
- `apps/api-internal/Migrations/MariaDb/050_billing_v2_payment_agreement_idempotency.sql` : contrainte additive d'idempotence sur les abonnements fournisseur V2 locaux, avec requête préalable de détection des doublons.
- `apps/api-internal/Migrations/MariaDb/051_billing_v2_provisioning_readiness.sql` : readiness explicite par client pour empêcher toute action V2 par simple flag global.
- `apps/api-internal/Migrations/MariaDb/052_billing_v2_outbox_idempotency.sql` : clé additive d'idempotence pour les événements outbox provider/provisioning V2.
- `apps/api-internal/Migrations/MariaDb/053_billing_v2_provider_checkout_sessions.sql` : stockage local idempotent des sessions checkout provider V2 et URLs d'approbation.
- `apps/api-internal/Migrations/MariaDb/054_billing_v2_provider_inbound_events.sql` : journal idempotent des événements entrants Stripe/PayPal V2, rejouable après échec.
- `apps/api-internal/Migrations/MariaDb/055_billing_v2_authoritative_checkout_requests.sql` : requêtes locales idempotentes de checkout V2 autoritaire pour préparer le premier vrai nouvel abonnement sans créer de subscription legacy.
- `apps/api-internal/Migrations/MariaDb/056_billing_v2_document_issuance.sql` : liaison additive entre une souscription V2 et un document commercial/BPCE, avec snapshots financiers de lignes V2.
- `apps/api-internal/Migrations/MariaDb/058_billing_v2_stripe_financial_rail.sql` : rail Stripe additif — cadence sur les lignes d'événement, liens intention/tentative, trace de vérification, et mappings provider ramenés au rôle de référence (`amount_authority='local'`).
- `apps/api-internal/Migrations/MariaDb/057_billing_v2_financial_core.sql` : cœur financier additif et dormant — `billing_events`, `billing_event_lines`, `payment_attempts`, `version` sur `subscriptions`, intention idempotente sur `subscription_changes`, et liens `billing_event_id` sur les tables checkout/session/document existantes.
- `READINESS-CHECKS.sql` : requêtes read-only de pré-activation, hors migrations, pour prouver l'absence de vrais abonnements clients actifs à migrer.
- `ROLLBACK.md` : procédure de rollback applicatif Billing V2 par flags, outbox, provider events et provisioning fail-closed.

Ne pas exécuter en production avant audit du schéma réel, sauvegarde, test en environnement de développement et revue des migrations.
