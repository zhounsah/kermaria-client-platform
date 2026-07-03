# Procedure de mise en production

Livrable V0.24 Brique 3. Cette procedure decrit la bascule de la
phase de tests staging (KERMARIA-SRV-01/02/07) vers l'infrastructure
definitive (R740xd), en deux temps :

- **V1.0 beta 1** : premier deploiement complet sur R740xd, sans
  client reel, avec activation progressive des modes `live`.
- **V1.0 RC** : ouverture publique, premier client reel integre,
  ouverture des signups apres validation juridique.

La procedure est **redigee**, non executee tant que la V0.24 n'est
pas cloturee et que le R740xd n'est pas livre. Elle sera raffinee
au fur et a mesure de l'execution.

Runbook d'installation infrastructure : [`DEPLOYMENT_WINDOWS.md`](DEPLOYMENT_WINDOWS.md).
Ce document precise uniquement les deltas et l'ordre de bascule
production.

## 1. Prerequis avant execution

### Hardware et OS

- Serveur R740xd livre et rack, configuration Dell iDRAC operationnelle.
- Disques configures (RAID selon plan capacite — 2x SSD systeme
  mirror, N+1 pour donnees, N+1 pour sauvegardes).
- Windows Server 2022 Standard ou Datacenter installe, patch level a
  jour a J-7.
- IP LAN statique attribuee, IP publique reservee (via l'operateur
  et/ou reverse proxy edge).
- Serveur joint au domaine `home.bzh` (**seulement** si l'OU cible
  V0.31 est deja provisionnee — sinon executer V0.31 d'abord).

### Sign-offs bloquants

Toute la V0.24 doit etre cloturee :

- Brique 1 recette staging : tableau de suivi rempli, aucun scenario
  KO ouvert, restauration MariaDB testee sur base separee.
- Brique 2 audit securite : rapport rendu, aucune vulnerabilite
  `high`/`critical` en attente, matrice secrets validee, isolation
  client verifiee.
- Brique 3 : ce document relu et valide.

V0.25 sortie AD prod cible (V0.31) executee :

- `RequiredTestOuRoot` releve dans le code (PR merge sur `main`).
- Allowlist `AD_ALLOWED_ROOTS` positionnee.
- Recette AD sur l'OU cible rejouee (search, create, rename, move,
  groups, password change).

### DNS et TLS

- Enregistrements DNS finalises et propages :
  - Zone interne `home.bzh` (sur SRV-03 DC AD) : `A` records pour
    `www`, `portail`, `dashboard`, `api` (si expose interne)
    pointant sur l'IP LAN du R740xd (ou sur le reverse proxy edge).
  - Zone publique `zacharyhounsa.ovh` (OVH) : `CNAME` ou `A`
    records `www`, `portail`, `dashboard` vers l'IP WAN. Verifier
    aussi les enregistrements SPF/DKIM/DMARC finalises en V0.30
    final.
- Certificat TLS production :
  - Option A (reprise wildcard existant) : valider la couverture des
    nouveaux hostnames par le wildcard Let's Encrypt en place
    (`*.home.bzh`, `*.zacharyhounsa.ovh`, `*.kermaria35580.ovh`) et
    son renouvellement automatique.
  - Option B (cert dedie) : monter win-acme sur R740xd, faire
    emettre un certificat propre par site avec HTTP-01 ou DNS-01.
    Preferer DNS-01 pour ne pas ouvrir 80 aux jetons Let's Encrypt.
- HSTS `Strict-Transport-Security` **pas encore active** au premier
  boot — activer apres 2 semaines de stabilite (voir section 7).

### Secrets et comptes

Aucun secret de la phase de tests staging n'est reutilise en prod.
La liste minimale a rotater et generer avant execution :

