---
name: hcaptcha-signup-state
description: Inscriptions ouvertes en recette @home.bzh (2026-07-06) ; signup tourne avec les clés hCaptcha DUMMY (zéro protection, à remplacer avant V1.0) ; gotcha remoteip IPv6 lien-local corrigé
metadata: 
  node_type: memory
  type: project
  originSessionId: fc8bfbd2-8c9c-45eb-824a-e7f251b98025
---

État hCaptcha du signup self-service V0.26 sur `portail.home.bzh` (SRV-01), constaté le 2026-07-05.

**Clés factices en prod = aucune protection anti-bot réelle.** La config déployée (`C:\ProgramData\Kermaria\webportal.config.json`) utilise la **paire de test hCaptcha** :
- `HCAPTCHA_SITE_KEY = 10000000-ffff-ffff-ffff-000000000001`
- `HCAPTCHA_SECRET_KEY = 0x0000000000000000000000000000000000000000`
- siteverify renvoie alors `hostname: "dummy-key-pass"`, accepte le token dummy `10000000-aaaa-bbbb-cccc-000000000001`, et **valide n'importe quoi**.

**À FAIRE avant V1.0** : enregistrer le domaine `portail.home.bzh` dans le dashboard hCaptcha et remplacer les DEUX clés par les vraies (même site) dans le `.env.ps1`, puis `build-webportal-config.ps1` → `Restart-Service KermariaWebportal`. Sinon le signup est ouvert aux bots (seuls honeypot + timing + rate-limit 3/IP/h restent). Voir [[roadmap-current]].

**Gotcha remoteip corrigé (commit `0cdb0e7` sur main).** Le reverse proxy interne injecte un `x-forwarded-for` IPv6 lien-local **avec zone index** (`fe80::1%12`) que hCaptcha rejette (`invalid-remoteip`) → siteverify échouait pour tout client LAN, aplati en `CAPTCHA_FAILED` générique. `verifyHCaptcha` retire désormais le `%…` et valide via `net.isIP` (⚠️ `net.isIP` sur Node 24 considère l'adresse zonée comme valide — d'où le strip explicite du zone index).

**Ouverture recette `@home.bzh` (2026-07-06).** Inscriptions ouvertes pour testeurs `@home.bzh` uniquement (allowlist email fermée). Le vrai blocage n'était pas hCaptcha (déjà déployé) mais l'**email** : plan MX `support@` résilié → sender basculé sur `contact@zacharyhounsa.ovh`, cf [[smtp-ovh-live-config]]. Sans email fonctionnel, aucune inscription n'aboutit (pas de token de vérif). Les clés hCaptcha de test étaient dans le config déployé mais **pas** dans `.local.env.ps1` source — désormais ajoutées, sinon un `build-webportal-config.ps1` les supprimait (⇒ `CAPTCHA_MISCONFIGURED`). Procédure complète + checklist pré-public : `docs/SIGNUP_OUVERTURE_RECETTE.md`. Prérequis liens email : `PUBLIC_PORTAL_URL=https://portail.home.bzh` en -Override API (sinon liens vérif/set-password en localhost).

**Règle ops** : toute modif de `webportal.config.json` exige `Restart-Service KermariaWebportal` — le process Node ne relit pas le fichier à chaud. Voir [[deployment-topology]].

**Diag rapide d'un CAPTCHA_FAILED** : les 4 causes internes (`CAPTCHA_MISCONFIGURED` / `REQUIRED` / `FAILED` / `UNAVAILABLE`) sont désormais loguées via `logBffFailure` (category `captcha`, + `error-codes` hCaptcha) dans le stderr du webportal (`C:\apps\webportal\logs\stderr.log`) — le message client reste générique (non-leak). Tester hCaptcha en direct depuis SRV-01 : `Invoke-RestMethod https://hcaptcha.com/siteverify -Method Post -Body @{secret=...;response=...}`.
