---
name: srv11-security-headers
description: "En-têtes de sécurité SRV-11 — doublons RÉSOLUS le 2026-08-05 ; app = source de vérité ; SSH OK en zhounsah mais sudo interactif ; gabarit nginx du dépôt périmé, ne jamais déployer tel quel."
metadata: 
  node_type: memory
  type: project
  originSessionId: 5091a314-79a7-435b-95ba-6df760db654f
  modified: 2026-08-04T23:11:40.318Z
---

Décidé le 2026-08-04 (livré en `v1.1.10.2`) : **l'application est source de
vérité unique des en-têtes de sécurité**, le proxy nginx SRV-11
(`192.168.100.211`, nginx/1.28.3 Ubuntu) doit relayer sans ajouter.

**RÉSOLU le 2026-08-05.** Les `add_header` ont été retirés de
`/etc/nginx/sites-available/kermaria` sur SRV-11 (sauvegarde
`kermaria.bak-20260804T230719Z`), nginx rechargé, `assert:security:headers`
passe. Il y en avait **huit**, pas quatre : une série par bloc TLS (FQDN
principaux + `portfolio`) — le gabarit du dépôt n'en montrait qu'une, s'y fier
aurait laissé la moitié en place.

**Reste ouvert** : `kermaria-tls.pending` sur SRV-11, inactif, porte encore les
quatre directives — elles resurgiraient lors d'une future bascule TLS.

⚠️ **Ne jamais déposer `scripts/r740xd-vm/srv11/kermaria-nginx.conf` par-dessus
le vhost de production.** Gabarit périmé : 83 lignes contre 148, 2 blocs
`server` contre 4, TLS sur `443` alors que la prod écoute
`127.0.0.1:8443 ssl proxy_protocol`, et vhost `portfolio.zacharyhounsa.ovh`
absent. Un `cp` casserait la prod. Éditer en place, jamais remplacer.

Faits non redérivables du dépôt :

- **La conf nginx EST versionnée** — corrigé le 2026-08-05, l'inverse avait été
  écrit en `v1.1.10.2` faute d'avoir regardé ailleurs que `main`. Elle vit sur
  la branche **`codex/r740xd-automation`**, fichier
  `scripts/r740xd-vm/srv11/kermaria-nginx.conf`, **lignes 46-49** : les quatre
  `add_header` fautifs, dont `X-Frame-Options "SAMEORIGIN"` et l'ordre
  `camera/microphone/geolocation` qui correspondent au caractère près à la
  prod. C'est la source à corriger, pas seulement le serveur. Réflexe à
  retenir : chercher dans **toutes** les branches avant de conclure qu'un
  fichier d'infra n'est pas versionné.
- **Accès SRV-11** : `ssh -i ~/.ssh/kermaria_srv12 zhounsah@192.168.100.211`
  fonctionne (même clé que SRV-12, `claude-code-deploy@RDC-07`). Le compte à
  utiliser est **`zhounsah`**, pas `claude-code-deploy` — l'échec initial venait
  de là, pas de la clé. En revanche **`sudo` exige une authentification
  interactive** : lecture seule depuis une session d'agent, toute modification
  passe par une main humaine. L'origine `192.168.100.212:3000` est injoignable
  directement (filtrage) — seul SRV-11 l'atteint.
- **Chaîne devant WEBPORTAL** : NAT → SRV-11:443 → **HAProxy en `mode tcp`**
  (routage SNI ; `rdgateway.home.bzh` → SRV-27, défaut → `127.0.0.1:8443` en
  PROXY v2) → nginx → SRV-12:3000. HAProxy ne lisant pas le HTTP, il ne peut
  pas toucher aux en-têtes : nginx est le seul injecteur possible. `cloudflared`
  tourne aussi mais en tunnel à jeton (ingress côté Cloudflare, invérifiable
  localement) et ne sert pas `www.zacharyhounsa.ovh` (DNS public = 82.67.32.172).
- **DNS interne en vue dédoublée** (SRV-19 rend `192.168.100.211`) : un `curl`
  depuis le LAN mesure le chemin direct, pas forcément celui d'un visiteur.
- **`add_header` nginx n'écrase jamais la valeur amont, il en ajoute une
  seconde.** Pour imposer une valeur au proxy : `proxy_hide_header` **puis**
  `add_header … always`. Et un `add_header` dans un `location` annule tous ceux
  hérités de `server`/`http`.
- **Un `X-Frame-Options` contradictoire n'est PAS ignoré** : vérifié
  empiriquement sous Chromium le 2026-08-04 (iframe same-origin, réponse
  `DENY` + `SAMEORIGIN` sans CSP) → cadrage **bloqué**, échec fermé. Donc pas
  de perte d'anti-clickjacking, contrairement à la crainte initiale.
- Les tests contrat (`test:operations`, `test:seo`) **lisent le code source** :
  ils ne voient jamais le proxy. Seul `assert:security:headers` couvre ce qui
  est réellement livré. Même angle mort pour le `X-Robots-Tag` à ne pas
  réintroduire (cf. [[deployment-topology]]).

Docs : `docs/SECURITY.md` (décision + écart), `docs/OPERATIONS.md` (procédure +
rollback), `docs/v1.1/deploy/V1.1.0_DEPLOY.md` (bloc « à faire sur SRV-11 »).
