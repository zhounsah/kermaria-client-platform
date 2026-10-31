---
name: tva-transition-2026-10-14
description: "Transition TVA Zachary IT au 01/10/2026 : calcul TTC inclusif Billing V2, archives légales, migration 102 et garde BPCE/Stripe."
metadata:
  node_type: memory
  type: project
  status: dev
---

# TVA Zachary IT — état DEV et suivi PROD

Le propriétaire a fourni les données du mémento DGFiP : réel normal de TVA
sur option depuis le **01/10/2026**, TVA intracommunautaire **FR85105111520**,
CA3 trimestrielles et micro-BNC inchangé. Le mémento original n'est pas dans
Git. Le détail des corrections et limites est dans
`docs/legal/TVA_TRANSITION_2026-10-01.md`.

## Invariants

- Les prix Billing V2 déjà publiés sont les **TTC à préserver**. Depuis le
  changement, `BillingVatPolicy` ventile HT + TVA après remises, avec les mêmes
  groupes et arrondis que les lignes Stripe. La date fiscale est un jour civil
  Paris ; les périodes mixtes et les pays non qualifiés bloquent l'automatisme.
- Un taux `NULL` ou 0 ne signifie plus « franchise actuelle ». Les lignes
  anciennes gardent leur texte et leurs montants. Les nouvelles lignes portent
  un traitement explicite ; les factures émises ne sont jamais recalculées.
- Stripe Test utilise un Tax Rate FR 20 % inclusif identifié dans le JSON API
  DEV. Deux sessions non payées, `payment` et `subscription`, ont retourné
  120,00 € TTC dont 20,00 € de TVA. Le rail vérifie montant **et** TVA ; aucun
  objet Stripe Live n'a été modifié.
- BPCE DEV est configuré `live`, donc le PDF réel du vendeur et son numéro TVA
  ne sont pas prouvés par les tests mock. Le garde
  `BPCE_SELLER_VAT_NUMBER_CONFIRMED` doit rester faux jusqu'à cette preuve :
  nouveaux Checkouts taxables et émissions BPCE réelles fermés, anciennes
  factures toujours lisibles.
- Le Cart fige l'identifiant de version de CGV accepté. Les autres parcours
  transmettent la version affichée ; API-INTERNAL vérifie qu'elle est encore
  courante et lie son identifiant au nouveau contrat.

## Preuves et limites

- Migration `102_vat_transition` appliquée seulement à `kermaria_dev` après
  sauvegarde vérifiée. La sauvegarde des tables exclut une vue et deux
  triggers faute de droits DEV ; leurs définitions sont versionnées dans `066`.
- CGV et mentions légales DEV version du 14/10/2026, trois versions archivées
  par clé (ancienne, correction initiale et version mise en forme). L'ancien
  régime n'apparaît plus dans les contenus courants DEV.
- Deux événements/factures BPCE DEV après le 01/10, totaux 31,70 € et 41,60 €,
  TVA nulle et numéros fiscaux : revue avant tout avoir ou rectificatif. Une
  troisième facture Stripe Test de 5,90 € demande rapprochement. Stripe Live
  conserve un abonnement actif de 0,50 € créé avant le changement, à revoir
  sans mutation automatique.
- Ce relevé DEV précédait la livraison PROD ultérieure. Revalider les faits
  live avant toute régularisation, activation de Checkout ou nouveau paiement.

## Complément de contrôle du 14/10/2026

- Le propriétaire confirme qu'il n'y a aucun client réel ; les factures et
  abonnements connus proviennent des tests. Conserver quand même les pièces
  fiscales réellement émises, sans communication commerciale à organiser.
- PDF BPCE `FC-TEST-2026-025` et `026` récupérés par GET, empreintes identiques
  à la base DEV. Le numéro de TVA vendeur est présent, mais le taux est 0 %
  pour les périodes françaises d'octobre et la désignation du vendeur omet
  `EI`. Le garde exige désormais une confirmation distincte de cette mention.
  DEV et PROD désignent le même profil BPCE émetteur `774259` ; corriger son
  nom affectera les futures pièces des deux environnements, sans changer les
  PDF historiques.
- Le règlement BPCE effaçait `pdf_content` via une mise à jour de validation
  avec argument PDF nul. Le correctif sépare la mise à jour du statut `paid` ;
  l'existant n'a pas été réécrit. Six PDF fiscaux DEV et trois PROD ont été
  archivés hors Git avec empreintes conformes à leurs traces SQL.
- Avant la configuration fiscale PROD, Stripe Live n'avait pas de Tax Rate
  configuré dans l'API. L'abonnement test à 0,50 € était actif, sans taxes,
  échéance au 19/10/2026. La préparation et le rollback sont dans
  `docs/releases/TVA_PROD_PREPARATION_2026-10-14.md`.
- La candidate `v2.1.1` avait été validée sur DEV. Derniers correctifs
  API DEV et WebPortal DEV installés ; DLL API
  `3FFC1641A61A755EBAE40C1FEB94D1579FCC075844836AC1799AE0D3C8499525`,
  Web DEV `vat-dev-v2.1.1-20261014-rc1`. Validation globale et navigateur
  réussies, paiement toujours fermé. Voir la fiche de release pour les hashes,
  GO/NO-GO et actions soumises à autorisation.

## Configuration API PROD du 14/10/2026

- HEAD Git observé `5ce2898` ; Web PROD observé sur
  `/opt/kermaria/releases/v2.1.1-5ce2898-prod`. L'API PROD conserve sa DLL
  SHA-256 `2A9028ECC8CA15BFD91CED5742F3B0AAC0407822C561A551CBF663935519EE61`.
- Sur demande expresse, le JSON API PROD a été sauvegardé puis enrichi du
  Tax Rate Stripe Live `txr_1UQS5FPeE7Jrh43pSi6OSIyc` (FR, 20 %, inclusif,
  actif), du numéro vendeur `FR85105111520`, de la confirmation TVA `true`
  et de la confirmation EI `false`. Le flag Checkout reste `false`.
- Ancien SHA-256 JSON et sauvegardes :
  `91E118C48278FACB03780084D5800B2FB0CF3A039DF0D6BE4C928107CE6260CB` ;
  nouveau SHA-256 :
  `9ADCF972653FE78AE9F770C420872A207E3D224771113E99BCC7BE8135510614`.
  Les 109 valeurs existantes sont inchangées. Seul `KermariaApiInternal` a
  redémarré ; `/health/ready` répond 200. Détail et chemins de sauvegarde :
  `docs/releases/V2.1.1_VAT_RUNTIME_CONFIG_2026-10-14.md`.
