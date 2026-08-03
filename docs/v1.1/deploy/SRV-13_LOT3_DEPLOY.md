# Déploiement SRV-13 — V1.1 Lot 3 (comptes de démonstration / essai réel)

> Fiche de passation destinée à l'opérateur du déploiement (Codex).
> Conception : [`../V1.1_CUSTOM_DEMO_ACCOUNTS.md`](../V1.1_CUSTOM_DEMO_ACCOUNTS.md) ·
> Runbook général : [`../../DEPLOYMENT_WINDOWS.md`](../../DEPLOYMENT_WINDOWS.md) §15.

## 0. Résumé

| | |
|---|---|
| **Cible** | `KERMARIA-SRV-13` (api-internal), dossier `C:\apps\api-internal\` |
| **Paquet** | `out/kermaria-api-internal-v1.1.0.zip` (1,38 Mo) — construit depuis le tag **`v1.1.0`** |
| **SHA256** | `60BA324D9CE6EE1BEC0722EAED3554362C5D14109566289A277882F8A0D9974B` |
| **Type** | .NET 10 **framework-dependent** win-x64, avec apphost (`Kermaria.ApiInternal.exe`) |
| **Base de données** | ⚠️ **Une migration reste à appliquer** (`031_backup_policy…`) — voir §1 |
| **Ordre** | Déployer **SRV-13 avant SRV-12** — voir [`V1.1.0_DEPLOY.md`](V1.1.0_DEPLOY.md) |

> **Le tag `v1.1.0` ne contient pas que les comptes de démo** : il intègre aussi la
> remise à plat agentique (ex-releases 1.0.0.7/1.0.0.8) — CGV et politique de
> confidentialité au 03 août 2026, politique de sauvegarde des packs, scripts KoXo.
> Le paquet embarque le `SeedContent` à jour.

## 1. État de la base

- ✅ Migrations `036` / `037` / `038` **déjà appliquées sur la base prod
  `kermaria`@SRV-06** (2026-08-03). Vérifié : 4 lignes dans `demo_profiles`, tous les
  `customers` existants en `is_demo = 0`.
- ⚠️ **`031_backup_policy_public_copy_refresh.sql` est EN ATTENTE** : elle vient de la
  remise à plat et n'était pas présente lors du passage du 2026-08-03. Il faut donc
  **lancer `--apply-migrations`** (voir la procédure du runbook §« Appliquer les
  migrations » : bascule temporaire sur `kermaria_migrator` + `--environment
  Development`, le process quitte tout seul). Les `036`/`037`/`038` seront
  automatiquement ignorées, le runner les trace dans `schema_migrations`.
  Cette migration est un `UPDATE` idempotent sur la description de l'offre `SAVE-PERSO`.
- ✅ Groupes AD `GG_DEMO_NEXTCLOUD` / `GG_DEMO_RDS` / `GG_DEMO_VPN` créés dans
  `OU=Groupes_TEST,DC=clients,DC=home,DC=bzh`.
- ✅ OU des comptes démo : `OU=CLI-DEMO,OU=CLIENTS,OU=Utilisateurs,OU=KoXoAdm,DC=clients,DC=home,DC=bzh`
  (créée automatiquement par la chaîne KoXo).
- ✅ FSRM, collection RDS Clients-1, VLAN 64 (`10.35.64.0/24`, GW `10.35.64.254`).

- ✅ Groupes AD, OU `CLI-DEMO`, FSRM, RDS et VLAN 64 : voir la liste ci-dessus.

> Toutes ces migrations sont additives et idempotentes : les rejouer est sans risque.

## 2. Configuration à ajouter (⚠️ étape bloquante)

Config lue depuis le **JSON plat** `C:\ProgramData\Kermaria\api-internal.config.json`
(généré par `scripts/build-api-config.ps1` depuis `.local.env.ps1` ; les variables
d'environnement gagnent sur le fichier).

Les clés `AD_PROVISIONING_GROUP_DNS__*` sont **volontairement plates** : le code
reconstitue la section via le préfixe `__`
([`SubscriptionProvisioningRuntimeConfiguration.cs`](../../../apps/api-internal/Data/Configuration/SubscriptionProvisioningRuntimeConfiguration.cs)).
Le script d'extraction fonctionne par *blocklist*, ces clés passent donc sans modification.

```powershell
# --- Racines AD : les groupes GG_DEMO_* vivent HORS de AD_CLIENTS_OU_DN ---
$env:AD_CLIENTS_OU_DN    = "OU=KoXoAdm,DC=clients,DC=home,DC=bzh"
$env:AD_ALLOWED_ROOTS    = "OU=KoXoAdm,DC=clients,DC=home,DC=bzh;OU=Groupes_TEST,DC=clients,DC=home,DC=bzh"
$env:AD_REQUIRED_OU_ROOT = "DC=clients,DC=home,DC=bzh"

# --- DN des groupes de démo (bind direct pour ajout/retrait) ---
$env:AD_PROVISIONING_GROUP_DNS__GG_DEMO_RDS       = "CN=GG_DEMO_RDS,OU=Groupes_TEST,DC=clients,DC=home,DC=bzh"
$env:AD_PROVISIONING_GROUP_DNS__GG_DEMO_VPN       = "CN=GG_DEMO_VPN,OU=Groupes_TEST,DC=clients,DC=home,DC=bzh"
$env:AD_PROVISIONING_GROUP_DNS__GG_DEMO_NEXTCLOUD = "CN=GG_DEMO_NEXTCLOUD,OU=Groupes_TEST,DC=clients,DC=home,DC=bzh"

