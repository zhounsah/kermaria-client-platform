# Autorité commerciale unique — Billing V2 / V2.1

Statut : **deployed in production**.

The Billing V2-only cutover was completed with migrations 070/071 in `v2.0.0.0` on 2026-08-25. The redesigned admin catalog is deployed in `v2.0.0.2` on 2026-08-26. This document is the commercial-authority reference; legacy cart/configurator/offer documents are historical.

See also [`CURRENT_STATE.md`](CURRENT_STATE.md) and [`releases/V2.0.0.2.md`](releases/V2.0.0.2.md).

Le socle Billing V2 lui-même reste décrit par
[`v1.4/V1.4.0.0_BILLING_V2.md`](v1.4/V1.4.0.0_BILLING_V2.md) : invariants
financiers, axes de statut, ancre de facturation, rails provider.

## Ce que le refactor établit

**Une seule autorité commerciale.** Le catalogue Billing V2 décrit ce qui est
vendable ; `BillingV2PricingEngine` est le seul à établir un montant ; le
checkout autoritaire est le seul à engager un rail de paiement. Il n'existe
plus de second catalogue, plus d'adaptateur de compatibilité, plus de mode
ombre comparant deux sources.

## Avant / après

### Avant

```
commercial_offers ──┬─> IBillingCatalog ──> LegacyBillingCatalogAdapter
                    │                        ShadowBillingCatalogAdapter
                    ├─> cart_items ────────> panier one-shot ──> document
                    ├─> recurring_checkout_items ──> subscriptions
                    ├─> subscriptions.commercial_offer_id
                    ├─> commercial_document_lines.offer_id
                    └─> billing_v2_legacy_offer_mappings ──> Billing V2

billing_v2_services ──> BillingV2PricingEngine ──> checkout autoritaire
```

Deux descriptions concurrentes de la même offre, réconciliées par une table de
correspondance et un comparateur d'ombre. Le prix pouvait venir de l'une ou de
l'autre selon le chemin emprunté.

### Après

```
billing_v2_services
  ├─ billing_v2_service_tiers
  ├─ billing_v2_service_prices          (versionné, immuable)
  ├─ billing_v2_service_fulfillment_profiles
  └─ billing_v2_offer_presets
       └─ billing_v2_preset_items
            + billing_v2_commitment_terms
              billing_v2_commitment_payment_options
                        │
                        v
              BillingV2PricingEngine        <- seule autorité de montant
                        │
                        v
              checkout autoritaire          <- seule autorité d'engagement
                        │
                        v
              Stripe (price_data inline) / autres rails
```

Les documents commerciaux (devis, factures) sont sortis de cette chaîne : ils
n'en dépendent plus du tout (voir « Documents commerciaux » ci-dessous).

## Parcours public

- `/diagnostic` produit une `BillingV2PublicSelection` et sort vers
  `/formules/{code}`.
- `/formules` et `/formules/{code}` remplacent l'ancien `/configurer` : la page
  compose une sélection, le devis vient de `/api/formules/devis`, calculé par le
  pricing engine.
- `/souscrire` propose les formules publiées **et** une composition service par
  service (`BillingV2DirectSubscribe`), sans preset ni engagement
  (`presetCode: null`, `commitmentCode: "FLEX"`).
- `/signup` reprend une sélection V2 revalidée côté serveur.

Invariant structurel : `BillingV2PublicSelection` **ne porte aucun montant** —
uniquement des codes catalogue et des quantités. Le navigateur ne peut donc pas
devenir une seconde autorité financière. Un test par réflexion échoue si un
champ de montant y est réintroduit.

## Administration

| Écran | Rôle |
|---|---|
| `/admin/catalog` | Listes métier du catalogue V2 : services, formules et engagements |
| `/admin/catalog/services/{id}` | Fiche service : essentiel, paliers, tarification et commercialisation |
| `/admin/catalog/formules/{id}` | Fiche formule : essentiel, composition et aperçu commercial calculé côté serveur |
| `/admin/catalog/engagements/{id}` | Fiche engagement : essentiel, modes de règlement et remises |
| `/admin/catalog/integrations` | État Stripe/PayPal et mappings externes avancés ; Stripe conserve `price_data` inline |
| `/admin/billing-v2` | Écran d'exploitation : abonnements, outbox, réconciliation, drapeaux |
| `/admin/public-pack-catalog` | Vitrine éditoriale des formules (textes, fiches techniques) |

Les anciennes pages `/admin/catalog/new` et `/admin/catalog/[id]`, les composants
`AdminCatalogOfferForm` / `AdminCatalogOfferStatusToggleButton`, les routes BFF
`/api/admin/catalog*` et les routes API `/internal/admin/catalog*` sont
supprimés.

Les onglets de fiche sont des liens adressables (`?tab=...`). Les codes et les
paramètres structurants sont définis à la création puis présentés en lecture
seule. Les créations de service et de palier sont toujours inactives et non
publiques ; leur publication reste une action d'édition explicite.

### Prix : versionnement strict

