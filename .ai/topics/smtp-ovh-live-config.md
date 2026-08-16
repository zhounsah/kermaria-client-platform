---
name: smtp-ovh-live-config
description: "Conf SMTP OVH validée pour l'envoi live + technique de diag curl ; la régression email 2026-07-05 était un plan MX résilié, pas du code."
metadata: 
  node_type: memory
  type: project
  originSessionId: 0b5fc129-5275-47a2-b4fd-373feb9fe8c2
---

Envoi email live (mode `EMAIL_INTEGRATION_MODE=live`) via OVH. Sender applicatif :
`contact@zacharyhounsa.ovh` sur `ssl0.ovh.net:587` **STARTTLS** (SMTP_USE_STARTTLS=true).
`SMTP_FROM_ADDRESS` DOIT être égal au compte authentifié (OVH refuse sinon), et le
destinataire doit figurer dans `EMAIL_LIVE_ALLOWLIST` sous peine de `blocked_allowlist`
(cf [[roadmap-current]] brique V0.30). Les 4 clés liées au sender vont ensemble :
`SMTP_USERNAME`, `SMTP_FROM_ADDRESS`, `EMAIL_LIVE_ALLOWLIST`, `CONTACT_FORM_RECIPIENT`.

Régression du 2026-07-05 (recette V0.24, bloc V0.30) : envoi live échouait en
`535/530 Authentication required`. Ce n'était PAS le code — le plan MX de
`support@zacharyhounsa.ovh` avait été résilié. Nouvelle adresse `contact@zacharyhounsa.ovh`
créée (même mot de passe), les 4 clés basculées vers `contact@` dans `.local.env.ps1`,
re-validé `235 Authentication successful` + mail queued.

**Diag SMTP à réutiliser** (isole creds vs transport vs réseau, indépendant de l'app) :
`curl.exe -v --ssl-reqd "smtp://ssl0.ovh.net:587" --mail-from X --mail-rcpt Y --user "X" --upload-file probe.eml`
(587 STARTTLS) ou `smtps://ssl0.ovh.net:465` (SSL implicite, ce que fait le client
desktop OVH). Le `-v` montre le dialogue AUTH : `235`=ok, `535`=mauvais mot de passe,
`530`=session non authentifiée. NB : `System.Net.Mail` (LiveEmailService) masque un `535`
OVH en `530 MustIssueStartTlsFirst` — trompeur. Ne fait QUE du STARTTLS, jamais SSL
implicite/465 : passer à MailKit serait requis si OVH n'exposait que 465.

Ne jamais mettre les `SMTP_*` en variables d'env Machine sur SRV-02 : elles écrasent
le JSON `api-internal.config.json` (env > fichier dans Program.cs). Voir [[deployment-topology]].
