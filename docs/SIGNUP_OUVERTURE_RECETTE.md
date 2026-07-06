# Ouverture des inscriptions — recette interne & checklist pré-public

Statut : **inscriptions ouvertes en recette interne `@home.bzh`** le 2026-07-06,
après correction de l'envoi email live (voir plus bas). Ouverture publique
**non faite** (gated V1.0 RC + hardware R740xd).

> **Déploiement effectué le 2026-07-06.** Configs régénérées et poussées sur
> SRV-01 (`webportal.config.json`) et SRV-02 (`api-internal.config.json`) avec
> les overrides staging (`PUBLIC_PORTAL_URL=https://portail.home.bzh`,
> `INTERNAL_API_URL=http://192.168.100.202:5000`, `SQL_HOST=192.168.100.207`).
> Services redémarrés : `KermariaWebportal` (SRV-01, 24 clés chargées dont
> hCaptcha, Next.js Ready, stderr vide) et `KermariaApiInternal` (SRV-02,
> `health/ready` = healthy : config + mariadb + ad). Reste la **recette
> fonctionnelle manuelle** (signup `@home.bzh` → email vérif → approbation →
> set-password), à consigner dans [V0.24_SUIVI.md](V0.24_SUIVI.md).

Références : [V0.26_SELF_SERVICE_SIGNUP.md](V0.26_SELF_SERVICE_SIGNUP.md) (cadrage
+ archi), [V0.26_USER_GUIDE_SIGNUP.md](V0.26_USER_GUIDE_SIGNUP.md),
[V0.30_EMAIL_LIVE_TEST.md](V0.30_EMAIL_LIVE_TEST.md) (allowlist),
[DEPLOYMENT_WINDOWS.md](DEPLOYMENT_WINDOWS.md) (runbook SRV-01/02/07).

## Objet

Procédure pour **ouvrir les inscriptions self-service** (parcours V0.26) en
recette interne, restreinte aux testeurs `@home.bzh`, sans lever le fail-closed
de l'allowlist email. Inclut la checklist des verrous à lever **avant** toute
ouverture publique.

## Chaîne de dépendances

Le kill switch `SIGNUP_ENABLED` ne suffit pas : le parcours n'aboutit que si
**tous** les maillons sont verts.

| Maillon | Rôle | État recette `@home.bzh` |
|---|---|---|
| `SIGNUP_ENABLED=true` | Ouvre `/signup` (webportal) + accepte `/internal/signup` (API) | ✅ requis |
| hCaptcha (`HCAPTCHA_SITE_KEY` + `HCAPTCHA_SECRET_KEY`) | Anti-bot ; en `NODE_ENV=production`, clé absente/placeholder ⇒ `CAPTCHA_MISCONFIGURED` (fail-closed) | ✅ clés de **test** always-pass |
| Email live fonctionnel | Envoi du token de vérification + email « compte approuvé » | ✅ `contact@zacharyhounsa.ovh` (587 STARTTLS) |
| `EMAIL_LIVE_ALLOWLIST` contient `@home.bzh` | Le mail de vérif vers le demandeur doit passer l'allowlist, sinon `blocked_allowlist` | ✅ `contact@…,@home.bzh` |
| `PUBLIC_PORTAL_URL` = URL portail staging | L'API construit les liens vérif/set-password depuis cette base (`SignupService.BuildUrl`) | ⚠️ **doit** valoir `https://portail.home.bzh`, pas `localhost` |
| `SIGNUP_AUTO_APPROVE=false` | Validation admin manuelle obligatoire | ✅ (voulu jusqu'à V1.0 RC) |

Le vrai blocage historique n'était pas hCaptcha (déjà déployé avec les clés de
test sur `portail.home.bzh`) mais **l'email** : le plan MX de
`support@zacharyhounsa.ovh` avait été résilié, tout envoi live échouait en
`535`. Corrigé en basculant le sender vers `contact@zacharyhounsa.ovh` (même
mot de passe). Sans email, aucune inscription ne peut aboutir (pas de token).

## Clés hCaptcha de test (recette)

Câblées dans le `.local.env.ps1` source (elles étaient déjà dans le config
déployé mais **pas** dans la source — un `build-webportal-config.ps1` les aurait
sinon supprimées, cassant le signup) :

```
HCAPTCHA_SITE_KEY   = 10000000-ffff-ffff-ffff-000000000001
HCAPTCHA_SECRET_KEY = 0x0000000000000000000000000000000000000000
```

Ces clés officielles hCaptcha valident **tout** (siteverify renvoie
`hostname: "dummy-key-pass"`). **Aucune protection anti-bot réelle** — seuls
honeypot, timing et rate-limit (3/IP/h) subsistent.

## Procédure d'ouverture (recette)

Depuis le poste de dev (les scripts lisent `.local.env.ps1`) :

**1. Webportal — SRV-01** (hCaptcha + page `/signup` tournent ici) :
```powershell
.\scripts\build-webportal-config.ps1 `
  -OutputPath \\KERMARIA-SRV-01\C$\ProgramData\Kermaria\webportal.config.json `
  -Override @{ INTERNAL_API_URL = "http://192.168.100.202:5000"; PUBLIC_PORTAL_URL = "https://portail.home.bzh" }
# sur SRV-01 :
Restart-Service KermariaWebportal
```

**2. API-internal — SRV-02** (`SIGNUP_ENABLED` + liens email) :
```powershell
.\scripts\build-api-config.ps1 `
  -OutputPath \\KERMARIA-SRV-02\C$\ProgramData\Kermaria\api-internal.config.json `
  -Override @{ SQL_HOST = "192.168.100.207"; PUBLIC_PORTAL_URL = "https://portail.home.bzh" }
Restart-Service KermariaApiInternal
```

> Toute modif de config exige un `Restart-Service` : ni le process Node
> (webportal) ni le service API ne relisent le fichier à chaud.

## Recette pas-à-pas (`@home.bzh`)

1. `https://portail.home.bzh/signup` → le formulaire s'affiche (widget hCaptcha de test).
2. Soumettre avec une adresse **`@home.bzh`** → réponse `SIGNUP_ACCEPTED`.
3. Boîte `@home.bzh` → email de vérification (expéditeur **contact@zacharyhounsa.ovh**)
   → cliquer le lien `https://portail.home.bzh/signup/verify?token=…`.
4. Côté admin : approuver la demande → email « compte approuvé » avec lien `/set-password`.
5. Définir le mot de passe → connexion. ✅

Vérifs : `email_messages` doit montrer `status=sent` pour les deux envois ; un
demandeur hors `@home.bzh` produit `blocked_allowlist` (comportement voulu).

## Checklist AVANT ouverture publique (V1.0 RC — pas maintenant)

- [ ] Enregistrer `portail.home.bzh` dans le dashboard hCaptcha et remplacer les
      **deux** clés de test par les vraies (`.local.env.ps1` → rebuild webportal → restart).
- [ ] `EMAIL_LIVE_ALLOWLIST_ONLY=false` (lève le fail-closed pour livrer aux
      demandeurs publics) — geste RC, cf. [V0.30_EMAIL_LIVE_TEST.md](V0.30_EMAIL_LIVE_TEST.md).
- [ ] Vérifier SPF/DKIM/DMARC de `zacharyhounsa.ovh` (délivrabilité hors OVH).
- [ ] Confirmer la capacité infra (gating hardware R740xd) avant d'exposer le flux.
- [ ] Décider du maintien de `SIGNUP_AUTO_APPROVE=false` (validation manuelle).
