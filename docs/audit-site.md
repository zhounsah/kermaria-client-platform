# Audit initial du site Zachary IT

**Date :** 28 octobre 2026  
**Base examinée :** `main` à `5ce2898`, puis branche `codex/zachary-it-refonte`.  
**Périmètre :** vitrine Next.js, parcours publics, enveloppes client/admin, contenu éditorial, BFF et dépendances utiles à la refonte. Les données CMS persistées et les parcours authentifiés de DEV demandent une vérification distincte ; l'analyse du dépôt ne les prouve pas.

## État initial et méthode

Le checkout principal contenait déjà des modifications sans rapport avec ce chantier dans `.ai/MEMORY.md`, `.ai/topics/tva-transition-2026-10-14.md` et `docs/releases/V2.1.1_VAT_RUNTIME_CONFIG_2026-10-14.md`. Un worktree sur branche dédiée les isole. Le frontend est `apps/webportal` (Next.js 16.3.8, React 19), les contrats sont dans `packages/shared`, l'API privée est `apps/api-internal`. `AppShell.tsx` choisit la coque publique, client ou admin selon route, hôte et session. Les routes publiques sont listées dans `lib/public-route-config.ts` ; `PublicShell.tsx` porte navigation et pied de page. `SitePageFrame.tsx` rend les blocs du constructeur de pages. Les prix et la disponibilité commerciale viennent de l'API, par le BFF, notamment sur `/offres`, `/tarifs` et `/formules`.

Les captures de référence ont été prises sur `localhost:3000` avec l'API locale en mode mock de `Development`. Elles sont des preuves de rendu local, avec le bandeau DEV et l'indicateur Next.js visibles ; elles ne montrent aucune donnée de production.

| Vue | Avant (`5ce2898`) | Après refonte |
| --- | --- | --- |
| Accueil, 1440 × 900 | [Capture avant](audit-captures/avant-accueil-1440.webp) | [Capture après](audit-captures/apres-accueil-1440.webp) |
| Accueil, 390 × 844 | [Capture avant](audit-captures/avant-accueil-390.webp) | [Capture après](audit-captures/apres-accueil-390.webp) |
| Contact, 1440 × 900 | [Capture avant](audit-captures/avant-contact-1440.webp) | [Capture après](audit-captures/apres-contact-1440.webp) |

Le flux navigateur → WebPortal/BFF → API-INTERNAL → MariaDB est effectivement matérialisé par `lib/internal-api.ts` (`server-only`) et les routes `app/api`. Les sessions et contrôles CSRF existants sont hors périmètre de réécriture. Le serveur local de référence utilise l'API en persistance mock de `Development` ; il ne représente pas le contenu ni le comportement de la base DEV ou PROD.

Les constats ci-dessous sont classés **VALIDE**, **FAUX POSITIF** ou **INCERTAIN** avant correction. Les numéros de ligne renvoient à la base `5ce2898`.

## Critique

| Statut | Constat et preuve | Conséquence | Correction recommandée |
| --- | --- | --- | --- |
| **VALIDE** | `apps/webportal/package.json:61` fixe Next.js à `16.3.8`. `npm audit --omit=dev --json` signale trois avis sur cette plage, dont deux critiques. Les avis de l'éditeur [GHSA-2464-fhr4-w9xw](https://github.com/vercel/next.js/security/advisories/GHSA-2464-fhr4-w9xw) et [GHSA-673q-mjm6-r5xr](https://github.com/vercel/next.js/security/advisories/GHSA-673q-mjm6-r5xr) confirment les versions affectées. `next.config.ts` ne déclare pas `dangerouslyAllowSVG` ; l'exploitabilité effective du premier avis dépend du chemin d'optimisation SVG, non établie ici. | Dépendance de production dans une plage vulnérable ; aucune exploitation n'est affirmée. | Mettre à jour vers une version corrigée sans changer de framework, puis refaire compilation, contrats et revue des routes sensibles. |

## Élevé

