# Catalogue V2 candidat

Tous les prix ci-dessous sont HT.

## Attributs publics des paliers

Les valeurs techniques et commerciales d'un palier sont administrées dans
Billing V2.1. Le frontend peut les formater pour l'affichage, mais ne possède
aucune copie autoritaire de leurs valeurs et ne les utilise jamais pour
calculer un prix.

Le flux public est strictement en lecture seule :

```text
billing_v2_service_tier_attributes
        ↓
BillingV2PublicCatalogService
        ↓
BillingV2PublicTier.attributes
        ↓
@kermaria/shared
        ↓
WebPortal
```

Par exemple, changer `ram_gib` d'un palier de `8` à `12` met à jour la
présentation publique au prochain chargement du catalogue, sans révision
tarifaire ni modification du moteur de pricing. `publicVisible`,
`selfServiceOrderable` et `publicSelectable` restent trois décisions
commerciales indépendantes.

## Socle

| Service | Prix mensuel |
|---|---:|
| BASE-SERVICE | 6,90 € |
| SUPPORT-STANDARD | Inclus |
| MONITORING-INTERNAL | Inclus |

## Stockage personnel

| Palier | Prix mensuel |
|---:|---:|
| 16 Go | 2,00 € |
| 32 Go | 3,00 € |
| 64 Go | 5,00 € |
| 128 Go | 7,00 € |
| 256 Go | 9,90 € |
| 512 Go | 15,90 € |

512 Go peut rester non sélectionnable publiquement au lancement.

## Stockage partagé

| Palier | Prix mensuel |
|---:|---:|
| 32 Go | 3,90 € |
| 64 Go | 5,90 € |
| 128 Go | 8,90 € |
| 256 Go | 13,90 € |
| 512 Go | 19,90 € |

## Sauvegarde personnelle

| Volume protégé | Prix mensuel |
|---:|---:|
| 16 Go | 1,00 € |
| 32 Go | 2,00 € |
| 64 Go | 3,00 € |
| 128 Go | 4,00 € |
| 256 Go | 6,00 € |
| 512 Go | 9,00 € |

## Sauvegarde partagée

| Volume protégé | Prix mensuel |
|---:|---:|
| 32 Go | 2,00 € |
| 64 Go | 3,50 € |
| 128 Go | 5,00 € |
| 256 Go | 8,50 € |
| 512 Go | 12,00 € |

## VPN

Les vitesses sont des plafonds techniques internes et ne doivent pas être présentées comme des débits garantis.

| Tier | Plafond interne | Prix mensuel |
|---|---:|---:|
| Essentiel | ~100 Mbit/s | 3,90 € |
| Plus | ~250 Mbit/s | 5,90 € |
| Performance | ~500 Mbit/s | 8,90 € |
| Pro | ~1 Gbit/s | 12,90 € |

## Autres services

| Service | Prix mensuel |
|---|---:|
| RDS-ACCESS | 15,90 € / utilisateur |
| USER-ADDITIONAL | 3,90 € / utilisateur |
| SUPPORT-PLUS | 9,90 € / abonnement |

## Mise en service

`INIT-SERVICE` candidat : 12,90 € HT.

Le provisioning étant automatisé, ce tarif rémunère essentiellement la mise en service, les contrôles et la valeur du processus.
