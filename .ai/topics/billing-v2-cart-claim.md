# Reprise Cart et vidage (2026-09-23)

La règle métier actuelle donne priorité au Cart customer `open` existant pour
la même devise. `claim_current` conserve ce Cart, expire transactionnellement
le Cart anonyme concurrent et renvoie `CART_CLAIM_RESUMED`. Aucun item n'est
fusionné. Le BFF supprime alors le cookie anonyme ; la session client porte
l'ownership du Cart conservé. Le checkout doit réaligner une ancienne URL
`/souscription?cart=B` sur A avant toute relecture du quote.

`clear` conserve l'identifiant et le statut `open`, supprime les CartItems,
réinitialise preset/engagement/paiement et incrémente `version`. Les anciens
quotes restent historiques mais leur version n'est plus acceptable. Les
index de slots garantissent toujours l'unicité owner/devise. Test de claim
concurrent MariaDB disponible uniquement en opt-in sur cible jetable.
Validé le 2026-09-26 sur base jetable SRV-06 (courses avec et sans panier
client, `claim` explicite, `clear` d'un panier rempli, mutants détectés).
Le `FOR UPDATE` du claim est redondant avec le verrou de l'expiration ciblée
qui le précède : ne pas retirer les deux.

Voir `docs/billing-v2/CART_CLAIM_AND_CLEAR.md` et les tests
`BillingV2CartSchemaTests` / `BillingV2CartPolicyTests`.
