# Mémoire commune — Kermaria / Zachary IT

> Source commune pour Codex et Claude Code. Lire d'abord ce fichier puis les topics pertinents.
> Les secrets ont été expurgés lors de la fusion du 2026-08-15.

## Règles de vérité

- Le code, les tests, la configuration réellement lue et l'état live priment sur cette mémoire.
- Les informations d'exploitation datées doivent être revalidées avant modification ou déploiement.
- `archive/` est un historique de recherche, **pas** la vérité courante.
- Voir [SHARED_MEMORY_POLICY.md](SHARED_MEMORY_POLICY.md).

## État courant / informations récentes

### Version

- **v1.4.0.1 (2026-08-20)** : diagnostic public migre vers Billing V2, sans changement SQL/pricing/provisioning. Voir [billing-v2-public-diagnostic.md](topics/billing-v2-public-diagnostic.md). Version de base Billing V2 : v1.4.0.0.

### Vitrine, SEO et éditorial

- **Référence la plus récente importée : v1.3.3.4, vérifiée en production le 2026-08-11.** Routage canonique `www` / `dashboard` / `administration`, métadonnées, robots/sitemap, favicon et vraie 404 ont été validés.
- `/ressources` est le hub public des pages SEO publiées ; `/solutions` est volontairement `noindex`.
- Le topic Claude [seo-vitrine-state.md](topics/seo-vitrine-state.md) décrit v1.1.12 au 2026-08-06 et doit donc être traité comme **historique** pour la version/état de déploiement.
- Pour le détail récent, rechercher `editorial platform` ou `canonicalisation` dans [archive/codex-task-groups](archive/codex-task-groups/).

### Déploiement / topologie

- Topologie dédiée importée : SRV-11 = edge/TLS ; SRV-12 = Ubuntu/Next webportal ; SRV-13 = Windows/.NET API ; SRV-16 = collecteur Veeam ; SRV-21 = KoXo. **Revalider les IP, services et rôles avant une opération destructive.**
- Référence détaillée : [deployment-topology.md](topics/deployment-topology.md).
- Pour SRV-12, une readiness privée ne suffit pas : vérifier aussi le symlink/release actif et la page publique exacte.
- Pour une release Kermaria, préserver le séquencement SRV-13 puis SRV-12, les sauvegardes/staging swaps et les preuves post-déploiement.

### hCaptcha / email

- Le snapshot Claude [hcaptcha-signup-state.md](topics/hcaptcha-signup-state.md) mentionne d'anciennes clés DUMMY du 2026-07-06. **Cette partie est périmée** : la mémoire Codex indique que la paire hCaptcha a été remplacée sur SRV-12/SRV-13 le 2026-08-01 et que le SMTP live a été testé avec succès.
- Référence SMTP : [smtp-ovh-live-config.md](topics/smtp-ovh-live-config.md). Les valeurs de secrets ne doivent jamais être mémorisées.

### KoXo / AD

- Les topics Claude du 2026-08-05/06 restent les références détaillées pour les comportements mesurés de KoXo : accents, groupes primaires, orphelins, fiche utilisateur, maîtrise du mot de passe et adoption AD.
- Références : [koxo-accents-majuscules.md](topics/koxo-accents-majuscules.md), [koxo-groupes-primaires-separes.md](topics/koxo-groupes-primaires-separes.md), [koxo-orphelins-supprimes.md](topics/koxo-orphelins-supprimes.md), [koxo-fiche-utilisateur-maitre.md](topics/koxo-fiche-utilisateur-maitre.md), [koxo-ad-password-mastery.md](topics/koxo-ad-password-mastery.md), [koxo-api-ne-cree-plus.md](topics/koxo-api-ne-cree-plus.md).
- **Deux routes sur le récepteur SRV-21, portées incomparables** :
  `/internal/koxo/sync/` est **globale** (une ligne absente du CSV désactive le
  compte) ; `/internal/koxo/storage/reconcile/` ne touche **qu'un** objet KoXo.
  Ne jamais utiliser la première pour poser un quota. Détail :
  [billing-v2-koxo-storage-targets.md](topics/billing-v2-koxo-storage-targets.md).
- **Un client payant ordinaire a déjà son `customer_ad_links` avant l'export CSV** (`ListExportCandidatesAsync` l'exige) ; seul un essai `demo_kind = 'trial'` part sans lien et est créé par KoXo. Le stockage personnel Billing V2 **ne crée aucune identité** — le code affirmait le contraire jusqu'au 2026-08-17. Détail : [billing-v2-koxo-storage-targets.md](topics/billing-v2-koxo-storage-targets.md).
- Une mémoire Codex plus récente (preuve datée 2026-08-10) couvre la synchro SRV-13→SRV-21, le remapping des groupes AD vers le domaine enfant et la release 1.0.0.8. Elle précise que code/config/TCP étaient prouvés mais qu'aucun nouveau POST signé / replay KoXo n'avait été exécuté : conserver cette limite de preuve.

