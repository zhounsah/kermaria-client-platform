# Vitrine, mise en page et demandes de données

> État du 2026-10-05 : livré en **DEV** sur SRV-12/SRV-13 après les migrations
> 099–101 sur `kermaria_dev`. Le WebPortal DEV sert Next.js 16.3.8 ; la
> production conserve v2.0.3.2. Les empreintes,
> la sauvegarde et les limites de preuve figurent dans
> [DEV_UX_CMS_2026-10-05.md](releases/DEV_UX_CMS_2026-10-05.md).

## Parcours public

L'accueil oriente d'abord par situation et par besoin. Les tarifs et la capacité
de commande proviennent toujours de Billing V2. Les textes de présentation ne
créent ni prix ni droit de souscrire. Le footer dirige vers les offres, l'aide,
l'espace client, les pages légales et `/demander-mes-donnees`.
Les anciennes formulations techniques connues des pages Services sont
présentées en langage plus clair uniquement lorsqu'elles correspondent
exactement au texte historique ; un texte réécrit par un administrateur garde
la priorité. Les identifiants de route restent stables.
La même règle s'applique aux catégories Réseau et Hébergement, aux résumés des
offres et à leurs fiches détaillées : les anciens libellés VPN/VPS/CMS connus
sont reformulés pour la vitrine. Les noms personnalisés et les données du
catalogue restent à leur source ; les quatre prix affichés ont été vérifiés
inchangés après cette projection.
Les quinze fiches de services connues ont été relues sur mobile : leurs textes
historiques techniques et leurs descriptions SEO exactes sont reformulés en
langage d'usage. Les champs administrés librement ne sont pas réécrits, pour
que le CMS reste l'autorité éditoriale.
Sur mobile, les espaces client et administration affichent d'abord le contenu ;
leur navigation se déplie à la demande et se ferme avec Échap.
Inter et JetBrains Mono sont servis depuis `apps/webportal/assets/fonts` avec
leurs licences OFL. Le build ne contacte plus Google Fonts et conserve la même
hiérarchie typographique.

## Constructeur de pages

