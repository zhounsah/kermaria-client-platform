---
name: diagnostic-panne-donnees
description: "Chaîne de diagnostic quand le site public perd son contenu (packs vides, pages SEO 404, sitemap sans lastmod) : les 3 sondes qui isolent le maillon fautif en une minute, + les accès réellement disponibles depuis le poste de dev (WinRM SRV-13 par FQDN seulement, pas d'SSH vers SRV-06)."
metadata: 
  node_type: memory
  type: reference
  originSessionId: 22499f37-d426-4354-9886-e6be8317f399
  modified: 2026-08-13T16:19:32.212Z
---

Symptôme type : la vitrine rend, mais **tout le contenu venant de l'API disparaît d'un coup** — « Les packs ne sont pas encore disponibles en ligne », « Ressources indisponibles », pages SEO éditoriales absentes du sitemap et en 404, plus aucun `lastmod`. Cause = un maillon de la chaîne données, jamais le code : tout le public passe par `getPublicData()` qui renvoie une valeur vide en cas d'échec.

**Les 3 sondes, dans l'ordre** (elles isolent le maillon sans accès serveur) :
1. `https://dashboard.zachary-it.fr/api/health/ready` — sépare `configuration` (env SRV-12) de `api_internal`.
2. `http://192.168.100.213:5000/health/ready` — expose `configuration` / `persistence` / `mariadb` / `ad`. **Pas protégé par `X-Service-Auth`** : le middleware ne garde que `/internal/*`, donc interrogeable directement. Un `mariadb: unhealthy` ici = cause trouvée.
3. Handshake TCP brut sur 3306 (`TcpClient` + lecture des premiers octets) : **révèle l'erreur MariaDB sans aucun credential** — `Too many connections` arrive en clair là où `Test-NetConnection` dit juste « port ouvert ».

**Accès réels depuis le poste de dev** (vérifiés 2026-08-13) :
- WinRM vers SRV-13 : **`kermaria-srv-13.home.bzh` fonctionne, l'IP `192.168.100.213` est REFUSÉE** (auth par défaut interdite sur IP hors TrustedHosts/HTTPS). Toujours passer par le FQDN.
- Logs API lisibles à distance : `C:\apps\api-internal\logs\api-internal-AAAA-MM-JJ.log` (JSON par ligne). `Select-String 'Readiness check failed for MariaDB'` par fichier date **donne l'heure exacte de bascule** — c'est ce qui a permis de dater la panne à 08h00 pile et d'orienter vers un job planifié.
- Les variables `SQL_*` sont des **variables Machine** sur SRV-13 (le registre du service `KermariaApiInternal` a un `Environment` vide) : `[Environment]::GetEnvironmentVariables('Machine')`.
- **Pas d'accès SSH aux VM SQL** (`Permission denied (publickey,password)` sur SRV-06) : le `df -h` / `SHOW PROCESSLIST` passe forcément par ZH en console.

**Précédent 2026-08-13** : disque plein sur SRV-06 → écritures bloquées → threads empilés → `Too many connections` → `mariadb: unhealthy` → vitrine vidée ~8 h. Libérer l'espace ne suffit pas, les connexions bloquées ne retombent pas seules (`systemctl restart mariadb`). Aucun redéploiement ni purge de cache nécessaire au rétablissement : les pages publiques sont en `force-dynamic`.

⚠️ **Effet de bord SEO à connaître** : pendant une panne base, les pages éditoriales renvoient **404 et non 503** (`notFound()` sur `result.data` vide) — Google peut désindexer. Distinguer « slug inexistant » de « API injoignable » reste à faire.

Voir [[deployment-topology]] (SQL_HOST = KERMARIA-SRV-06), [[seo-vitrine-state]].