| Statut | Constat et preuve | Conséquence | Correction recommandée |
| --- | --- | --- | --- |
| **VALIDE** | L'accueil contient deux sources de texte : `app/page.tsx:36-99` et `Services/SitePageService.cs:684-735`. `SitePageFrame.tsx:36-45` affiche les blocs CMS quand un layout existe ; il n'affiche le JSX de repli qu'en son absence. | Modifier uniquement `page.tsx` ne change pas l'accueil CMS existant. La publication d'un nouveau layout en base reste une opération distincte. | Aligner le rendu, le repli et les valeurs initiales ; vérifier séparément les contenus persistés de DEV. Préserver les personnalisations CMS. |
| **VALIDE** | La page d'accueil ouvre avec « Une informatique fiable, simplement » et une liste générique de cinq besoins (`app/page.tsx:173-237`, mêmes blocs dans `SitePageService.cs`). Les liens des trois publics mènent tous à `/services`. | L'activité B2B et les prestations concrètes restent peu visibles ; le visiteur doit chercher le parcours approprié. | Énoncer immédiatement l'offre et le public, lier chaque besoin à une page utile, réduire les répétitions et hiérarchiser les parcours. |
| **VALIDE** | `app/globals.css` dépasse 10 000 lignes et redéfinit les mêmes familles de sélecteurs à plusieurs endroits, par exemple `.vitrine-hero-2026` vers 9824 puis dans plusieurs media queries, `.public-main` vers 6140 et 9794, `.app-shell` vers 128 et 9608. | Cascade difficile à prédire ; corrections visuelles fragiles sur les pages et en responsive. | Extraire une couche de refonte ciblée avec tokens, composants et media queries cohérents ; supprimer les règles remplacées lorsqu'elles sont sûres à retirer. |
| **VALIDE** | `PublicShell.tsx:169-190` présente « Offres », « Tarifs », « À propos » et un méga menu de services, mais « Contact » est enfoui dans un CTA long. Le pied de page `PublicShell.tsx:200-244` répète plusieurs chemins et intitulés. | Navigation peu lisible pour les décideurs qui veulent comprendre les prestations, puis demander un devis. | Clarifier les entrées principales, raccourcir le CTA, organiser le pied de page par tâche, garder les routes et la connexion existantes. |

## Moyen

| Statut | Constat et preuve | Conséquence | Correction recommandée |
| --- | --- | --- | --- |
| **VALIDE** | `app/globals.css:4592-4739` donne aux étapes, services et publics des grilles de cartes de forme presque identique. `:9931-9948` leur applique encore une même surface et ombre. | Rythme visuel monotone et aspect de gabarit assemblé. | Employer des listes éditoriales, séparateurs et blocs de service distincts ; limiter les cartes aux contenus qui le justifient. |
| **VALIDE** | Le hero public est une image de fond `home-hero-2026.png` posée derrière le texte (`globals.css:9824-9844`) ; au plus petit écran l'image reste en fond et le `photo-spacer` disparaît (`:9877-9881`). | Lisibilité et cadrage dépendent de la photo et de la largeur. | Construire un hero à deux zones avec média réellement séparé et texte sur fond uni ; vérifier les points de rupture. |
| **VALIDE** | Le contact est un seul bloc de 720 px (`globals.css:6006-6009`) ; les « étapes » et autres liens passent après le formulaire dans `SitePageService.cs:115-140`. | Le visiteur doit remplir le formulaire avant de savoir ce qu'il se passe ensuite. | Présenter une introduction et le déroulement près du formulaire, sans ajouter de promesse de délai non vérifiée. |
| **VALIDE** | `PublicStorefrontPage.tsx:91-123` et `PublicServicesLandingPage.tsx:44-56` répètent une surétiquette « Zachary IT » avant le H1 ; `app/offres/page.tsx:59-82` emploie en plus « Nouveau » et « Espace client ». | Signal visuel répétitif et concurrence avec le vrai titre. | Supprimer les surétiquettes décoratives ; faire porter les différences par les titres et le contenu. |
| **VALIDE** | `PublicPackCatalogService.cs:163` et `packages/shared/src/index.ts:1705` qualifiaient les tarifs du catalogue de « hors taxes », tandis que `PublicPackCard.tsx` et le tableau public affichent le point de départ « TTC / mois ». | Note commerciale contradictoire pour un visiteur qui compare les offres. | Retirer la qualification fiscale générique du texte éditorial ; laisser le prix et la taxe affichés provenir du catalogue et du moteur serveur. |
| **VALIDE** | `ContactForm.tsx:111` utilisait `noValidate`. Au test local avec formulaire vide, le BFF renvoyait seulement « Vérifiez les champs signalés » et aucun champ ne recevait `aria-invalid` ni message de champ, car la réponse de `requestBffJson` ne fournissait pas `field_errors` au composant. | Erreur difficile à corriger au clavier et pour un lecteur d'écran. | Valider les champs évidents avant l'envoi, associer les messages aux champs et placer le focus sur le premier invalide ; conserver la validation serveur. |
| **VALIDE** | `app/layout.tsx:23-25` décrit tout le site comme « Sauvegarde distante et continuité d'activité », alors que l'accueil et le catalogue couvrent aussi maintenance, réseau, messagerie et assistance. | Métadonnée de repli réductrice. | Rédiger un titre et une description de repli conformes aux prestations vérifiées. Garder les canonical et le noindex des routes privées. |
| **INCERTAIN** | `getPublicManagedContent` et `getPublicSitePageLayout` lisent des contenus administrables, tandis que `mock-data.ts` et `StorefrontContentSeed.cs` apportent des replis. Les valeurs réellement publiées en DEV/PROD n'ont pas été relevées à ce stade. | Une réécriture de seed peut ne pas apparaître sur un environnement existant. | Relever les valeurs DEV en lecture seule ; proposer une mise à jour CMS ciblée après comparaison, sans écraser une personnalisation ni toucher PROD. |
| **INCERTAIN** | L'examen du code montre des labels, `aria-invalid`, `aria-describedby`, focus de retour et `aria-live` sur `ContactForm.tsx`; aucun parcours clavier et lecteur d'écran complet n'a encore été observé. | La conformité WCAG 2.2 AA ne peut pas être déduite du seul code. | Tester clavier, focus, 320/390/768/1440 px, zoom et contrastes ; corriger les défauts observés. |

