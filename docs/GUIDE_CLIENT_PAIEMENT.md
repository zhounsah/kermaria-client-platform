# Guide client — payer une facture depuis le portail

Guide court à destination des clients pour régler un document de
facturation depuis l'espace portail. (DU-4 de V0.24.)

## Où trouver un document à payer

1. Se connecter sur le portail client (`/login`).
2. Menu **Factures** (ou **Documents**) : la liste montre vos documents
   commerciaux et factures avec leur statut (`en attente`, `payé`…).
3. Ouvrir un document **partagé** et **non réglé** : un bouton **« Payer »**
   apparaît. Les brouillons internes ou déjà payés n'affichent pas de
   bouton.

## Choisir le moyen de paiement

Si les deux moyens sont proposés, un choix **Stripe / PayPal** s'affiche
(Stripe sélectionné par défaut) :

- **Carte bancaire (Stripe)** : vous êtes redirigé vers une page de
  paiement sécurisée Stripe. Saisissez votre carte et validez.
- **PayPal** : vous êtes redirigé vers la connexion PayPal pour approuver
  le paiement.

Si un seul moyen est configuré, le paiement démarre directement dessus.

## Après le paiement

1. Vous êtes **automatiquement redirigé** vers le portail sur une page de
   confirmation (« paiement réussi »).
2. Le document passe au statut **`payé`** et le moyen de paiement est
   enregistré.
3. Une **facture** est émise : son numéro et son **PDF** deviennent
   disponibles sur la fiche du document (bouton de téléchargement).
4. Un e-mail de confirmation de paiement vous est adressé.

## En cas de problème

- **Paiement annulé** (vous fermez la page Stripe/PayPal) : le document
  reste `en attente`, vous pouvez recommencer.
- **Retour sans confirmation** : rechargez la fiche du document ; le statut
  se met à jour dès réception de la confirmation du prestataire (quelques
  secondes). En cas de doute, contactez le support via `/contact` — ne
  relancez pas plusieurs paiements pour le même document.
- Le paiement est **idempotent** côté serveur : un même règlement n'est
  jamais compté deux fois.

## Sécurité

- Vos informations de carte ne transitent **jamais** par le portail : elles
  sont saisies directement chez Stripe/PayPal.
- Le portail ne stocke aucune donnée de carte.
