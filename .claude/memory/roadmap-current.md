---
name: roadmap-current
description: "Snapshot roadmap kermaria-client-platform au 2026-07-03 — V0.15 à V0.30 partiel livrés + V0.24 infra debout depuis 2026-07-03 (KERMARIA-SRV-01/02/07, IIS split vitrine/portal, compte AD HOME\\svc_api_portal_ad, config mono-fichier JSON, --seed-admin). Reste à exécuter recette Brique 1, audit Brique 2, procédure prod Brique 3."
metadata:
  node_type: memory
  type: project
  originSessionId: 316dd2c1-620c-4ba1-833b-0b5d317971ba
---

État de la roadmap au **2026-07-03** (source de vérité dans
[docs/ROADMAP.md](docs/ROADMAP.md)) :

**Livré et figé dans le dépôt :**
- V0.15 à V0.23.2 (voir historique complet dans [docs/ROADMAP.md](docs/ROADMAP.md)).
- V0.25 AD finalisation (briques 1+2+3) — livrée et recettée 2026-06-30.
- V0.26 signup self-service — livrée et recettée 2026-07-02 (SMTP OVH réel, allowlist).
- V0.27 site vitrine public — livrée 2026-06-30, flag `PUBLIC_VITRINE_ENABLED=false` par défaut, activée en staging le 2026-07-03.
- V0.29 Stripe — livrée 2026-07-02, rail parallèle PayPal.
- V0.30 partiel (allowlist SMTP `EMAIL_LIVE_ALLOWLIST`) — livrée et recettée 2026-07-02, fail-closed par défaut.

**V0.24 en cours — infra debout au 2026-07-03 :**

Cadrage rédigé le 2026-07-02 dans [docs/V0.24_STABILISATION.md](docs/V0.24_STABILISATION.md).
Runbook infra : [docs/DEPLOYMENT_WINDOWS.md](docs/DEPLOYMENT_WINDOWS.md).

Infra staging debout sur KERMARIA-SRV-01/02/07 le 2026-07-03 :
- SRV-01 Dell Optiplex 5070 : WEBPORTAL Node standalone via NSSM +
  wrapper `start-webportal.ps1`, IIS front split `kermaria-vitrine`
  (www.home.bzh + www.zacharyhounsa.ovh, X-Robots-Tag strippé) et
  `kermaria-portal` (portail.* + dashboard.*, / → /login), wildcard
  Let's Encrypt réutilisé.
- SRV-02 ASUS FX753VD : API-INTERNAL dotnet 10 en Windows Service
  natif via New-Service + UseWindowsService(), config JSON unifiée
  dans `C:\ProgramData\Kermaria\api-internal.config.json`.
- SRV-07 kermaria-srv-07.home.bzh (192.168.100.207) : MariaDB, base
  `test_web` réutilisée, migrations 001-020 appliquées.
- Compte AD partagé `HOME\svc_api_portal_ad`.
- 1er admin créé via nouveau flag CLI `--seed-admin`.

Reste à exécuter :
- **Brique 1** : recette staging complète (scénarios V0.15→V0.30
  partiel), restauration MariaDB testée.
- **Brique 2** : audit sécurité (dépendances, secrets, headers, rate
  limiting), rotation des mots de passe faibles utilisés en
  installation (mdp AD de recette + ancien mdp `test_web`, valeurs
  non consignées ici).
- **Brique 3** : rédaction `docs/PRODUCTION_DEPLOYMENT.md`, doc
  utilisateur, plan de continuité minimal.

**Gap remis en avant** : garde-fou `PAYPAL_MODE=live` n'est pas
codé en dur dans `RuntimeConfigurationValidator.cs` alors que
Stripe l'est — à arbitrer avant V1.0 beta 1.

**Ajouts fonctionnels V0.24 (bootstrap tooling, pas du métier)** :
- `--seed-admin` CLI + `MariaDbAdminSeeder` (usable staging/prod,
  prompt interactif masqué, hash PBKDF2, sentinel customer INTERNAL).
