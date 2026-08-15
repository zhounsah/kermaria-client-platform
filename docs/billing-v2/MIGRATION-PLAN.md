# Plan de migration

## Phase 0 — Audit

Aucune modification fonctionnelle.

Cartographier tout le code qui dépend de :

- `commercial_offers` ;
- `public_pack_code` ;
- `technical_service_references` ;
- `provisioning_group_sam_account_names` ;
- Stripe Price IDs ;
- PayPal Plan IDs ;
- checkout ;
- abonnements ;
- factures ;
- provisioning ;
- back-office ;
- pages publiques.

## Phase 1 — Tests legacy

Ajouter les tests de non-régression avant toute refactorisation.

État dépôt : fait pour les chemins Billing legacy critiques, avec correction préalable des défauts d'idempotence Stripe return, PayPal webhook retry et provisioning AD.

## Phase 2 — Schéma V2 additif

Créer uniquement les nouvelles tables.

Aucune lecture production depuis V2.

État dépôt : en cours via `047_billing_v2_schema_dormant.sql`, migration additive sans seed catalogue et sans lecture applicative `billing_v2_*`.

## Phase 3 — Catalogue / presets / mappings

Insérer le catalogue, les tiers, prix, presets et mappings legacy.

État dépôt : fait en seed dormant `048_billing_v2_catalog_seed.sql` pour services, tiers, dépendances, engagements, options engagement × paiement, prix versionnés V1, presets, mappings techniques legacy et mappings des 20 PACK-*.

## Phase 4 — Compatibility layer

Introduire un service applicatif central pour empêcher le reste du code de lire directement la structure SQL métier.

État dépôt : fait pour les chemins lecture catalogue critiques via `IBillingCatalog` et `LegacyBillingCatalogAdapter`; `commercial_offers` reste autoritaire.

## Phase 5 — Shadow mode

Comparer :

- comportement legacy attendu ;
- calcul V2 équivalent lorsque le contrat est destiné à être identique.

Ne pas considérer comme anomalie un changement tarifaire V2 volontaire.

État dépôt : shadow catalogue disponible et désactivé par défaut. `ShadowBillingCatalogAdapter` retourne toujours le legacy et compare V2 uniquement si `BILLING_V2_CATALOG_SHADOW_MODE=true`.

## Phase 5.5 — Moteur pricing V2 pur

État dépôt : fait avec `BillingV2PricingEngine`. Le moteur calcule en centimes entiers :

- sous-totaux récurrents éligibles/non éligibles ;
- remise globale par basis points ;
- arrondi entier cohérent avec les totaux candidats ;
- plancher contractuel 45 % ;
- paiement comptant sur toute la période d'engagement ;
- prorata mensuel ;
- complément upfront sans remboursement automatique ;
- price locks `monthly_recurring` et `upfront_prepaid` ;
- usage des snapshots de prix d'items.

Le moteur est enregistré en DI mais n'est pas encore appelé par le checkout, les factures ou le worker de renouvellement legacy.

## Phase 5.75 — Verrou de prix legacy

État dépôt : fait via `049_subscription_billing_price_locks.sql`.

Le renouvellement legacy conserve `commercial_offers` comme rattachement métier, mais utilise désormais un verrou contractuel d'abonnement pour le prix et la TVA. Sans lock actif, la facture de renouvellement est bloquée et le cas est marqué pour revue, afin d'éviter tout fallback silencieux sur `commercial_offers.price_amount_cents`.

Les créations et activations d'abonnement créent ce verrou depuis l'offre résolue au moment de la souscription.

La migration backfill les abonnements legacy non terminés uniquement depuis une ligne historique fiable associée à l'abonnement (`commercial_documents.subscription_id` ou `commercial_document_line_subscriptions`). Elle ne lit pas `commercial_offers.price_amount_cents` comme preuve contractuelle.

Si aucune ligne historique exploitable n'existe, aucun lock n'est inventé : l'abonnement est inscrit dans `subscription_billing_price_lock_review_required` avec `missing_reliable_historical_price` pour revue manuelle.

## Phase 5.8 — Shadow provisioning V2

