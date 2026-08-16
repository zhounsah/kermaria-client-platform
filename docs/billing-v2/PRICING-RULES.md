# Règles tarifaires

## Formule générale

Pour les services récurrents éligibles :

```text
subtotal = somme(service_amount)
discounted_total = subtotal × (1 - reduction_contractuelle)
```

Les prestations ponctuelles ne reçoivent pas automatiquement la remise d'engagement.

Les montants doivent toujours être calculés en centimes entiers.

Les réductions sont stockées en basis points.

Lorsqu'une réduction produit un demi-centime, l'arrondi se fait au centime le
plus proche en arithmétique entière :

```text
amount_after_discount =
(amount_cents × (10000 - discount_basis_points) + 5000) / 10000
```

## Barème candidat

| Engagement / paiement | Réduction totale |
|---|---:|
| Sans engagement, mensuel | 0 % |
| 6 mois, mensuel | 10 % |
| 12 mois, mensuel | 15 % |
| 6 mois, comptant | 15 % |
| 12 mois, comptant | 20 % |

Ne pas empiler deux réductions engagement + comptant.

## Mensuel avec engagement

Les services peuvent évoluer pendant l'engagement.

Le plancher candidat est :

```text
minimum_commitment_amount =
MRR_initial_apres_remise × 45 %
```

La facture est :

```text
MAX(
  services_actuels_apres_remise,
  minimum_commitment_amount
)
```

## Paiement comptant

Le client paie la période complète.

Réduction de consommation ou de provisioning :

```text
autorisé
remboursement = 0
```

Retour jusqu'au droit contractuel déjà payé :

```text
supplément = 0
```

Upgrade au-dessus du droit acheté :

```text
complément au prorata de la période restante
```

## Prorata mensuel

Pour une modification sur un abonnement payé mensuellement :

```text
ancien tarif jusqu'à effective_at
nouveau tarif à partir de effective_at
```

Le calcul exact devra être centralisé dans le Billing Engine et couvert par des tests de dates.
