# UX_CONTENT_REVIEW — pages publiques Zachary IT v2.0.2.5

Revue page par page, dans la peau d'un dirigeant de TPE non technique puis
d'un prospect sceptique. Les notes de qualité finale sont sur 10 et
volontairement sévères.

Méthode : lecture du code et du seed CMS, puis parcours réel de la pile
construite (API-INTERNAL + WEBPORTAL) sur `127.0.0.1:3055`, en desktop et en
mobile 375×812.

---

## `/` — Accueil

**Objectif de la page.** Faire comprendre en dix secondes ce que fait
l'entreprise et pour qui, puis orienter vers un premier pas.

**Critique initiale.** Le titre servi est
« Zachary IT | Sauvegarde informatique à Guichen (35) » et la meta description
ne parle que de « sauvegarde distante, stockage documentaire et continuité
d'activité ». Or `/services` publie quatre univers et quinze pages de service :
VPS, hébergement web, messagerie, DNS, VPN, bureau à distance, UniFi, firewall,
WAF, supervision, maintenance Linux et WordPress, support. **Un prospect qui
cherche « qui peut me refaire mon Wi-Fi » ou « qui gère mes boîtes mail » ne
reconnaît pas Zachary IT depuis le résultat de recherche ni depuis l'accueil.**
C'est l'écart le plus coûteux commercialement de tout le site.

**Changements effectués.** Aucun sur la page. Le titre et la description
portent le ciblage SEO actuel : les modifier déplace le référencement existant.
C'est une décision commerciale, arbitrée dans `OVERNIGHT_REPORT.md`.

En revanche, le nœud `LocalBusiness` émis par l'accueil décrivait lui aussi la
seule sauvegarde. C'est la **seule phrase lisible par machine** qui répond à
« que fait Zachary IT ? », et elle contredisait le catalogue du site : elle
couvre désormais les cinq familles réellement publiées, avec un `knowsAbout`
de quinze sujets, un par page existante.

**Problèmes restants.** Le décalage positionnement/catalogue (F16). Le pied de
page affiche « Version v2.0.2.5 » (F23), marqueur de déploiement volontaire.

**Qualité finale estimée : 6/10.** La page est propre, rapide et honnête ; elle
vend un métier plus étroit que celui de l'entreprise.

---

## `/services` — Routeur problème → solution

**Objectif.** Faire choisir un point d'entrée à partir d'un problème vécu,
avant tout catalogue technique.

**Critique initiale.** C'est la meilleure page du site sur le plan éditorial :
six entrées formulées en problèmes, quatre univers en second niveau, une FAQ
réelle. Deux défauts objectifs :
- aucun balisage `FAQPage` alors que les questions-réponses sont bien rendues ;
- en cas de contenu illisible, la page répondait 200 avec une canonical
  indexable et un corps « temporairement indisponible ».

**Changements effectués.** Balisage `FAQPage` limité aux questions réellement
affichées ; `noindex, follow` sur la seule branche qui rend l'`ErrorState`.
Le repli de titre « Services Zachary IT » dupliquait la marque : il devient
« Services », le gabarit ajoutant « | Zachary IT ».

**Problèmes restants.** La route est partagée avec « Mes services » côté
tableau de bord ; la bascule passe par `resolveServicesPortalMode`, désormais
couverte pour les cinq zones. Aucun défaut résiduel identifié.

**Qualité finale estimée : 8/10.**

---

## `/services/[category]` et les 15 pages de service

**Objectif.** Expliquer un service, ses limites, et donner le bon pas suivant.

**Critique initiale.** Le contenu du seed est remarquablement honnête : il dit
ce qui n'est pas inclus, refuse de promettre une disponibilité absolue, et
distingue sauvegarde et synchronisation. C'est exactement ce qui construit la
confiance d'un prospect sceptique. Défauts : pas de `FAQPage`, soft-404 en cas
de panne, et un repli de titre dupliquant la marque.

Un défaut de vocabulaire avait été introduit puis corrigé par l'équipe avant
cette revue (`013b6b4`) : le texte public renvoyait au « catalogue Billing
V2.1 ». Le contrat de test, lui, n'avait pas suivi — il **exigeait encore**
cette formulation interne, et échouait donc sur HEAD.

**Changements effectués.** `FAQPage`, `noindex` sur la branche d'erreur, repli
de titre nettoyé, contrat realigné sur l'intention client.

