# Plan de tests Billing V2

## Catalogue

Vérifier l'unicité des codes services, tiers et prix.

Vérifier les dépendances :

```text
BACKUP-PERSONAL 128
nécessite
STORAGE-PERSONAL 128
dans le même scope utilisateur
```

## Presets

Vérifier les prix sans remise :

```text
Dossier = 11,90 €
Accès = 15,80 €
Bureau = 36,70 €
Pro = 48,50 €
```

## Remises

Vérifier :

```text
6 mois monthly = -10 %
12 mois monthly = -15 %
6 mois upfront = -15 %
12 mois upfront = -20 %
```

## Plancher

Exemple :

```text
MRR initial après remise = 40 €
plancher 45 % = 18 €
configuration courante = 11 €
facture = 18 €
```

## Upfront

Cas :

```text
128 Go payé jusqu'au 31/12
provisionné à 64 Go
retour à 128 Go
=> 0 € supplémentaire
```

Puis :

```text
128 Go payé
upgrade 256 Go
=> supplément uniquement sur la différence et la période restante
```

## Legacy

Chaque PACK-* doit résoudre vers le bon :

```text
preset
engagement
payment_mode
```

Les items historiques doivent être construits depuis `technical_service_references`, pas depuis `preset_items`.

## Provisioning

Tester l'idempotence :

```text
add VPN
event retry
=> utilisateur présent une seule fois dans le bon groupe AD
```

Tester suppression, réessai, panne partielle et reprise.

## Facturation

Tester les changements :

- premier jour de période ;
- dernier jour ;
- milieu de période ;
- mois de 28/29/30/31 jours ;
- renouvellement ;
- annulation ;
- webhook reçu deux fois ;
- échec fournisseur ;
- rollback applicatif.

## Readiness premier abonnement V2

Tester :

- flag BFF off => checkout public legacy ;
- flags API off => refus explicite ;
- validation humaine absente => refus explicite ;
- outbox provider checkout V2 fermée => checkout V2 non autorisable ;
- executor provider Stripe/PayPal V2 fermé => checkout V2 non autorisable ;
- abonnement réel actif hors démo => refus explicite ;
- abonnements démo uniquement => pas traités comme contrats réels ;
- compteur `real_customer_subscription_count = 0` non vérifié contre SQL persistant => refus explicite ;
- mapping provider manquant ou ambigu => refus explicite ;
- document/facture V2 non pret => refus explicite ;
- document/facture V2 pret => la gate peut s'ouvrir seulement si les autres conditions sont vraies ;
- BFF checkout V2 sans `Idempotency-Key` explicite => refus, aucune clé inventée depuis `correlationId` ;
- retry checkout V2 avec même `Idempotency-Key` mais offre/client/provider/environnement différent => conflit explicite, aucune réutilisation silencieuse ;
- bouton public checkout V2 recevant `BILLING_V2_CHECKOUT_PENDING_PROVIDER_SESSION` => retries bornés avec la même `Idempotency-Key`, sans appel Stripe/PayPal direct depuis le BFF ;
- executor Stripe/PayPal avec faux HTTP local => en-têtes d'idempotence, Price IDs/Plan IDs résolus et parsing des URLs d'approbation ;
- outbox provider concurrente => claim local `processing` avant appel externe, `processing` non expiré non revendicable, `processing` expiré revendicable pour retry ;
- session provider locale déjà matérialisée => replay strictement identique accepté, IDs/URL divergents refusés avec `BILLING_V2_PROVIDER_CHECKOUT_SESSION_CONFLICT` sans overwrite ;
- provider event V2 déjà `processed` => aucun overwrite ; provider event V2 `failed`/`skipped` => retry autorisé avec rafraîchissement du payload stocké ;
- replay provider event V2 déjà `processed` => provisioning retenté uniquement si le `reason_code` stocké est une activation d'abonnement ;
- page `/admin/billing-v2` => affichage lecture seule du snapshot, sans mutation ni action provider ;
- retry checkout V2 avec même clé d'idempotence => même demande locale et même session provider locale si elle existe.
- checkout V2 autoritaire local => subscription/items/provisioning/outbox/audit et price lock contractuel dans la même transaction.
- `/internal/portal/subscriptions` après checkout V2 autoritaire => abonnement V2 visible via projection read-only, sans création de ligne `subscriptions` legacy.
- projection portail V2 => prix issu du price lock actif ou des snapshots d'items matérialisés, jamais du catalogue courant ; lignes shadow legacy exclues pour éviter les doublons.
- `/internal/portal/service-catalog` après abonnement V2 autoritaire => droits V2 visibles depuis les `subscription_items`, références techniques legacy mappées préférées, aucun effet AD/Nextcloud.
- centre de téléchargements après abonnement V2 actif => accès ciblé par pack/offre/groupe résolu depuis le scope V2 read-only, sans accès global implicite.
- `/admin/billing-v2` => souscriptions V2 autoritaires visibles en lecture seule via endpoint dédié, sans les exposer à `GetAdminSubscriptionsAsync` legacy ni aux workers legacy.

