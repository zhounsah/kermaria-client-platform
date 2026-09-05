# SEO_GEO_REVIEW — Zachary IT v2.0.2.5

Audit du référencement technique, sémantique et local, puis de la lisibilité du
site par les moteurs de réponse et les systèmes d'IA.

Les constats marqués **vérifié en réel** l'ont été sur la pile construite
(API-INTERNAL + WEBPORTAL) servie localement, en interrogeant les hôtes
`zachary-it.fr`, `www.zachary-it.fr`, `dashboard.zachary-it.fr` et
`administration.zachary-it.fr` via les en-têtes `Host` / `X-Forwarded-Proto`.

---

## 1. Indexabilité et crawl

**État : solide.** C'est la partie la plus aboutie du produit.

| Contrôle | Résultat |
| --- | --- |
| `robots.txt` sur l'hôte public | `Allow: /` + 18 préfixes applicatifs interdits, alignés sur `NOINDEX_ROUTE_PREFIXES` |
| `robots.txt` sur `dashboard.` et `administration.` | `Disallow: /` — **vérifié en réel** |
| `www.zachary-it.fr/tarifs` | 301 → `https://zachary-it.fr/tarifs` — **vérifié en réel** |
| `dashboard.zachary-it.fr/tarifs` | 301 → hôte public — **vérifié en réel** |
| URL inconnue | 404 réel, pas de page molle — **vérifié en réel** |
| `PUBLIC_VITRINE_ENABLED` absent | `robots.txt` bascule en `Disallow: /` et le sitemap se vide |
| Directive `Host` | Absente, à raison : non standard et signalée en erreur |

Le `sitemap.xml` ne publie de `lastmod` que pour les pages dont le contenu
administrable porte une date de modification réelle. C'est le bon arbitrage :
recalculer la date à l'heure de la requête annoncerait tout le site comme
modifié à chaque passage du robot. `/solutions` est volontairement absent du
sitemap **et** en `noindex` — les deux signaux concordent, ce qui est rare.

### Problème corrigé — soft-404 sur panne de contenu

`/services`, `/services/[category]` et `/tarifs` répondaient **200 avec une
canonical légitime et un corps « temporairement indisponible »** quand le
contenu administrable ne pouvait pas être lu. Un robot passant pendant une
panne d'API indexait la panne à la place de la page.

`generateMetadata` sait déjà que le contenu manque — il bascule sur un titre de
repli à cet endroit précis. La même branche déclare désormais
`noindex, follow`. **Vérifié en réel** contre une API délibérément injoignable :
`/tarifs` renvoie `noindex, follow` avec le corps d'erreur, et le rendu normal
est inchangé.

---

## 2. Métadonnées

### Problème corrigé — marque en double dans les titres

`parseStorefrontPageContent` retire un « | Zachary IT » **final** pour que le
gabarit du layout l'ajoute exactement une fois. Quatre titres plaçaient la
marque là où le nettoyage ne la voit pas :

| Page | Titre servi avant | Après |
| --- | --- | --- |
| `/tarifs` | `Tarifs des services IT Zachary IT \| unités et devis \| Zachary IT` (76 car.) | `Tarifs des services IT : unités et devis \| Zachary IT` (53 car.) |
| `/a-propos` | `À propos de Zachary IT \| Zachary IT` | `À propos \| Zachary IT` |
| `/decouvrir-espace-client` | `Découvrez l'espace client Zachary IT \| Zachary IT` | `Découvrez l'espace client \| Zachary IT` |
| `/services/vps/[id]`, `/services/vps/choisir/confirmation` | `… \| Zachary IT \| Zachary IT` | corrigé |
| Replis `/tarifs` et `/services/[category]` | `Tarifs Zachary IT`, `Services Zachary IT` | `Tarifs`, `Services` |

Le garde-fou existant ne cherchait qu'un **suffixe** « | Zachary IT », ce qui
explique qu'aucun de ces cas n'ait été signalé. Il rejette maintenant la marque
à **toute position** d'un titre de page — l'accueil conserve sa forme
marque-en-tête, documentée — et applique la même règle au seed CMS, avec un
plafond de longueur pour qu'un titre ne dépasse plus silencieusement ce
qu'affiche un résultat de recherche.

**Limite à connaître** : les titres servis en production viennent de la base,
pas du seed. La correction doit être reportée depuis `/admin/content`.

### Reste conforme