| Secret | Notes |
|---|---|
| `SQL_PASSWORD` (compte runtime `kermaria_api`) | mot de passe **ne doit pas commencer par "test"** ([`RuntimeConfigurationValidator.cs`](../apps/api-internal/Data/Configuration/RuntimeConfigurationValidator.cs)) |
| `SQL_PASSWORD` (compte DDL `kermaria_migrator`) | idem, actif uniquement pendant les migrations |
| `SERVICE_AUTH_TOKEN` | valeur forte partagee API + WEBPORTAL |
| `BPCE_REFRESH_TOKEN` | genere depuis le dashboard BPCE avec un label `RDC-PROD-<date>` |
| `PAYPAL_CLIENT_ID` / `PAYPAL_CLIENT_SECRET` | app PayPal Developer en mode `live`, distinct de l'app sandbox |
| `STRIPE_SECRET_KEY` / `STRIPE_PUBLISHABLE_KEY` / `STRIPE_WEBHOOK_SECRET` | comptes Stripe en mode `live` |
| `SMTP_PASSWORD` | boite email dediee prod (recommande : sous-domaine `mail.<domaine>`) |
| `HCAPTCHA_SECRET_KEY` / `HCAPTCHA_SITE_KEY` | site hCaptcha en mode prod |
| `AD_SERVICE_ACCOUNT_PASSWORD` (si mode `controlled_write` cote AD) | compte AD dedie service, rotation trimestrielle |
| Mot de passe du compte de service Windows (AD `HOME\svc_api_portal_ad` ou dedie prod) | valeur forte, jamais reutilisee du staging |

Consigner les credentials dans le coffre-fort d'exploitation
(gestionnaire de secrets, aucune capture, aucun ticket). Voir
[`SECRET_ROTATION.md`](SECRET_ROTATION.md) pour la procedure de
rotation.

### Sauvegardes et supervision

- Cible de sauvegarde definitive (NAS, stockage tiers) avec
  retention documentee (recommande : 30 quotidiennes + 12
  hebdomadaires + 24 mensuelles).
- Cible de supervision definitive (UptimeRobot, healthchecks.io,
  supervision interne) branchee sur `/health/ready` et
  `/api/health/ready`.

## 2. Topologie cible et deltas vs staging

```text
Internet
   │  443 (+ 80 pour redirect HSTS)
   ▼
[ R740xd — role WEBPORTAL + FRONT ]  Windows Server 2022
   IIS 443 (TLS) + ARR + URL Rewrite
      └── proxy 127.0.0.1:3000
            └── KermariaWebportal (Windows Service via NSSM + wrapper)
                  = Node.js 24 + Next standalone
   │  loopback ou VLAN prive, TCP 5000
   ▼
[ R740xd — role API-INTERNAL ]  (co-heberge sur meme serveur R740xd)
   KermariaApiInternal (Windows Service natif .NET)
      = Kermaria.ApiInternal.exe + Kestrel bind 127.0.0.1:5000
   │  VLAN prive, TCP 3306, TLS obligatoire
   ▼
[ R740xd — role MariaDB ]  (co-heberge ou machine separee)
   MariaDB 11.x avec REQUIRE SSL sur comptes applicatifs
```

**Deltas vs staging** :

- **Un seul hote R740xd** heberge tous les roles (contrairement au
  staging SRV-01 + SRV-02 + SRV-07). La segmentation reseau reste
  logique via bindings loopback ou VLAN prive.
- **TLS MariaDB obligatoire** : les comptes `kermaria_api` et
  `kermaria_migrator` sont declares `REQUIRE SSL`. Le connecteur
  API-INTERNAL positionne `SslMode=Required` (voir configuration
  API).
- **Base propre `kermaria`** (pas `test_web` reutilise) creee a
  neuf, migrations appliquees `--apply-migrations` sur base vide.
- **Comptes de service** : soit reprise du compte AD
  `HOME\svc_api_portal_ad` deja utilise en staging (audit securite
  V0.24 Brique 2 valide), soit compte dedie prod (recommande pour
  isolation permissions), a decider en Brique 2.
- **HSTS `preload=false`** au premier boot, upgrade progressif :
  `max-age=300` (5 min) sur 24h -> `max-age=86400` (1j) sur 1
  semaine -> `max-age=31536000` (1 an) stable -> preload uniquement
  en V1.0 RC apres validation continue.
