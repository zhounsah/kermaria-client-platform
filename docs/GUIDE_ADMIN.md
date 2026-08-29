#
> Current catalog UI - 2026-08-26: `v2.0.0.2` is deployed. See [`CURRENT_STATE.md`](CURRENT_STATE.md) for production truth.
 Guide administrateur - portail interne

Guides courts d'exploitation admin livres en V0.24. Toutes les sections
sont accessibles depuis le menu **Administration** du portail interne
(role `internal_admin`).

Rappel de cadrage :

- le portail emet des documents commerciaux informatifs et des factures via
  BPCE ;
- les paiements confirmes peuvent maintenant declencher, le cas echeant,
  l'activation ou le provisioning attendus ;
- les modes d'integration (BPCE, e-mail, Stripe, PayPal, AD) restent pilotes
  par configuration.

---

## Catalogue commercial Billing V2 — `/admin/catalog`

Le catalogue est l'unique autorité commerciale. La page principale sépare les
listes **Services**, **Formules** et **Engagements** ; **Intégrations** regroupe
Stripe, PayPal et les identifiants externes avancés.

- Ouvrir **Modifier** pour travailler dans une fiche dédiée. Les onglets sont
  adressables et peuvent être partagés ou rechargés sans perdre la section.
- Un service se configure dans l'ordre : informations essentielles, paliers,
  tarification, puis commercialisation.
- Un tarif existant n'est jamais corrigé en place. Utiliser **Créer une nouvelle
  version**, vérifier l'ancien et le nouveau montant, puis choisir sa date
  d'effet. L'historique reste consultable.
- Les remises et la TVA se saisissent en pourcentage ; le portail effectue la
  conversion technique attendue par Billing V2.
- Une formule est une composition. Son aperçu de prix vient de la projection
  serveur et ne constitue pas un calcul local.
- Les codes immuables restent copiables dans les fiches. Les paramètres
  techniques structurants restent visibles en lecture seule.
- Tant qu'un formulaire affiche **Modifications non enregistrées**, utiliser la
  barre d'actions fixe pour enregistrer ou annuler les changements.

Un mapping Stripe est facultatif : Billing V2 continue à construire ses lignes
avec `price_data` inline. Un mapping PayPal manquant peut en revanche demander
une action selon le rail configuré.

---

## 1. Paiements - `/admin/payments`

Menu **Pilotage > Paiements**.

- **Liste** : chaque reglement avec client, document lie, montant, moyen
  (`stripe`, `paypal`, `manual`) et statut.
- **Origine document** : un paiement peut provenir d'un document one-shot,
  d'un panier client (`client_cart`) ou d'une facture d'abonnement facture
  (`recurring_checkout` / renouvellement).
- **Marquer comme paye** : pour un virement recu hors ligne, ouvrir le
  document puis utiliser l'action **Marquer comme paye**.
- **Effet du marquage paye** : cette action ne se contente plus de changer un
  badge. Elle reutilise le meme pipeline que Stripe/PayPal :
  - facture BPCE marquee payee ;
  - document local `paid` ;
  - e-mail de confirmation si configure ;
  - activation / provisioning automatique si le document porte des
    souscriptions facturees ou un panier provisionnable.
- **Idempotence** : un document deja `paid` ne doit pas etre traite une
  seconde fois.

Verification rapide apres un paiement :

- statut document `paid` ;
- `payment_method` correct ;
- facture BPCE disponible ;
- si document recurrent : souscription sortie de `pending_payment` ;
- si provisioning attendu : nouvel etat visible dans la fiche abonnement.

---

## 2. Abonnements - `/admin/subscriptions`

Menu **Pilotage > Abonnements**.

Le portail gere maintenant trois rails d'abonnement :

- `paypal`
- `stripe`
- `billing`

### Statuts a connaitre

- `pending_approval` : tunnel historique avant activation PSP ;
- `pending_payment` : souscription facturee creee localement mais encore non
  reglee ;
- `pending_activation` : paiement recu, activation/provisioning en cours ;
- `active` : souscription en service ;
- `suspended` : souscription suspendue, typiquement apres impaye ;
- `pending_cancellation` : resiliation demandee a fin de terme ;
- `cancelled` / `expired` : fin de vie.

### Cas des abonnements factures (`rail='billing'`)

- La creation depuis `/panier` commence en `pending_payment`.
- Le premier paiement peut venir de Stripe, PayPal ou d'un virement marque
  paye par l'admin.
- Une fois le document regle, la souscription passe automatiquement vers
  `pending_activation` puis `active`.
