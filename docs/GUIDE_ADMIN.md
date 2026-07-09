# Guide administrateur - portail interne

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

## 4. Journal e-mails - `/admin/email-log`

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

## 5. Demandes d'inscription - `/admin/signups`

Menu **Relation client > Demandes d'inscription**.

- liste filtrable par statut ;
- detail d'une demande ;
- approbation apres verification e-mail ;
- refus avec motif facultatif ;
- audit de chaque etape.

Le detail fonctionnel reste documente dans
[`V0.26_USER_GUIDE_SIGNUP.md`](V0.26_USER_GUIDE_SIGNUP.md).

---

## 6. Active Directory - `/admin/customers/[ref]/ad`

Menu **Relation client > Clients** puis fiche client.

- lecture des groupes effectifs ;
- renommage utilisateur ;
- deplacement `Users <-> Disabled` ;
- move cross-client borne a l'OU configuree ;
- changement de mot de passe AD via `/password` si active.

Le detail du cadrage AD reste documente dans
[`V0.25_AD_FINALISATION.md`](V0.25_AD_FINALISATION.md) et
[`AD_PRODUCTION_MIGRATION.md`](AD_PRODUCTION_MIGRATION.md).

---

## 7. Diagnostic rapide

Quand un client remonte un probleme de panier, paiement ou abonnement :

1. Recuperer la **reference de correlation** affichee.
2. Verifier le document commercial et son `payment_method`.
3. Verifier la souscription liee s'il s'agit d'un abonnement facture.
4. Verifier le journal e-mails pour `invoice_issued` et `payment_confirmed`.
5. Si le paiement etait un virement, confirmer que l'action **Marquer comme
   paye** a bien ete faite sur le bon document.