- **Journalisation supervision** : logs applicatifs vers agent
  central (Windows Event Log Forwarding ou agent SIEM), pas
  seulement fichier local.

## 3. Etape 0 — Preparation hors chemin critique

Peut se faire **plusieurs jours avant** l'execution de la bascule
sur R740xd. N'engage pas la production.

- Rotation des secrets sur les fournisseurs :
  - BPCE : generer un `BPCE_REFRESH_TOKEN` prod avec label
    `RDC-PROD-<date>`. Documenter dans le coffre. Ne pas encore
    injecter dans la config.
  - PayPal : creer une app `live` distincte, generer
    `PAYPAL_CLIENT_ID` / `PAYPAL_CLIENT_SECRET`. Configurer les
    webhook endpoints avec l'URL cible
    `https://portail.<domaine>/api/webhooks/paypal`.
  - Stripe : basculer sur les cles `live`, generer un
    `STRIPE_WEBHOOK_SECRET` pour l'endpoint
    `https://portail.<domaine>/api/webhooks/stripe`.
  - hCaptcha : cle prod avec le domaine autorise.
  - SMTP : provisionner la boite prod (recommande via
    sous-domaine dedie `mail.<domaine>` pour la reputation).
- DNS : mettre en place les enregistrements avec TTL court (5 min)
  pour permettre les ajustements de dernier moment. Une fois la
  bascule stable, remonter le TTL a 1h.
- Sauvegarde de reference staging : `npm run backup:mariadb` sur
  SRV-07 vers stockage tiers.

## 4. Etape 1 — Bootstrap R740xd

Suit strictement la sequence 0-4 du runbook
[`DEPLOYMENT_WINDOWS.md`](DEPLOYMENT_WINDOWS.md) :

1. Prerequis operateur : creation ou reprise du compte de service
   Windows (`HOME\svc_api_portal_ad` ou compte dedie prod).
2. Preparation des dossiers `C:\apps\`, `C:\ProgramData\Kermaria\`
   avec ACL restrictives (langue-neutre SIDs).
3. Installation runtimes : .NET 10 Runtime + Node.js 24 LTS +
   NSSM + IIS + URL Rewrite + ARR (voir section 5 du runbook).
4. Build depuis le poste de dev (`dotnet publish` + `npm build`),
   transfert des artefacts vers `C:\apps\api-internal-staging\` et
   `C:\apps\webportal-staging\` sur R740xd.

**Deltas prod** :

- Ne pas activer `KermariaApiInternal` ni `KermariaWebportal` avant
  d'avoir monte MariaDB et cree la base.
- Le pare-feu doit etre configure avec les regles minimum de la
  section 7 du runbook.

## 5. Etape 2 — MariaDB production

### Installation

MSI MariaDB 11.x LTS. Choisir un mot de passe root fort, refuser
"remote root access". Installer sur la partition dediee (RAID N+1).

### Configuration TLS obligatoire

Editer `my.ini` :

```ini
[mysqld]
bind-address = <IP_privee_R740xd>
skip-name-resolve
default-storage-engine = InnoDB
innodb_buffer_pool_size = 2G          # ajuster selon RAM R740xd

# TLS obligatoire — chemins des cert/cle a preparer avant
ssl-ca = C:/mariadb-ssl/ca.pem
ssl-cert = C:/mariadb-ssl/server-cert.pem
ssl-key = C:/mariadb-ssl/server-key.pem
require_secure_transport = ON
```

Provisionner les certificats MariaDB via une CA interne (le DC
`home-KERMARIA-SRV-03-CA` peut delivrer un certif serveur) ou via
OpenSSL en local (documenter la procedure de renouvellement).

### Base et comptes applicatifs

Depuis un client `mysql` local, en SSL :

```sql
CREATE DATABASE kermaria
  DEFAULT CHARACTER SET utf8mb4
  DEFAULT COLLATE utf8mb4_unicode_ci;

CREATE USER 'kermaria_api'@'127.0.0.1' IDENTIFIED BY '<mdp_prod_fort>'
  REQUIRE SSL;