État dépôt : fait pour la comparaison AD uniquement.

`BillingV2ProvisioningShadowService` reconstruit les groupes attendus depuis les références techniques legacy réelles et les mappings/règles V2 dormants. Le legacy reste autoritaire et le résultat V2 n'est utilisé que pour logger les écarts lorsque `BILLING_V2_PROVISIONING_SHADOW_MODE=true`.

`ClientServiceCatalogService` reste également legacy autoritaire pour les droits visibles du portail, mais déclenche une comparaison shadow V2 des références techniques exposées au client. Les droits ponctuels historiques marqués `legacy_one_time_entitlement` sont détectés explicitement au lieu d'être transformés en item récurrent V2.

Les règles V2 dormantes couvrent `ACCES-VPN -> GG_VPN`, `ACCES-RDS -> GG_RDS` et préparent les règles de quota Nextcloud à partir de la valeur numérique des tiers. Aucun provisioning AD ou Nextcloud n'est exécuté depuis V2 à cette phase.

## Phase 6 — Nouveaux abonnements V2

Les nouveaux clients peuvent utiliser V2.

Les contrats legacy actifs restent inchangés.

État dépôt : amorcé sous flag `BILLING_V2_NEW_SUBSCRIPTIONS_ENABLED=false`.

Quand le flag est activé sur une base persistante avec les migrations V2 présentes, une nouvelle souscription legacy réussie peut matérialiser un contrat local V2 de même id dans `billing_v2_subscriptions`, avec ses `billing_v2_subscription_users`, `billing_v2_subscription_items` et `billing_v2_subscription_item_provisioning`.

Limite volontaire : Stripe et PayPal restent branchés au legacy à cette étape. Aucun `billing_v2_provider_price_mappings` n'est utilisé et V2 ne crée ni ne pilote encore un abonnement fournisseur.

## Phase 6.5 — Accords provider V2 locaux

État dépôt : premier jalon local uniquement.

Lorsqu'une nouvelle souscription legacy est matérialisée en V2, `BillingV2NewSubscriptionService` délègue à `BillingV2ProviderAgreementService` l'enregistrement transactionnel de l'identifiant d'abonnement fournisseur déjà obtenu par le flux legacy dans `billing_v2_payment_agreements`.

Cette étape ne déclenche aucun appel Stripe/PayPal, ne modifie aucun ancien Stripe Price ID / PayPal Plan ID et ne change pas le checkout. Elle prépare seulement l'idempotence locale par abonnement fournisseur via `050_billing_v2_payment_agreement_idempotency.sql`.

`BillingV2ProviderAgreementService` expose aussi une vérification fail-closed des mappings `billing_v2_provider_price_mappings`. Le futur checkout V2 devra prouver que chaque `service_price_id` requis possède exactement un identifiant provider actif pour le rail/environnement demandé avant de créer quoi que ce soit chez Stripe ou PayPal.

## Phase 6.6 — Commande outbox checkout provider V2

État dépôt : préparé localement, sans worker provider actif.

`052_billing_v2_outbox_idempotency.sql` ajoute une clé `idempotency_key_hash` unique à `billing_v2_outbox_events`, avec requête de précondition anti-doublons.

`BillingV2ProviderCheckoutCommandService` peut préparer un événement local `billing_v2.provider_checkout.create_requested` pour une souscription V2 déjà créée, seulement si la gate de checkout autoritaire est passée. L'insert outbox et l'audit `billing_v2_audit_log` sont dans la même transaction. Un retry pour le même couple abonnement/provider/environnement retrouve la même clé d'idempotence.

`BillingV2ProviderOutboxWorker` est conditionné à `BILLING_V2_PROVIDER_OUTBOX_ENABLED=true`. Même avec ce flag, `BillingV2ProviderOutboxDispatcher` refuse tout traitement tant que `BILLING_V2_PROVIDER_EXECUTOR_ENABLED=false` ou que la configuration provider requise manque.

Quand ces verrous sont ouverts, le dispatcher lit uniquement les événements `billing_v2.provider_checkout.create_requested` disponibles, les traite par lots bornés et applique une politique retry-safe : succès vers `processed`, échec conservé en `pending` avec délai de retry et diagnostic.

