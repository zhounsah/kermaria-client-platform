# Lot A - Audit /services

Statut : audit en cours, aucune modification fonctionnelle.

## Decisions et pistes retenues

- Conserver le H1 actuel : « L'informatique dont votre activité a besoin. Gérée pour vous. »
- Transformer `/services` en routeur de besoins avant de présenter le catalogue technique.
- Ajouter six portes d'entrée orientées problèmes : messagerie, protection des données, travail à distance, réseau/Wi-Fi, serveur/site/application à maintenir, délégation informatique.
- Conserver les quatre catégories existantes comme deuxième niveau de navigation.
- Déplacer le message « services modulaires » après l'orientation par problèmes/catégories.
- Ne pas absorber les lots B, C ou D dans le lot A ; conserver leurs périmètres.
- Revoir la place du CTA « Comparer les formules » : le visiteur doit d'abord pouvoir identifier son problème.

## Audit technique du modèle CMS

À compléter pendant la suite de l'audit.

## Audit de l'ancienne architecture Services

### Constat

Le commit `8ff20e6` avait déjà introduit une landing `/services` dédiée avant sa généralisation dans le Storefront CMS. Cette landing contenait :

1. un hero ;
2. quatre cartes de catégories ;
3. trois entrées orientées bénéfices/besoins ;
4. un parcours en trois étapes ;
5. un bloc de confiance ;
6. un CTA final.

Les composants et styles associés existent toujours (`ServiceCategoryCard`, `ServiceFeatureList`, `.service-category-grid`, `.service-overview-grid`, `.service-process`, `.service-trust`, `.service-problem-section`). Ils sont donc réutilisables sans recréer un design system.

### Décision d'architecture proposée

- Ne pas restaurer `PublicServicesLandingPage` telle quelle : son contenu est codé en dur et contournerait le CMS.
- Ne pas forcer la landing `/services` dans le rendu générique `PublicStorefrontPage` : son rôle de routeur de besoins est spécifique.
- Conserver `storefront:services` comme contenu CMS autoritatif pour SEO, H1, lead, CTA et contenu éditorial.
- Ajouter un rendu spécialisé de landing qui consomme ce contenu CMS et une structure dédiée aux entrées problème/besoin.
- Réutiliser les composants/styles existants lorsque leur sémantique correspond.

### Périmètre des lots

Le lot A doit couvrir l'architecture problème -> solution et la landing `/services`. Les quatre catégories restent le lot B, les six services prioritaires le lot C, les preuves et le parcours quatre étapes le lot D. Le lot A peut préparer les emplacements/contrats nécessaires sans réécrire le contenu des lots suivants.

### Point de vigilance

La route `/services` est double : vitrine publique sur les zones public/local, et page « Mes services » après authentification dans le portail client. Toute évolution doit rester strictement dans la branche publique/local et ne pas modifier le comportement client.

## Contrat de donnees et validation

### Validation actuelle

Le JSON Storefront est valide deux fois :
- cote API (`ManagedContentService.ValidateStorefrontJson`) avant persistance ;
- cote WebPortal (`parseStorefrontPageContent`) avant rendu/admin.

L API ne connait pas un DTO Storefront type : elle valide la forme du JSON via `JsonDocument`. Le stockage reste un unique `BodyMarkdown` contenant le JSON. Aucune migration SQL n est necessaire pour ajouter une propriete au document.

### Consequence pour le lot A

La modification minimale peut rester retrocompatible : ajouter une propriete optionnelle specialisee a `storefront:services`, puis la valider uniquement lorsque la cle vaut `storefront:services`. Les autres pages `storefront_page` gardent leur contrat actuel.

Le parseur WebPortal peut exposer un type `StorefrontServicesLandingContent` derive du socle `StorefrontPageContent`. Le renderer specialise ne doit etre utilise que par la branche publique/local de `/services`.

### Validation recommandee pour les cartes besoins

Chaque entree doit contenir : `title`, `description`, `href`. Recommandation : exactement 6 entrees pour la version initiale du lot A, avec longueurs bornees et chemin interne sur. Les destinations doivent etre limitees a une liste fonctionnelle connue du Storefront/Ressources, et non a une URL arbitraire.