CREATE USER 'kermaria_migrator'@'127.0.0.1' IDENTIFIED BY '<mdp_prod_fort>'
  REQUIRE SSL;

GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE
  ON kermaria.* TO 'kermaria_api'@'127.0.0.1';

GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, DROP, INDEX,
      REFERENCES, TRIGGER
  ON kermaria.* TO 'kermaria_migrator'@'127.0.0.1';

FLUSH PRIVILEGES;
```

Sauvegarde de reference **avant** toute application de migration :

```powershell
npm run backup:mariadb
```

## 6. Etape 3 — API-INTERNAL production

### Config JSON externe

Deposer `C:\ProgramData\Kermaria\api-internal.config.json` (soit
manuellement, soit via `scripts/build-api-config.ps1` avec un
fichier `.env.ps1` prod dedie hors du repo). Contenu minimal
initial :

```json
{
  "LOG_LEVEL": "Information",
  "LOG_FILE_DIRECTORY": "C:\\apps\\api-internal\\logs",
  "LOG_FILE_LEVEL": "Information",
  "LOG_FILE_RETENTION_DAYS": "90",
  "SQL_PROVIDER": "mariadb",
  "SQL_HOST": "127.0.0.1",
  "SQL_PORT": "3306",
  "SQL_DATABASE": "kermaria",
  "SQL_USERNAME": "kermaria_api",
  "SQL_PASSWORD": "<mdp_prod_fort>",
  "SQL_USE_SSL": "true",
  "SERVICE_AUTH_TOKEN": "<token_partage>",
  "SESSION_DURATION_MINUTES": "480",
  "LOGIN_MAX_FAILURES": "5",
  "LOGIN_LOCKOUT_MINUTES": "15",
  "AD_INTEGRATION_MODE": "disabled",
  "BPCE_INTEGRATION_MODE": "disabled",
  "EMAIL_INTEGRATION_MODE": "disabled",
  "PAYPAL_MODE": "sandbox",
  "STRIPE_MODE": "test",
  "SIGNUP_ENABLED": "false",
  "PUBLIC_VITRINE_ENABLED": "false",
  "AD_PASSWORD_CHANGE_ENABLED": "false"
}
```

Restreindre les ACL :

```powershell
icacls C:\ProgramData\Kermaria\api-internal.config.json /inheritance:r `
  /grant:r '*S-1-5-32-544:F' `
  /grant:r '<compte_service_prod>:R'
```

### Migrations sur base propre

En session PowerShell elevee **temporairement** avec l'utilisateur
`kermaria_migrator` :

```powershell
$env:SQL_USERNAME = "kermaria_migrator"
$env:SQL_PASSWORD = "<mdp_migrator>"

C:\apps\api-internal\Kermaria.ApiInternal.exe --environment Development --apply-migrations
```

Verifier `schema_migrations` contient 001 -> 020 (ou la version en
vigueur). **Fermer la session PowerShell** apres pour purger les env
locaux.

### Enregistrement du service

```powershell
$cred = Get-Credential -UserName "<compte_service_prod>" `
  -Message "Mot de passe compte service"

New-Service -Name "KermariaApiInternal" `
  -BinaryPathName '"C:\apps\api-internal\Kermaria.ApiInternal.exe" --environment Production --urls http://127.0.0.1:5000' `
  -DisplayName "Kermaria API Internal (Production)" `
  -Description "Config from C:\ProgramData\Kermaria\api-internal.config.json." `
  -StartupType Automatic `
  -Credential $cred

sc.exe failure KermariaApiInternal reset= 86400 actions= restart/5000/restart/10000/restart/30000
```

**Note** : `--environment Production` (pas `Staging`).
`RuntimeConfigurationValidator` applique les checks les plus stricts
en Production ; toute placeholder secret ou mode incoherent bloque
le start.

### Bootstrap du premier admin

Immediat apres le premier start reussi :

```powershell
C:\apps\api-internal\Kermaria.ApiInternal.exe --environment Production --seed-admin
```

Prompt masque, hash PBKDF2, aucun credential en args CLI. Voir
[README.md](../README.md) "Bootstrap du premier admin" pour la
procedure detaillee.

## 7. Etape 4 — WEBPORTAL production

Suit la sequence 4 du runbook staging avec les deltas suivants :

- Config JSON externe `C:\ProgramData\Kermaria\webportal.config.json`
  avec `NODE_ENV=production`, `INTERNAL_API_URL=http://127.0.0.1:5000`
  (loopback si co-heberge, sinon VLAN prive), `SERVICE_AUTH_TOKEN`
  identique cote API, `SESSION_COOKIE_SECURE=true` obligatoire.