Avant tout appel externe, le dispatcher revendique l'événement localement en `processing` avec une expiration courte. Un second worker qui a lu le même événement ne peut donc pas appeler Stripe/PayPal. Si le worker tombe pendant l'appel, le `processing` expiré redevient revendicable pour retry avec la même clé d'idempotence.

`BillingV2ProviderCheckoutExecutor` contient les builders dédiés aux requêtes Stripe Checkout Session et PayPal Subscription, avec `Idempotency-Key` / `PayPal-Request-Id`. Il n'est pas actif par défaut : le DI fournit `DisabledBillingV2ProviderCheckoutExecutor` tant que le flag executor est off.

`053_billing_v2_provider_checkout_sessions.sql` conserve localement et idempotemment le résultat provider (`provider_checkout_id`, `provider_subscription_id`, `approval_url`) avant que l'événement outbox soit marqué traité. Si PayPal retourne déjà un abonnement fournisseur, le dispatcher inscrit aussi l'accord local dans `billing_v2_payment_agreements`.

Après l'insert idempotent, le dispatcher relit la session locale avec verrou `FOR UPDATE` et compare le résultat provider courant aux IDs/URL déjà matérialisés. Un replay strictement identique reste no-op. Un replay qui change `provider_checkout_id`, `provider_subscription_id`, l'URL d'approbation, le provider, l'environnement ou l'abonnement local est marqué `failed` avec `BILLING_V2_PROVIDER_CHECKOUT_SESSION_CONFLICT` pour revue humaine ; il n'écrase jamais la session locale et ne crée pas d'accord provider contradictoire.

Le même invariant vaut pour les événements entrants : un retour/webhook dont le checkout ou l'abonnement provider contredit l'état local est refusé (`BILLING_V2_PROVIDER_CHECKOUT_ID_CONFLICT` / `BILLING_V2_PROVIDER_SUBSCRIPTION_ID_CONFLICT`) et ne peut pas activer l'abonnement local. Les événements déjà `processed` restent strictement idempotents. Un replay déjà `processed` ne retente le provisioning que si le `reason_code` stocké correspond à une activation d'abonnement provider ; un simple retour checkout traité ne déclenche pas de reconcile. Les événements `failed` ou `skipped` repassent en `processing` au retry et rafraîchissent le type, les IDs provider et le payload stocké avant retraitement, afin que l'audit conserve le dernier essai réellement exploité.

Cette phase ne marque aucun événement outbox comme traité par défaut et ne crée pas encore d'URL d'approbation provider dans le runtime normal.

## Phase 6.8 — Gate provisioning V2 réel

État dépôt : préparé, désactivé par défaut.

`BILLING_V2_PROVISIONING_ENABLED=true` n'autorise jamais seul une action V2. `BillingV2ProvisioningService` est fail-closed et ne peut appeler le `ProvisioningService` existant que si toutes les conditions sont vraies pour le client :

- tous les abonnements actifs concernés sont matérialisés en V2 ;
- les règles et tiers V2 requis sont résolus sans ambiguïté ;
- le dernier shadow est `success`, sans mismatch legacy/V2 ;
- aucun mismatch non résolu n'est enregistré ;
- les groupes AD cibles sont résolus ;
- `billing_v2_provisioning_client_readiness.ready_for_v2_provisioning = 1`.

Un item d'abonnement V2 actif sans ligne `billing_v2_subscription_item_provisioning` n'est jamais ignoré : il devient une règle non résolue, ce qui ferme la gate au lieu de produire un provisioning partiel.

Le mode initial est `add_only_mode = 1`. Dans ce mode, V2 ne transmet au `ProvisioningService` que les groupes désirés, donc aucun retrait de droit legacy n'est possible lors de la première activation.

Les quotas Nextcloud peuvent être calculés depuis les règles `nextcloud_*`, mais aucun changement réel de quota n'est exécuté : le dépôt ne contient pas encore de provider opérationnel fiable pour modifier Nextcloud.