**Problèmes restants.** Aucun défaut résiduel identifié sur ces pages.

**Qualité finale estimée : 8/10.**

---

## `/tarifs`

**Objectif.** Rendre lisible ce qui est facturé, à quelle unité, et ce qui
reste sur devis.

**Critique initiale.** La page dit clairement pourquoi certains services n'ont
pas de prix affiché — bon point face à un sceptique. Deux défauts :
- le titre CMS était « Tarifs des services IT Zachary IT | unités et devis »,
  et le gabarit ajoutait un second « | Zachary IT » : **76 caractères avec la
  marque en double** dans les résultats de recherche ;
- même soft-404 que les autres pages de contenu administrable.

**Changements effectués.** Titre du seed ramené à « Tarifs des services IT :
unités et devis » (53 caractères une fois la marque ajoutée) ; `noindex` sur
la branche d'erreur ; repli de titre nettoyé.

**Problèmes restants.** Le titre servi en production vient de la base, pas du
seed : il doit être corrigé depuis `/admin/content`. La page emploie
« formule » et « offre » sans les distinguer (F17).

**Qualité finale estimée : 7/10.**

---

## `/offres` et `/offres/[slug]`

**Objectif.** Présenter quatre packs et permettre de demander celui qui
correspond.

**Critique initiale.** La page emploie **les trois mots à la fois** —
« pack », « formule », « offre » — pour désigner le même objet. Un lecteur non
technique ne peut pas savoir s'il s'agit de trois choses différentes ou d'une
seule. `/formules` parle de « formules », `/offres` de « packs », le pied de
page de « formules ». C'est le défaut de cohérence éditoriale le plus visible
du site.

**Changements effectués.** Aucun sur la page : choisir le terme unique et
renommer les parcours est une décision commerciale, pas une correction. Seule
la contradiction franche a été traitée, sur `/contact` (voir plus bas).

**Problèmes restants.** F17 entier.

**Qualité finale estimée : 6/10.** Le contenu est bon, la terminologie flotte.

---

## `/contact` — revue critique demandée

**Objectif.** Convertir une intention en prise de contact qualifiée.

**Critique initiale.** Quatre défauts, du plus grave au plus léger.

1. **Le backend renvoyait au visiteur des textes d'exploitation.** En cas
   d'échec, `/api/contact` recopiait le message d'API-INTERNAL : « L'adresse de
   destination du formulaire de contact n'est pas configurée. » pour un
   destinataire absent, et la **réponse SMTP brute** — hôte, échec
   d'authentification, adresse refusée — pour un échec de remise. En cas de
   succès, la réponse JSON contenait « Message transmis à \<boîte interne\> ».
   Un visiteur anonyme de la vitrine pouvait lire tout cela.
2. **Le lien retour contredisait sa destination** : intitulé « Retour aux
   formules », il pointait vers `/offres`. Les liens `?formule=` viennent des
   cartes et du tableau comparatif de `/offres` : la destination était juste,
   le libellé faux.
3. **L'accroche excluait la moitié des prospects.** Elle cantonnait la page à
   « la sauvegarde distante, du stockage documentaire, de la continuité
   d'activité », et promettait une réponse « sous un délai raisonnable » —
   formule qui n'engage rien tout en ayant l'air d'engager.
4. **Rien ne disait ce qui se passe après l'envoi**, et le résultat n'était
   annoncé qu'en `aria-live` : au clavier, on reste sur le bouton.

**Changements effectués.**

- Le BFF ne relaie plus ni le code ni le message amont. Il rend une phrase
  stable qui dit quoi faire, et journalise le code amont via `logBffFailure`
  sous le même `correlation_id`. Côté API, le détail SMTP part dans un
  `LogError` et le journal d'e-mails, plus sur le fil ; la réponse de succès ne
  nomme plus la boîte de réception.
- Libellé « Retour aux offres ».
- Titre « Parlons de votre besoin », accroche ouverte aux cinq familles de
  services, avec une phrase que peu de prestataires écrivent : « si nous ne
  sommes pas les bons interlocuteurs, nous vous le dirons ».
- Bloc « Ce qui se passe ensuite » en trois étapes, réutilisant la carte
  `signup-steps-card` de la ligne visuelle existante. Il ne décrit **que** ce
  que le système fait réellement, et **n'annonce aucun délai** : rien dans le
  produit ne permet d'en tenir un.