# --- Whitelist d'écriture : COMPLÉTER la valeur existante, ne pas la remplacer ---
$env:AD_ALLOWED_GROUPS = "TEST_SITE_WEB,GG_DEMO_RDS,GG_DEMO_VPN,GG_DEMO_NEXTCLOUD"
```

### Pièges à ne pas manquer

1. **`AD_REQUIRED_OU_ROOT` doit être remonté à la racine du domaine.** S'il reste sur
   `AD_CLIENTS_OU_DN`, `OU=Groupes_TEST` n'est plus « sous » lui, `ConfigurationValid`
   passe à **faux** et **toutes** les écritures AD sont refusées — pas seulement la démo.
   Ses composantes doivent être **uniquement des `DC=`**.
2. **Ne jamais retirer `AD_CLIENTS_OU_DN` de `AD_ALLOWED_ROOTS`** : le contrat
   `apps/webportal/scripts/verify-signup-contract.mjs` exige ≥ 2 racines, sans doublon,
   toutes sous le required-root, et `AD_CLIENTS_OU_DN` présent **exactement une fois**.
3. Élargir le required-root **ne relâche pas** la sécurité d'exécution : le contrôle réel
   des écritures reste `AD_ALLOWED_ROOTS` (liste fermée) + `AD_ALLOWED_GROUPS`.
4. **Redémarrage obligatoire** après toute modification de configuration.

### Droits du compte de service

`svc_api_portal_ad` doit pouvoir **modifier l'appartenance** des `GG_DEMO_*` dans
`OU=Groupes_TEST` et **désactiver** les comptes de `OU=CLI-DEMO`.

## 3. Déploiement du binaire

Procédure standard du runbook : copie en `-staging` **puis** bascule (jamais d'écrasement direct).

```powershell
Expand-Archive -Path .\kermaria-api-internal-v1.1-lot3.zip -DestinationPath C:\apps\api-internal-staging -Force
```

Puis arrêt du service, bascule du dossier, redémarrage :

```powershell
Stop-Service KermariaApiInternal; Move-Item C:\apps\api-internal C:\apps\api-internal-old -Force; Move-Item C:\apps\api-internal-staging C:\apps\api-internal -Force; Start-Service KermariaApiInternal
```

Réappliquer les ACL et recréer `logs\` si le dossier a été remplacé (cf. runbook §3).

> ⚠️ **Le service démarre `DemoAccountExpirationWorker`** (balayage au démarrage puis
> horaire). C'est sans effet aujourd'hui : aucun `customers.is_demo = TRUE` en base, et
> la purge filtre strictement `is_demo = TRUE`. Aucun client réel n'est atteignable.

## 4. Tâche planifiée (filet de sécurité)

Doublon du service de fond — rejoue révocation + purge puis quitte.

```powershell
.\docs\v1.1\deploy\Register-DemoExpirationTask.ps1 -AppDll 'C:\apps\api-internal\Kermaria.ApiInternal.dll' -RunAsUser 'CLIENTS\svc_api_portal'
```

L'argument `--run-demo-expiration` n'ouvre aucun port et ne demande aucune authentification.

## 5. Vérification post-déploiement

1. Le service démarre et journalise `Active Directory mode controlled_write`.
2. Aucune ligne `AD_CONFIGURATION_INVALID` dans les logs → la config des racines est bonne.
3. Contrôle du contrat de config : `npm --prefix apps/webportal run test:signup`.
4. **Recette, dans cet ordre** :
   - **`showcase-tpe`** d'abord — inerte par garde-fou dur dans le code : aucun AD,
     aucun KoXo, aucun mail, quelle que soit la configuration globale. Valide la
     création, le contenu semé et l'affichage admin sans le moindre effet réel.
   - **`trial-ad-koxo`** ensuite, avec une durée de vie **très courte** : vérifier
     l'ajout aux `GG_DEMO_*`, puis la révocation (retrait + désactivation) et la purge
     au balayage suivant.
5. Codes utiles dans les logs : `DEMO_PROVISIONING_APPLIED`,
   `DEMO_PROVISIONING_PENDING_IDENTITY` (identité KoXo pas encore créée — normal, rejoué),
   `DEMO_REVOCATION_APPLIED`, `DEMO_REVOCATION_PARTIAL` (réessayé au balayage suivant).

## 6. Hors de ce paquet — SRV-12 (webportal)

L'écran d'administration (`/admin/demo`, `/admin/demo/profiles`, entrée de navigation,
colonne « Statut ») vit dans le **webportal** et n'est **pas** inclus ici. Sans lui,
l'API est fonctionnelle mais les comptes démo ne sont pilotables que par appel direct
aux endpoints internes.

⚠️ `build:web` **ne peut pas** être lancé depuis le worktree (limite MAX_PATH Windows) :
le build du webportal doit se faire depuis le **checkout principal**, après merge de la
branche `claude/custom-demo-accounts-3b4e59`.

## 7. Retour arrière

```powershell
Stop-Service KermariaApiInternal; Remove-Item C:\apps\api-internal -Recurse -Force; Move-Item C:\apps\api-internal-old C:\apps\api-internal -Force; Start-Service KermariaApiInternal
```

Le schéma n'a pas besoin d'être défait : les migrations sont **additives** (colonnes
nullables + table + index). L'ancien binaire ignore simplement les nouvelles colonnes.