- Patch `KERMARIA_CONFIG_PATH` / config JSON unifié dans
  `Program.cs`.
- Blocklist `LOG_FILE_DIRECTORY` machine-spécifique + injection
  default cible dans `scripts/build-api-config.ps1`.
- Param `-Override @{}` sur `build-webportal-config.ps1` +
  `build-api-config.ps1` (force les clés host-spécifiques —
  `INTERNAL_API_URL`, `SQL_HOST` — sans éditer le `.local.env.ps1`
  de dev) + garde-fou : avertissement au build si `INTERNAL_API_URL`
  locale avec `NODE_ENV=production` (miroir de
  `validateServerRuntimeConfiguration()` dans
  `apps/webportal/lib/runtime-config.ts`). Corrige la régression
  split-host où un rebuild réinjectait `localhost:5000` (dev) alors
  que l'API est bindée sur `192.168.100.202:5000` (SRV-02) →
  `/api/health/ready` 503. Commit `276f6f2` sur main. Contexte dans
  journal [docs/V0.24_SUIVI.md](docs/V0.24_SUIVI.md).
- Fix migrations 004/005/006 (INSERT redondant sur
  schema_migrations supprimé).
- Scripts : `build-api-config.ps1`, `build-webportal-config.ps1`,
  `start-webportal.ps1`.

**À venir (non-hardware) :**
- V0.28 catalogue packs et offres groupées — non démarré.
- V0.30 final — statuts email étendus + SPF/DKIM/DMARC + recette
  multi-fournisseurs Gmail/Outlook + sous-domaine `tests-mail.*`.
- V0.31 provisioning AD réel hors OU de test — PR code de levée
  `RequiredTestOuRoot` (const hardcodé) + allowlist
  `AD_ALLOWED_ROOTS`, Option A (bascule franche par client)
  recommandée dans [docs/AD_PRODUCTION_MIGRATION.md](docs/AD_PRODUCTION_MIGRATION.md).

**Hardware-gated (R740xd) :**
- V1.0 beta 1 — test R740xd, bascule modes `live` progressivement,
  exécute la procédure `docs/PRODUCTION_DEPLOYMENT.md` rédigée en
  V0.24 Brique 3.
- V1.0 RC — mise en production réelle, premier client réel,
  ouverture signup si validation juridique OK.

**Hors séquence (réservé) :** prélèvement SEPA hors PayPal/Stripe,
intégration comptable automatique, automatisation NAS/RDS/VPN
déclenchée par un encaissement, HTML enrichi dans les e-mails
(texte uniquement depuis V0.21), application mobile native.

**Why:** la séquence respecte un principe : aucune obligation
externe (e-mail réel, numérotation fiscale officielle activée en
live, AD prod, client réel, prélèvement récurrent réel) avant que
l'infra définitive ne soit en place. V0.24 est la porte de sortie
phase-de-tests vers V1.0 beta 1 : elle ne peut pas être
court-circuitée. Le cadrage V0.24 découpe explicitement chaque
brique pour que l'exécution puisse se faire par lots séparés.
L'infra staging est maintenant debout, les briques d'exécution
peuvent démarrer.

**How to apply:** quand l'utilisateur demande "où on en est" ou
"prochaine étape", lire [docs/ROADMAP.md](docs/ROADMAP.md) puis
[docs/V0.24_STABILISATION.md](docs/V0.24_STABILISATION.md) pour
l'état d'exécution V0.24, et [[deployment-topology]] pour l'infra
concrète. Le branchement de V1.0 beta 1 / RC sur la livraison
hardware est documenté dans [[infra-r740xd-blocker]]. Le détail
BPCE et le secret `BPCE_REFRESH_TOKEN` sont dans
[[bpce-invoicing-api]]. La discipline des modes
`EMAIL_INTEGRATION_MODE`/`PAYPAL_MODE`/`BPCE_INTEGRATION_MODE`/`STRIPE_MODE`
(tous `disabled`/`sandbox`/`test` par défaut) est en place et doit
rester ainsi jusqu'à V1.0 beta 1.