- Les dates `started_at`, `next_billing_at` et `commitment_ends_at` sont
  calculees au moment du paiement reel.
- La fiche abonnement conserve un resume AD, mais les actions de
  provisioning detaillees sont desormais regroupees sur une page dediee
  "Active Directory" de la souscription.
- Depuis cette page dediee, l'admin peut relancer le provisioning sur tout
  le client, sur une selection d'utilisateurs lies, ou sur un seul
  utilisateur.

### Renouvellements

- Un worker periodique emet les factures suivantes pour les souscriptions
  `billing` arrivees a echeance.
- Si une facture ouverte reste impayee trop longtemps, la souscription peut
  passer `suspended`.
- Si un paiement arrive ensuite, le portail peut la reactiver et relancer le
  provisioning attendu.

### Annulation

- Les abonnements Stripe / PayPal conservent leur flux d'annulation cote PSP.
- Les abonnements `billing` restent geres localement selon les memes etats
  (`pending_cancellation`, `cancelled`, fin de terme).

---

## 3. Contenus administrables - `/admin/content`

Menu **Activite commerciale > Contenus**.

- **Perimetre** : CGV, mentions legales, page `A propos` et fiches
  techniques des packs publics.
- **Liste** : type, titre, URL publique, version visible et date de mise
  a jour.
- **Edition** : ouvrir une ligne pour modifier le `Markdown` et le
  `libelle de version`. Le titre, le type et l'URL publique ne sont pas
  editables.
- **Apercu** : l'editeur affiche un rendu Markdown immediat avec le meme
  composant que le site public.
- **Publication** : la sauvegarde est persistante cote API/MariaDB ;
  aucun redeploiement du site n'est necessaire.
- **Liens publics** : l'action **Voir la page publique** s'ouvre dans un
  nouvel onglet pour garder le contexte admin.

Pour les packs :

- `/admin/public-pack-catalog` reste la surface de pilotage de la page
  comparative `/offres` ;
- chaque pack y propose aussi un lien **Modifier la fiche technique**
  vers `/admin/content/pack-sheet:...`.

---

## 4. Telechargements - `/admin/downloads`

Menu **Activite commerciale > Telechargements**.

- **Perimetre** : logiciels, scripts, fichiers RDP, documentation et outils
  complementaires visibles depuis `/downloads` cote client.
- **Liste** : chaque ressource avec categorie, type, source (`fichier
  interne` ou `lien externe`), statut et date de mise a jour.
- **Creation** : choisir une categorie, un titre clair, une description
  courte, un type de ressource et un mode de visibilite.
- **Fichier interne** : enregistrer d'abord la fiche, puis utiliser l'upload
  dedie. Le fichier est stocke cote API sur un chemin prive, jamais dans
  `public/`.
- **Lien externe** : renseigner uniquement une URL absolue officielle.
- **Visibilite** : soit `Tous les clients`, soit des regles ciblees basees sur
  les packs publics, les offres du catalogue ou les `service_type` actifs.
- **Activation** : une ressource ne peut pas etre activee sans fichier prive
  si elle est interne, ni sans URL valide si elle est externe, ni sans regle
  si elle est ciblee.
- **Suppression** : la desactivation se fait depuis la liste ; la suppression
  definitive d'une ressource se fait depuis sa fiche avec confirmation.

Gestion des categories via `/admin/downloads/categories` :

- ajuster le titre, la description, le statut et l'ordre ;
- conserver des categories clients simples et rassurantes ;
- une categorie encore utilisee par une ressource ne peut pas etre supprimee.

Verification rapide quand un client ne voit pas un telechargement :

- categorie `active` ;
- ressource `active` ;
- fichier interne present ou URL externe valide ;
- au moins une regle de visibilite qui correspond a un abonnement ou service
  `active` du client.

---

## 4 bis. Portail solutions - `/admin/solutions`

Menu **Activite commerciale > Portail solutions**. Documentation dediee :
[`v1.1/V1.1_PORTAIL_SOLUTIONS.md`](v1.1/V1.1_PORTAIL_SOLUTIONS.md).

- **Perimetre** : les tuiles de la page publique `/solutions`, consultable
  sans compte depuis le site vitrine.
- **En-tete de page** : surtitre, titre, introduction et note de bas de page
  se modifient directement en haut de l'ecran.
- **Creation** : nom affiche, lien du service (URL absolue `https`), phrase
  courte optionnelle, ordre d'affichage et logo.