## Phase 7 — Activation du premier vrai nouvel abonnement

Information de pilotage : aucun vrai client production avec abonnement actif n'est présumé exister à migrer. La stratégie ne doit donc pas se complexifier autour d'une migration massive théorique.

Avant toute décision humaine d'activation Billing V2, exécuter en lecture seule `READINESS-CHECKS.sql` sur les données concernées. Le résultat attendu est `real_customer_subscription_count = 0`; les comptes `customers.is_demo = TRUE` et les comptes d'essai/démo ne sont pas des contrats clients réels. Un client revenu à `is_demo = FALSE` doit en revanche être compté comme réel.

État dépôt : gate préparée, branchée uniquement derrière flags et désactivée par défaut.

`BillingV2CheckoutReadinessService` compose la vérification read-only des abonnements existants et la readiness des mappings `billing_v2_provider_price_mappings`. Même si aucun client réel n'est à migrer, Billing V2 ne peut être autorisé pour le premier vrai nouvel abonnement que si :

- `BILLING_V2_AUTHORITATIVE_CHECKOUT_BFF_ENABLED=true` côté BFF ;
- `BILLING_V2_NEW_SUBSCRIPTIONS_ENABLED=true` ;
- `BILLING_V2_AUTHORITATIVE_CHECKOUT_ENABLED=true` ;
- `BILLING_V2_FIRST_REAL_SUBSCRIPTION_APPROVED=true` après validation humaine ;
- `BILLING_V2_PROVIDER_OUTBOX_ENABLED=true` ;
- `BILLING_V2_PROVIDER_EXECUTOR_ENABLED=true` pour obtenir une session provider réelle ;
- `real_customer_subscription_count = 0` dans le snapshot read-only ;
- `verifiedAgainstPersistentSql = true` dans ce même snapshot ; un fallback sans SQL n'est pas une preuve ;
- les données de démo ne sont pas comptées comme contrats réels ;
- `/admin/billing-v2` expose un snapshot admin `BILLING_V2_ADMIN_READY_FOR_FIRST_SUBSCRIPTION` ;
- chaque `service_price_id` requis possède exactement un mapping provider actif pour le rail et l'environnement demandés.
- le chemin document/facture V2 est prêt : BPCE n'est pas désactivé, les tables `billing_v2_subscription_documents` et `billing_v2_document_line_snapshots` existent, et la création documentaire V2 ne dépend ni d'une ligne `subscriptions` ni d'une ligne `commercial_offers` artificielle.

Quand le flag BFF reste off, la route publique `/api/subscriptions/create` conserve le chemin legacy. Quand il est explicitement on, le BFF appelle `/internal/portal/billing-v2/subscriptions/checkout` : la base locale reste source de vérité, la demande est idempotente, la création locale V2 et l'outbox sont atomiques, puis l'URL provider n'est exposée que si une session provider locale a déjà été matérialisée. Si la session provider est encore en attente, le bouton public retente de façon bornée avec la même `Idempotency-Key`; le BFF ne déclenche toujours pas Stripe/PayPal directement.

La création locale V2 matérialise aussi un `billing_v2_subscription_price_locks` actif dans la même transaction. En paiement mensuel, le lock porte le MRR payable calculé à la souscription ; en paiement upfront, il porte le montant récurrent prépayé jusqu'à la fin de l'engagement. Les snapshots d'items restent conservés, mais le lock donne le verrou contractuel global utilisé par les futurs renouvellements/changements.

La clé d'idempotence du checkout V2 est unique par client et liée à une empreinte des paramètres métier stockés (`customer_id`, acteur, provider, environnement, offre legacy). Un retry avec la même clé mais une intention différente est refusé par `BILLING_V2_AUTHORITATIVE_CHECKOUT_IDEMPOTENCY_CONFLICT` au lieu de réutiliser silencieusement une demande existante.

Après retour provider, le portail client continue de lire `/internal/portal/subscriptions`. Cet endpoint fusionne désormais une projection V2 read-only pour les abonnements autoritaires V2 sans créer de ligne legacy `subscriptions`. Le prix affiché vient du `billing_v2_subscription_price_locks` actif, ou à défaut des snapshots `subscription_items` déjà matérialisés ; il ne relit pas un prix catalogue courant. Les souscriptions V2 de shadow legacy restent exclues de cette projection si une ligne legacy de même identifiant existe.