### Point decouvert sur les destinations

La whitelist UI actuelle du CMS ne contient que les routes Storefront commerciales et exclut les pages Ressources SEO. Si une carte besoin doit mener vers une Ressource (ex. comparaison VPN/bureau distant), il faudra etendre explicitement les destinations autorisees du formulaire. Cote API, `HasAllowedRoute` n est en realite pas une whitelist : il accepte tout chemin interne commencant par `/`. Le nom est donc plus fort que la garantie reelle.

### Durcissement recommande

Pour le lot A, ne pas elargir silencieusement la confiance dans les liens CMS. Definir une liste partagee/fermee de destinations admissibles pour les cartes de besoins, ou au minimum un validateur dedie. Ne pas modifier les regles Billing : les cartes sont des routes d orientation, pas des CTA de commande.

### Migration

Aucune migration de base de donnees n est requise. Le contenu `storefront:services` existe deja et devra etre enrichi via le CMS/API. Le seed ne met a jour que les contenus manquants ; changer uniquement `StorefrontContentSeed` ne modifierait donc pas la ligne deja presente en production. Une strategie explicite de mise a jour du contenu existant est necessaire lors de l implementation.

## SEO, schema et indexation

Constats :
- `/services` utilise `buildPublicMetadata` avec canonical `/services`, OpenGraph et Twitter card.
- La page est indexable et declaree dans le sitemap avec `contentKey: storefront:services`, donc son `lastmod` suit la date du contenu CMS.
- Le rendu actuel emet un `BreadcrumbList` JSON-LD valide pour Accueil > Services.
- Aucun `Service`, `ItemList` ou schema specifique aux six besoins n est actuellement emis. Ce n est pas bloquant pour A ; le balisage transversal doit rester dans le lot I afin d eviter de melanger architecture de conversion et optimisation schema.
- Les Ressources candidates testees sont toutes publiees et repondent 200 en production.

## Accessibilite et semantique

Points sains :
- fil d Ariane en `nav` avec `aria-current`;
- un H1 unique;
- sections avec H2;
- FAQ basee sur `details/summary`;
- skip-link global dans le shell public;
- les boutons commerciaux sont de vrais liens et non des div cliquables.

Recommandations pour les six cartes :
- utiliser une section nommee par un H2 explicite;
- chaque carte doit etre un `article` ou un element de liste contenant H3 + description + lien;
- ne pas rendre toute la carte cliquable via JavaScript; conserver un lien explicite avec libelle comprehensible;
- conserver un ordre DOM identique a l ordre visuel;
- ne pas communiquer la destination uniquement par une icone.

## Responsive

Le design Services existant possede deja les breakpoints necessaires :
- categories : 4 colonnes desktop, 2 sous 820 px, 1 sous 560 px;
- autres grilles : 1 colonne sous 820 px;
- hero et CTA : empiles sur petit ecran;
- boutons du hero pleine largeur sous 560 px.

Pour six cartes de besoins, recommandation : 3 colonnes desktop, 2 tablette, 1 mobile. Le style `.service-overview-grid` fournit deja presque exactement ce comportement et peut etre reutilise ou specialise sans nouveau systeme de grille.

### Dette CSS observee

Les selecteurs Services apparaissent a plusieurs endroits de `globals.css`, notamment des blocs responsive historiques puis des surcharges plus recentes. Le lot A ne doit pas lancer un grand nettoyage CSS hors perimetre, mais il doit eviter d ajouter une troisieme copie des memes regles. Ajouter seulement les regles specifiques indispensables au nouveau bloc, pres de la couche Storefront actuelle.

## Billing et conversion

`resolveStorefrontServicesLandingActions` n est utilise que par `/services`. Il injecte actuellement `Comparer les formules` des qu au moins un preset Billing existe. Cette fonction peut donc etre ajustee pour la landing sans effet sur les pages services individuelles.