- **Logo** : PNG, JPEG, WebP ou SVG, 512 Ko maximum. Il est stocke en base et
  couvert par la sauvegarde MariaDB. Sans logo, la tuile affiche les
  initiales du nom.
- **Etat** : `Brouillon` masque totalement la tuile (logo compris) ;
  `Publiee` la rend visible. Creer en brouillon, verifier, puis publier.
- **Suppression** : depuis la fiche de la solution, avec confirmation. Pour
  masquer sans perdre la fiche, repasser en brouillon.

Verification rapide quand une tuile n'apparait pas sur `/solutions` :

- solution en etat `Publiee` ;
- lien renseigne en `http`/`https` absolu ;
- ordre d'affichage coherent avec les autres tuiles.

---

## 5. Journal e-mails - `/admin/email-log`

Menu **Relation client > Journal e-mails**.

- **Contenu** : chaque e-mail avec `template`, destinataire, statut,
  `correlation_id`, date de creation et d'envoi.
- **Templates a surveiller sur ce chantier** :
  - `invoice_issued`
  - `payment_confirmed`
  - les templates signup si le client vient d'un parcours self-service
- **Correlation** : le `correlation_id` relie l'e-mail, le paiement et
  l'action admin ou client correspondante.
- **Mode mock** : le corps est visible dans le journal pour verification sans
  envoi reel.

---

## 6. Demandes d'inscription - `/admin/signups`

Menu **Relation client > Demandes d'inscription**.

- liste filtrable par statut ;
- detail d'une demande ;
- approbation apres verification e-mail ;
- refus avec motif facultatif ;
- audit de chaque etape.

Le detail fonctionnel reste documente dans
[`V0.26_USER_GUIDE_SIGNUP.md`](V0.26_USER_GUIDE_SIGNUP.md).

Important : meme depuis la creation du domaine enfant
`clients.home.bzh` (2026-07-18), ce workflow reste le workflow V0.26
actuel : il cree des comptes portail, pas encore des comptes AD
automatiques. L'alignement des donnees est documente dans
[`v0.38/V0.38_SITE_AD_ALIGNMENT.md`](v0.38/V0.38_SITE_AD_ALIGNMENT.md).

---

## 7. Active Directory - `/admin/customers/[ref]/ad`

Menu **Relation client > Clients** puis fiche client.

- lecture des groupes effectifs ;
- renommage utilisateur ;
- deplacement `Users <-> Disabled` ;
- move cross-client borne a l'OU configuree ;
- changement de mot de passe AD via `/password` si active.

Le detail du cadrage AD reste documente dans
[`V0.25_AD_FINALISATION.md`](V0.25_AD_FINALISATION.md) et
[`AD_PRODUCTION_MIGRATION.md`](AD_PRODUCTION_MIGRATION.md).

Pour la cible `clients.home.bzh` et le futur alignement signup/site/AD,
voir aussi :

- [`v0.38/V0.38_SITE_AD_ALIGNMENT.md`](v0.38/V0.38_SITE_AD_ALIGNMENT.md)
- [`v0.38/V0.38_KOXO_SIGNUP_INTEGRATION.md`](v0.38/V0.38_KOXO_SIGNUP_INTEGRATION.md)

---

## 7 bis. Centre de configuration - `/admin/settings`

Les parametres metier centralises se modifient sans deploiement. Chaque
parametre affiche sa classe :

- **Dynamique** : la valeur prend effet des l'enregistrement ;
- **Redemarrage requis** : lecture seule, la valeur vient du demarrage ;
- **Secret** : jamais affiche, seulement « Configure » ou « Non configure » ;
- **Invariant code** : non modifiable par principe.

Si un message « ce parametre a ete modifie ailleurs » apparait, un autre
administrateur a enregistre entre-temps : recharger la page avant de
recommencer, sinon le travail de l'autre serait ecrase.

Un parametre marque **Risque high** demande une confirmation avant
enregistrement : il change le comportement du service en production
immediatement, sans redeploiement.

Un parametre verrouille par le code s'affiche en lecture seule, avec sa valeur
appliquee. Il reste visible volontairement : son etat doit pouvoir etre
constate, meme s'il ne se modifie pas ici.

### Inscriptions

- **Inscriptions ouvertes** ferme le parcours cote serveur. Ce n'est pas un
  simple masquage d'interface : une soumission directe est refusee par
  API-INTERNAL.
- **Limite IP par heure** est comptee en base. Un depassement donne un refus
  explicite au visiteur.
