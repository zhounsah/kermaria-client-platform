# Direction graphique et éditoriale — Zachary IT

## Intention actuelle

Le retour du commanditaire du 28 octobre 2026 demande un site plus vivant, avec des animations et un rythme proche de [enzonaute.me](https://enzonaute.me/). La référence a été examinée dans le navigateur : entrée typographique, grand titre, défilement expressif, alternance de zones claires et sombres, micro-interactions. Cette direction remplace la première proposition, jugée trop neutre. Elle est adaptée au métier de Zachary IT, sans reprendre ses illustrations, références clients, chiffres ni promesses.

Le site doit faire comprendre immédiatement l'activité, les publics concernés et la prochaine action utile. L'expression visuelle vient de la typographie, de la couleur et du mouvement ; les informations commerciales restent vérifiables. Les offres, prix, droits et données du client ne changent pas de source.

## Système visuel

`apps/webportal/app/site-refonte.css` conserve le socle des composants, formulaires, états de focus et écrans authentifiés. `site-motion.css`, importée après ce socle, porte la direction expressive de la vitrine. La séparation permet de faire évoluer les animations publiques sans modifier les écrans transactionnels. Les tokens CSS principaux sont : encre `#081630`, fond ivoire `#f7f6f0`, bleu d'action `#0750ed`, accent lime `#cdf43d`, texte secondaire `#34445b` et focus `#f49b28`. Les textes atténués `--muted` et `--slate-500` ont été renforcés à `#53627a` pour conserver leur contraste sur l'ivoire, y compris dans les pages authentifiées.

La police locale Inter est conservée. Les titres passent à une échelle très affirmée, avec un interlignage serré et une largeur contrôlée ; les paragraphes et libellés restent lisibles. L'accueil fait alterner ivoire, bleu et bleu nuit. Les services se lisent comme de larges lignes interactives ; les cartes restent utiles pour les offres réellement comparables. Les boutons sont arrondis, avec une translation brève au survol et un focus clavier explicite. Le menu et les contrôles existants restent accessibles au clavier.

Le composant `VitrineHero` sert la même composition avec et sans mise en page CMS : titre, texte, deux actions, illustration et index des domaines d'intervention. L'illustration générée `network-illustration-motion.webp` est un visuel fictif sans logo ni client, compressé à environ 48 Ko. Ses dimensions sont réservées en HTML et elle est chargée en priorité. Elle n'affirme aucune installation réelle de Zachary IT.

## Mouvement

L'accueil ouvre sur le titre seul, centré dans la fenêtre sur un fond ivoire. Une copie visuelle masquée aux technologies d'assistance suit une trajectoire mesurée jusqu'à l'emplacement du véritable H1 ; l'en-tête, le texte, les actions et l'illustration apparaissent ensuite avec des décalages courts. Le contenu conserve son ordre et un seul H1 sémantique. Cette entrée est réservée à l'accueil publié, y compris lorsque le héros provient du CMS ; l'aperçu de l'éditeur reste direct. Une sortie de secours CSS rend le contenu accessible si JavaScript ne termine pas la séquence. Le mouvement réduit ou la pause enregistrée ouvrent directement sur la composition finale.

Le titre, les actions et l'illustration entrent avec des translations courtes. Trois repères apparaissent l'un après l'autre près de l'illustration. L'index des quatre domaines reste lisible en permanence, sur quatre colonnes puis deux sur mobile ; ses libellés ont une entrée unique. Les lignes de services répondent au survol et au focus par un trait et une flèche, sans déplacer leur texte. La méthode relie ses trois étapes par un filet qui se trace au défilement. Les sections éditoriales avancent légèrement avec les CSS View Timelines lorsque le navigateur les prend en charge ; sans cette API, les contenus restent visibles.

La direction motion globale de la vitrine DEV renforce ce rythme : une ligne cobalt/lime sous l'en-tête suit le défilement, les cartes éditoriales entrent avec une translation et une inclinaison, les titres de section prennent davantage d'élan, et les cartes répondent au survol comme au focus. Les boutons montrent un reflet à l'interaction. Les petits écrans utilisent une entrée de carte plus courte. Les boucles décoratives de l'illustration, du rail, des filets et du CTA ont été retirées ; restent les cycles du H1 et de sa phrase tournante demandés par le commanditaire. Les prix, formulaires et actions de commande ne reçoivent pas d'animation liée au défilement.

L'enregistrement mobile fourni par le commanditaire montre surtout une **seconde ligne de titre qui change par séquences dans une fenêtre masquée**. Cette mécanique est reprise dans le héros d'accueil : « Votre informatique » reste fixe, tandis que « suit votre activité », « sécurise vos accès » et « relie vos équipes » se succèdent. Ces trois formulations correspondent aux prestations existantes. La hauteur de la fenêtre est réservée, les phrases ont des emplacements identiques et le retour à la première phrase est continu : le contenu suivant ne saute pas à chaque changement. Le H1 garde un nom accessible stable, « Votre informatique suit votre activité. » ; les variantes visuelles sont masquées aux technologies d'assistance.

La même grammaire de mouvement s'applique aux titres de toutes les pages publiques : arrivée des H1 au chargement, translation des H2/H3 et des contenus commerciaux au défilement, puis réponses au survol des actions. Les H1 publics ont aussi une vague de petits sauts, lettre après lettre, sur un cycle de 4 secondes avec 18 ms de décalage entre les lettres. Les mots gardent leurs retours à la ligne, les graphèmes accentués restent entiers, et un H1 porte toujours un nom accessible complet. Les titres issus du CMS gardent leur texte exact et adoptent le même effet sur le site publié ; leur aperçu dans l'éditeur reste statique. La connexion et les espaces client/admin gardent leurs mouvements propres sans cette vague répétée.

Les textes des cartes et des sections restent opaques lors des entrées au défilement : le premier passage d'axe-core avait montré qu'un fondu diminuait temporairement leur contraste. Depuis la correction du 31 octobre, l'accueil montre directement le titre, le descriptif et le CTA, y compris pendant le chargement. `prefers-reduced-motion: reduce` laisse la première phrase du héros visible et désactive le cycle, la vague de lettres, la progression animée, les translations et les transitions. Les composants administrateur et client ne reçoivent pas d'animation de défilement.

## Responsive et parcours

La navigation publique emploie les liens internes Next.js pour garder l'en-tête et le panier en place entre les pages. Une entrée courte accompagne le nouveau contenu, et le menu Services ainsi que le menu mobile s'ouvrent progressivement. Le lien actif et le chargement reçoivent un repère visuel ; après changement de route, le focus passe au contenu principal. Les ancres défilent doucement, avec un décalage sous l'en-tête, tandis que Next.js remet le défilement en haut lors d'un changement de page.

Une commande dans l'en-tête met les mouvements en pause pour la session du navigateur et les relance à la demande. Elle coupe aussi les transitions et le défilement doux. `prefers-reduced-motion: reduce` reste prioritaire. La rotation des phrases du héros d'accueil est raccourcie pour limiter la lecture de fragments pendant le changement de ligne. Les transitions entre documents qui ne passent pas par le routeur sont une amélioration CSS facultative dans les navigateurs compatibles.

À 820 px, le héros, les sections et les zones à deux colonnes se replient. À 600 px, les deux actions du héros occupent la largeur disponible, les prestations et les offres deviennent des listes lisibles, tandis que l'index des quatre domaines passe sur deux colonnes. La première ligne du titre cyclique reste entière même à 320 px et chaque variante tient dans la largeur réservée. Le contenu structurel n'est jamais caché pour créer une apparition. L'absence de débordement global a été contrôlée à 320, 390, 768 et 1440 px.

L'accueil garde ses routes réelles vers Services, Offres, Tarifs, Diagnostic et Contact. Les titres CMS édités librement gardent la priorité ; seuls les libellés historiques exacts et le titre par défaut de la première refonte sont projetés vers la nouvelle version. Le formulaire de contact reste relié au BFF. Le constructeur de pages montre un aperçu statique pour faciliter l'édition. Aucune preuve sociale, statistique, certification ou garantie n'est ajoutée.

Le hub `/assistance-maintenance` distingue l'intervention ponctuelle de l'abonnement sans dévaloriser la prestation à la carte. Un bandeau encre et lime expose trois raisons vérifiables de choisir un suivi mensuel : le temps d'assistance prévu, le périmètre des équipements et les contrôles propres au niveau retenu. Les liens mènent aux pages Particuliers et Professionnels existantes. Aucun prix, économie garantie, délai de réponse ou intervention illimitée n'est inventé ; la page conserve sa source de prix Billing V2 et son mode devis.

## Revue de fidélité

| Point | Concept | Rendu et décision |
| --- | --- | --- |
| Premier écran | Titre très grand, ivoire, bleu cobalt et illustration réseau. | Titre et actions réels, image sans texte ni marque fictive. Le logo SVG existant et le panier réel sont conservés. |
| Rythme | Alternance claire/sombre, titres forts, index de services. | Sections ivoire, bleu et bleu nuit ; ordre des blocs CMS conservé pour maintenir l'éditeur et la lecture. |
| Mouvement | Ligne cyclique du film fourni, entrée, défilement et états interactifs. | Trois phrases dans une fenêtre masquée, titres et contenus animés sur les pages publiques, signaux, rail lent et View Timelines ; aucune dépendance d'animation supplémentaire. |
| Contenu | Prestations concrètes sans chiffres inventés. | Textes existants de la refonte précédente conservés, seul le titre d'accueil devient plus direct. |
| Mobile | Titre compact, actions empilées et illustration visible. | Contrôles à 320 et 390 px ; aucun débordement global observé dans la recette locale. |
| Accessibilité | Contraste, focus et mouvement réduit. | Axe-core et `prefers-reduced-motion` contrôlés ; une recette manuelle avec lecteur d'écran reste nécessaire. |

Les concepts, captures de travail et résultats de test sont dans `C:\Users\zhounsah\Documents\Dev\zachary-it-qa\`. Les captures comparatives sélectionnées sont dans `docs/audit-captures/`. Les images de concept guident le rythme visuel ; elles ne servent pas de contenu public.