Recommandation A : le hero conserve `Demander un audit` comme action principale et ne presente plus automatiquement `Comparer les formules` comme action secondaire. Les formules restent accessibles dans la navigation et peuvent etre mentionnees plus bas. Cette modification ne change aucun statut `selfServiceOrderable`, aucun mapping serviceCode/preset et aucun provisioning.

Les cartes de besoins ne doivent jamais construire de liens `/formules/<preset>` ni deduire un produit Billing. Elles orientent vers une Ressource, une categorie ou une page service ; le composant cible reste responsable de son mode devis/formule.

## Destinations recommandees - version finale de l audit

1. `Mes emails posent probleme` -> `/services/messagerie-professionnelle`. La landing service traite migration, boites et delivrabilite et propose ensuite le bon CTA.
2. `Je veux proteger mes donnees` -> `/services/sauvegarde-externalisee`. Besoin suffisamment determine pour aller directement au service.
3. `Je dois travailler a distance` -> `/vpn-ou-bureau-a-distance-que-choisir`. Le besoin est ambigu ; la Ressource aide a choisir sans pousser arbitrairement VPN ou RDS.
4. `Mon reseau ou mon Wi-Fi fonctionne mal` -> `/services/unifi`. La page couvre reprise, Wi-Fi, switching et segmentation ; la Ressource Wi-Fi pourra etre maillee depuis cette page au lot I.
5. `J ai un serveur, un site ou une application a maintenir` -> `/services/cloud-hebergement`. Le besoin peut mener a VPS, hebergement web, maintenance ou supervision : la categorie est la bonne porte d entree.
6. `Je veux deleguer mon informatique` -> `/services/support-it`. Le besoin est transversal et correspond au role de cette categorie.

Alternative editoriale acceptable : envoyer certains problemes vers des Ressources avant les services. Pour A, limiter ce schema aux cas ou un choix est reellement necessaire afin de ne pas transformer `/services` en second hub `/ressources`.

## Ecart fonctionnel actuel

Le bloc `Services associes` de `/services` affiche actuellement Cloud & Hebergement, Domaines & Messagerie, Reseau & Securite et Tarifs : `Support & IT` manque. Dans la nouvelle landing, les quatre univers doivent etre exposes explicitement et `Tarifs` ne doit pas prendre la place du quatrieme univers.

## Contrats a ajouter

Un nouveau contrat `verify-services-landing-contract.mjs` (ou extension clairement isolee d un contrat existant) doit verifier au minimum :
- `/services` public utilise le renderer specialise;
- la branche portail client `Mes services` reste presente et protegee;
- exactement six cartes `problemEntries` valides;
- les quatre categories sont presentes;
- aucun lien probleme ne pointe directement vers un configurateur `/formules/<...>`;
- breadcrumb JSON-LD conserve;
- H1 CMS conserve;
- CTA audit conserve;
- le hero ne reinjecte pas automatiquement `Comparer les formules`;
- parsing frontend et validation API sont alignes.

Les contrats existants `test:seo`, `test:public-site-quality`, `test:managed-content`, `test:billing`, `test:formules`, typecheck et lint doivent rester verts.

## Risques de regression identifies

1. **Contenu existant incompatible** : si `problemEntries` devient obligatoire avant que la ligne CMS soit enrichie, `/services` peut tomber en `Services indisponibles`. Implementer une transition explicite (compatibilite temporaire ou mise a jour atomique du contenu).
2. **Seed non retroactif** : modifier le seed seul ne met pas a jour la ligne existante.
3. **Divergence API/WebPortal** : deux validateurs existent ; ils doivent accepter/refuser exactement la meme forme.
4. **Portail client** : ne jamais remplacer le rendu de la branche authentifiee `/services`.
5. **Billing** : ne pas reutiliser les cartes besoins comme CTA self-service.
6. **Liens Ressources** : la whitelist frontend doit etre etendue explicitement si une Ressource devient destination.
7. **CSS** : eviter les duplications de couches historiques.
8. **CMS** : l administrateur ne doit pas pouvoir supprimer la structure minimale de la landing par accident.

## Strategie de transition recommandee