Le menu **Administration → Mise en page** ouvre l'éditeur des espaces public,
client et administration. Chaque page possède un document de blocs versionné.
L'accueil public et le pied de page possèdent des blocs initiaux complets et
administrables. Si la nouvelle persistance est indisponible, l'accueil conserve
son ancien rendu pour ne pas devenir vide.
Les blocs disponibles sont : texte, image, cartes, questions, lien et formulaire
à action autorisée. Sur les pages métier non encore découpées, le bloc
`route_content` conserve le contenu et les actions portés par la route ; il
reste obligatoire. La page client « Mes données » possède plutôt des modules : son
en-tête, son formulaire et son historique sont trois modules distincts,
déplaçables dans l'éditeur ; les trois restent obligatoires. La liste
administrateur des demandes possède aussi une présentation et une liste
réordonnables séparément, toutes deux obligatoires. Les fiches de détail
client/admin exposent chacune le résumé, les échanges et leurs actions de
réponse ou de téléchargement comme modules distincts ; ces actions restent
obligatoires même lorsqu'elles ne sont pas visibles dans l'état courant.
La page `/formules` sépare également l'introduction, la grille d'offres issue
de Billing V2 et les conseils. La grille reste obligatoire ; réordonner ou
retirer un bloc facultatif ne change ni les prix ni les prestations.
Les quatre fiches `/formules/<code>` partagent une mise en page dédiée dans le
constructeur (`/formules/[code]`). Leur retour aux offres, leur présentation
issue du catalogue et leur configurateur sont des modules obligatoires.
Les conseils et le lien de contact sont des blocs éditoriaux déplaçables ; ils
ne contiennent ni montant ni règle de commande. Le prix de départ et les devis
continuent de venir de Billing V2. Si le constructeur est indisponible, le
rendu historique garde le configurateur. Si le catalogue est indisponible,
la page affiche une information sans jargon et reste non indexable.
La page `/offres` permet d'éditer directement son récit d'ouverture et ses
cartes, puis d'ordonner la présentation, la configuration et les deux vues du
catalogue. La configuration, la vue simple et le comparatif sont requis.
Les fiches `/offres/<slug>` partagent une mise en page : présentation, retour
au comparatif, résumé avec le choix de l'offre, services inclus et détails.
Chaque module reste obligatoire. Le contenu des offres conserve ses éditeurs
actuels ; la mise en page ne devient pas une seconde source de prix.
Le panier `/panier` sépare sa présentation, les services et choix du panier,
et le récapitulatif avec la suite de la commande. Ces modules restent requis.
Les états vide, expiré ou indisponible affichent également les blocs
éditoriaux, mais aucune action de paiement n'y est présentée. La page
`/souscription` sépare présentation, détail de l'abonnement et récapitulatif
avec confirmation et paiement. Le statut d'un paiement déjà lancé reste
hors des blocs éditables pour conserver une information fiable.
La page `/tarifs` permet d'ordonner l'introduction, le catalogue, les
explications et le contact. Le catalogue et le chemin vers un devis restent
requis ; les questions fréquentes et services associés sont facultatifs.
La page `/services` sépare le choix selon le besoin, les domaines
d'intervention, les explications, les questions et le contact. Le choix par
besoin, les domaines et le contact restent présents après chaque publication.
Les textes et liens de ces sections continuent de venir du contenu géré déjà
existant. Son ancien titre réservé aux professionnels est reformulé uniquement
si le texte historique exact est encore présent.
Le diagnostic `/diagnostic` possède une introduction éditable et des modules
obligatoires pour le questionnaire et le résultat. À la fin du questionnaire,
l'introduction laisse la place au résultat pour garder un seul titre principal.
Les questions, l'évaluation, les prix et la recommandation continuent de venir
des configurations et calculs métier existants.
La page `/contact` conserve son formulaire d'envoi, le contexte de l'offre
choisie et le lien de retour comme modules obligatoires. Son introduction, les
étapes suivantes et les deux points d'entrée alternatifs sont éditables sans
changer l'envoi du formulaire ni recopier un prix.
Dans l'espace client, `/souscrire` sépare la présentation, les offres et le
choix d'un service à la carte. Les deux chemins de souscription restent
obligatoires et continuent d'utiliser le catalogue et les calculs Billing V2.
L'accueil client `/dashboard` sépare la présentation, les indicateurs, les
services et démarches, les documents et demandes récents, l'activité et les
messages d'état. Les accès aux services et l'état de chargement restent
obligatoires. L'accueil `/admin` expose séparément les demandes à traiter,
les chiffres d'ensemble, le mode des intégrations et les raccourcis ; ces
modules restent obligatoires. Les données et permissions proviennent des
routes existantes et ne sont pas enregistrées dans les blocs CMS.
Le profil `/profile` sépare la présentation, les coordonnées et les actions de
sécurité du compte. Le catalogue `/admin/catalog` sépare présentation, accès
à la vitrine et éditeur métier ; les droits et les validations tarifaires
restent contrôlés par l'API.
Pour les anciens noms de services connus, le portail client présente une
formulation plus simple ; les libellés modifiés dans les données gardent la
priorité. La référence technique d'un service reste disponible dans un détail
dépliable, sans occuper la lecture principale.
La page `/signup` sépare sa présentation, la reprise éventuelle du panier ou
du serveur, le récapitulatif de l'offre, les étapes et le formulaire. Le
récapitulatif, le formulaire et le lien de connexion existant sont requis ;
les calculs et la sélection continuent de venir des chemins serveur normaux.
La page publique `/demander-mes-donnees` possède un titre et une introduction
éditables, des étapes, une explication des délais et un lien vers la politique
de confidentialité. Les deux sorties utiles — ouvrir la demande dans l'espace
client et contacter l'équipe en cas de difficulté de connexion — restent dans
un module obligatoire. Le serveur refuse une publication sans introduction ou
sans ce module. L'ancien rendu reste disponible si la lecture du constructeur
échoue ; une mise en page ancienne avec `route_content` est projetée vers les
nouveaux blocs lors de sa lecture.
Le formulaire adapte ses titres et champs visibles au choix particulier ou
organisation ; les données transmises au BFF et leurs validations restent les
mêmes.
Par défaut, le formulaire apparaît avant les explications détaillées des
étapes suivantes afin de raccourcir l'accès à l'action principale sur mobile.
Les quatre descriptions historiques des offres sont reformulées à l'affichage
pour décrire l'usage plutôt que les noms de paliers. Une description modifiée
dans le catalogue garde la priorité et aucun montant n'est recopié.
Les libellés et descriptions historiques des services Billing V2 affichés sur
`/tarifs` et `/formules` suivent la même règle : seuls les textes exacts connus
sont reformulés. Les noms, descriptions et choix de palier administrés gardent
leur priorité. Les codes, montants, frais et actions commerciales ne changent
pas. Les capacités d'un serveur sont présentées comme unités de calcul,
mémoire et espace de stockage, avec les valeurs issues du catalogue.
La même présentation en langage courant accompagne maintenant la
configuration d'une offre, son aide, son récapitulatif, le panier, la revue
avant paiement et le récapitulatif de l'inscription. Le sigle des paliers
d'accès à distance historiques est retiré à l'affichage seulement ; leurs
codes et montants contractuels restent ceux de Billing V2. La description SEO
des fiches d'offre reprend le résumé présenté au client, y compris lorsqu'un
ancien texte technique est projeté. Si le catalogue manque, la page indique
simplement que les offres sont momentanément indisponibles.
Un enregistrement publie immédiatement. Une version périmée ou un document sans
bloc requis est refusé avant écriture ; une restauration crée une nouvelle
version. Le pied de page est une page spéciale dont les liens légaux et la
demande de données sont obligatoires.
L'aperçu de l'éditeur réutilise le rendu des blocs éditoriaux dans la largeur
du panneau avant publication. Les modules fonctionnels restent des repères
visuels : ils ne chargent pas de données privées dans l'éditeur. Les liens et
les formulaires de l'aperçu sont inactifs, et le bouton **Voir la page
actuelle** ouvre la version publiée dans un autre onglet.

