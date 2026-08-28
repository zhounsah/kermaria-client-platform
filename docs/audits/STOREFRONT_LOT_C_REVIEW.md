# Storefront Lot C - Revue utilisateur avant corrections

Date : 2026-08-28
Statut : remarques utilisateur consignees avant toute nouvelle modification fonctionnelle.

## Contexte

Cette note fait suite a la premiere implementation locale du lot C et consigne la revue visuelle et fonctionnelle avant la passe corrective.

Pages concernees :
- `/services/messagerie-professionnelle` ;
- `/services/vpn-entreprise` ;
- `/services/sauvegarde-externalisee` ;
- `/services/unifi` ;
- `/services/infogerance-vps` ;
- `/services/hebergement-web` ;
- `/services/domaines-messagerie` est aussi incluse dans la passe corrective, meme si elle appartient au niveau categorie.

## Remarques utilisateur a corriger

### 1. Espacement global sous le header

Constat : le vide entre le header principal et le debut reel du contenu / fil d'Ariane est trop important.

Objectif : reduire ce padding vertical sur les pages Storefront concernees pour remonter le contenu, sans casser la respiration generale ni le responsive.

### 2. Taille et largeur des titres

Constat : les H1 sont trop gros et se replient trop tot en un bloc vertical massif, particulierement visible sur `sauvegarde-externalisee`.

Objectif :
- reduire la taille maximale des H1 des pages service ;
- donner davantage de largeur au bloc texte du hero ;
- favoriser une composition plus horizontale sur desktop ;
- conserver une taille lisible et responsive sur tablette/mobile.

### 3. Diagnostic non contextualise

Constat : plusieurs CTA `Demander un audit` redirigent vers `/diagnostic`, mais le diagnostic actuel n'est pas toujours pertinent pour le probleme de depart. Exemple : la sauvegarde est coherente avec le diagnostic actuel, alors qu'un besoin VPN peut tomber sur un parcours trop oriente stockage.

Decision pour cette passe :
- ne pas modifier la page `/diagnostic` ;
- ne pas tenter de resoudre le moteur de diagnostic dans le lot C ;
- conserver ce sujet explicitement ouvert pour une passe ulterieure avec Codex ;
- eviter d'ajouter de nouveaux parcours qui aggraveraient cette incoherence.

### 4. Page VPN - bloc prochaine etape

Constat : le texte actuel `Le service est qualifie avant mise en oeuvre. Lorsqu'aucune projection Billing autoritative n'est disponible...` est trop interne / technique et pas assez customer-friendly.

Objectif : remplacer la presentation publique par un texte comprehensible par un client, sans exposer les notions internes Billing, projection ou autorite commerciale.

### 5. Page VPN - comparaison VPN / bureau Windows distant

Constat : renvoyer le visiteur vers une page SEO pour comprendre la difference peut etre frustrant alors que l'information est utile directement au moment de choisir le service VPN.

Objectif :
- condenser l'explication utile directement dans `/services/vpn-entreprise` ;
- proposer un complement discret du type `En savoir plus` ;
- ouvrir ce complement de facon non agressive et sans sortir brutalement du parcours principal ;
- conserver la Ressource SEO comme contenu approfondi, pas comme etape obligatoire du parcours.

### 6. Page `/services/domaines-messagerie`

Constat : la page categorie actuelle n'est pas assez orientee client.

Objectif : revoir entierement sa presentation pour partir des problemes et usages reels :
- adresse professionnelle et identite de domaine ;
- mails qui arrivent en spam / delivrabilite ;
- migration de boites ;
- Microsoft 365 / licences / comptes ;
- DNS et responsabilites ;
- orientation claire vers les pages service pertinentes ;
- vocabulaire client avant vocabulaire technique.

## Contraintes a preserver

- Ne pas modifier `/diagnostic` dans cette passe.
- Ne pas ajouter de prix codes en dur dans le CMS ou le renderer.
- Billing reste l'autorite des parcours en formule.
- Un service non self-service ne doit jamais devenir commandable par simple contenu CMS.
- Conserver les canonical, breadcrumbs, SEO et garde-fous existants.
- Ne pas transformer la Ressource SEO VPN / bureau distant en passage obligatoire.
- Conserver un rendu mobile propre et accessible.

## Criteres de validation

La passe corrective sera consideree satisfaisante si :
- le contenu demarre sensiblement plus haut sous le header ;
- les H1 desktop sont moins massifs et utilisent mieux la largeur disponible ;
- aucun texte public ne mentionne Billing, projection autoritative ou autre vocabulaire interne ;
- la page VPN permet de comprendre directement la difference VPN / bureau distant ;
- un lien ou complement `En savoir plus` reste discret et secondaire ;
- `/services/domaines-messagerie` est reecrite comme une vraie page d'orientation client ;
- `/diagnostic` reste inchangee ;
- les contrats SEO, Billing, Formules, managed-content, lint, typecheck et build restent verts.

## Etat apres passe corrective

Corrections realisees localement :
- padding sous le header reduit sur les pages Storefront ;
- taille maximale des H1 reduite globalement ;
- hero des six pages prioritaires et de Domaines & Messagerie etendu sur la largeur pour eviter les gros blocs verticaux ;
- texte public `Tarif et devis` remplace sur les pages prioritaires par une prochaine etape orientee client ;
- aucune mention publique de Billing, projection ou projection autoritative dans les renderers corriges ;
- page VPN : comparaison VPN / bureau Windows distant conservee sur la page et completee par un panneau `En savoir plus` deplie sur place ;
- la Ressource SEO VPN / bureau distant reste uniquement un lien d approfondissement facultatif ;
- `/services/domaines-messagerie` remplacee par une page d orientation client partant de quatre situations concretes ;
- liens principaux de cette categorie orientes vers messagerie professionnelle, gestion DNS/domaines et, en second niveau, la Ressource sur les e-mails en spam ;
- responsive et garde-fous Billing conserves.

Element volontairement differe :
- `/diagnostic` et son moteur de recommandation n ont pas ete modifies ;
- la contextualisation du diagnostic selon la page d origine fera l objet d une passe distincte avec Codex.

Validation :
- `test:managed-content` : OK ;
- contrat Lot C : OK ;
- `test:seo` : OK ;
- `test:public-site-quality` : OK ;
- `test:billing` : OK ;
- `test:formules` : OK ;
- typecheck : OK ;
- lint : OK ;
- build Next.js production : OK ;
- `git diff --check` : OK ;
- aucun fichier ou route Diagnostic modifie.