### Temps / timezone

- Référence : [timezone-utc-convention.md](topics/timezone-utc-convention.md). DB en UTC, API ISO `Z`, affichage Europe/Paris, dates fiscales = jour civil Paris.
- Revalider le garde-fou `test:timezone` après toute modification SQL ou C# touchant aux horodatages.

### Diagnostic public Billing V2 (2026-08-20)

- /diagnostic produit d├®sormais une BillingV2PublicSelection ou `requires_quote`, demande le devis via /api/formules/devis, puis sort vers /formules/{preset} avec la s├®lection compl├¿te. Aucun SQL/pricing/provisioning ajout├®. R├®f├®rence : [billing-v2-public-diagnostic.md](topics/billing-v2-public-diagnostic.md).

### Billing V2 (v1.4.0.0)

- **Livré en v1.4.0.0.** Le code est dormant par défaut (drapeaux absents =
  `false`), mais **la production ne l'est pas** : relevé du 2026-08-17,
  `NEW_SUBSCRIPTIONS`, `AUTHORITATIVE_CHECKOUT`, `PROVIDER_OUTBOX`,
  `PROVIDER_EXECUTOR` et `RECONCILIATION_WORKER` sont à `true`, `STRIPE_MODE`
  et `PAYPAL_MODE` à `live`. Seul `FIRST_REAL_SUBSCRIPTION_APPROVED=false`
  bloque encore — il est évalué **avant** tout appel sortant, donc aucun objet
  Stripe n'est créé. Mais la phase de validation prévue en `STRIPE_MODE=test`
  a été sautée : lever ce verrou ferait passer de « rien » à « argent réel »
  sans palier. Référence : `docs/v1.4/V1.4.0.0_BILLING_V2.md`.
- **Provisioning du stockage KoXo câblé de bout en bout (2026-08-17)** : plan →
  résolution stricte → route ciblée SRV-21 → quota vérifié → *seulement ensuite*
  les droits AD. Un stockage bloqué ou échoué empêche VPN/RDS du même
  utilisateur. Reste inaccessible en production par
  `BILLING_V2_PROVISIONING_ENABLED=false`, non touché. Sans
  `BILLING_V2_KOXO_STORAGE_URL`/`_TOKEN`, le provider est dormant et bloque tout
  quota — aucun repli silencieux. **Une réduction de quota n'est jamais
  appliquée.** La vérification FSRM existe mais n'a **pas** été validée en réel :
  la preuve courante s'arrête à `xml_verified`.
- Invariants à ne pas casser : `BillingEvent` immuable = autorité du montant
  (pas le provider) ; trois axes de statut orthogonaux ; identité de
  renouvellement = `(subscription_id, cycle_sequence)` ; `billing_anchor_at`
  matérialisé à l'activation et **jamais recalculé** ; le webhook est un
  signal, le refetch provider est la convergence.
- Rail Stripe en `price_data` **inline** : aucun `price_id` externe, donc
  aucune dépendance à `billing_v2_provider_price_mappings` pour un checkout V2.
- Pièges base réelle invisibles des suites mock : MySqlConnector matérialise
  `CHAR(36)` en **`Guid`** (`reader.GetString` lève) ; MariaDB accepte
  plusieurs `NULL` dans un index UNIQUE (d'où `cycle_sequence NOT NULL` en
  migration 062) ; un seul lecteur ouvert par connexion.
- Migrations `046` à `063`, additives. **`062` n'a pas de rollback SQL** : le
  rollback est applicatif, par les drapeaux. Sauvegarde préalable bloquante.
- Avant lancement, à trancher : `PAYPAL_MODE=live` en production alors que le
  périmètre exige `disabled` ; aucune variable `BILLING_V2_*` posée
  explicitement en production ; rotation des secrets de pilotage.

### Paiements / facturation

- [bpce-invoicing-api.md](topics/bpce-invoicing-api.md) : intégration BPCE ; aucun token ne doit vivre dans la mémoire. L'environnement de la clé y était **supposé** production et non confirmé : ne pas transformer cette hypothèse en fait.
- [paypal-v022-gotchas.md](topics/paypal-v022-gotchas.md) et [v0.35-cart.md](topics/v0.35-cart.md) conservent les pièges historiques utiles.
- La mémoire Codex rappelle qu'une configuration live/readiness ne prouve pas un paiement/webhook réellement exécuté.

