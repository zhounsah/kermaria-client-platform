# Diagnostic public — Billing V2

> **Historique.** Le modele commercial decrit ici (`commercial_offers`, panier,
> checkout recurrent, configurateur `/configurer`) a ete retire du depot. L'etat
> courant est decrit par [`BILLING_V2_ONLY.md`](BILLING_V2_ONLY.md).

## Architecture

Le diagnostic public vit dans `apps/webportal/app/diagnostic` et conserve son questionnaire ainsi que son expérience utilisateur. Son moteur pur est `apps/webportal/lib/public-diagnostic.ts`.

Le diagnostic ne dépend plus du catalogue commercial legacy. Le flux cible est dés ormais :

```text
/diagnostic
  -> DiagnosticAnswers
  -> BillingV2PublicSelection | requires_quote
  -> POST /api/formules/devis
  -> /formules/{preset}?sélection V2 complète
  -> signup
  -> activation
  -> login
  -> /formules/reprendre
  -> Stripe
  -> provisioning Billing V2
```

`/configurer` reste un parcours legacy indépendant pour les écrans qui l'utilisent encore. Il n'est plus la sortie du diagnostic public.

## Source de vérité commerciale

Le diagnostic charge `getBillingV2FormulesCatalog()` et ne lit plus :

- `getPublicCommercialCatalog()`;
- `getPublicPackCatalogContent()`;
- `ResolvedPublicPackManifest`;
- `CatalogConfigurationInput`.

La sélection produite est un `BillingV2PublicSelection`. Elle ne contient que des codes catalogue et des choix fonctionnels. Elle ne transporte aucun prix faisant autorité.

Les prix restent exclusivement calculés par API-INTERNAL via le moteur Billing V2. Le diagnostic demande un devis au BDF existant :

```text
POST /api/formules/devis
  -> quoteBillingV2Formule
  -> BillingV2PublicQuoteBuilder
  -> BillingV2PricingEngine
```

Le navigateur peut formater les montants retournés, mais il ne les additionne ni ne les reconstruit.

## Règles du diagnostic

### Stockage personnel

Le diagnostic utilise uniquement les paliers `STRORAGE-PERSONAL` marqués `publicSelectable` dans le catalogue Billing V2.

Le questionnaire expose actuellement :

- jusqu'à 16 Go ;
- jusquğà 32 Go ;
- jusqu'à 64 Go ;
- jusquğà 128 Go ;
- jusquğà 256 Go ;
- plus de 256 Go ;
- « Je ne sais pas ¹.

Pour une valeur numérique, le moteur choisit le plus petit palier public couvrant le besoin.

`Plus de 256 Go` produit `requires_quote`. Si le volume est inconnu, le diagnostic conserve le palier de la formule de base et ajoute `storage_unknown` ; le client pourra l'ajuster dans `/formules/{preset}`.

### Utilisateurs

La sélection Billing V2 transporte `additionalUsers`, pas une plage approximative.

Le questionnaire expose donc 1 à 11 utilisateurs totaux. Le moteur calcule :

```text
additionalUsers = totalUsers - 1
```

Billing V2 autorise au maximum 10 utilisateurs supplémentaires. `12 ou plus` produit `requires_quote`.

### Choix de la formule de base

Le diagnostic conserve les mêmes intentions fonctionnelles mais les traduit vers les presets Billing V2 :

- besoin simple de stockage/sauvegarde : `pack-dossier-securise` ;
- besoin VPN : `pack-acces-distance` ;
- besoin de bureau Windows pour un cas simple : `pack-bureau-windows-distance` ;
- entreprise, association ou plusieurs utilisateurs : `pack-pro-association`.

Une structure qui demande aussi un bureau Windows conserve la base Pro / Association et reçoit `remoteDesktop: true`. Une composition Billing V2 personnalisée n'est donc plus rejetée uniquement parce qu'elle ne correspond pas à un ancien pack figé.

Le diagnostic démarre sur l'engagement `FLEX` et le paiement `monthly`. Le choix d'un engagement de 6 ou 12 mois et des modes de paiement disponibles reste dans le configurateur `/formules`.

### `requires_quote`

Le diagnostic demande un cadrage lorsqu'il ne peut pas produire une sélection publique valide, notamment :

- stockage au-delà du maximum public ;
- plus de 11 utilisateurs au total ;
- type de structure `other` ;
- preset ou engagement Billing V2 nécessaire indisponible dans le catalogue.

Les anciens motifs spécifiques `windows_storage_requires_quote` et `windows_team_requires_quote` ont disparu : le moteur Billing V2 sait représenter ces compositions.

## Résumé « Avant / Après »

`apps/webportal/lib/diagnostic-before-after.ts` ne lit plus les capacités d'un `ResolvedPublicPackManifest`.

Le résumé s'appuie sur la `BillingV2PublicSelection` :

- `backupPersonal` / `backupShared`;
- `vpnTierCode` ;
- `remoteDesktop` ;
- `storagePersonalTierCode`.

Les libellés de palier sont résolus depuis le catalogue V2. Aucun prix n'est calculé dans ce module.

## Navigation et reprise

Le CTA du diagnostic sérialise la sélection complète avec `billingV2SelectionToSearchParams()` puis ouvre :

```text
/formules/{preset}?v2=1& ... &source=diagnostic
```

`/formules/[code]` sait déjà relire cette sélection et la fournir à `BillingV2FormuleConfigurator`.

Sur la vitrine, la souscription rejoint ensuite le tunnel existant :

```text
/formules/{preset}
  -> /signup avec selection V2
  -> activation
  -> /login?next=/formules/reprendre
  -> restauration de la selection persistée
  -> /formules/{preset}
  -> checkout authoritative
```

## Persistance signup

Aucune migration SQL n'est nécessaire.

`MariaDbSignupRepository` stocke déjà la sélection Billing V2 dans `signup_pending.catalog_configuration_snapshot_json` via l'enveloppe :

```text
kind = "billing_v2"
selection = BillingV2PublicSelection
```

`SignupService.GetPendingBillingV2SelectionAsync()` et `/formules/reprendre` relisent déjà cette sélection après authentification.

Le snapshot legacy `pack_selection_snapshot_json` et le format `CatalogConfigurationInput` restent disponibles pour les parcours historiques qui les utilisent encore ; la migration du diagnostic ne les supprime pas globalement.

## Tests de contrat

Les garanties principales sont couvertes par :

```powershell
npm --prefix apps/webportal run test:diagnostic
npm --prefix apps/webportal run test:public-site-quality
npm --prefix apps/webportal run test:formules
npm --prefix apps/webportal run test:signup
```

Le contrat diagnostic vérifie notamment :

- absence des types legacy dans son moteur ;
- absence de logique de prix dans `public-diagnostic.ts` ;
- 64, 128 et 256 Go restent des sélections standard ;
- plus de 256 Go produit `requires_quote` ;
- 5 utilisateurs produisent 4 `additionalUsers` ;
- 11 utilisateurs produisent 10 `additionalUsers` ;
- 12 utilisateurs produisent `requires_quote` ;
- une configuration RDS + stockage personnalisé reste représentable ;
- le dévis passe par `/api/formules/devis` ;
- le CTA utilise `/formules/{preset}` et transporte la sélection V2 complète ;
- le configurateur legacy `/configurer` conserve ses propres contrats indépendants.