- Renvoi vers `/diagnostic` et `/services` pour le visiteur qui cherche encore
  à situer son besoin.
- Le bloc de résultat prend le focus après soumission ; « Sujet (optionnel) »
  indique le seul champ facultatif.

**Vérifié en conditions réelles.** Champs vides, e-mail invalide, code de
formule forgé (`../../etc/passwd`), code inconnu, JSON malformé, limitation de
débit : tous corrects, avec `correlation_id`. Le chemin d'échec renvoie bien la
nouvelle phrase et plus aucun détail d'exploitation. Rendu mobile 375 px
vérifié.

**Problèmes restants.** L'adresse de contact publiée reste `zhounsah@home.bzh`,
un domaine interne (F18) — c'est pourquoi le message d'échec renvoie aux
mentions légales plutôt que d'inventer une adresse. La classe
`signup-steps-card` est désormais partagée avec l'inscription (F21).

**Qualité finale estimée : 8/10.** La fuite d'information était le vrai sujet ;
elle est fermée et testée.

---

## `/diagnostic`

**Objectif.** Orienter un visiteur qui ne sait pas nommer son besoin.

**Critique initiale.** Bon principe, bornes de contexte validées côté serveur,
repli sûr sur le parcours général quand le contexte est inconnu. Le mapping
profil → formule est administrable depuis `/admin/diagnostic` et validé contre
le catalogue public avant persistance : une formule indisponible retombe sur
cadrage/devis plutôt que de proposer un produit inexistant.

**Changements effectués.** Aucun. La page n'a montré aucun défaut objectif.

**Problèmes restants.** Aucun identifié.

**Qualité finale estimée : 8/10.**

---

## `/a-propos`, `/infrastructure`, `/ressources`

**Objectif.** Établir la crédibilité et servir de hub éditorial.

**Critique initiale.** `/a-propos` servait « À propos de Zachary IT | Zachary
IT ». `/infrastructure` est une bonne page pour un sceptique : elle parle
d'exploitation, de fournisseurs et de localisation sans surpromettre.

**Changements effectués.** Titre `/a-propos` ramené à « À propos ».

**Problèmes restants.** Aucun identifié.

**Qualité finale estimée : 7/10.**

---

## `/decouvrir-espace-client`

**Critique initiale.** Titre « Découvrez l'espace client Zachary IT | Zachary
IT ». La démonstration reste fictive, marquée DEMO et isolée, conformément à la
règle du projet.

**Changements effectués.** Titre ramené à « Découvrez l'espace client ».

**Qualité finale estimée : 7/10.**

---

## Pages légales — `/mentions-legales`, `/politique-confidentialite`, `/cgv`

**Critique initiale.** Contenu administrable, `lastmod` réel dans le sitemap,
priorité basse. Rien à redire, sinon que l'adresse de contact y est le domaine
interne (F18).

**Changements effectués.** Aucun.

**Qualité finale estimée : 7/10.**

---

## Transversal — UX / UI / CSS

**Ligne visuelle de référence.** Les composants récents (`PublicShell`,
`ServiceCard`, `signup-steps-card`, `form-card`) forment une ligne cohérente :
rayons, ombres, jetons de couleur et échelle typographique sont centralisés en
variables CSS. Les nouveaux blocs de `/contact` réutilisent cette ligne au lieu
d'en créer une.

**États d'interaction.** `SubmitButton` gère `aria-busy`, l'état désactivé et
un libellé de chargement distinct. `FormMessage` porte `role="alert"` /
`role="status"` selon le ton. `ErrorState` affiche la référence de corrélation,
ce qui rend un incident traçable côté exploitant — bonne pratique déjà en place.

**Responsive.** Vérifié en 375×812 et en desktop sur `/contact` : en-tête,
formulaire, carte d'étapes et pied de page se comportent correctement, sans
débordement horizontal.

**Doublons CSS.** Les sélecteurs déclarés plusieurs fois ont été contrôlés :
ce sont des variantes de media queries légitimes, pas des contradictions. Le
seul cas d'un même sélecteur deux fois dans un même bloc `@media`
(`.public-header-signup`) porte sur des propriétés disjointes.

**Dette restante.** `app/globals.css` fait 11 304 lignes (F20).

**Qualité finale estimée : 7/10.**
