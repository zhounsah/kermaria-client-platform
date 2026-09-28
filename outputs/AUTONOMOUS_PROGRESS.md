# Journal autonome

## 2026-09-28 — Priorité 1 : déploiement API DEV

- Résultat : installateur DEV fail-closed. Il préserve la configuration runtime
  existante par défaut, exige une validation distante préalable et n'accepte un
  rafraîchissement que sur opt-in explicite avec une source `DEV_API_*` analysée
  sans exécution.
- Tests : 17 tests Pester, exécutés sous Windows PowerShell 5.1 ; validation,
  refus des valeurs PROD, secret préservé, absence d'écriture en échec/dry-run,
  stabilité répétée et absence de secret dans le diagnostic.
- Commit : `2ba96ed` — `fix(dev): sécuriser la génération de configuration API DEV`.
- Prochain sujet : documenter le modèle DEV non secret puis compléter les
  garde-fous runtime DEV/PROD sans toucher au runtime déployé.
- Blocker : aucun pour le travail local ; aucune commande de déploiement n'a été
  exécutée contre SRV-13.