Les contenus déjà portés par `managed_content_entries`, la plateforme
éditoriale ou Billing V2 restent lus depuis leurs autorités existantes. Le
constructeur organise les blocs autour de ces contenus et n'en duplique pas la
valeur. La publication refuse un prix chiffré saisi librement dans les blocs :
les montants viennent du catalogue Billing V2. Il ne permet pas de définir un
appel API arbitraire. Les formulaires
insérables réutilisent les actions de contact et de demande de données. Leurs
champs complémentaires (texte, e-mail, nombre, choix, case) sont configurables
dans l'éditeur ; leurs réponses sont ajoutées au message soumis. Les champs
essentiels et le traitement restent ceux du parcours autorisé.
Si une page convertie en modules possède déjà une mise en page enregistrée avec
un bloc `route_content`, l'API projette ce bloc vers les modules initiaux lors
de la lecture. Les blocs éditoriaux placés avant ou après sont conservés ; une
sauvegarde ou une restauration publie ensuite cette mise en page compatible.
La projection ne modifie pas silencieusement le document stocké.

La médiathèque accepte PNG, JPEG et WebP jusqu'à 5 Mo, avec description
obligatoire. Les octets et les métadonnées sont stockés par API-INTERNAL dans
MariaDB et livrés via le BFF. Ne jamais y déposer un document privé : ces
images sont publiques. L'éditeur montre une galerie avec recherche, aperçu et
insertion dans la page. Les migrations `099`, `100` et `101` sont additives ; leur
application suit le protocole de sauvegarde MariaDB du dépôt. Elles doivent être
appliquées avant la livraison du nouveau code dans chaque environnement : le stockage des notifications
personnelles et la vérification d'inscription lisent leurs nouvelles colonnes.
L'API vérifie la présence des tables du CMS et des demandes en lecture seule ;
elle vérifie aussi la colonne d'inscription de la migration 101. Elle ne tente
aucune création de schéma pendant une requête.

## Demandes de données

La page publique explique la démarche. Le dépôt et le suivi s'effectuent sous
`/profile/donnees`, après connexion. Une demande appartient à l'utilisateur
qui l'a créée : un autre utilisateur du même client ne peut ni la lire ni y
répondre. L'administration la traite sous `/admin/data-requests`. Les réponses
et demandes de précision sont visibles dans l'espace client et une notification
est créée lors d'un message de l'équipe. Elle peut remettre un document PDF,
CSV, JSON ou ZIP de 10 Mo maximum ; le téléchargement exige la session de
l'utilisateur auteur de la demande. La date d'échéance initiale est un
mois civil après la réception. Pour une demande complexe, l'équipe peut
prolonger une seule fois de deux mois avant cette première échéance, avec un
motif rendu visible et notifié au client. Cette règle suit la
[CNIL](https://www.cnil.fr/fr/repondre-une-demande-de-droit-dacces).
Le système ne conclut pas automatiquement au
bien-fondé d'une demande et n'efface aucune donnée de lui-même.

## Inscriptions

Le paramètre administrateur `signup_auto_approve` est désactivé par défaut.
La variable historique `SIGNUP_AUTO_APPROVE` n'est plus lue : une valeur
d'environnement ne peut pas activer ce mode à l'insu de l'administration.
La page publique lit à chaque rendu le mode effectif auprès d'API-INTERNAL :
elle décrit soit l'ouverture après confirmation de l'e-mail, soit l'examen par
l'équipe. Le message après dépôt suit également ce mode. Si la lecture échoue,
le formulaire est fermé plutôt que d'annoncer une procédure inexacte. Le mode
définitif est évalué lors de la vérification de l'e-mail, et la page de
confirmation décrit le résultat effectivement obtenu.
Le courriel de vérification envoyé avant cette décision utilise un texte
neutre : il annonce soit les instructions d'ouverture, soit un message
indiquant que la demande est en cours d'examen, sans promettre une revue
manuelle lorsque l'approbation automatique est activée.
S'il est activé, la vérification de l'e-mail d'une inscription standard appelle
le chemin d'approbation existant. L'état `email_verified` peut être repris avec
le même lien après un échec intermédiaire. Le mode choisi lors de la
vérification est enregistré avec la demande : l'activation ultérieure du
réglage n'approuve pas une demande déjà vérifiée en mode manuel. Les
inscriptions self-service déjà
approuvées à la création conservent leur logique distincte et leur vérification
e-mail. Le passage au mode manuel s'applique aux nouvelles vérifications.
Si le compte est créé mais que l'e-mail de mot de passe échoue, l'état de
livraison reste marqué `pending` en base. Le même lien de vérification permet
de reprendre l'envoi après une minute, puis avec un délai de cinq minutes
entre tentatives. Un envoi réussi éteint cet état ; le lien de vérification
expiré ne relance pas l'envoi. Le compte n'est jamais recréé lors d'un rejeu.
