# Migration des domaines Zachary IT - 20 aout 2026
Ce document est la **source de verite actuelle** pour les domaines publics du projet `kermaria-client-platform`.
Les documents de versions anterieures peuvent encore citer `zacharyhounsa.ovh` lorsqu'ils decrivent un etat historique.
## Topologie canonique
| Surface | Domaine canonique |
| --- | --- |
| Site public | `https://zachary-it.fr` |
| Portail client | `https://dashboard.zachary-it.fr` |
| Administration | `https://administration.zachary-it.fr` |
| Portfolio personnel | `https://portfolio.zacharyhounsa.ovh` |
| Wiki | `https://wiki.zacharyhounsa.ovh` |
`www.zachary-it.fr` est un alias et redirige en `301` vers `https://zachary-it.fr`.
## Compatibilite des anciens domaines
SRV-11 conserve les redirections permanentes suivantes en preservant chemin et query string :
- `https://zacharyhounsa.ovh/*` -> `https://zachary-it.fr/*` ;
- `https://www.zacharyhounsa.ovh/*` -> `https://zachary-it.fr/*` ;
- `https://dashboard.zacharyhounsa.ovh/*` -> `https://dashboard.zachary-it.fr/*` ;
- `https://administration.zacharyhounsa.ovh/*` -> `https://administration.zachary-it.fr/*`.
Les endpoints `/api/webhooks/*` de l'ancien dashboard restent temporairement proxies vers le webportal pour absorber les retries fournisseurs et la transition PayPal. Ils ne doivent pas devenir des redirections HTTP.
## Aliases internes
- `dashboard.home.bzh` et `portail.home.bzh` -> `https://dashboard.zachary-it.fr` ;
- `administration.home.bzh` -> `https://administration.zachary-it.fr` ;
- `home.bzh` / `www.home.bzh` -> `https://zachary-it.fr`.
## DNS et TLS
Les noms publics suivants pointent vers `82.67.32.172` :
- `zachary-it.fr` ;
- `www.zachary-it.fr` ;
- `dashboard.zachary-it.fr` ;
- `administration.zachary-it.fr`.
Le certificat servi par SRV-11 couvre `zachary-it.fr` et `*.zachary-it.fr`. Il conserve aussi la famille `zacharyhounsa.ovh`, necessaire aux redirections et aux sous-domaines personnels/techniques encore utilises.
## Configuration applicative
```text
PUBLIC_PORTAL_URL=https://dashboard.zachary-it.fr
WEBPORTAL_BASE_URL=https://dashboard.zachary-it.fr
```
Ces valeurs sont alignees sur SRV-12 et SRV-13. Les liens publics/SEO utilisent `https://zachary-it.fr` via `PUBLIC_SITE_URL`.
## Paiements
Endpoints canoniques :
```text
https://dashboard.zachary-it.fr/api/webhooks/stripe
https://dashboard.zachary-it.fr/api/webhooks/paypal
```
Stripe a ete bascule sur le nouvel endpoint le 20 aout 2026. Tant que la migration PayPal n'est pas explicitement confirmee, conserver le passthrough de l'ancien endpoint PayPal sur SRV-11.
## Deploiements de reference
- `89fc2830dd3d9b646f0ee0fce8027ca4f0e7eae1` - `Fix public canonical domain routing` ;
- `cf949e7f49148e7ea980105ace69860b5c2ad2c0` - `Migrate portal hosts to zachary-it.fr` ;
- `f76182c3d68415401529434849daf907e2427001` - `Keep portal roots on portal hosts`.
Release webportal de reference apres migration :
```text
/opt/kermaria/releases/20260820-172637-portal-roots-f76182c
```
## Controles operationnels
```bash
curl -I https://zachary-it.fr/
curl -I https://dashboard.zachary-it.fr/login
curl -I https://administration.zachary-it.fr/login
curl -I 'https://zacharyhounsa.ovh/offres?from=legacy'
curl -I 'https://dashboard.zacharyhounsa.ovh/profile?from=legacy'
curl https://dashboard.zachary-it.fr/api/health/ready
```
Attendus : site et portails en `200`, anciens domaines en `301` vers leurs nouveaux canoniques, readiness `healthy`.
## Regle pour les futures modifications
Ne jamais faire un remplacement global de `zacharyhounsa.ovh` : `portfolio.zacharyhounsa.ovh` et `wiki.zacharyhounsa.ovh` restent volontairement utilises. Toute evolution doit distinguer vitrine, client, admin, portfolio, wiki et webhooks.