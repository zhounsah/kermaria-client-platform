# Direction graphique et éditoriale — Zachary IT

## Intention actuelle

Le retour du commanditaire du 28 octobre 2026 demande un site plus vivant, avec des animations et un rythme proche de [enzonaute.me](https://enzonaute.me/). La référence a été examinée dans le navigateur : entrée typographique, grand titre, défilement expressif, alternance de zones claires et sombres, micro-interactions. Cette direction remplace la première proposition, jugée trop neutre. Elle est adaptée au métier de Zachary IT, sans reprendre ses illustrations, références clients, chiffres ni promesses.

Le site doit faire comprendre immédiatement l'activité, les publics concernés et la prochaine action utile. L'expression visuelle vient de la typographie, de la couleur et du mouvement ; les informations commerciales restent vérifiables. Les offres, prix, droits et données du client ne changent pas de source.

## Système visuel

`apps/webportal/app/site-refonte.css` conserve le socle des composants, formulaires, états de focus et écrans authentifiés. `site-motion.css`, importée après ce socle, porte la direction expressive de la vitrine. La séparation permet de faire évoluer les animations publiques sans modifier les écrans transactionnels. Les tokens CSS principaux sont : encre `#081630`, fond ivoire `#f7f6f0`, bleu d'action `#0750ed`, accent lime `#cdf43d`, texte secondaire `#34445b` et focus `#f49b28`. Le texte atténué global a été renforcé à `#53627a` pour conserver son contraste sur l'ivoire.

La police locale Inter est conservée. Les titres passent à une échelle très affirmée, avec un interlignage serré et une largeur contrôlée ; les paragraphes et libellés restent lisibles. L'accueil fait alterner ivoire, bleu et bleu nuit. Les services se lisent comme de larges lignes interactives ; les cartes restent utiles pour les offres réellement comparables. Les boutons sont arrondis, avec une translation brève au survol et un focus clavier explicite. Le menu et les contrôles existants restent accessibles au clavier.

Le composant `VitrineHero` sert la même composition avec et sans mise en page CMS : titre, texte, deux actions, illustration et index des domaines d'intervention. L'illustration générée `network-illustration-motion.webp` est un visuel fictif sans logo ni client, compressé à environ 48 Ko. Ses dimensions sont réservées en HTML et elle est chargée en priorité. Elle n'affirme aucune installation réelle de Zachary IT.

## Mouvement

Le titre, les actions et l'illustration entrent avec des translations courtes. Trois petits signaux pulsent près de l'illustration. L'index des services défile lentement en continu et s'arrête au survol. Les lignes de services réagissent au survol et au focus ; les sections avancent légèrement au défilement avec les CSS View Timelines lorsque le navigateur les prend en charge. Si cette API n'existe pas, les contenus restent visibles et les parcours fonctionnent sans JavaScript supplémentaire.

Les animations ne font varier l'opacité d'aucun texte : le premier passage d'axe-core a montré que cet effet diminuait temporairement le contraste. `prefers-reduced-motion: reduce` désactive les animations et transitions de la vitrine. Les composants administrateur et client ne reçoivent pas d'animation de défilement.

## Responsive et parcours

À 820 px, le héros, les sections et les zones à deux colonnes se replient. À 600 px, les deux actions du héros occupent la largeur disponible, les prestations et les offres deviennent des listes lisibles, tandis que l'index des domaines reste sur une ligne défilante. Le contenu n'est jamais caché pour créer une apparition. L'absence de débordement global a été contrôlée à 320, 390, 768 et 1440 px.

L'accueil garde ses routes réelles vers Services, Offres, Tarifs, Diagnostic et Contact. Les titres CMS édités librement gardent la priorité ; seuls les libellés historiques exacts et le titre par défaut de la première refonte sont projetés vers la nouvelle version. Le formulaire de contact reste relié au BFF. Le constructeur de pages montre un aperçu statique pour faciliter l'édition. Aucune preuve sociale, statistique, certification ou garantie n'est ajoutée.

## Revue de fidélité

| Point | Concept | Rendu et décision |
| --- | --- | --- |
| Premier écran | Titre très grand, ivoire, bleu cobalt et illustration réseau. | Titre et actions réels, image sans texte ni marque fictive. Le logo SVG existant et le panier réel sont conservés. |
| Rythme | Alternance claire/sombre, titres forts, index de services. | Sections ivoire, bleu et bleu nuit ; ordre des blocs CMS conservé pour maintenir l'éditeur et la lecture. |
| Mouvement | Entrée, défilement et états interactifs. | Translations d'entrée, signaux, rail lent et View Timelines ; aucune dépendance d'animation supplémentaire. |
| Contenu | Prestations concrètes sans chiffres inventés. | Textes existants de la refonte précédente conservés, seul le titre d'accueil devient plus direct. |
| Mobile | Titre compact, actions empilées et illustration visible. | Contrôles à 320 et 390 px ; aucun débordement global observé dans la recette locale. |
| Accessibilité | Contraste, focus et mouvement réduit. | Axe-core et `prefers-reduced-motion` contrôlés ; une recette manuelle avec lecteur d'écran reste nécessaire. |

Les concepts, captures de travail et résultats de test sont dans `C:\Users\zhounsah\Documents\Dev\zachary-it-qa\`. Les captures comparatives sélectionnées sont dans `docs/audit-captures/`. Les images de concept guident le rythme visuel ; elles ne servent pas de contenu public.
