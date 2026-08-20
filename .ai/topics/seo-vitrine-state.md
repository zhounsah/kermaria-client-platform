---
name: seo-vitrine-state
description: "SEO vitrine www.zacharyhounsa.ovh — v1.1.12 livrée ET déployée en production le 2026-08-06, vérifiée sur le HTML servi. Reste : GBP, contenu, resoumission sitemap, chantier ISR."
metadata: 
  node_type: memory
  type: project
  originSessionId: 326e39e6-ff3d-491d-9776-85b0f8c59d7c
  modified: 2026-08-06T12:53:22.408Z
---
Etat courant 20/08/2026 : domaine public canonique = https://zachary-it.fr. Les references a www.zacharyhounsa.ovh ci-dessous sont historiques. Voir docs/DOMAIN_MIGRATION_2026-08-20.md.


Passe SEO **v1.1.12** (audit du 2026-08-05, rapport dans
`C:\Users\zhounsah\Documents\Dev\seo\`) : six défauts corrigés — canonical
par page, balisage `LocalBusiness`/`WebSite`/`Service`/`BreadcrumbList`
(`lib/seo.tsx`), un seul `h1` sur les pages à contenu administrable,
`og:image` générée, `/solutions` + `/signup` hors index, URL du sitemap
normalisées. Doc : `docs/v1.1/V1.1.12_SEO_BALISAGE.md`.

**État au 2026-08-06 : mergée `main`, taguée `v1.1.12`, poussée, et
DÉPLOYÉE en production** (release SRV-12
`20260806-125033-manual-webportal`, bascule faite à la main par ZH).
Vérifié sur le HTML servi : 12 canonical, un seul `h1` partout,
`og:image` + `twitter:card=summary_large_image`, `noindex, follow` sur
`/solutions` et `/signup`, 11 URL au sitemap sans `/solutions`, les 4 blocs
JSON-LD parsent, 301 apex et `home.bzh` intacts.

Je n'ai pas d'accès SSH à SRV-12 : `ssh zhounsah@KERMARIA-SRV-12.home.bzh`
(192.168.100.212) répond `Permission denied (publickey,password)`, et la
bascule est intégralement en `sudo`. Toute future mise en production
demandera donc les mains de ZH, sauf dépôt d'une clé publique + `NOPASSWD`
ciblé. Runbook : `docs/WEBPORTAL_SRV12_DEPLOYMENT.md`.

**Why:** l'écart dépôt/production est invisible depuis le code et se
rattrape mal : croire la vitrine corrigée alors que Googlebot lit encore
l'ancien HTML fausserait toute lecture de la Search Console.

**How to apply:**
- Avant de conclure quoi que ce soit sur le SEO en ligne, vérifier le HTML
  servi, pas le dépôt : `curl -sS https://www.zacharyhounsa.ovh/ | grep -c 'rel="canonical"'`.
- Deux pièges documentés qui ressemblent à des régressions et n'en sont pas :
  la canonical de l'accueil est **sans** slash final (Next normalise tout
  chemin racine ; seul `trailingSlash: true` changerait ça) et `/solutions`
  et `/signup` sont volontairement **absentes** du `Disallow` de
  `robots.txt`, sinon leur `noindex` ne serait jamais lu.
- Les vrais leviers restants ne sont pas du code : fiche Google Business
  Profile, contenu répondant à de vraies requêtes, resoumission du sitemap.
  Le chantier ISR reste ouvert (voir [[roadmap-current]] et le TODO dans
  `app/layout.tsx`).
- Empaqueter depuis une ref Git explicite, jamais depuis un dossier local :
  voir [[workflow-preferences]] pour le contournement MAX_PATH.
