# Journal autonome

## 2026-09-28 — Priorité 1 : déploiement API DEV

- Résultat : installateur DEV fail-closed. Il préserve la configuration runtime
  existante par défaut, exige une validation distante préalable et n'accepte un
  rafraîchissement que sur opt-in explicite avec une source `DEV_API_*` analysée
  sans exécution.
- Tests : 18 tests Pester, exécutés sous Windows PowerShell 5.1 ; validation,
  refus des valeurs PROD, secret préservé, absence d'écriture en échec/dry-run,
  stabilité répétée et absence de secret dans le diagnostic.
- Commit : `2ba96ed` — `fix(dev): sécuriser la génération de configuration API DEV`.
- Prochain sujet : documenter le modèle DEV non secret puis compléter les
  garde-fous runtime DEV/PROD sans toucher au runtime déployé.
- Blocker : aucun pour le travail local ; aucune commande de déploiement n'a été
  exécutée contre SRV-13.

## 2026-09-28 — Priorités 2 et 3 : modèle explicite et garde runtime

- Résultat : template non secret DEV, exemple hors Git `DEV_API_*` et workflow
  documenté. Le garde de démarrage API refuse maintenant les croisements
  AD/KoXo : DEV exige `CLIENTS DEV` et `:8043` pour les écritures, PROD refuse
  ces cibles DEV.
- Tests : Pester (18/18) ; build et exécution du target smoke API Release sans
  échec. Les avertissements CA1416 préexistants restent liés aux API AD Windows.
- Commit documentation : `65c5a19` — `docs(dev): documenter le workflow de configuration sûr`.
- Commit garde runtime : `4df041b` — `test(dev): bloquer les cibles AD et KoXo croisées`.
- Prochain sujet : analyse code-only du stockage headless, sans lancement KoXo.

## 2026-09-28 — Priorité 4 : stockage headless KoXo (code-only)

- Résultat : cause certaine limitée au wrapper (aucun mode headless ni gestion
  de dialogue) ; cause native la plus probable classée comme inférence : UI
  KoXo invisible sous SYSTEM. Les tests injectent le processus et ne constituent
  pas une preuve runtime.
- Décision : aucune correction automatique, aucun lancement KoXo. Le workflow
  doit refuser cette réparation headless jusqu'à un test fournisseur autorisé.
- Prochain sujet : préparation code-only du scénario E2E abonnement DEV.
- Blocker : preuve finale dépendante d'un test KoXo réel explicitement autorisé.
- Commit : `0045409` — `docs(koxo): cadrer le blocage stockage headless`.

## 2026-09-28 — Priorité 5 : prochain E2E abonnement (code-only)

- Résultat : plan automatisable préparé, mais STOP avant runtime. Aucun service
  actuellement mappé à une appartenance AD ne reste dans le seul périmètre
  `CLIENTS DEV` ; stockage, VPN et RDS portent des dépendances interdites ou
  non validées.
- Décision attendue : groupe de service DEV dédié, autorisation bornée d'un
  groupe parent-domain, ou validation KoXo stockage préalable.
- Blocker : décision de modèle et autorisation d'infrastructure de Zachary.
- Commit : `48e6c3e` — `docs(dev): préparer le prochain E2E de provisioning`.

## 2026-09-29 — Nuit : provisioning direct, latence checkout, intention orpheline

- Bug 1, cause : `BillingV2ProvisioningPlanner.EnforcePersonalStoragePrerequisite`
  exigeait un stockage personnel pour **toute** règle `ad_group_membership`.
  `SERVICE-E2E-DEV` (service direct, sans tier, scope user) produisait donc un
  bloqueur `PersonalStorageRequired`, remonté par la gate en
  `BILLING_V2_PROVISIONING_INCOMPLETE_MATERIALIZATION`. Les lignes de
  matérialisation DEV (`items`, `users`, `item_provisioning`, règle) sont
  complètes : vérifié en lecture sur `kermaria_dev`.
- Correction : `7da7309` limite ce prérequis aux services `VPN-ACCESS` et
  `RDS-ACCESS` (codes vérifiés dans le catalogue DEV) ; test de régression dans
  `--billing-v2-provisioning-semantics`. `identity_reference = portal_users.id`
  inchangé.
- Bug 2 : `f1e7ce3` — dispatch immédiat de l'événement d'outbox après COMMIT
  (même gate, même claim atomique que le worker, borne 20 s détachée de la
  requête) ; l'URL d'approbation est renvoyée directement. `PollInterval`
  inchangé, le worker reste le filet.
- Bug 3 : `724d1e8` — `BillingV2IntentReusePolicy` ; une intention dont
  l'abonnement est annulé/expiré ou la tentative échouée/abandonnée n'est plus
  reprise et est fermée (`cancelled`, `cancelled_at`,
  `BILLING_V2_INTENT_PROVIDER_TERMINAL`) sans DELETE. `amount_mismatch` seul
  conserve l'intention (pas de second paiement avant réconciliation).
- Tests : 21 suites Billing V2 non-DB PASS (dont les nouveaux tests), smoke API
  PASS, build/publish Release PASS. Suites MariaDB opt-in non rejouées (aucune
  base jetable fournie).
- DEV : API redéployée depuis un `git archive` de `f1e7ce3` (JSON runtime
  préservé), santé 200, `kermaria_dev`, Stripe test.
- **STOP — action humaine requise** : aucun chemin automatique ne rejoue le
  provisioning V2 d'un abonnement déjà actif. Les événements Stripe enregistrés
  pour `43bee433` ne portent pas `SUBSCRIPTION_ACTIVATED` (un renvoi ne
  déclencherait rien), et le bouton admin « réconcilier » passe par
  `BillingV2SubscriptionProvisioningManager`, pas par le planner/gate V2 : il
  ne validerait pas la correction. La route V2
  `/internal/admin/billing-v2/subscriptions/{id}/provisioning/reconcile` n'a
  pas d'interface. Preuve attendue : un nouvel achat Stripe TEST de
  `SERVICE-E2E-DEV` par Melis sur la DEV, qui valide aussi la latence et la
  fermeture d'intention.
- PROD : non touchée. Migrations appliquées en PROD jusqu'à `095` ; manquent
  `096` et `097`, toutes deux purement additives (`CREATE TABLE IF NOT EXISTS`).
- Push, tag `v2.0.2.9` et déploiement PROD : non faits (DEV non validée).
