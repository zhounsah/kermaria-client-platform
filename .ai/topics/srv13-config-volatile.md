---
name: srv13-config-volatile
description: "SRV-13 tournait en controlled_write avec une configuration AD qui n'existait QUE dans la mémoire du processus — un redémarrage l'aurait fait retomber en disabled en silence. Corrigé et persisté le 2026-08-06."
metadata: 
  node_type: memory
  type: project
  originSessionId: d79dbe2b-9ae3-4e1a-bc48-9b5d713aee39
  modified: 2026-08-06T08:09:18.068Z
---

Découvert le 2026-08-06 en préparant la bascule : `KermariaApiInternal` sur
SRV-13 rapportait `ad controlled_write healthy`, alors qu'**aucune variable
`AD_*` n'existait nulle part sur disque**.

Vérifié sur les quatre sources possibles, toutes vides :
`HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment` (qui ne
portait que les `SQL_*`), l'environnement utilisateur de `HOME\svc-kermaria`,
la clé de service (aucune valeur `Environment`), et l'absence totale
d'`appsettings*.json` ou de `.env` dans `C:\apps\api-internal`.

La configuration ne vivait que dans la **mémoire du processus**, démarré le
2026-08-05 à 06:39. Elle avait été posée, le service démarré, puis les
variables retirées.

**Pourquoi c'était grave** : `AD_INTEGRATION_MODE` absent retombe sur
`disabled` (voir `ParseMode`), pas sur une erreur. Or en `disabled`,
`WritesEnabled` est faux et `ProvisionActiveDirectoryAsync` sort immédiatement :
le `set-password` aurait répondu « succès » sans rien écrire dans l'annuaire.
Le premier redémarrage venu — dont celui qu'impose tout déploiement — cassait
l'intégration AD **en silence**.

## Valeurs persistées (Machine, 2026-08-06)

| Variable | Valeur |
|---|---|
| `AD_INTEGRATION_MODE` | `controlled_write` |
| `AD_DOMAIN` | `clients.home.bzh` |
| `AD_CLIENTS_OU_DN` | `OU=CLIENTS,OU=Utilisateurs,OU=KoXoAdm,DC=clients,DC=home,DC=bzh` |
| `AD_REQUIRED_OU_ROOT` | `DC=clients,DC=home,DC=bzh` |
| `AD_ALLOWED_ROOTS` | `<AD_CLIENTS_OU_DN>;OU=Groupes_TEST,DC=clients,DC=home,DC=bzh` |
| `AD_SERVICE_ACCOUNT_USERNAME` | `HOME\svc_api_portal_ad` |
| `AD_USE_CURRENT_WINDOWS_CREDENTIALS` | `false` |
| `KOXO_SYNC_WEBHOOK_URL` | `http://192.168.100.221:8042/internal/koxo/sync/` |
| `KOXO_SYNC_WEBHOOK_ALLOW_INSECURE_HTTP` | `true` |

Plus les deux secrets `AD_SERVICE_ACCOUNT_PASSWORD` et
`KOXO_SYNC_WEBHOOK_TOKEN` (ce dernier relu depuis
`koxo-webhook-token.txt` sur SRV-21, il n'était donc pas perdu).

Redémarrage de contrôle effectué : PID 3564 → 5308, readiness toujours
`controlled_write healthy`. La durabilité est prouvée, pas supposée.

## Faits d'annuaire utiles

- Il n'existe **aucun compte de service dans `clients.home.bzh`** — seulement
  les comptes intégrés, et le groupe « Administrateurs de KoXo Administrator
  CLIENTS » est vide. Le compte vient du domaine `HOME` par l'approbation.
- `HOME\svc_api_portal_ad` est le seul principal non intégré ayant des droits
  d'écriture sur `OU=KoXoAdm`. C'est la bonne façon de retrouver le compte :
  lire l'ACL de l'OU plutôt que deviner et risquer de verrouiller un compte en
  essayant des mots de passe.
- OU de premier niveau : `Domain Controllers`, `Groupes_TEST`, `KoXoAdm`, `RDS`.

**Why:** on croit qu'une configuration lue par un service tourne « depuis un
fichier » ; ici elle n'existait plus nulle part, et rien ne le signalait tant
que le processus vivait.

**How to apply:** avant tout redémarrage ou déploiement sur SRV-13, vérifier
que `HKLM\...\Session Manager\Environment` porte bien les `AD_*` et `KOXO_*`,
et contrôler `/health/ready` **après** redémarrage — pas seulement avant.
Voir [[deployment-topology]] et [[koxo-ad-password-mastery]].
