---
name: roadmap-current
description: "Snapshot roadmap kermaria-client-platform (maj 2026-07-09 soir). V0.15→V0.30 partiel + V0.35/.1 livrés. V0.24 : DEUX passes de recette contradictoires — passe 1 (07-03, ZH, ~tout [x], ex-branche priceless mergée) vs passe 2 (07-08, auto, prudente, = SUIVI de main = RÉFÉRENCE) qui conteste dont rotation P04/P05 NON FAITE. V0.24 PAS clos. Bug Stripe V0.29-2 corrigé dans main. V1.0 beta 1 hardware-gated R740xd."
metadata:
  node_type: memory
  type: project
  originSessionId: 316dd2c1-620c-4ba1-833b-0b5d317971ba
---

État réel au **2026-07-06**, reconstitué sur la source de vérité
prioritaire (code + historique Git + tests de contrat + docs datées).
Source canonique : [docs/ROADMAP.md](docs/ROADMAP.md) et
[docs/V0.24_SUIVI.md](docs/V0.24_SUIVI.md).

> ⚠️ Correction 2026-07-09 (remplace la note du 2026-07-06) : la recette
> V0.24 **a bien été exécutée** (2026-07-03→06) — mais sur la branche
> `claude/priceless-driscoll-a6928d`, jamais fusionnée. La note du 2026-07-06
> avait conclu « non exécutée / aucune trace Git / guides inexistants » parce
> qu'elle n'a inspecté que `main` (SUIVI resté vide, fichier rempli vivant sur
> la branche). Preuve d'authenticité : le bloquant `V0.29-2` décrit (Stripe
> `invoice.paid` lisant le champ legacy) était un vrai bug, depuis corrigé
> dans `main`. Contenu désormais consolidé (SUIVI, audit, guides). Leçon :
> avant de conclure « pas fait », chercher aussi sur **les branches non
> mergées** (`git branch --all`, `git log --all`), pas seulement `main`.

**Livré et figé dans le dépôt (code présent) :**
- V0.15 à V0.23.2 (historique complet dans [docs/ROADMAP.md](docs/ROADMAP.md)).
- V0.25 AD finalisation (briques 1+2+3) — recette utilisateur 2026-06-30 (AD réel `home.bzh`).
- V0.26 signup self-service — recette utilisateur 2026-07-02, `SIGNUP_ENABLED=false` par défaut.
- V0.27 site vitrine public — livré 2026-06-30, flag `PUBLIC_VITRINE_ENABLED=false` par défaut.
- V0.29 Stripe — livré 2026-07-02, rail parallèle PayPal (Checkout Sessions, colonne `rail`).
- V0.30 partiel (allowlist SMTP `EMAIL_LIVE_ALLOWLIST`) — livré/recetté 2026-07-02, fail-closed.

**V0.24 — DEUX passes de recette contradictoires ; PAS clos :**

> Passe 1 (07-03→06, `ZH`, ex-branche priceless consolidée) marque ~tout
> `[x]`. Passe 2 (07-06→08, `auto`, = `V0.24_SUIVI.md` de `main` = RÉFÉRENCE)
> est plus prudente et conteste plusieurs `[x]` : **rotation P04/P05 NON
> FAITE**, validate/backup `[~]`, majorité des cas client V0.17 non re-prouvés.
> Ne pas se fier à la passe 1. Détail dans `V0.24_ANOMALIES.md`.

Cadrage 2026-07-02 dans [docs/V0.24_STABILISATION.md](docs/V0.24_STABILISATION.md).
Infra staging montée le 2026-07-03 sur KERMARIA-SRV-01/02/07
(cf. [[deployment-topology]] et [docs/DEPLOYMENT_WINDOWS.md](docs/DEPLOYMENT_WINDOWS.md)) :
SRV-01 WEBPORTAL Node+IIS split, SRV-02 API dotnet Service natif,
SRV-07 MariaDB `test_web` (migrations 001-020), compte AD partagé
`HOME\svc_api_portal_ad`, 1er admin via `--seed-admin`.

Statut réel des 3 briques (référence = `docs/V0.24_SUIVI.md` de `main` =
**passe 2 prudente**) :
- **Brique 1 (recette staging)** : jouée mais **PAS close**. Passe 1 couvrait
  V0.17, V0.20/21/22, V0.29 Stripe `test`, V0.25 AD par réf, V0.26, V0.27,
  V0.30 allowlist SMTP, V0.23.2, T-1..T-4 ; passe 2 laisse en `[ ]`/`[~]`
  validate:staging, backup MariaDB, majorité des cas client V0.17. Seul acquis
  code ferme : **V0.29-2** (Stripe abo) corrigé dans `main`.
- **Brique 2 (audit sécurité)** : matrice des 8 secrets renseignée, mais
  **rotation P04/P05 (mdp AD + `test_web`) = FAITE selon passe 1 / NON FAITE
  selon passe 2** → à exécuter/confirmer avant sortie de recette.
