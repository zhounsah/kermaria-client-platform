# Billing V2.1 — Exposition publique des attributs de paliers

## Contexte

Billing V2.1 possède déjà des attributs commerciaux structurés sur les paliers de services, notamment pour les VPS.

Exemple actuel sur `VPS-LOCAL / MEDIUM` :

- `vcpu_count = 4 count`
- `ram_gib = 8 GiB`
- `disk_gib = 80 GiB`

Ces attributs sont administrables dans le Centre de configuration / Catalogue, mais ils ne sont actuellement pas projetés dans le catalogue public Billing V2 et ne peuvent donc pas être rendus sur les parcours publics.

Le but de ce chantier n'est **pas** de créer un nouveau modèle VPS ni un catalogue parallèle.

Le but est de prolonger proprement le modèle Billing V2.1 existant afin que les caractéristiques commerciales d'un palier puissent suivre ce flux :

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
        ↓
présentation commerciale
```

## Objectif fonctionnel

Un palier contenant :

```text
MEDIUM
vcpu_count = 4 count
ram_gib    = 8 GiB
disk_gib   = 80 GiB
```

doit pouvoir être affiché côté public sous une forme lisible telle que :

```text
Medium
4 vCPU · 8 Go RAM · 80 Go stockage
```

Les valeurs doivent provenir **exclusivement du catalogue Billing V2.1**.

Aucune capacité VPS ne doit être codée en dur dans React, dans un seed frontend ou dans une table de correspondance métier parallèle.

---

# 1. Audit préalable obligatoire

Avant toute modification, lire au minimum :

```text
AGENTS.md
.ai/MEMORY.md

apps/api-internal/Services/BillingV2PublicCatalogModel.cs
apps/api-internal/Services/BillingV2PublicCatalogService.cs
apps/api-internal/Services/BillingV2CatalogAdministrationService.cs
apps/api-internal/Contracts/BillingV2CatalogAdminContracts.cs

packages/shared/src/index.ts

apps/webportal/lib/billing-v2-formules.ts
apps/webportal/components/BillingV2DirectSubscribe.tsx

tests/api-internal/BillingV2PublicCatalogTests.cs
```

Inspecter aussi la migration ayant créé :

```text
billing_v2_service_tier_attributes
```

Confirmer son schéma exact avant d'écrire la lecture publique.

Ne créer aucune migration si le schéma existant suffit.

---

# 2. Étendre le modèle public des paliers

Ajouter une représentation publique structurée des attributs.

Forme conceptuelle :

```csharp
public sealed record BillingV2PublicTierAttribute(
    string Code,
    decimal? ValueNumeric,
    string? ValueText,
    string? Unit);
