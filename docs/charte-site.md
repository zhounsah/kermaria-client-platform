# Direction graphique et éditoriale du site Zachary IT

## Intention

La vitrine doit expliquer une activité de services informatiques à une personne qui dirige une PME, un commerce ou une petite structure. La lecture suit un ordre simple : **activité et public → problèmes pris en charge → manière de travailler → offres et tarifs → contact**. Les mots désignent des situations et des prestations réelles, sans preuve sociale inventée ni promesse de délai.

Le parti pris est éditorial : fond blanc, encre bleu nuit, bleu de lien et de décision, traits fins, photographie documentaire limitée à l'accueil. Les cartes sont réservées aux offres réellement comparables et aux contrôles qui ont besoin d'un cadre. Les autres contenus utilisent des listes, des séparateurs et des bandes de section. Cette règle s'applique aussi à l'aperçu du constructeur de pages quand il rend les mêmes blocs.

## Système de design

La couche `apps/webportal/app/site-refonte.css` est importée après `globals.css`. Elle contient les règles nouvelles du site et les variables `--site-*`, sans changer les alias métier ni les styles des écrans transactionnels qui n'ont pas été réexaminés. Les couleurs principales sont : encre `#0b1730`, texte `#35445c`, bleu d'action `#1956c8`, trait `#d6deea`, bande douce `#eef3f9` et blanc `#fff`. L'accent orange `#f49b28` sert au focus clavier, afin que celui-ci soit visible sur fond blanc comme sur fond bleu nuit.

Le contenu public ne dépasse pas `1360px`; la marge latérale varie avec l'écran. Les titres ont une échelle plus marquée que les libellés d'interface, avec un interlignage serré et des lignes courtes. Les paragraphes restent entre 15 et 19px selon leur rôle. Les boutons ont une hauteur minimale de 48px, des bords peu arrondis et un état de survol sans déplacement décoratif. Les champs du contact conservent leurs labels visibles, erreurs associées et focus. Le menu mobile garde son bouton, son état `aria-expanded` et sa fermeture par Échap.

L'image de l'accueil possède des dimensions réservées dans le HTML et une priorité de chargement élevée. Pendant une navigation qui attend des données, `app/loading.tsx` réserve au moins une hauteur d'écran et emploie un message neutre ; le pied de page ne saute plus dans le premier écran avant le vrai contenu. La police monospace des zones techniques ne doit pas être préchargée sur toutes les pages publiques.

À 820px, le hero et les zones à deux colonnes se replient. À 600px, les listes et les offres passent sur une colonne, les actions remplissent la largeur utile et les séparateurs gardent une lecture claire. Les préférences de réduction des mouvements sont respectées.

## Composants et parcours

- **Accueil :** titre explicite, action « Demander un devis », accès direct aux services, besoins professionnels identifiés. L'image est dans sa propre colonne et ne porte aucun texte ; elle illustre un équipement réseau générique, pas une installation ou une personne de Zachary IT.
- **Services :** le lecteur peut partir d'un problème ou d'une famille de prestations. Les entrées conservent leurs destinations existantes et les contenus administrables restent la source des fiches.
- **Accès distant :** le lien de comparaison déjà présent dans les parcours mène désormais à une page réelle qui distingue VPN et bureau Windows à distance, sans prix ni engagement inventé.
- **Offres et tarifs :** les prix, options, statuts de commande et actions sont toujours fournis par l'API. La page les présente avec moins de surfaces décoratives. Le récapitulatif éditorial ne qualifie plus tous les prix de HT lorsque les composants affichent du TTC.
- **Contact :** formulaire réel à gauche et déroulement à droite sur ordinateur ; une seule colonne sur mobile. Aucun délai de réponse n'est annoncé. Les retours d'erreur et de succès du BFF restent en place.
- **Espaces client et admin :** la navigation et les actions restent inchangées ; les fonds, séparateurs, panneaux et contrôles reprennent les tokens de la vitrine. L'absence de session de test n'est pas une preuve de recette complète des écrans authentifiés.

## Autorités de contenu

L'accueil, le contact et le pied de page peuvent être publiés par le constructeur de pages. Les autres pages commerciales combinent code, contenu administrable et catalogue serveur. Les valeurs initiales de l'API et les replis WebPortal sont réécrits. `lib/public-site-editorial.ts`, `lib/storefront-content.ts` et `lib/public-packs.ts` projettent seulement des formulations historiques exactes ; une édition libre du CMS garde la priorité. Les valeurs persistées ne sont pas migrées ni écrasées par ce changement de code. Une revue du CMS DEV reste nécessaire avant de considérer chaque texte administrable comme livré.

## Références visuelles et limites

Trois concepts ont été créés pour cadrer l'accueil, la page Services et le contact. Leur rôle est d'établir la typographie, la densité, la palette et le modèle de mise en page. Les variantes de texte ou de logo générées qui ne sont pas conformes au dépôt ne sont pas reprises. L'identité juridique et le logo SVG existants restent l'autorité. L'image d'accueil `network-office-editorial.webp` est un visuel illustratif généré puis optimisé à 116 Ko ; elle ne constitue pas une preuve de clientèle, d'équipe ou d'infrastructure exploitée.

Les concepts et captures de travail sont conservés hors dépôt dans `C:\Users\zhounsah\Documents\Dev\zachary-it-qa\`. Les six captures comparatives utiles à la revue sont versionnées dans `docs/audit-captures/`. Une capture locale en mode mock ne prouve ni le rendu de DEV avec ses contenus persistés ni celui de PROD.

### Revue de fidélité des concepts

| Point contrôlé | Concept | Rendu vérifié | Décision |
| --- | --- | --- | --- |
| Marque et navigation | Logo compact, six entrées utiles, contact bleu | Logo SVG existant, services/offres/tarifs/à propos/espace client/contact ; panier conservé | Le panier réel est une différence voulue. |
| Accueil au premier écran | Titre concret sur blanc, deux actions, image distincte | Titre et libellés identiques ; image réseau sans personne ; rendu contrôlé à 1586 × 992 | Photo illustrative sans personne pour éviter de suggérer un salarié ou un chantier réel. |
| Palette et typographie | Blanc, encre bleu nuit, bleu d'action, aucun halo | Variables `--site-*`, Inter locale, lignes fines et focus orange | Conforme à la direction. |
| Rythme de l'accueil | Service list dès la fin du hero | Bloc « organisations » puis méthode et services, selon les types et l'ordre CMS existants | Ordre conservé pour que l'éditeur publié et le parcours affiché restent cohérents ; les blocs ne sont plus des cartes identiques. |
| Services | Titre fort et lignes de prestations | Titre et lead validés ; problèmes puis quatre domaines en listes éditoriales | Les six points de départ existants et leurs liens sont conservés. |
| Contact | Formulaire à gauche, étapes à droite | Même architecture sur desktop, une colonne sur mobile | Le sujet reste un champ libre réel, avec nom ou raison sociale et note de confidentialité. |
| Responsive | Colonnes repliées et actions accessibles | 320, 390, 768 et 1440 px contrôlés sans débordement global ; comparatif d'offres défilant dans son propre conteneur | Aucune fonctionnalité n'est masquée pour obtenir le rendu. |

Les réparations de la boucle visuelle ont porté sur la largeur du hero CMS, son cadrage, la hauteur du premier écran, les couleurs des CTA sombres, la colonne explicative du contact et le débordement du comparatif. Les différences restantes ci-dessus sont intentionnelles et liées aux parcours réels. Le rendu final des surfaces publiques examinées suit fidèlement la direction retenue pour ce chantier.
