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
- Commit garde runtime : ce commit, limité à `DeploymentEnvironmentGuard` et à
  ses cas smoke.
- Prochain sujet : analyse code-only du stockage headless, sans lancement KoXo.
