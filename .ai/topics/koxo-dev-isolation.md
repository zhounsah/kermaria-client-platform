---
name: koxo-dev-isolation
description: "Isolation KoXo DEV/PROD (2026-09-26) : namespace KoXo de l'API (CLI-D, DEV-CLI-, CLIENTS DEV), lanceur SRV-21 à instance isolée, mutex Global\\Kermaria-KoXoAdm. Fondations logicielles seulement, infrastructure DEV non déployée."
metadata:
  type: project
---

Constat du 2026-09-26 : DEV et PROD partagent `clients.home.bzh` et `KoXoAdm.exe`.
La PROD porte déjà `employeeNumber` CLI-000001 à CLI-000003, et le compteur de
`kermaria_dev` repartait de 1. Le lanceur SRV-21 rechargeait sans condition les
variables Machine `KOXO_*`, qui pointent sur l'export PROD.

Fondations posées (code local, non commité au moment de l'écriture) :

- **API** : `KoxoNamespace` (`KOXO_IDENTIFIER_PREFIX`, `CUSTOMER_REFERENCE_PREFIX`,
  `KOXO_PRIMARY_GROUP_CLIENTS`, `KOXO_PRIMARY_GROUP_DEMO`).
  - Défaut = production au bit près ; tout ou rien.
  - Le garde-fou `APP_ENV` exige un namespace DEV dès que `controlled_write` ou
    `KOXO_SYNC_WEBHOOK_URL`, et interdit tout namespace hors production en PROD.
- **SRV-21** : `Resolve-KoxoSyncLaunchPlan`.
  - Sans définition, c'est le plan PROD historique.
  - Avec une définition JSON, toute variable `KOXO_*` est retirée du processus
    et chaque réglage vient de la définition, jetons lus dans des fichiers.
  - Une définition qui reprend un élément PROD est refusée : groupe, CSV,
    profil, port 8042, répertoire, URL ou jeton.
- **Verrou** : mutex système `Global\Kermaria-KoXoAdm` dans `Invoke-KoxoProcess`.
  Nom non configurable, délai explicite (600 s), libération en `finally`,
  reprise d'un mutex abandonné.

Ne pas activer `controlled_write` en DEV avant les validations runtime listées
dans `docs/KOXO_DEV_ISOLATION.md` : isolation par profil dans KoXoAdm, formes
acceptées par KoXo, emplacement du CSV DEV, `X-Forwarded-For` via SRV-11, profil
démo DEV.

Piège de test mesuré : un mutex nommé disparaît avec sa dernière poignée. Pour
prouver la reprise d'un mutex abandonné, un autre processus doit garder une
poignée ouverte. Et `Copy-Item` conserve la date de modification : après la
restauration d'un fichier C#, MSBuild peut garder la DLL mutée, donc il faut
toucher le fichier.

Voir aussi [[dev-environment]], [[koxo-api-ne-cree-plus]], [[primary-identity-bootstrap]].