- Wrapper `start-webportal.ps1` copie et NSSM installe pointant sur
  le compte de service prod.

## 8. Etape 5 — IIS front production

### Sites IIS

Sequence 5 du runbook, avec les hostnames de production finaux.
Wildcard reutilise ou cert prod dedie selon prerequis TLS.

Deux sites :

- `kermaria-vitrine` sur les hostnames publics `www.*`.
- `kermaria-portal` sur les hostnames backoffice
  (`portail.*`, `dashboard.*` ou noms retenus).

### HSTS progressif

Ne **pas** activer HSTS `preload` au premier boot. Ajouter dans
le `web.config` du site vitrine et portal, apres 2 semaines de
stabilite :

```xml
<httpProtocol>
  <customHeaders>
    <add name="Strict-Transport-Security"
         value="max-age=31536000; includeSubDomains" />
  </customHeaders>
</httpProtocol>
```

`preload` uniquement en V1.0 RC apres soumission a
[hstspreload.org](https://hstspreload.org/).

## 9. Etape 6 — Bascule des modes vers `live`

**Ordre strict**. Chaque bascule est independante et testable. Ne
pas passer a la suivante sans validation de la precedente.

### 9.1 `AD_INTEGRATION_MODE=controlled_write`

Prerequis : V0.31 executee, OU cible provisionnee, cert LDAP TLS
valide.

Editer `api-internal.config.json` :

```json
{
  "AD_INTEGRATION_MODE": "controlled_write",
  "AD_DOMAIN": "home.bzh",
  "AD_CLIENTS_OU_DN": "OU=CLIENTS,DC=home,DC=bzh",
  "AD_SERVICE_ACCOUNT_USERNAME": "HOME\\svc_ad_kermaria",
  "AD_SERVICE_ACCOUNT_PASSWORD": "<mdp_prod_fort>"
}
```

`Restart-Service KermariaApiInternal`. Verifier `/health/ready`
retourne `ad=healthy` (non `disabled`).

Test controle : lecture d'un utilisateur AD reel via
`/admin/customers/{ref}/ad/users/{sam}`. Aucun ecart de scope
attendu (l'OU cible est bornée par `AD_ALLOWED_ROOTS` livree en
V0.31).

### 9.2 `BPCE_INTEGRATION_MODE=live`

Prerequis : cible BPCE production activee, `BPCE_REFRESH_TOKEN`
prod obtenu.

Verification prealable :

```powershell
$env:BPCE_INTEGRATION_MODE = "live"
$env:BPCE_REFRESH_TOKEN = "<inject_prod>"
C:\apps\api-internal\Kermaria.ApiInternal.exe --verify-bpce-sender
```

Doit lister les profils de facturation sans 401.

Injecter `BPCE_INTEGRATION_MODE=live` et `BPCE_REFRESH_TOKEN` dans
`api-internal.config.json`, `Restart-Service`. Emettre une facture
de controle sur un client fictif interne (ex. la sentinel INTERNAL
creee par `--seed-admin`). Verifier :

- Facture visible cote dashboard BPCE avec numero fiscal reel.
- PDF telechargeable via `/admin/commercial-documents/{id}/pdf`.
- Table `bpce_invoices` locale contient la ligne avec hash SHA-256.

Si erreur : rebasculer `BPCE_INTEGRATION_MODE=disabled`, corriger,
creer un avoir cote BPCE pour la facture emise. Une facture BPCE
validee est **immuable cote banque**.

### 9.3 `PAYPAL_MODE=live`

Prerequis : app PayPal live enregistree, webhook endpoint
configure sur `https://portail.<domaine>/api/webhooks/paypal`.

Bascule dans `webportal.config.json` **et** `api-internal.config.json`
en meme temps (les deux composants lisent `PAYPAL_MODE`) :

```json
{
  "PAYPAL_MODE": "live",
  "PAYPAL_CLIENT_ID": "<live_id>",
  "PAYPAL_CLIENT_SECRET": "<live_secret>",
  "PAYPAL_WEBHOOK_ID": "<live_webhook_id>",
  "PAYPAL_WEBHOOK_VERIFY": "true"
}
```

⚠️ Le validator API refuse `PAYPAL_MODE=live` sans les 3 variables
correspondantes (gap identifie en V0.29 non retrofit — a livrer en
V1.0 RC).

Redemarrer les deux services. Test : creer un paiement one-shot de
1 EUR depuis un compte test PayPal, verifier la reception du
webhook `PAYMENT.SALE.COMPLETED`, la creation de la facture BPCE
associee, l'email de confirmation.

### 9.4 `STRIPE_MODE=live`

Prerequis : compte Stripe en mode live, webhook endpoint configure
sur `https://portail.<domaine>/api/webhooks/stripe`.

Bascule identique :

```json
{
  "STRIPE_MODE": "live",
  "STRIPE_SECRET_KEY": "<live_secret>",
  "STRIPE_PUBLISHABLE_KEY": "<live_publishable>",
  "STRIPE_WEBHOOK_SECRET": "<live_webhook_secret>"
}
```

`RuntimeConfigurationValidator` refuse `STRIPE_MODE=live` sans les
3 variables non-placeholder (garde-fou en dur, contrairement a
PayPal). Test : Checkout Session `mode=payment` de 1 EUR, webhook
`payment_intent.succeeded`, facture BPCE, email.

### 9.5 `EMAIL_INTEGRATION_MODE=live` avec allowlist stricte

Prerequis : DNS SPF/DKIM/DMARC finalises (V0.30 final), boite prod
provisionnee, cert SMTP TLS valide.

Bascule prudente :

```json
{
  "EMAIL_INTEGRATION_MODE": "live",
  "EMAIL_LIVE_ALLOWLIST_ONLY": "true",
  "EMAIL_LIVE_ALLOWLIST": "ops@<domaine>,test@<domaine>",
  "SMTP_HOST": "<host_prod>",
  "SMTP_PORT": "587",
  "SMTP_USE_STARTTLS": "true",
  "SMTP_USERNAME": "<user_prod>",
  "SMTP_PASSWORD": "<mdp_prod>",
  "SMTP_FROM_ADDRESS": "<from_prod>",
  "SMTP_FROM_DISPLAY_NAME": "Kermaria"
}
```

`EMAIL_LIVE_ALLOWLIST_ONLY=true` reste actif pendant 1 semaine avec
elargissement progressif de l'allowlist. Verifier chaque envoi
reussi arrive **Inbox** (pas spam) sur Gmail, Outlook, boite
interne, boite domain client de test.

Une fois valide : passer a `EMAIL_LIVE_ALLOWLIST_ONLY=false` (mais
garder l'allowlist en place comme filet de securite en cas de
regression).

### 9.6 `SIGNUP_ENABLED=true` (V1.0 RC uniquement)

Prerequis :

- V1.0 beta 1 validee et stable depuis 2 semaines minimum.
- CGV publiees et referencees dans `/signup` (V0.27).
- hCaptcha prod configure.
- Validation juridique du parcours signup (protection donnees,
  duree conservation, CNIL).

Bascule dans `api-internal.config.json` et `webportal.config.json` :

```json
{
  "SIGNUP_ENABLED": "true",
  "HCAPTCHA_SITE_KEY": "<prod_site_key>",
  "HCAPTCHA_SECRET_KEY": "<prod_secret_key>"
}
```

Rate limit `SIGNUP_RATE_LIMIT_PER_HOUR` (3 par defaut) suffisant
au demarrage, ajuster selon telemetrie.

### 9.7 `PUBLIC_VITRINE_ENABLED=true` (V1.0 RC uniquement)

Prerequis : contenu redactionnel finalise (pages `/a-propos`,
`/mentions-legales`, `/politique-confidentialite`, `/cgv`), SEO
verifie.

Bascule dans `webportal.config.json` :

```json
{
  "PUBLIC_VITRINE_ENABLED": "true"
}
```

Depuis V0.24 staging, l'`X-Robots-Tag: noindex, nofollow` est
strippe par l'outbound rule IIS du site `kermaria-vitrine` — la
vitrine est indexable des la bascule.

`sitemap.xml` et `robots.txt` s'activent automatiquement (les
routes sont gates sur le meme flag). Soumettre le sitemap a Google
Search Console et Bing Webmaster Tools.