- résiliation V2 (suite `--billing-v2-cancellation` et `verify-billing-v2-lifecycle-contract.mjs`) => ancre résolue sur les trois sources autoritaires, conflit et ancre manquante sur contrat récurrent en échec fermé, `suspended` avec période payée courante résilié au terme, fin de terme PayPal suspendue puis réellement résiliée au terme, appel refusé sur désaccord d'environnement d'exécution, `cancelled` local seulement après acceptation d'un geste terminal.

## Documents / factures V2

Tester :

- creation de document V2 sans `subscriptions` legacy ni `commercial_offers` artificielle ;
- lignes documentaires sans `offer_id`, avec snapshots V2 complets ;
- items, quantites, prix unitaires, remise, taxes, montant final, devise et periode conserves sans relecture catalogue ;
- price lock contractuel prioritaire sur les snapshots d'items si les montants divergent ;
- retry apres activation provider => meme document et pas de deuxieme facture ;
- BPCE disabled ou schema documentaire incomplet => readiness hard blocker.

## Cœur financier (Phase 1)

Schéma / migration, sur MariaDB locale jetable uniquement :

- migration `057` appliquée sur base vierge => tables, colonnes et contraintes présentes ;
- second passage de la migration => idempotent, aucune erreur, aucun doublon ;
- `version` présent sur `billing_v2_subscriptions`, valeur initiale déterministe `1` ;
- `billing_v2_subscription_changes` porte l'intention idempotente ;
- insertion violant `total != net + tax` => rejetée par la base ;
- insertion avec devise vide ou montant négatif interdit => rejetée par la base ;
- réutilisation d'une `idempotency_key_hash` => rejetée par la base ;
- réutilisation d'un `provider_request_key` sur le même provider/environnement => rejetée ;
- deux documents V2 pour un même `billing_event_id` => rejetés.

Invariants applicatifs, en tests purs :

- événement `finalized` sans ligne => refus ;
- somme des lignes différente des totaux de l'événement => refus ;
- ligne dans une devise différente de l'événement => refus ;
- ligne négative dans un événement `debit` => refus ;
- transition `draft → finalized`, `draft → void`, `finalized → void` => autorisées ;
- transition depuis `void` ou vers `draft` => refus ;
- `void` avec settlement réussi => refus ;
- `void` avec document légal émis => refus ;
- réutilisation d'une clé d'idempotence après `void` => refus ;
- settled == expected => succès ; écart de montant ou de devise => `amount_mismatch` ;
- appel provider sans PaymentAttempt persistée => refus ;
- retry provider => même ligne, même clé provider, jamais de nouvelle clé ;
- compare-and-swap Subscription avec version périmée => conflit explicite, jamais no-op ;
- prix applicable ambigu avec versions distinctes => résolution versionnée explicite ;
- prix applicable ambigu à versions égales => fail closed, jamais de somme ;
- plancher d'engagement mensuel => 45 % du MRR initial remisé, identique sur les deux chemins ;
- `customer.subscription.created` / `customer.subscription.updated` => signal inerte,
  aucune activation, aucun document, aucun provisioning.

## Rollback

Tester :

- désactivation des flags V2 après préparation locale => aucune nouvelle action provider ;
- événement outbox provider en échec => reste rejouable ;
- retour/webhook Stripe ou PayPal rejoué après échec => traitement idempotent ;
- retour/webhook provider V2 avec `provider_checkout_id` ou `provider_subscription_id` contradictoire => refus explicite, aucun passage local en actif ;
- webhook Stripe/PayPal marqué V2 => rattachement par abonnement local V2 (`billing_v2_subscription_id` / `custom_id`) avant fallback legacy ;
- activation provider traitée + échec provisioning post-commit => événement provider conservé traité, provisioning retentable ;
- provisioning V2 réel désactivé => legacy reste autoritaire ;
- item V2 actif sans état `subscription_item_provisioning` => règle non résolue, aucune action AD ;
- première activation provisioning en add-only => aucun retrait de groupe legacy.