`billing_v2_service_prices` est **immuable**. Un prix en vigueur n'est jamais
modifié en place : publier un nouveau tarif ferme atomiquement l'ancienne
version (`valid_until = T`) et ouvre la version `N+1` (`valid_from = T`), avec
`supersedes_price_id` renseigné. Deux fenêtres actives ne peuvent pas se
chevaucher pour un même `(service, palier, devise, cadence, déclencheur)`.

Conséquence directe : une refacturation ne peut pas réécrire un montant déjà
facturé. Le montant qui fait foi reste celui du `BillingEvent`, jamais celui du
catalogue courant ni celui du provider.

### Formules

Le total d'une formule n'est **jamais** stocké comme vérité métier. Il est
recalculé par le pricing engine à partir de `billing_v2_preset_items` et des
prix en vigueur.

## Documents commerciaux

Devis et factures **survivent** au retrait du catalogue et en deviennent
indépendants. Une ligne de document est un **instantané** : libellé,
description, quantité, prix unitaire, taux de taxe, totaux. Elle ne porte plus
`offer_id`.

Le catalogue ne sert plus qu'à **pré-remplir** un formulaire de ligne, via
`CatalogLineTemplate` — une valeur copiée, qui transporte `priceCode` pour la
traçabilité mais aucun identifiant stockable. Une révision tarifaire ne peut
donc pas réécrire un devis déjà émis.

Le règlement d'un document ne déclenche **pas** de provisioning : en V2, c'est
l'événement provider qui active l'abonnement, et l'activation qui déclenche la
réconciliation. Un document est une trace comptable, pas un ordre de
provisioning.

## Migrations

| Migration | Contenu |
|---|---|
| `070_billing_v2_catalog_administration.sql` | Additive. Colonnes d'administration du catalogue V2 et **index** servant le contrôle applicatif de non-chevauchement des fenêtres tarifaires. |
| `071_drop_legacy_commercial_model.sql` | **Destructive.** Retire les colonnes et tables du modèle historique. |

`071` supprime notamment `commercial_offers`, `cart_items`,
`recurring_checkout_items`, `billing_v2_legacy_offer_mappings`, ainsi que les
colonnes `subscriptions.commercial_offer_id`,
`commercial_document_lines.offer_id`,
`billing_v2_authoritative_checkout_requests.legacy_offer_id` et
`source_legacy_offer_id`.

`071 n'a pas de rollback SQL.` Une sauvegarde préalable
(`npm run backup:mariadb`) est bloquante. Après application,
`SELECT ... FROM commercial_offers;` échoue — c'est l'objectif.

MariaDB ne sait pas exprimer déclarativement « pas deux fenêtres actives qui
se chevauchent ». `070` n'ajoute donc **aucune contrainte SQL** de ce type :
elle pose un index, et le contrôle est appliqué par
`BillingV2CatalogAdministrationService` avant chaque écriture de prix, dans la
même transaction que la révision.

### Règles de visibilité des téléchargements

`071` ne se contente pas de renommer `target_type`. La référence legacy et le
code Billing V2 sont deux vocabulaires distincts — `STOCK-PERSO-32` devient
`STORAGE-PERSONAL`, `SUPPORT-LV1` devient `SUPPORT-STANDARD`. La migration
**traduit** les valeurs via `billing_v2_legacy_service_mappings` (posée par
`048`) avant de supprimer cette table, puis **refuse de s'appliquer** si une
règle reste sans équivalent. Un simple renommage aurait rendu la ressource
invisible pour ses ayants droit, silencieusement : `DownloadService` est
fail-closed.

Deux `mapping_kind` sont exclus de la traduction automatique :
`storage_increment` (traduire élargirait la visibilité) et
`legacy_one_time_entitlement` (aucun service V2 correspondant). Dans ces cas la
migration refuse plutôt que de deviner.

### Préflight obligatoire

[`billing-v2/PREFLIGHT-070-071.sql`](billing-v2/PREFLIGHT-070-071.sql) rassemble
les requêtes **en lecture seule** à passer sur la base cible avant d'appliquer
quoi que ce soit : volumétrie des tables supprimées, règles de visibilité à
arbitrer, chevauchements tarifaires préexistants, clés étrangères réellement
présentes. Les `SIGNAL SQLSTATE '45000'` de `071` sont un filet de sécurité ;
le préflight est le plan.

`BillingV2LaunchReadinessService` vérifie en lecture seule, via
`information_schema`, que ces tables ont bien disparu.

## Ce qui reste volontairement en place

- **Les migrations historiques `001` à `069`.** Elles décrivent un état
  réellement appliqué en base. Les réécrire pour effacer le mot
  `commercial_offers` falsifierait l'historique.
- **Les assertions `doesNotMatch` des scripts de contrat.** Elles citent les
  noms legacy précisément pour garantir qu'ils ne reviennent pas.
- **`PUBLIC_PACKS` dans `packages/shared`.** C'est le manifeste éditorial des
  formules (textes, audience, slug) : il ne porte aucun prix et n'a jamais été
  une source tarifaire.

## Vérification

```bash
npm run validate
```

Suites spécifiques :

```bash
npm run test:billing
```

```bash
npm --prefix apps/webportal run test:formules
```

```bash
npm --prefix apps/webportal run test:commercial
```