### Sauvegardes / Veeam

- La mémoire Codex du 2026-08-08 documente le collecteur Veeam, le mapping business KoXoDATA et une release v1.1.14 alors partiellement déployée. **Le statut de déploiement est historique : revalider `/backups` et la release active avant toute conclusion actuelle.**
- Ne jamais exposer au client les détails internes Veeam, chemins SMB, repositories ou erreurs techniques.

### Démonstrations publiques

- La démo publique `/decouvrir-espace-client` doit rester fictive, visible comme DEMO, en lecture seule et isolée des API/auth/billing/backup de production.
- La conception plus générale des comptes de démonstration personnalisés est dans [custom-demo-accounts.md](topics/custom-demo-accounts.md).

## Préférences de workflow utiles

- Commencer par l'architecture et les modèles existants ; ne pas créer un système parallèle si un mécanisme canonique existe déjà.
- Pour les demandes d'audit/lecture seule, ne modifier ni fichier ni production.
- Pour les releases, isoler strictement les changements de la tâche et éviter `git add -A`, reset/stash destructifs ou écrasement d'autres travaux.
- Le projet a historiquement préféré travailler sur `main`; voir [workflow-preferences.md](topics/workflow-preferences.md) pour les nuances Windows/Turbopack/worktrees.
- Une fonctionnalité publique n'est pas considérée terminée uniquement parce que son URL directe fonctionne : navigation, rendu public et preuves réelles comptent.

## Snapshots historiques à ne pas traiter comme état courant

- [roadmap-current.md](topics/roadmap-current.md) : snapshot du 2026-07-09. Il dit encore que V1.0 est bloquée par le R740xd ; [infra-r740xd-blocker.md](topics/infra-r740xd-blocker.md) indique que ce blocage a été levé vers le 2026-07-23. **Donc le roadmap snapshot est historique.**
- [seo-vitrine-state.md](topics/seo-vitrine-state.md) : v1.1.12 au 2026-08-06, remplacée ensuite par des releases plus récentes.
- [hcaptcha-signup-state.md](topics/hcaptcha-signup-state.md) : état recette/DUMMY au 2026-07-06, remplacé par la mise à jour live du 2026-08-01.

## Tous les topics Claude importés

- [billing-v2-koxo-storage-targets.md](topics/billing-v2-koxo-storage-targets.md)
- [bpce-invoicing-api.md](topics/bpce-invoicing-api.md)
- [custom-demo-accounts.md](topics/custom-demo-accounts.md)
- [deployment-topology.md](topics/deployment-topology.md)
- [diagnostic-panne-donnees.md](topics/diagnostic-panne-donnees.md)
- [hcaptcha-signup-state.md](topics/hcaptcha-signup-state.md)
- [infra-r740xd-blocker.md](topics/infra-r740xd-blocker.md)
- [koxo-accents-majuscules.md](topics/koxo-accents-majuscules.md)
- [koxo-ad-password-mastery.md](topics/koxo-ad-password-mastery.md)
- [koxo-api-ne-cree-plus.md](topics/koxo-api-ne-cree-plus.md)
- [koxo-fiche-utilisateur-maitre.md](topics/koxo-fiche-utilisateur-maitre.md)
- [koxo-groupes-primaires-separes.md](topics/koxo-groupes-primaires-separes.md)
- [koxo-orphelins-supprimes.md](topics/koxo-orphelins-supprimes.md)
- [paypal-v022-gotchas.md](topics/paypal-v022-gotchas.md)
- [roadmap-current.md](topics/roadmap-current.md)
- [seo-vitrine-state.md](topics/seo-vitrine-state.md)
- [smtp-ovh-live-config.md](topics/smtp-ovh-live-config.md)
- [srv11-security-headers.md](topics/srv11-security-headers.md)
- [srv13-config-volatile.md](topics/srv13-config-volatile.md)
- [timezone-utc-convention.md](topics/timezone-utc-convention.md)
- [v0.35-cart.md](topics/v0.35-cart.md)
- [version-release-checklist.md](topics/version-release-checklist.md)
- [workflow-preferences.md](topics/workflow-preferences.md)

## Historique Codex importé

- [Résumé Codex](archive/codex-memory-summary.md) — bon point d'entrée pour chercher un sujet ancien.
- [Mémoire Codex complète, expurgée](archive/codex-memory-full.md) — volumineuse, ne pas charger par défaut.
- `archive/codex-task-groups/` — groupes de tâches découpés individuellement pour recherche ciblée.

**Recherche recommandée :** `rg -n -i "mot-clé" .ai` plutôt que charger tout l'historique dans le contexte.