## 10. Recette V1.0 beta 1

Rejouer la recette V0.24 Brique 1 **sur R740xd**. Aucun scenario ne
peut etre skippe.

Livrables :

- Tableau de suivi rempli, colonnes OK/KO/commentaire par scenario.
- Verification que la journalisation n'expose aucun secret
  (procedure grep de la Brique 2).
- Verification des sauvegardes automatiques (au moins 3 sauvegardes
  quotidiennes reussies avant sortie de beta 1).
- Verification de la supervision externe : au moins un test de
  panne simulee (arret manuel du service) declenche l'alerte
  attendue en < 5 min.

## 11. Plan de continuite

### Objectifs

- **RPO cible** : 24h max (derniere sauvegarde MariaDB reussie).
  Cible V1.0 RC : 6h (sauvegardes toutes les 6h en dehors des
  fenetres a fort trafic).
- **RTO cible** : 4h en mode degrade (base restauree + services
  UP, modes `disabled` pour BPCE/PayPal/Stripe le temps de
  verifier).

### Scenarios couverts

- **Perte MariaDB** : restauration depuis dernier dump sur base
  vide, verification `schema_migrations`, redemarrage services.
  Voir [`BACKUP_RESTORE.md`](BACKUP_RESTORE.md).
- **Perte R740xd** (host complet) : reprovisionnement depuis les
  artefacts et le config JSON (les deux vivent hors de R740xd sur
  le poste de dev et le coffre-fort). Reinjection secrets depuis
  le coffre.
