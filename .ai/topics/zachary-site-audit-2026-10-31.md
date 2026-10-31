# Audit du site Zachary IT — 31/10/2026

Sur `codex/zachary-it-refonte-ready`, l'accueil a été rendu immédiatement utile : l'écran plein format `HomeIntroMotion` a été retiré, et le fallback de chargement reprend le descriptif et le CTA de la même source que le hero. Le rappel du diagnostic associe désormais les erreurs BFF aux champs et au consentement. Un contrat panier obsolète a été aligné sur « Reprendre mon serveur ». Les prix, l'API, la base et la production n'ont pas été modifiés.

`npm run validate` passe ; la suite Playwright Chromium locale donne 15 réussites et une matrice mobile volontairement ignorée dans le projet mobile. La matrice exécutée sur desktop couvre 320, 390, 768, 1024, 1440 et 1920 px pour accueil, assistance, contact et diagnostic. Le fallback local sans API interne ne prouve pas les catalogues `/services`, `/offres`, `/tarifs`, les e-mails, la connexion ni Stripe. Pas de mesure Lighthouse/CWV comparable. Détails et checklist : `docs/AUDIT_ZACHARY_IT_2026-10-31.md`.

Le worktree principal `codex/frontend-premium-20261030` reste distinct et conserve ses changements commerciaux non commités. Lors de l'intégration, garder le contexte des six paliers Assistance jusqu'au contact et la lecture des erreurs de contact via `response.details` déjà présentes dans la refonte.