- **Limite e-mail par 24 h** est comptee de la meme facon, mais le depassement
  renvoie une reponse identique a un succes : c'est volontaire, l'API ne doit
  pas reveler qu'une adresse est deja connue. Une demande apparemment acceptee
  peut donc ne rien avoir enregistre.
- **Durees des liens** bornent la validite des e-mails de verification et de
  definition de mot de passe. Les liens deja envoyes conservent leur echeance.
- **Approbation automatique** est verrouillee a « desactivee ». Toute demande
  reste soumise a une revue humaine dans `/admin/signups`.

### Securite

Duree de session, nombre d'echecs de connexion autorises et duree de
verrouillage prennent effet immediatement, dans les bornes du registre. Les
sessions deja ouvertes gardent l'echeance posee a leur creation.

Les proprietes de securite des cookies ne sont pas modifiables ici : elles
restent pilotees par la configuration de deploiement.

## 7 ter. Messages et communications - `/admin/settings/messages`

Trois familles de textes, chacune avec son historique et sa restauration.

**E-mails transactionnels.** Objet et corps en texte brut. Les variables
autorisees sont listees sous chaque modele et s'inserent d'un clic ; toute
autre variable fait echouer l'enregistrement, volontairement. Le bouton
**Apercu** rend le texte avec des valeurs d'exemple, sans rien envoyer.
L'**envoi de test** n'accepte que votre propre adresse et n'existe que pour
les modeles independants d'un document commercial reel.

Mettre un modele sur **Inactif** ne casse rien : le texte integre au code
reprend la main. C'est le repli a utiliser si une modification s'avere
mauvaise et que l'historique n'est pas a portee de main.

**Notifications du portail.** Titre et message affiches au client lors d'un
changement de statut ou d'une reponse publique. Les identifiants de
notification et les codes de statut restent des invariants du code : seuls
les textes changent.

**Textes systeme.** Textes courts reellement operationnels : confirmation du
formulaire de contact, note de confidentialite, message de fermeture
temporaire, mention commerciale. Ce n'est pas un second CMS : les pages
editoriales restent dans `/admin/content` et le wiki.

Dans les trois cas, **Restaurer le texte par defaut** reecrit la valeur du
code comme nouvelle version ; rien n'est supprime et l'historique reste
consultable.

## 7 quater. Diagnostic - `/admin/settings/diagnostic`

Le parcours public `/diagnostic` est entierement administrable : contextes,
libelles, questions, options, conditions d'affichage, textes de resultat et
correspondance vers les besoins Billing V2.

Deux etats coexistent :

- **Brouillon** : ce que vous editez. Il n'est jamais visible du public.
- **Publie** : ce que voient les visiteurs.

Marche a suivre :

1. choisir le contexte a modifier dans la liste ;
2. modifier presentation, questions et textes de resultat ;
3. verifier le bandeau d'erreurs : tant qu'il est present, l'enregistrement
   est refuse ;
4. utiliser l'onglet **Simulateur** pour repondre comme un visiteur et lire le
   resultat, la selection Billing V2 et les identifiants des regles
   appliquees ;
5. **Enregistrer le brouillon** ;
6. **Publier** apres confirmation : le parcours public bascule d'un seul coup
   sur la nouvelle version.

Points de vigilance :

- la derniere regle de resultat d'un contexte doit rester sans condition ;
  elle garantit qu'un visiteur obtient toujours une reponse ;
- un contexte « eligible a une formule » sans correspondance Billing V2 sort
  systematiquement en cadrage/devis ;
- le diagnostic ne calcule aucun prix : la tarification reste l'affaire de
  Billing V2 ;
- sans version publiee, le site utilise la configuration integree au code : ce
  n'est pas une panne ;
- le bouton « Repartir de la configuration du code » ne modifie que le
  brouillon en cours d'edition ; il faut ensuite enregistrer puis publier ;
- les mutations exigent la permission `settings.diagnostic.write` ;
- l'onglet **Historique** liste les enregistrements et publications avec leur
  reference de correlation.

## 8. Diagnostic rapide

Quand un client remonte un probleme de panier, paiement, abonnement ou
telechargement :

1. Recuperer la **reference de correlation** affichee.
2. Verifier le document commercial et son `payment_method`.
3. Verifier la souscription liee s'il s'agit d'un abonnement facture.
4. Verifier le journal e-mails pour `invoice_issued` et `payment_confirmed`.
5. Si le paiement etait un virement, confirmer que l'action **Marquer comme
   paye** a bien ete faite sur le bon document.
6. Pour un telechargement, verifier aussi la fiche ressource et la regle de
   visibilite attendue.
