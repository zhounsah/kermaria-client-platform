# Mapping legacy vers Billing V2

## Principe critique

Le mapping d'une offre legacy vers un preset V2 exprime sa **lignée commerciale**.

Il ne faut jamais utiliser le contenu actuel du preset V2 pour reconstruire automatiquement les droits historiques d'un client.

Exemples :

- Bureau legacy : stockage 32 Go ; Bureau V2 : 64 Go.
- Pro legacy : pas d'espace partagé V2 128 Go.
- ACCES-VPN legacy : tier exact non déterminé dans les données disponibles.

Les droits historiques doivent être reconstruits à partir des `technical_service_references`.

## 20 offres

| Legacy | Preset V2 | Engagement | Paiement |
|---|---|---|---|
| PACK-DOSSIER-1M-MENS | Dossier sécurisé | FLEX | monthly |
| PACK-DOSSIER-6M-MENS | Dossier sécurisé | 6 mois | monthly |
| PACK-DOSSIER-6M-COMPT | Dossier sécurisé | 6 mois | upfront |
| PACK-DOSSIER-12M-MENS | Dossier sécurisé | 12 mois | monthly |
| PACK-DOSSIER-12M-COMPT | Dossier sécurisé | 12 mois | upfront |
| PACK-ACCES-1M-MENS | Accès sécurisé | FLEX | monthly |
| PACK-ACCES-6M-MENS | Accès sécurisé | 6 mois | monthly |
| PACK-ACCES-6M-COMPT | Accès sécurisé | 6 mois | upfront |
| PACK-ACCES-12M-MENS | Accès sécurisé | 12 mois | monthly |
| PACK-ACCES-12M-COMPT | Accès sécurisé | 12 mois | upfront |
| PACK-BUREAU-1M-MENS | Bureau à distance | FLEX | monthly |
| PACK-BUREAU-6M-MENS | Bureau à distance | 6 mois | monthly |
| PACK-BUREAU-6M-COMPT | Bureau à distance | 6 mois | upfront |
| PACK-BUREAU-12M-MENS | Bureau à distance | 12 mois | monthly |
| PACK-BUREAU-12M-COMPT | Bureau à distance | 12 mois | upfront |
| PACK-PRO-1M-MENS | Pro / Association | FLEX | monthly |
| PACK-PRO-6M-MENS | Pro / Association | 6 mois | monthly |
| PACK-PRO-6M-COMPT | Pro / Association | 6 mois | upfront |
| PACK-PRO-12M-MENS | Pro / Association | 12 mois | monthly |
| PACK-PRO-12M-COMPT | Pro / Association | 12 mois | upfront |

## Traduction technique legacy

| Référence legacy | V2 |
|---|---|
| STOCK-PERSO-32 | STORAGE-PERSONAL 32 |
| STOCK-SUP-32 | +32 Go à agréger, jamais un item V2 séparé |
| SAVE-PERSO | BACKUP-PERSONAL au tier du stockage résolu |
| ACCES-VPN | VPN-ACCESS LEGACY |
| ACCES-RDS | RDS-ACCESS |
| SUPERV-SERVICE | absorbé dans BASE-SERVICE |
| SUPPORT-LV1 | absorbé dans BASE-SERVICE |
| SUPPORT-LV2 | SUPPORT-PLUS |
| USER-ADD | USER-ADDITIONAL |
| DOC-TECH | droit ponctuel historique, pas un récurrent V2 |

## Prix des contrats existants

Un contrat legacy actif ne doit pas être repricé silencieusement avec le catalogue V2.

Le mécanisme `subscription_price_locks` conserve le prix réellement contracté jusqu'au point de transition prévu.

Au renouvellement, la bascule vers les tarifs V2 doit être explicite.
