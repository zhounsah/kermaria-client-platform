---
name: bpce-invoicing-api
description: "Reference for the Banque Populaire invoicing API that will power V0.20 — endpoints, key naming, rate limits, and the rule that the credential targets production."
metadata: 
  node_type: memory
  type: reference
  originSessionId: 316dd2c1-620c-4ba1-833b-0b5d317971ba
---

L'utilisateur a un acces a l'API de gestion de factures de la Banque Populaire pour V0.20.

**URLs documentation** (gated derriere session BPCE, WebFetch echoue) :
- API Vente : `https://www.gestion-factures.banquepopulaire.fr/inv/api/v5`
- API hors Vente (Redoc) : `https://www.gestion-factures.banquepopulaire.fr/api/v5/redoc/`

**Cle API active** (dashboard BPCE) :
- Nom : `Test API (RDC-07)`
- Permissions : `api:full`
- Date creation : 18/06/2026
- Refresh token : NE JAMAIS persister dans le repo, dans cette memoire, ni dans `WEBPORTAL`. Vit uniquement dans un secret cote `API-INTERNAL` (ex: `.env.local`, variable `BPCE_REFRESH_TOKEN`).
- Authentification : JWT (refresh -> access). Detail dans la section Authentication du Redoc.

**Rate limit declare cote dashboard** :
- 1 000 requetes / heure
- 10 000 requetes / jour

**Environnement** : l'utilisateur **suppose que la cle pointe en production** (non confirme via doc puisque celle-ci est gatee). Consequence : tout appel reussi cree des donnees reelles cote BPCE.

**How to apply :**
- `BPCE_INTEGRATION_MODE` dans `API-INTERNAL` a les valeurs `disabled` (defaut), `mock`, `live`. Aucun appel HTTP sortant en `disabled` ou `mock`.
- Aucune valeur de token, meme partielle, ne doit etre ecrite ailleurs que dans un secret applicatif. Si un token apparait dans un diff, c'est un incident.
- Le bascule en `live` doit etre une decision explicite de l'utilisateur, jamais un defaut.
- Respecter le rate limit en `live` : budget conservateur, file d'attente locale si besoin.
- Cette doc evolue : verifier la version de l'API (v5 aujourd'hui) avant chaque integration de nouvel endpoint.

Voir aussi [[infra-r740xd-blocker]] (pourquoi le mode reste defaut `disabled` pendant la phase de tests) et [[roadmap-current]] (positionnement V0.20).