- **Brique 3 (doc)** : `docs/PRODUCTION_DEPLOYMENT.md` rédigé (non signé off) ;
  guides `GUIDE_ADMIN.md` / `GUIDE_CLIENT_PAIEMENT.md` + complément
  `SECRET_ROTATION.md` rédigés et mergés dans `main` (07-09).
- **Reste** : rotation P04/P05, rejouer les scénarios prudents, puis
  **sign-off** formel de V0.24.

**Seule tranche réellement travaillée en staging (tracée en Git) —
signup + email, 2026-07-05/06 :**
- hCaptcha : fix `remoteip` IPv6 `%zone` → n'envoie remoteip que si IP
  valide (commit `0cdb0e7`). Clés hCaptcha **DUMMY** (zéro protection, à
  remplacer avant V1.0). Cf. [[hcaptcha-signup-state]].
- set-password : validation du lien au chargement, GET non destructif
  (`7f1af8f`), tracé V0.26-2b.
- SMTP OVH live : régression `MustIssueStartTlsFirst` / plan MX résilié
  (côté fournisseur, **pas le code**). Cf. [[smtp-ovh-live-config]].
- Docs de déploiement/ouverture recette signup @home.bzh (`a1204c0`,
  `6973ce3`, `2a4cd1d`).

**Point de fiabilité résolu (ex-« blocker »)** : la régression
`INTERNAL_API_URL=localhost` au rebuild split-host est **corrigée** par le
tooling `-Override` + garde-fou build (commit `276f6f2`). Ce n'est plus un
bloquant ouvert.

**Bug Stripe V0.29-2 — confirmé PUIS corrigé :** la recette a démontré KO
l'activation d'abonnement Stripe (`invoice.paid` ne lisait que le champ legacy
`data.object.subscription`, absent en API `2026-06-24.dahlia`). **`main`
corrige** : `StripeWebhookService.ReadStripeInvoiceSubscriptionId` lit désormais
aussi `parent.subscription_details.subscription`. Plus un bloquant ouvert.

**Tests de contrat (MariaDB-less) — 8/8 verts au 2026-07-06** après
correction de 2 scripts devenus obsolètes vs produit :
- `test:bpce` : l'assertion « aucun PayPal dans la fiche admin » datait de
  V0.20 ; V0.21 a ajouté le marquage manuel « Marquer payé (hors PayPal) ».
  Assertion recentrée sur « pas d'initiation de paiement en ligne ».
- `test:subscriptions` : la refonte catalogue V0.23 a déplacé l'affichage
  des `paypalPlanId*` de la liste vers la fiche `/admin/catalog/[id]` ; et
  V0.29 a renommé `GetByPayPalIdAsync` → `GetByExternalIdAsync` (multi-rail).
  Assertions retargetées. Correctifs = scripts de test uniquement, code
  produit inchangé.

**À traiter avant prod (rappel)** : comptes démo `@example.invalid` (dont
`internal_admin` mdp faible), rotation des secrets exposés pendant le
pilotage (mdp AD `Test12345!`, whsec Stripe, secret hCaptcha), remplacement
des clés hCaptcha DUMMY.

**Gap process** : garde-fou `PAYPAL_MODE=live` non codé en dur dans
`RuntimeConfigurationValidator.cs` alors que Stripe l'est — à arbitrer avant
V1.0 beta 1.

**À venir (non-hardware) :** V0.28 packs catalogue (non démarré),
V0.30 final (statuts email étendus + SPF/DKIM/DMARC + recette multi-fournisseurs),
V0.31 sortie OU AD réelle (levée `RequiredTestOuRoot`, allowlist `AD_ALLOWED_ROOTS`,
cf. [docs/AD_PRODUCTION_MIGRATION.md](docs/AD_PRODUCTION_MIGRATION.md)).

**Hardware-gated (R740xd) :** V1.0 beta 1 (exécute
`docs/PRODUCTION_DEPLOYMENT.md`, bascule modes `live`), puis V1.0 RC (prod
réelle, 1er client, ouverture signup si validation juridique). Cf.
[[infra-r740xd-blocker]].

**Why:** aucune obligation externe (email réel, numérotation fiscale live,
AD prod, client réel, prélèvement récurrent) avant que l'infra définitive
soit en place. V0.24 = porte de sortie phase-de-tests, non court-circuitable ;
elle n'est pas franchie tant que le suivi vivant n'est pas rempli avec preuves.

**How to apply:** pour « où en est le projet », lire [docs/ROADMAP.md](docs/ROADMAP.md)
puis **[docs/V0.24_SUIVI.md](docs/V0.24_SUIVI.md)** (source de vérité de l'état
d'exécution V0.24 — se fier aux cases cochées + dates, pas à un récit). Infra
concrète dans [[deployment-topology]], gate hardware dans
[[infra-r740xd-blocker]], BPCE dans [[bpce-invoicing-api]]. Les modes
`EMAIL/PAYPAL/BPCE/STRIPE` restent `disabled`/`sandbox`/`test`/`mock` par
défaut jusqu'à V1.0 beta 1.