## Points conformes ou hors conclusion

- **FAUX POSITIF — prix calculé dans le navigateur :** `PublicPackCard.tsx:7-17` et `lib/public-packs.ts` décrivent et appliquent un prix de départ calculé par l'API ; la décision de paiement reste côté serveur. La refonte n'introduira aucun prix statique.
- **FAUX POSITIF — absence de SEO technique :** `lib/public-metadata.ts`, `app/sitemap.ts`, `app/robots.ts` et `next.config.ts` portent déjà canonical, sitemap, noindex et en-têtes. Le chantier concerne surtout cohérence éditoriale et rendu, pas une réécriture de ces garde-fous.
- **FAUX POSITIF — formulaire purement décoratif :** `ContactForm.tsx` soumet au BFF `/api/contact`, gère erreurs de champ et résultat. La livraison effective d'un message ne sera affirmée qu'après un test sur l'environnement concerné.
- **INCERTAIN — espace client et administration :** `AppShell.tsx` et `AdminNavigation.tsx` montrent une coque séparée, et des tests de contrat existent. Le comportement authentifié et la permission de chaque action exigent des comptes de test ; aucun accès de production n'est utilisé pour cet audit.

## Informations commerciales à confirmer

Le dépôt contient une identité juridique, une adresse, des formulations de disponibilité et des descriptions de services. Une présence dans le code ou dans le CMS ne constitue pas une preuve de contrat, de capacité, de délai d'intervention, de certification ou de couverture géographique. La refonte ne créera aucun chiffre, témoignage, partenaire, garantie de récupération ni engagement de réponse. Les prix et statuts de souscription restent issus du catalogue serveur. Toute donnée commerciale persistée non lisible localement sera signalée dans le rapport de validation.

## Suite de la mission

Refondre d'abord les composants et styles publics transversaux, puis l'accueil, les services, les offres/tarifs et le contact. Aligner les textes de repli avec les défauts API/CMS, préserver les blocs obligatoires du constructeur et ses actions. Vérifier le rendu avant/après à plusieurs tailles, les contrats et les commandes web/API applicables. Les résultats observés et les limites de preuve seront ajoutés à un rapport de livraison séparé.