Après activation locale par retour/webhook provider V2, `BillingV2DocumentIssuerService` matérialise idempotemment un document commercial d'origine `billing_v2` et des lignes sans `offer_id`. La liaison à l'abonnement V2 passe par `billing_v2_subscription_documents`, jamais par `commercial_documents.subscription_id` qui reste une FK legacy vers `subscriptions`. Les lignes émises vers BPCE portent le montant net HT figé ; `billing_v2_document_line_snapshots` conserve pour revue les items, quantités, prix unitaires bruts, remise allouée, taxes, montant final, devise et période. Un replay retrouve le document existant via l'unicité `(subscription_id, document_kind, period_start, period_end)` et ne crée pas de deuxième facture.

Le catalogue client `/internal/portal/service-catalog` fusionne également les droits V2 autoritaires issus des `subscription_items`. Cette lecture ne déclenche aucune action AD/Nextcloud : elle expose uniquement les droits achetés comme sources de services, en préférant les références techniques legacy mappées pour garder la continuité des libellés.

Le centre de téléchargements conserve ses règles ciblées existantes. `BillingV2DownloadAccessProjection` ajoute au scope client les `public_pack_code` / références d'offres associées à la demande de checkout V2 autoritaire et les groupes AD issus des règles V2 actives, uniquement pour des abonnements V2 actifs et non doublés par une ligne legacy.

L'administration suit les abonnements V2 depuis `/admin/billing-v2`, pas depuis la liste legacy `/admin/subscriptions`. L'endpoint dédié `/internal/admin/billing-v2/subscriptions` est read-only et exclut les jumeaux legacy, ce qui évite d'alimenter les workers legacy avec des lignes V2.

Les routes BFF de résiliation client et admin restent explicitement legacy-only. Si une souscription projetée porte `billingSystem = "billing_v2"`, elles renvoient `BILLING_V2_CANCELLATION_NOT_AVAILABLE` avant tout appel Stripe/PayPal et avant toute mutation locale legacy. La résiliation V2 devra passer par un flux dédié, audité et idempotent, probablement outbox/provider, ou par une décision humaine documentée pendant la toute première activation.

Si cette preuve reste vraie, Billing V2 peut devenir le système autoritaire du premier vrai nouvel abonnement, après tests et validation humaine. Le legacy reste disponible et les mécanismes de price lock restent conservés comme garde architectural.

## Phase 8 — Migration progressive éventuelle

Seulement si la vérification read-only découvre des contrats clients réels actifs. Dans ce cas, migration au renouvellement ou selon une procédure explicite.

## Phase 9 — Retrait du legacy

Seulement lorsque :

```text
0 abonnement actif dépend du legacy
0 chemin de facturation lit directement commercial_offers
0 provisioning dépend d'un pack legacy
```

## Rollback

La procédure applicative détaillée est dans `ROLLBACK.md`.

Résumé opérationnel :

- fermer d'abord `BILLING_V2_AUTHORITATIVE_CHECKOUT_BFF_ENABLED`,
  `BILLING_V2_AUTHORITATIVE_CHECKOUT_ENABLED`,
  `BILLING_V2_NEW_SUBSCRIPTIONS_ENABLED`,
  `BILLING_V2_PROVIDER_OUTBOX_ENABLED`,
  `BILLING_V2_PROVIDER_EXECUTOR_ENABLED` et
  `BILLING_V2_PROVISIONING_ENABLED` ;
- ne supprimer aucune table V2 et ne réécrire aucune ligne legacy ;
- conserver les outbox, provider events, payment agreements et price locks comme
  traces d'audit ;
- ne jamais convertir automatiquement une subscription V2 activée en
  subscription legacy ;
- ne jamais retirer automatiquement un droit AD/Nextcloud pendant le rollback
  initial ;
- exiger une revue humaine si une session provider ou un paiement V2 a déjà
  existé.