Canonicals déclarées page par page (choix délibéré : un helper posé au layout
racine masquerait un oubli), OpenGraph et Twitter cards cohérents, favicon et
`apple-touch-icon` présents, `lang="fr"`, `metadataBase` sur le domaine public.

---

## 3. Structure et hiérarchie

`H1` unique par page, `H2` par section, fil d'Ariane rendu en `<nav
aria-label="Fil d'Ariane">` avec `aria-current="page"` sur le dernier maillon.
Le `BreadcrumbList` correspondant est émis sur les vingt et une routes vitrine
et vérifié par contrat, y compris l'alignement entre la canonical dynamique et
la route.

---

## 4. Données structurées

### Avant

| Entité | État |
| --- | --- |
| `LocalBusiness` | Présent sur l'accueil, avec adresse, `areaServed`, SIRET en `taxID`, `sameAs` unique et réel |
| `WebSite` | Présent, `publisher` → `#business` |
| `BreadcrumbList` | Présent sur les pages vitrine |
| `Service` | Présent sur les fiches pack, sans `Offer` ni prix — délibéré |
| **`FAQPage`** | **Absent partout**, alors que cinq rendus affichent des FAQ réelles |

Le balisage existant est de bonne qualité et, fait notable, **honnête** : pas
d'horaires inventés, pas de `priceRange` fabriqué, pas de `SearchAction` pour
une recherche interne qui n'existe pas, pas de `sameAs` mort. Ces absences sont
commentées dans le code comme des choix, pas des oublis.

### Corrigé — `FAQPage`

Cinq rendus publics (`PublicStorefrontPage`, `PublicMessagingCategoryPage`,
`PublicPriorityServicePage`, `PublicServicesLandingPage`,
`PublicVpsServicePage`) affichent des couples question/réponse administrables
dans des `<details>` — contenu présent dans le HTML et dépliable, ce que Google
exige. Aucun balisage ne les décrivait.

`faqPageJsonLd` balise **exactement** ce que la page rend. Parce que le contenu
est administrable : les entrées incomplètes sont écartées, une FAQ vide produit
`null` plutôt qu'un `FAQPage` sans `mainEntity`, et `JsonLd` ignore désormais
une donnée nulle au lieu d'émettre `<script>null</script>`.

**Vérifié en réel** sur `/services/vps` : nœud `FAQPage` avec `@id`
`…/services/vps#faq`, `inLanguage: fr-FR`, trois `Question` / `Answer`, et
chacune des trois questions retrouvée dans le corps de la page.

Google ne produit plus de résultat enrichi FAQ pour un site commercial depuis
2023. La valeur ici est ailleurs : elle donne aux moteurs de réponse le couple
question/réponse explicite, là où le texte libre les oblige à le deviner.

### Corrigé — description de l'entreprise

Le nœud `LocalBusiness` annonçait « Sauvegarde distante, stockage documentaire
et continuité d'activité ». C'est **la seule phrase lisible par machine** qui
répond à « que fait Zachary IT ? », et elle contredisait le catalogue du site.

Elle suit désormais la taxonomie publiée, et `knowsAbout` liste les quinze
sujets ayant chacun une page réelle. Le contrat impose un sujet par slug de
service publié et refuse les doublons : la liste ne peut ni se transformer en
bourrage de mots-clés, ni survivre aux pages qu'elle nomme.

---

## 5. Maillage interne

Chaque page de service porte trois liens « services associés », les six entrées
problème de `/services` pointent vers des destinations distinctes (unicité
vérifiée par contrat), et `/ressources` sert de hub éditorial. Le maillage est
sain : pas de page orpheline détectée parmi les routes vitrine.

---

## 6. SEO local

L'ancrage est correct et **factuel**, ce qui est le point important :

- adresse réelle issue des mentions légales publiées (3 Kermaria, 35580
  Guichen), reprise en `PostalAddress` ;
- `areaServed` en trois niveaux — Guichen, Ille-et-Vilaine, France — sans
  empilement de communes ;
- `name` = nom commercial, `legalName` = dénomination juridique, chacun dans le
  champ prévu ;
- SIRET en `taxID`, et non `vatID` : l'EI est en franchise en base ;
- « Guichen » cité dans le titre et la description de l'accueil, contrôlé par
  contrat.

**Aucun spam géographique.** Le site ne fabrique pas de pages « informatique
Rennes / Bruz / Guichen ». C'est le bon choix : ces pages n'auraient aucun
contenu utile, et Google les traite comme du doorway.

