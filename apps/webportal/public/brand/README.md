# Zachary IT — Brand Guide (2026, figé)

Ce document est la référence de marque. Statut : **verrouillé** — géométrie du monogramme, palette et typographie ne doivent plus être modifiées sans repasser par une décision explicite de refonte.

## Logo officiel

Le monogramme est un **Z tracé comme un chemin de liaison** : deux barres horizontales reliées par une diagonale, un raccord doux en bas à gauche (Bézier cubique, tangente horizontale, alignement exact avec la barre haute sur `x = 15` d'une grille 48), et un **nœud actif** en haut à droite (deux cercles concentriques) qui symbolise le point supervisé du réseau.

Tracé source de vérité (grille 48×48) :
```
M15 15H33L17.12 30.88C16.06 31.94 14.3 33 15 33H33
```
Variante optique petites tailles (≤ 32 px, trait renforcé à 4, nœud r 5/3,4) :
```
M15 15H33L17.83 30.17C16.77 31.23 14.3 33 15 33H33
```

### Fichiers logo (`logo/`)
| Fichier | Usage |
|---|---|
| `zachary-it-logo.svg` | Logo horizontal principal, fond clair |
| `zachary-it-logo-dark.svg` | Logo horizontal, à poser sur fond sombre |
| `zachary-it-logo-light.svg` | Logo horizontal monochrome blanc, fonds colorés/photo |
| `zachary-it-symbol.svg` | Symbole seul, principal (bloc encre, nœud bleu) |
| `zachary-it-symbol-blue.svg` | Symbole seul, bloc bleu |
| `zachary-it-symbol-black.svg` | Symbole monochrome noir (impression 1 ton) |
| `zachary-it-symbol-white.svg` | Symbole monochrome blanc, sans bloc |
| `zachary-it-symbol-outline.svg` | Variante contour — réservée aux tailles ≥ 40 px |
| `zachary-it-symbol-optical.svg` | Variante optique, tailles ≤ 32 px |

## Fonds autorisés
Encre `#0B1220` (défaut), blanc/brume `#F8FAFC`, bleu `#2563EB`. Jamais de dégradé derrière le logo.

## Tailles minimales
- Symbole seul : 16 px (favicon), avec variante optique en dessous de 32 px.
- Logo horizontal : 96 px de large minimum (texte illisible en dessous).
- Contour : jamais sous 40 px, il perd sa densité.

## Zone de protection
Marge libre autour du logo ≥ la hauteur d'une barre du monogramme (soit 1/16 de la largeur du bloc). Ne rien accoler dans cette zone : texte, bords de page, autres logos.

## Palette
| Rôle | Couleur | Hex |
|---|---|---|
| Bleu action | `--brand-blue` | #2563EB |
| Bleu appui / hover | `--brand-blue-dark` | #1D4ED8 |
| Ciel (accent nœud, fonds sombres) | `--brand-sky` | #38BDF8 |
| Encre (fond de marque) | `--brand-ink` | #0B1220 |
| Slate 900 (texte) | `--slate-900` | #0F172A |
| Slate 500 (texte secondaire) | `--slate-500` | #64748B |
| Bordure | `--slate-border` | #E2E8F0 |
| Surface | `--surface` | #F8FAFC |

Voir `tokens/tokens.css` et `tokens/tokens.json` pour la liste complète, incluant les couleurs d'état.

## Typographie
- **Inter** : tout le discours (titres, corps, boutons, tableaux).
- **JetBrains Mono** : uniquement les libellés techniques — références, seuils, horodatages, badges d'état.
- 1 à 3 poids : 400 (texte courant), 500 (accents, boutons), 600 (titres).

## Iconographie
Lucide exclusivement. Trait 1,75 px, tailles 16/20/24 px. Jamais de style rempli ou dégradé.

## Motif réseau
Grille de points 22 px + traits de liaison 1 px, réservée aux fonds sombres et grands aplats. Opacité maximale 50 %, jamais derrière du texte courant. Usage décoratif secondaire uniquement — ne remplace jamais le monogramme.

## Couleurs fonctionnelles
Vert (`#16A34A`), orange (`#D97706`), rouge (`#DC2626`) : réservées aux états système (opérationnel, vigilance, incident). Jamais utilisées comme couleurs de marque ou d'accent décoratif.

## Choix clair / sombre
Mode clair par défaut (fond `#F8FAFC`/blanc, texte `#0F172A`). Mode sombre (fond `#0B1220`) pour les héros, CTA de conversion, panneaux de supervision et cartes produit à forte intention. Un seul des deux par section, jamais mélangés dans un même bloc.

## Baseline officielle
**Votre informatique. Gérée, sécurisée, disponible.**

Les accroches publicitaires (Ads, réseaux sociaux) peuvent varier et ne doivent pas nécessairement reprendre cette baseline mot pour mot — elles doivent rester cohérentes avec le positionnement (infogérance, sauvegarde, support, TPE/indépendants/associations).

## Exemples d'utilisation
Voir `Zachary IT - Monogramme final.dc.html` (déclinaisons complètes) et `Zachary IT - Identite 2026.dc.html` (page d'accueil et espace client assemblés) pour des mises en situation réelles : nav, hero, cartes, tableaux, pricing, dashboard, mobile.

## Usages interdits
- Ne pas modifier le tracé du monogramme.
- Ne pas déplacer ou redimensionner le nœud indépendamment du reste du signe.
- Ne pas changer arbitrairement les couleurs du logo (seules les déclinaisons listées ci-dessus sont valides).
- Ne pas étirer le logo hors de ses proportions natives.
- Ne pas ajouter d'effets, glow, ombres ou dégradés au logo.
- Ne pas utiliser l'ancien logo à barres empilées — archivé, deprecated (`archive/legacy/`).
- Ne pas recréer ou redessiner approximativement le monogramme à la main : toujours partir des fichiers sources de `logo/`.

## Domaine et contact officiels
`zachary-it.fr` — aucun autre domaine ne doit apparaître sur un support actif.
