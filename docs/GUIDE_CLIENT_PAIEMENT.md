# Guide client - payer une facture depuis le portail

Guide court a destination des clients pour regler un document de
facturation depuis l'espace portail.

## Ou trouver un document a payer

1. Se connecter sur le portail client.
2. Ouvrir le menu **Documents & factures** ou terminer son parcours depuis
   `/panier`.
3. Ouvrir un document partage et non regle.

Un document deja paye n'affiche plus de bouton de reglement.

## Choisir le mode de reglement

Selon la configuration du portail, la page document peut proposer :

- **Carte bancaire (Stripe)**
- **PayPal**
- **Virement bancaire**

Si plusieurs moyens sont disponibles, le client choisit explicitement celui
qu'il souhaite utiliser avant de continuer.

### Stripe

- Le client est redirige vers une page de paiement securisee Stripe.
- Une fois le paiement confirme, il revient automatiquement sur le portail.

### PayPal

- Le client est redirige vers PayPal pour approuver le paiement.
- Une fois le paiement confirme, il revient automatiquement sur le portail.

### Virement bancaire

- Le client choisit **Virement bancaire** sur la page document.
- Le portail enregistre ce choix et affiche les coordonnees de paiement :
  beneficiaire, IBAN, BIC / SWIFT et reference a indiquer.
- La commande est alors **validee mais non encore reglee**.
- Le document reste en attente tant que le virement n'a pas ete recu.

Le virement bancaire n'active donc pas immediatement les services : il signale
simplement que le client a choisi ce mode de reglement.

## Ce qui se passe apres le paiement

Quand le paiement est effectivement confirme :

- le document passe au statut **paye** ;
- le moyen de paiement retenu est conserve sur le document ;
- la facture PDF reste telechargeable depuis la meme page ;
- un e-mail de confirmation peut etre envoye selon la configuration ;
- les effets metier attendus se declenchent automatiquement.

Exemples :

- un achat ponctuel issu du panier peut declencher le provisioning prevu pour
  cette offre, si un mapping existe ;
- une souscription facturee reste d'abord `pending_payment`, puis passe
  automatiquement `active` apres encaissement du premier terme.

## Cas particulier des abonnements factures

Pour un abonnement ajoute depuis `/souscrire` puis confirme depuis `/panier` :

- le portail cree d'abord une facture initiale ;
- le client choisit ensuite Stripe, PayPal ou virement sur la page document ;
- tant que le paiement n'est pas recu, la souscription reste en attente ;
- apres encaissement, le portail active automatiquement la souscription et
  prepare les echeances suivantes.

## En cas de probleme

- **Paiement annule** : le document reste a regler, le client peut
  recommencer plus tard.
- **Retour sans confirmation visible** : recharger la page document et
  verifier le statut.
- **Virement deja envoye** : si le document n'est pas encore paye, il faut
  attendre la confirmation administrative de reception.
- **Erreur affichee** : la page montre une reference de correlation quand
  elle est disponible ; elle permet d'identifier rapidement la tentative.

## Securite

- Les donnees de carte bancaire ne transitent jamais par le portail.
- Le portail ne stocke aucune donnee de carte.
- Les paiements en ligne se font chez Stripe ou PayPal.