**Manque, non corrigé** : ni `openingHoursSpecification` ni `priceRange`.
L'absence est délibérée et documentée — aucune donnée fiable n'existe dans le
dépôt. Publier des horaires supposés serait un motif d'action manuelle. À
compléter uniquement si l'éditeur fournit les valeurs réelles.

---

## 7. Compréhension par les moteurs de réponse et les IA

Les huit questions du cahier des charges, et ce que le site permet d'y répondre
après cette revue :

| Question | Avant | Après |
| --- | --- | --- |
| Qu'est-ce que Zachary IT ? | `LocalBusiness` + `WebSite` liés par `publisher` | inchangé, correct |
| Quels services propose-t-il ? | **Sauvegarde uniquement** dans la description machine | Cinq familles + `knowsAbout` de 15 sujets |
| À qui s'adresse-t-il ? | « particuliers, associations, indépendants et petites entreprises » | conservé et repris dans la description |
| Quels problèmes résout-il ? | Implicite dans la prose | Explicite : `FAQPage` sur cinq familles de pages |
| Comment le contacter ? | Formulaire + `email` en données structurées | inchangé — mais l'adresse reste un domaine interne (F18) |
| Quelle zone d'intervention ? | `areaServed` en trois niveaux | inchangé, correct |
| Quelles sont ses offres ? | `Service` sur les fiches pack | inchangé ; terminologie encore flottante (F17) |
| Quelle page fait autorité par sujet ? | Canonicals page par page, un seul hôte en 200 | inchangé, correct |

### `llms.txt` — non implémenté, délibérément

Le cahier des charges en autorise l'ajout « si cela est cohérent avec
l'architecture actuelle ». Ce n'est pas le cas ici, pour trois raisons :

1. le contenu vitrine est **administrable** et servi dynamiquement ; un
   `llms.txt` statique divergerait du site à la première modification en
   administration, et un `llms.txt` généré dupliquerait le sitemap sans rien
   ajouter ;
2. aucun crawler majeur ne le consomme aujourd'hui — c'est une proposition, pas
   un standard ;
3. le gain réel visé — rendre le site interprétable — est mieux servi par ce
   qui vient d'être fait : données structurées correctes, entités explicites,
   FAQ balisées, une page canonique par sujet.

Ajouter un fichier qui ne serait ni lu ni maintenu donnerait une fausse
impression de couverture. Recommandation : ne pas le faire tant qu'un moteur
identifié ne le consomme pas.

---

## 8. Recommandations non implémentées

| Sujet | Recommandation | Pourquoi ce n'est pas fait ici |
| --- | --- | --- |
| **Positionnement de l'accueil** (F16) | Élargir titre et meta description de `/` au-delà de la sauvegarde | Ces deux champs portent le ciblage SEO actuel. Les modifier déplace le référencement existant : **décision commerciale** |
| **Titres administrés en base** (F03) | Reporter la correction du titre `/tarifs` depuis `/admin/content` | La correction du seed n'atteint pas les valeurs administrées en production |
| **Terminologie offre** (F17) | Choisir entre « pack », « formule » et « offre », puis aligner pages, URL et pied de page | Renommer un parcours commercial est une **décision commerciale** |
| **Adresse de contact** (F18) | Publier une adresse `@zachary-it.fr` en données structurées et mentions légales | Créer une boîte est une **action d'infrastructure** hors périmètre |
| `openingHoursSpecification`, `priceRange` | À ajouter si l'éditeur fournit les valeurs réelles | Ne rien inventer |
| Contenu promotionnel en FAQ | Rappeler aux rédacteurs que les règles Google interdisent prix et promotion dans un `FAQPage` | Le constructeur n'émet ni `Offer` ni prix, mais le texte des réponses reste libre |

---

## Post-audit SEO/GEO closure - v2.0.2.6

The original recommendations table is historical. The following items were subsequently completed:

- homepage positioning was broadened to match the real multi-service catalogue;
- customer vocabulary was standardized on `offre` without renaming internal Billing routes;
- persisted `/tarifs` and service CMS content was synchronized, so corrected metadata/copy is not limited to seeds;
- public/legal and structured brand contact identity now uses `contact@zachary-it.fr`;
- the public version marker was removed;
- storefront mojibake was removed in the database and source and is now covered by automated guards.

Technical SEO contracts (robots, sitemap, canonical behavior, structured data, title guards, FAQ markup and public-site quality) pass in the release candidate. External ranking and answer-engine visibility remain separate measurements and are not inferred from repository tests.
