---
name: billing-v2-public-diagnostic
description: "État courant du diagnostic public après migration vers Billing V2 : sélection V2, paliers publics, devis serveur et reprise signup."
metadata:
  node_type: memory
  type: reference
  modified: 2026-08-20
---

# Diagnostic public Billing V2

Depuis le 2026-08-20, `/diagnostic` ne dépend plus du moteur commercial legacy pour produire sa recommandation.

Invariants courants :

- Entrée : `DiagnosticAnswers`.
- Sortie : `BillingV2PublicSelection` ou `requires_quote`.
- Catalogue : `getBillingV2FormulesCatalog()`.
- Aucun calcul de prix dans `public-diagnostic.ts`.
- Le résultat demande le devis via `POST /api/formules/devis`, donc `quoteBillingV2Formule` / `BillingV2PricingEngine`.
- Le CTA transporte la sélection complète via `billingV2SelectionToSearchParams()` vers `/formules/{preset}`.
- Le tunnel existant reste inchangé : signup → activation → login → `/formules/reprendre` → Stripe → provisioning.
- Aucune migration SQL : la sélection V2 est déjà persistée dans `signup_pending.catalog_configuration_snapshot_json`.
- `/configurer` reste disponible pour les parcours legacy, mais n'est plus la sortie du diagnostic.

Bornes publiques retenues :

- stockage personnel : 16 / 32 / 64 / 128 / 256 Go ;
- plus de 256 Go : `requires_quote` ;
- 1 à 11 utilisateurs totaux ;
- 12 ou plus : `requires_quote` ;
- `additionalUsers = totalUsers - 1`, maximum 10 ;
- engagement initial : `FLEX`, paiement `monthly`.

Le moteur peut produire des compositions personnalisées Billing V2, notamment RDS avec stockage supérieur à l'ancien standard ou RDS ajouté à la base Pro / Association. Les anciens warnings `windows_storage_requires_quote` et `windows_team_requires_quote` ne sont plus utilisés.

Contrats principaux :

```powershell
npm --prefix apps/webportal run test:diagnostic-configurator
npm --prefix apps/webportal run test:public-site-quality
npm --prefix apps/webportal run test:formules
npm --prefix apps/webportal run test:signup
```

Documentation : `docs/DIAGNOSTIC_CONFIGURATEUR.md`.
## Production 2026-08-20
- commit applicatif : `bf535b7` ;
- SRV-12 : `/opt/kermaria/releases/20260820-093552-v1.4.0.1-bf535b7` ;
- rollback : `/opt/kermaria/releases/20260820-0936-a68d0d6` ;
- readiness privee + publique : `healthy` ;
- `/diagnostic` et `/formules` publics : HTTP 200 ;
- footer : `Version v1.4.0.1` ;
- SRV-13 non redeploye : aucune modification API/SQL/pricing/provisioning.