- **Perte AD** : basculer `AD_INTEGRATION_MODE=disabled` dans la
  config, notifier utilisateurs de la degradation partielle. Le
  reste du portail (paiement, factures, support) continue de
  fonctionner.
- **Compromission secret** : rotation urgente selon
  [`SECRET_ROTATION.md`](SECRET_ROTATION.md), revocation des
  sessions actives, notification aux utilisateurs si necessaire.
- **Panne fournisseur externe** :
  - BPCE indisponible : `BPCE_INTEGRATION_MODE=disabled`, factures
    non emises temporairement (aucune obligation legale
    d'immediatete), communication au client.
  - PayPal / Stripe indisponible : basculer sur l'autre rail
    unique (les deux rails sont parallels depuis V0.29), ou
    `disabled` si les deux tombent (rare).
  - SMTP indisponible : `EMAIL_INTEGRATION_MODE=mock` temporaire,
    envois logges pour rejeu manuel. Attention aux emails
    transactionnels critiques (signup verification) : bloquer
    temporairement `SIGNUP_ENABLED=false` si SMTP down > 1h.

### Scenarios hors scope V1.0 beta 1

- **Haute disponibilite multi-hote** : reservee V2.x. En V1.0, un
  seul R740xd = SPOF materiel. Mitigation via garantie hardware +
  contrat de maintenance rapide.
- **Reprise sur site secondaire** : reservee V2.x.
- **PRA automatise** : reservee V2.x. En V1.0, le PRA est
  operationnel manuel (le present document).

## 12. Rollback

### Rollback par etape

Chaque bascule de la section 9 est reversible :

| Mode active | Rollback |
|---|---|
| `AD_INTEGRATION_MODE=controlled_write` | passer a `disabled`, restart. Aucune mutation AD faite ne se rejoue. |
| `BPCE_INTEGRATION_MODE=live` avec facture emise | passer a `disabled`. Creer un avoir cote BPCE pour la facture. Une facture BPCE validee est **immuable** cote banque. |
| `PAYPAL_MODE=live` | passer a `sandbox`, restart. Un paiement PayPal capture reste chez PayPal — remboursement via dashboard PayPal. |
| `STRIPE_MODE=live` | passer a `test`, restart. Remboursement via dashboard Stripe. |
| `EMAIL_INTEGRATION_MODE=live` | passer a `mock`. Les emails deja envoyes ne se rappellent pas. |
| `SIGNUP_ENABLED=true` | passer a `false`. Les signups en cours (email_pending) restent, l'admin peut les traiter ou les purger. |
| `PUBLIC_VITRINE_ENABLED=true` | passer a `false`. Les routes vitrine retournent 404 immediatement, sitemap.xml/robots.txt aussi gates. |

### Rollback complet vers staging

Scenario extreme (incident majeur sur R740xd en V1.0 beta 1) :

1. Retirer le R740xd du trafic (arret IIS ou basculement DNS vers
   maintenance page hostee ailleurs).
2. Rebasculer les DNS `zacharyhounsa.ovh` publics vers l'IP
   staging SRV-01 si elle est encore up.
3. Documenter l'incident, RCA, correctifs, replanifier la
   bascule R740xd.

**Une fois V1.0 RC atteinte** (premier client reel), ce rollback
complet n'est plus applicable — la restauration depuis sauvegarde
est la seule voie.

## 13. Sign-offs execution

Chaque bascule de la section 9 est loguee et signee dans un
registre de mise en production :

| Etape | Date | Operateur | Verification | Commentaire |
|---|---|---|---|---|
| 1. Bootstrap R740xd | | | ping + rdp OK | |
| 2. MariaDB TLS | | | connexion SSL kermaria_api | |
| 3. API-INTERNAL Production | | | /health/ready 200 | |
| 4. --seed-admin | | | login admin OK | |
| 5. WEBPORTAL | | | /api/health/ready 200 | |
| 6. IIS + TLS | | | https://portail 200 | |
| 7. Recette V1.0 beta 1 | | | tableau OK | |
| 8.1 AD controlled_write | | | lecture user AD | |
| 8.2 BPCE live | | | facture controle | |
| 8.3 PayPal live | | | paiement 1 EUR | |
| 8.4 Stripe live | | | paiement 1 EUR | |
| 8.5 SMTP live + allowlist | | | Inbox Gmail | |
| 8.6 SIGNUP_ENABLED (V1.0 RC) | | | validation juridique | |
| 8.7 PUBLIC_VITRINE (V1.0 RC) | | | sitemap Google | |
| HSTS max-age 1 an | | | apres 2 semaines stable | |
| HSTS preload | | | soumission hstspreload.org | |

## 14. Suite et evolutions

Une fois la V1.0 RC en production stable :

- **Sauvegardes chiffrees** hors site : documenter la procedure de
  chiffrement + stockage tiers (Azure Blob, S3, NAS distant).
- **Rotation des secrets** planifiee : reconduction annuelle
  minimum, immediat sur suspicion. Voir
  [`SECRET_ROTATION.md`](SECRET_ROTATION.md).
- **Audit securite periodique** : rejouer la Brique 2 de V0.24 tous
  les 6 mois.
- **Passage V2.x** : haute dispo, reprise sur site secondaire, PRA
  automatise — hors scope V1.0.

Voir aussi [`DEPLOYMENT.md`](DEPLOYMENT.md), [`DEPLOYMENT_WINDOWS.md`](DEPLOYMENT_WINDOWS.md),
[`SECURITY.md`](SECURITY.md), [`SECRET_ROTATION.md`](SECRET_ROTATION.md),
[`OPERATIONS.md`](OPERATIONS.md), [`BACKUP_RESTORE.md`](BACKUP_RESTORE.md),
[`AD_PRODUCTION_MIGRATION.md`](AD_PRODUCTION_MIGRATION.md).
