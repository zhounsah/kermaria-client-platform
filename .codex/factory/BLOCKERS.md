# Journal des blockers

`STATE.json.blocker` est la source structurée du blocker actif. Ce journal
conserve les preuves lisibles et les résolutions. Aucun secret, identifiant
d'accès, contenu client ou chaîne de connexion ne doit y être copié.

## État initial — 2026-07-29

Aucun blocker actif. La phase P04 est prête à démarrer.

## Modèle d'entrée

```text
## B-YYYYMMDD-NN — OPEN | RESOLVED — Titre

- Phase et étape : Pxx / STEP
- Type/code : TECHNICAL ou HUMAN_GATE / code stable
- Première occurrence : horodatage UTC
- Dernière occurrence : horodatage UTC
- Empreinte : identifiant stable, sans données sensibles
- Preuves : commandes, erreurs assainies, chemins et tests
- Tentatives : actions bornées déjà essayées
- Décision attendue : uniquement pour une porte humaine
- Résolution : preuve de disparition et validation rejouée
```

Un test en échec n'est pas automatiquement un blocker. Le statut `BLOCKED`
n'est utilisé qu'après trois cycles de correction consécutifs sans progrès, ou
quand aucune action sûre ne peut avancer sans changement externe. Une porte
humaine suit exclusivement `HUMAN_GATES.md`.

## B-20260729-01 — OPEN — Validation web P06 incompatible avec son allowlist

- Phase et étape : P06 / FIX
- Type/code : TECHNICAL / TECHNICAL_BLOCKER
- Première occurrence : 2026-07-29T21:17:47.0674410Z
- Dernière occurrence : 2026-07-29T21:33:05.4829051Z
- Empreinte : `P06-BASELINE-LINT-43a5c096`
- Preuves : `validate-phase.ps1` échoue dans la validation obligatoire `check:web` sur 28 problèmes déjà présents dans 7 fichiers inchangés et tous hors de l'allowlist P06 ; empreinte SHA-256 `43a5c096f5bfdd3c264f3f641058a5b5ca46370fb6dd70d665d97da02455d577`. Ces fichiers sont planifiés en P09, P10, P12 et P13, donc aucune correction conforme n'est possible avant de valider P06.
- Tentatives : correction du défaut de duplication P06 ; typecheck webportal, contrat subscriptions, ESLint ciblé et `git diff --check` réussis ; revue de code et QA indépendantes sans défaut produit P06 ; aucun leurre textuel ou changement hors allowlist introduit.
- Confirmations : même condition reproduite pendant trois tours consécutifs de l'objectif ; aucun des sept fichiers responsables n'a changé et aucune action sûre dans l'allowlist P06 ne peut rendre `check:web` vert.
- Défaut secondaire non bloquant pour P06 : `npm run test:cart`, qui n'est pas une validation P06, exige encore `AddRecurringCheckoutButton` directement dans `SubscribeCatalogSections.tsx`. La mise à jour de `apps/webportal/scripts/verify-cart-contract.mjs` est autorisée et planifiée en P12.
- Contrôle du checkpoint : `STATE.json` reste syntaxiquement valide, mais `validate-global.ps1 -FactoryOnly` en mode `Resume` refuse le diff P06 et `BLOCKERS.md`, car ce mode n'autorise que `STATE.json` entre deux commits. Le processus exige pourtant de conserver le blocker lisible et interdit le commit d'une phase non validée.
- Décision reçue : `D-20260729-07` impose une phase corrective P06A distincte, sans élargir P06 ni affaiblir `check:web`. Le blocker reste ouvert jusqu'à la validation et au commit atomique de P06A.
- Résolution : OPEN.
