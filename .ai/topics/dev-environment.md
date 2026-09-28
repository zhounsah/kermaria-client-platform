---
name: dev-environment
description: "Stack DEV isolée (2026-09-21) : WebPortal :3100 sur SRV-12, API :5100 sur SRV-13, base kermaria_dev sur SRV-06. Pièges découverts : variables Machine PROD de SRV-13, triggers et binlog, pare-feu désactivé par GPO."
metadata:
  type: project
---

Mise en place le 2026-09-21, sans toucher à la production. Runbook :
`docs/DEV_ENVIRONMENT.md` ; scripts : `scripts/dev-env/`.

## État au 2026-09-21

- API DEV `KermariaApiInternalDev` (compte virtuel `NT SERVICE\KermariaApiInternalDev`),
  `192.168.100.213:5100`, `ASPNETCORE_ENVIRONMENT=Staging` + `APP_ENV=Development`,
  démarrage automatique différé.
- WebPortal DEV `kermaria-webportal-dev.service` (utilisateur `kermaria-web-dev`),
  `192.168.100.212:3100`, `APP_ENV=Development`.
- Base `kermaria_dev` (migrations 001→095 + seed fictif). Comptes `kermaria_dev`
  (DML) et `kermaria_dev_migrator` (DDL), tous deux `@192.168.100.213`, droits
  sur `kermaria_dev.*` uniquement.
- Stripe DEV **désactivé** tant que les clés TEST et l'endpoint webhook TEST ne
  sont pas fournis. `dev.zachary-it.fr` **non publié**.
- Le code DEV déployé = `main` 9ee4a77 + modifications **non commitées** du
  2026-09-21 (garde-fous). La production ne les a pas.

## Pièges mesurés

- **SRV-13 porte en variables Machine** `SQL_*`, `AD_*` (mot de passe compris)
  et `KOXO_SYNC_WEBHOOK_*` de la PROD, qui priment sur le JSON. Une seconde
  instance hérite de tout ce que son fichier ne redéfinit pas. Parade :
  `KERMARIA_CONFIG_AUTHORITATIVE=true`, qui retire les variables non préfixées
  des sources de configuration. Le garde-fou DEV a bloqué la première
  migration pour ce motif (`KOXO_SYNC_WEBHOOK_URL` hérité).
- **SRV-06 a le binlog actif** (réplication vers SRV-07/08, sans filtre) et
  `log_bin_trust_function_creators=0` : `CREATE TRIGGER` exige `SUPER`. La
  PROD a été migrée par `kermaria_api` (définisseur des triggers PROD). En DEV,
  les triggers de 066 ont été créés avec `DEFINER = kermaria_dev_migrator` via
  `Complete-DevMigration066.ps1`. Même traitement pour toute future migration
  avec trigger, fonction ou procédure.
- Le **pare-feu Windows de SRV-13 est désactivé par GPO** : les règles locales
  n'ont aucun effet. Les ports 5000 et 5100 ne sont protégés que par
  `X-Service-Auth`.
- Le trafic VPN de RDC-07 arrive sur le LAN avec l'adresse de **SRV-11**
  (192.168.100.211) : un filtrage par IP source ne distingue pas RDC-07 du proxy.
- `root` MariaDB sur SRV-06 n'est accessible ni par socket ni par
  `debian.cnf` ; le seul compte capable de `CREATE USER` est `kermaria_api`.

## Observation runtime du 2026-09-24

- `dev.zachary-it.fr` est publié ; WebPortal DEV :3100 et API DEV :5100
  répondent à leurs health checks. API : `APP_ENV=Development`, base
  `kermaria_dev`, Stripe `test`.
- `AD_INTEGRATION_MODE=test` dans le fichier DEV est interprété comme
  **`disabled`** au runtime : `AdConfigurationResolver.ParseMode` n'accepte
  que `disabled`, `mock`, `read_only`, `controlled_write`.
- L'installateur API DEV réécrit sa configuration. Pour une mise à jour
  strictement binaire, conserver le fichier externe et utiliser son basculement
  staging → ancien dossier → nouveau dossier, puis redémarrer uniquement
  `KermariaApiInternalDev`. Contrôler les empreintes de configuration avant et
  après. Ne jamais supposer que le nom « DEV » neutralise BPCE : la
  configuration observée avait `BPCE_INTEGRATION_MODE=live`.
- Après le correctif d'agrégation du planner par `identity_reference`, le
  retrigger admin ciblé a dépassé le contrôle de matérialisation, puis s'est
  arrêté sur `BILLING_V2_PROVISIONING_REVIEW_NOT_PASSED`, sans action externe.
  Ne pas contourner ce garde-fou.

## Mise à jour du 2026-09-26

- API DEV = `main` **828347d** (Cart claim/clear), publiée depuis un worktree
  propre, bascule binaire seule (dossier précédent :
  `api-internal-dev-old-20260926-090237`), configuration DEV inchangée.
- `kermaria_dev` porte la migration **096** (`portal_user_identity_bootstrap`,
  0 ligne) ; sauvegarde préalable
  `%USERPROFILE%\Backups\Kermaria\kermaria_dev_pre096_20260926_090133.sql`.
  `kermaria` reste à 095.
- Avec `AD_INTEGRATION_MODE=test` (lu `disabled`), le worker
  `PrimaryIdentityBootstrapConvergenceWorker` n'est pas enregistré : aucune
  convergence AD/KoXo. Le worker USER-ADDITIONAL démarre, comme avant.
- Le WebPortal DEV déployé (`webportal-dev-20260924-145513`) contient déjà la
  gestion de `CART_CLAIM_RESUMED` (code non commité). Ce n'est pas le cas du
  WebPortal de `main`.

## Déploiement DEV fail-closed (2026-09-28)

- Le correctif `2ba96ed` fait de `Install-ApiInternalDev.ps1` un déploiement
  binaire conservateur : par défaut, il valide puis préserve le JSON runtime
  DEV existant, sans le reconstruire depuis une source `.env`.
- Le contrôle non modifiant est `-ValidateOnly`. Le rafraîchissement éventuel
  d'un secret exige simultanément `-RefreshConfiguration` et un fichier hors
  Git contenant seulement des affectations littérales `DEV_API_*` autorisées,
  lu par AST sans exécution. Les secrets non fournis restent inchangés.
- Le validateur d'installation et le garde de démarrage API refusent : base autre que
  `kermaria_dev`, Stripe Live, namespace KoXo hors `CLI-D`/`DEV-CLI-`, groupe
  ou OU hors `CLIENTS DEV`, receveur autre que `:8043`, compte AD PROD connu et
  allowlist e-mail avec joker. L'API refuse symétriquement les cibles DEV en
  PROD. Le contrôle d'installation est retesté juste avant la bascule.
- Le fichier historique `kermaria-client-platform.dev.env.ps1` reste hors du
  workflow : il contient des variables génériques et est refusé par le nouveau
  contrat. Le template versionné non secret est
  `scripts/dev-env/api-internal.dev.template.json`.

Voir aussi [[pieges-sql-et-preuves]], [[deployment-topology]], [[srv13-config-volatile]].
