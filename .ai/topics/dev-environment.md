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

Voir aussi [[pieges-sql-et-preuves]], [[deployment-topology]], [[srv13-config-volatile]].