Etape 1 : le code sait parser l ancien document et le nouveau document. Pour `storefront:services`, `problemEntries` peut etre absent pendant la transition et le renderer fournit un fallback code-seed strictement temporaire ou conserve le rendu generique.

Etape 2 : mettre a jour `storefront:services` via le chemin d administration/API normal avec les six entrees.

Etape 3 : une fois le contenu enrichi et valide localement, rendre `problemEntries` obligatoire pour `storefront:services` dans les deux validateurs/contrats. Ainsi on evite une fenetre de panne tout en terminant avec un contrat ferme.

Une autre strategie acceptable est un script/migration applicative idempotente de contenu, mais une migration SQL n est pas necessaire et serait moins coherente avec le CMS.

## Verdict final du lot A - audit

Le lot A est techniquement faisable comme un petit chantier cible. Il ne necessite ni migration SQL, ni refonte du CMS, ni modification Billing, ni creation de nouvelles routes.

Architecture recommandee :
1. conserver `storefront:services` et son `contentType=storefront_page`;
2. ajouter `problemEntries` uniquement au contrat specialise de cette cle;
3. renderer public `/services` specialise et pilote par CMS;
4. six cartes besoin -> solution;
5. quatre univers en second niveau;
6. message de modularite ensuite;
7. FAQ puis CTA final;
8. aucun bloc preuve/parcours quatre etapes avant le lot D;
9. aucun schema enrichi transversal avant le lot I.

Effort estime : faible a moyen, essentiellement WebPortal + validation API + CMS admin + tests. Risque principal : transition du JSON CMS existant, facilement maitrisable avec une mise a jour en deux temps.

Audit Lot A : TERMINE. Aucune modification fonctionnelle effectuee pendant l audit.

## État d'implémentation

Implémentation locale terminée le 27 août 2026, non commitée et non déployée à ce stade.

### Réalisé

- renderer public spécialisé `PublicServicesLandingPage` pour `/services` ;
- conservation stricte de la branche portail client `/services` ;
- contrat `problemEntries` avec six besoins et destinations fermées ;
- quatre domaines d'intervention en second niveau, avec `Support & IT` à la place de `Tarifs` ;
- suppression de l'injection automatique `Comparer les formules` dans le hero ;
- validation backend spécifique à `storefront:services` ;
- seed Storefront enrichi sans migration SQL ;
- formulaire CMS enrichi pour éditer les six besoins et les quatre domaines ;
- fallback de transition : l'ancien JSON CMS est lu sans panne mais rendu selon la nouvelle architecture ;
- au prochain enregistrement CMS, le document est normalisé vers le nouveau contrat strict ;
- grille responsive 3 / 2 / 1 pour les besoins ;
- libellés accessibles contextualisés sur les liens de cartes ;
- nouveau contrat `verify-services-landing-contract.mjs`, intégré à `test:managed-content`.

### Validation effectuée

- lint WebPortal : OK ;
- typecheck WebPortal : OK ;
- build production WebPortal : OK ;
- build API dans un répertoire de sortie isolé : OK ;
- smoke tests API-INTERNAL : OK ;
- contrats managed-content, SEO, qualité site public, Billing, Formules, Catalogue et Éditorial : OK ;
- `git diff --check` : OK ;
- runtime API isolé en mode mock : seed accepté avec 6 problèmes, 4 catégories et 1 section ;
- smoke local public `/services` avec le JSON CMS historique : 200, 1 H1, 6 besoins, 4 univers, anciennes sections retirées et aucun CTA automatique `Comparer les formules` ;
- host portail `/services` : contenu public non rendu et `X-Robots-Tag: noindex, nofollow` conservé.

### Transition de contenu

La ligne CMS persistée n'est volontairement pas modifiée pendant le développement. Le code déployable accepte l'ancien document en lecture et applique un fallback déterministe. La persistance du nouveau JSON doit être effectuée via le chemin CMS normal après livraison du code, puis vérifiée en production. Aucune modification SQL directe n'est nécessaire ni souhaitée.

### Statut

Lot A : implémentation locale complète, prête pour revue pré-commit et livraison.
