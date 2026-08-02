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

## B-20260729-01 — RESOLVED — Validation web P06 incompatible avec son allowlist

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
- Résolution : P06A a corrigé uniquement les sept fichiers identifiés, sans
  désactivation de règle ni extension de l'allowlist P06. Les neuf validations
  de phase, dont `npm run check:web`, la QA indépendante et l'audit de sécurité
  sont verts. Commit atomique :
  `5fd8ce49f4aef7a1bda51c765dccdcab726977dc` (`fix(webportal): restaurer une
  baseline lint propre`). P06A est `DONE` et P06 reprend à `PRECHECK`.

## B-20260730-02 — RESOLVED — Continuité de session entre portails canoniques

- Phase et étape : P07 / ANALYSIS
- Type/code : HUMAN_GATE / HG-PUBLIC-CONTRACT
- Première occurrence : 2026-07-30T05:24:08.8561317Z
- Empreinte : `P07-CANONICAL-SESSION-HOSTONLY-20260730`
- Preuves : les redirections relatives actuelles conservent le mauvais hôte,
  mais `getSessionCookieOptions()` crée un cookie host-only. Une authentification
  administrateur sur `dashboard.*` suivie d'une redirection canonique vers
  `administration.*` perd donc la session. Le snapshot historique qui effectue
  cette redirection reproduit ce défaut et reflète aussi les hôtes non reconnus.
- Portée sécurisée : aucune modification applicative P07 n'a été réalisée ; le
  worktree contient uniquement `STATE.json` et ce journal/roadmap. Les baselines
  workspace `test:auth`, `typecheck:webportal`, `test:forms` et `test:ux` sont
  vertes. Les cibles futures devront rejeter les hôtes inconnus et les chemins
  absolus ou protocol-relative.
- Correction technique indépendante : la définition P07 utilise désormais
  `npm.cmd --prefix apps/webportal run test:auth`, commande rejouée avec succès.
  Cette correction de métadonnées ne préjuge pas de la décision contractuelle.
- Options : login strict séparé par zone avec refus/réorientation du mauvais
  rôle ; cookie partagé au domaine parent ; ticket de transfert one-shot entre
  portails.
- Résolution : `D-20260730-08` retient les logins strictement séparés par zone,
  sans cookie partagé ni mécanisme de handoff. P07 reprend depuis son checkpoint
  d'analyse avec cette règle et les tests de matrice correspondants.

## B-20260730-03 — RESOLVED — Référence d'offre du contact non vérifiée côté serveur

- Phase et étape : P10 / ANALYSIS
- Type/code : HUMAN_GATE / HG-PUBLIC-CONTRACT (avec conséquence HG-BUSINESS)
- Première occurrence : 2026-07-30T07:32:58.9236978Z
- Dernière occurrence : 2026-07-30T07:32:58.9236978Z
- Empreinte : `P10-CONTACT-OFFER-CONTRACT-20260730`
- Preuves : `apps/webportal/app/api/contact/route.ts` accepte actuellement toute
  chaîne `offerReference` de 64 caractères ou moins et la relaie à l'API interne
  sans vérifier qu'elle correspond à une offre publique active. La page et le
  formulaire P10 peuvent produire une référence validée, mais un POST forgé peut
  contourner cette validation d'interface. Cette route est hors de l'allowlist
  P10, alors que la définition de phase exige des paramètres contact bornés aux
  offres publiques connues.
- Portée sécurisée : aucune production P10 n'a commencé. Le checkpoint est
  `P10 / ANALYSIS`; le worktree applicatif est intact. La baseline officielle
  P10 est entièrement verte : contrats commercial, signup et managed-content,
  lint, typecheck, build Next.js de 57 pages et diff-check.
- Constats non bloquants dans l'allowlist actuelle : parsing strict des
  engagements/paiements, refus des doublons, variantes actives seulement,
  cohérence pack/offre, accessibilité du comparatif et tunnel contact/signup.
- Décision attendue : choisir entre (1) une phase corrective préalable P10A qui
  valide côté serveur et rejette une référence inconnue avec HTTP 400 ; (2) une
  P10A qui neutralise silencieusement la référence inconnue vers `null` pour
  préserver le statut du formulaire ; (3) conserver l'allowlist P10 et accepter
  explicitement que la sélection pack du contact reste une aide d'interface non
  fiable côté POST, avec le risque résiduel documenté.
- Résolution : `D-20260730-09` retient l'option 1. Une phase corrective P10A
  distincte valide côté serveur toute `offerReference` fournie contre le
  catalogue public actif et rejette les références inconnues ou inactives avec
  HTTP 400. P10 reste hors production et dépend désormais de P10A.