```

Puis enrichir `BillingV2PublicTier` avec une collection d'attributs :

```csharp
IReadOnlyList<BillingV2PublicTierAttribute> Attributes
```

Préférer une collection vide à `null` si cela reste cohérent avec les conventions actuelles du projet.

## Contraintes

Les attributs sont des **métadonnées commerciales et techniques d'affichage**.

Ils ne doivent jamais :

- déterminer un prix ;
- modifier `MonthlyAmountCents` ;
- modifier `PriceComponents` ;
- contourner `BillingV2PricingEngine` ;
- devenir une donnée tarifaire autoritaire envoyée par le navigateur.

---

# 3. Alimenter les attributs depuis MariaDB

Modifier `BillingV2PublicCatalogService`.

Le catalogue public doit lire les attributs depuis :

```text
billing_v2_service_tier_attributes
```

et les rattacher au bon palier.

## Stratégie recommandée

Éviter un `JOIN` naïf :

```text
service × tier × price × attribute
```

qui dupliquerait les lignes de prix et compliquerait la résolution des composantes tarifaires.

Préférer :

1. la lecture actuelle services / tiers / prix ;
2. une lecture séparée des attributs ;
3. un regroupement par identifiant de palier ;
4. l'injection des attributs lors de la construction de `BillingV2PublicTier`.

Exemple conceptuel :

```text
tier_id -> attributes[]
```

## Garanties attendues

- ordre déterministe ;
- absence de doublons ;
- valeur numérique conservée comme numérique ;
- valeur texte conservée comme texte ;
- unité conservée ;
- collection vide lorsqu'un palier n'a aucun attribut ;
- aucune contamination entre deux paliers.

---

# 4. Contrat TypeScript partagé

Étendre les types publics Billing V2 dans :

```text
packages/shared/src/index.ts
```

Forme conceptuelle :

```ts
export type BillingV2PublicTierAttribute = {
  code: string;
  valueNumeric: number | null;
  valueText: string | null;
  unit: string | null;
};
```

Puis ajouter sur `BillingV2PublicTier` :

```ts
attributes: BillingV2PublicTierAttribute[];
```

Respecter exactement les conventions de sérialisation actuelles API-INTERNAL ↔ WebPortal.

---

# 5. Ne pas introduire de logique VPS dans l'API

La projection publique doit rester générique.

Elle peut exposer par exemple :

```text
vcpu_count
ram_gib
disk_gib
```

mais elle ne doit pas contenir de logique du type :

```text
vcpu_count => "vCPU"
ram_gib    => "RAM"
disk_gib   => "stockage"
```

Cette transformation relève de la présentation frontend.

Le backend doit pouvoir exposer demain, sans refonte :

```text
storage_gib
retention_days
users_count
bandwidth_mbps
mailboxes
```

ou tout autre attribut administré.

---

# 6. Couche de présentation WebPortal

Créer un helper de présentation des attributs de palier, par exemple :

```ts
describeTierAttributes(tier)
```

Le nom exact est libre tant qu'il respecte les conventions du projet.

Pour les codes actuellement utiles aux VPS :

```text
vcpu_count
ram_gib
disk_gib
```

le rendu attendu est de type :

```text
4 vCPU
8 Go RAM
80 Go stockage
```

et éventuellement :

```text
4 vCPU · 8 Go RAM · 80 Go stockage
```

## Règle stricte

Le frontend peut connaître un **mapping de libellés et de formatage**, mais jamais les valeurs des paliers.

Interdit :

```ts
if (tier.code === "MEDIUM") {
  return "4 vCPU · 8 Go RAM · 80 Go stockage";
}
```

Autorisé :

```text
vcpu_count + valeur 4 -> "4 vCPU"
```

---

# 7. Intégration sur `/souscrire`

`BillingV2DirectSubscribe` affiche actuellement essentiellement le `tier.label`.

Faire évoluer l'UX afin que les caractéristiques du palier sélectionné soient visibles.

Deux possibilités :

## Option A — tout dans le select

```text
Nano — 1 vCPU · 1 Go RAM · 15 Go stockage
Micro — ...
Small — ...
Medium — 4 vCPU · 8 Go RAM · 80 Go stockage
```

## Option B — recommandée

Conserver un select court :

```text
Palier
[ Medium ▼ ]
```

et afficher juste dessous :

```text
4 vCPU · 8 Go RAM · 80 Go stockage
```

Cette option est préférable si elle s'intègre naturellement à l'UI existante et reste plus propre sur mobile.

---

# 8. Réutilisation sur les autres surfaces publiques

Chercher toutes les utilisations de :

```text
BillingV2PublicTier
tier.label
resolveTierLabel
selectableTiers
```

Examiner notamment :

```text
/souscrire
/formules
récapitulatif de configuration
diagnostic / recommandation
devis public
```

Ne pas afficher systématiquement toutes les caractéristiques partout.

Principe :

- carte commerciale : bénéfice et lisibilité ;
- configurateur / sélection : specs utiles à la décision ;
- récapitulatif : caractéristiques pertinentes ;
- devis : seulement si cohérent avec le design et le contrat existants.

---

# 9. Respect strict des flags commerciaux

Ne pas modifier la sémantique de :

```text
publicVisible
selfServiceOrderable
publicSelectable
```

Ces trois notions restent distinctes.

Exemple valide :

```text
Service publicVisible         = true
Service selfServiceOrderable  = false
Tier publicSelectable         = true
```

Cela permet de montrer publiquement les variantes d'un service sans rendre le service commandable seul en libre-service.

La tâche ne doit pas transformer `publicSelectable` en `selfServiceOrderable`, ni l'inverse.

---

# 10. Vérifier les paliers internes sans casser les presets

Le catalogue public peut avoir besoin de connaître certains paliers même lorsqu'ils ne sont pas sélectionnables publiquement, notamment lorsqu'ils sont référencés par une formule publique.

Vérifier le comportement existant.

L'ajout des attributs ne doit pas :

- casser les presets publics ;
- masquer un palier nécessaire à une formule ;
- exposer inutilement des informations internes sur une surface qui ne devrait pas les montrer.

Si un problème indépendant est découvert :

1. le documenter ;
2. le classer séparément ;
3. ne pas élargir le périmètre sans justification.

---

# 11. Tests backend

Étendre :

```text
tests/api-internal/BillingV2PublicCatalogTests.cs
```

Au minimum couvrir les cas suivants.

## 11.1 Palier avec attributs numériques

Entrée :

```text
vcpu_count = 4 count
ram_gib    = 8 GiB
disk_gib   = 80 GiB
```

Attendu : les trois attributs ressortent exactement sur le bon palier.

## 11.2 Palier sans attribut

Attendu :

```text
attributes = []
```

## 11.3 Valeur texte

Vérifier qu'une valeur textuelle reste textuelle.

## 11.4 Isolation

Les attributs d'un palier `SMALL` ne doivent jamais apparaître sur `MEDIUM`.

## 11.5 Régression tarifaire

Vérifier que l'ajout des attributs ne change pas :

```text
MonthlyAmountCents
PriceComponents
quote
discounts
setup fees
```

Le prix reste entièrement contrôlé par le moteur de pricing existant.

---

# 12. Tests frontend / guards

Ajouter ou étendre les tests et guards existants.

Cas minimum :

```text
MEDIUM
vcpu_count = 4
ram_gib = 8
disk_gib = 80
```

doit permettre d'obtenir :

```text
4 vCPU · 8 Go RAM · 80 Go stockage
```

Tester aussi :

- un attribut absent ;
- un attribut inconnu ;
- une valeur texte ;
- aucun attribut ;
- des attributs dans un ordre différent.

Un code d'attribut inconnu ne doit jamais casser le rendu.

Choisir un comportement stable :

- soit l'ignorer ;
- soit utiliser une représentation générique si le projet possède déjà un mécanisme adapté.

Ne pas inventer de libellé hasardeux.

---

# 13. Documentation

Documenter le principe d'autorité :

> Les valeurs techniques et commerciales d'un palier sont administrées dans Billing V2.1. Le frontend peut les formater pour l'affichage, mais ne possède aucune copie autoritaire de leurs valeurs.

Documenter également le flux :

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

---

# Contraintes de chantier

- Ne pas créer de catalogue VPS parallèle.
- Ne pas hardcoder les capacités Nano/Micro/Small/Medium dans le frontend.
- Ne pas modifier `BillingV2PricingEngine` sans nécessité démontrée.
- Ne jamais utiliser les attributs pour recalculer un prix côté navigateur.
- Ne pas ajouter de migration si le schéma actuel suffit.
- Ne pas confondre `publicSelectable` et `selfServiceOrderable`.
- Ne pas casser les presets qui utilisent un palier non sélectionnable.
- Respecter BFF / CSRF / permissions existants.
- Ne pas introduire de nouvelle écriture publique vers le catalogue.
- Garder la projection publique en lecture seule.
- Éviter les refactors hors périmètre.
- Ne pas push.
- Ne pas tag.
- Ne pas créer de release.
- Ne pas déployer.

---

# Validation finale

Avant de considérer le chantier terminé :

1. exécuter les tests concernés ;
2. exécuter les guards pertinents ;
3. vérifier les diffs complets ;
4. vérifier l'absence de migration inutile ;
5. vérifier qu'aucune valeur VPS n'a été codée en dur ;
6. vérifier qu'aucun calcul tarifaire n'a migré vers le frontend ;
7. vérifier les surfaces publiques concernées ;
8. vérifier la compatibilité avec les presets existants ;
9. vérifier qu'un attribut inconnu ne casse pas le rendu.

---

# Critère d'acceptation principal

Après modification d'un attribut dans l'administration, par exemple :

```text
VPS local / Medium
ram_gib : 8 -> 12
```

un rechargement du catalogue public doit permettre au WebPortal d'afficher automatiquement :

```text
4 vCPU · 12 Go RAM · 80 Go stockage
```

sans :

- modification de code ;
- duplication des caractéristiques ;
- modification du prix ;
- modification du moteur de pricing.

Billing V2.1 doit rester l'autorité unique des caractéristiques commerciales du palier et de leur valeur.

