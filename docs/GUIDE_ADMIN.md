# Guide administrateur — portail interne

Guides courts d'exploitation admin livrés en V0.24 (DU-5 à DU-9). Toutes
les sections sont accessibles depuis le menu **Administration** du portail
interne (rôle `internal_admin`).

Rappel de cadrage : le portail émet des **documents commerciaux
informatifs** et des **factures via BPCE** ; il ne réalise aucun
provisioning technique automatique. Les modes d'intégration (BPCE, e-mail,
Stripe, PayPal, AD) sont pilotés par configuration.

---

## 1. Paiements — `/admin/payments` (DU-5)

Menu **Pilotage > Paiements** (ou **Activité commerciale**).

- **Liste** : chaque règlement avec client, document lié, montant, moyen
  (`stripe` / `paypal` / manuel), statut et date (heure **Europe/Paris**).
- **Filtres** : par statut (`en attente`, `payé`…) et/ou par période pour
  retrouver un paiement.
- **Marquer payé (règlement manuel)** : pour un paiement reçu hors ligne
  (virement, espèces), ouvrir le document et utiliser l'action **« Marquer
  comme payé »**. Cela passe le document en `payé`, déclenche l'émission de
  la facture BPCE (selon le mode) et journalise l'action dans l'audit.
- **Idempotence** : un document déjà `payé` ne peut pas être re-réglé ; les
  webhooks Stripe/PayPal rejoués n'entraînent aucun double paiement ni
  double facture.

À vérifier après un paiement : statut `payé`, `payment_method` correct,
facture BPCE émise (numéro fiscal), et e-mail `payment_confirmed` dans le
journal e-mails (section 3).

---

## 2. Abonnements — `/admin/subscriptions` (DU-6)

Menu **Pilotage > Abonnements**.

- **Liste** : abonnements avec client, offre, moyen (PayPal/Stripe),
  identifiant externe (`I-…` PayPal / `sub_…` Stripe), statut
  (`pending_approval`/`pending_activation`, `active`, `cancelled`…) et
  prochaine échéance.
- **MRR** : un indicateur de revenu mensuel récurrent agrège les
  abonnements `active`. Il se met à jour quand un abonnement s'active ou
  s'annule.
- **Annuler un abonnement** : action **« Annuler »** sur la fiche. Elle
  demande l'annulation côté prestataire (PayPal `CANCELLED` / Stripe) et
  passe l'abonnement local en `cancelled` (`cancelled_at` renseigné).
  L'action est tracée en audit `subscription.admin_cancel`. Un webhook
  `subscription.cancelled` peut ensuite confirmer la bascule côté
  prestataire.
- **Cycle de vie** : `pending_activation` → `active` sur réception du
  premier paiement (webhook `PAYMENT.SALE.COMPLETED` PayPal /
  `invoice.paid` Stripe) ; chaque échéance émet une facture BPCE + un
  e-mail de confirmation.

---

## 3. Journal e-mails — `/admin/email-log` (DU-7)

Menu **Relation client > Journal e-mails**.

- **Contenu** : chaque e-mail avec `template`
  (`signup_verification`, `account_approved`, `payment_confirmed`,
  `contact_form`…), destinataire, statut, `correlation_id`, date de
  création et d'envoi.
- **Statuts** :
  - `sent` — envoyé réellement (mode `live`), `sent_at` renseigné ;
  - `mock_sent` — simulé (mode `mock`), rien n'est parti sur le réseau ;
  - `blocked_allowlist` — destinataire hors de l'allowlist en mode `live`
    (`sent_at` NULL, aucun trafic SMTP) ;
  - `smtp_error` — échec de transport SMTP (à diagnostiquer côté serveur
    e-mail).
- **Corrélation** : le `correlation_id` permet de relier un e-mail à
  l'action qui l'a déclenché (audit, requête). Utile pour tracer un
  parcours de bout en bout (ex. « pourquoi ce client n'a pas reçu son
  lien »).
- **Mode mock et liens** : en `mock`, le **corps** de l'e-mail est consigné
  ici — pratique pour récupérer un lien de vérification ou de définition de
  mot de passe sans envoi réel.

---

## 4. Demandes d'inscription — `/admin/signups` (DU-8)

Menu **Relation client > Demandes d'inscription**. Disponible quand le
self-service est activé (`SIGNUP_ENABLED=true`). Détail complet dans
[V0.26_USER_GUIDE_SIGNUP.md](V0.26_USER_GUIDE_SIGNUP.md).

- **Liste filtrable** par statut : en attente e-mail, vérifiées, approuvées,
  refusées.
- **Ouvrir une demande** (`/admin/signups/[id]`) : informations
  transmises, adresse IP source, état.
- **Approuver** (uniquement **après vérification e-mail** du visiteur) :
  crée le compte client (`customer` + `portal_user` actif **sans mot de
  passe**) et envoie le lien one-shot de définition de mot de passe.
  **Aucun compte Active Directory n'est créé** — c'est un acte distinct
  (section 5).
- **Refuser** : saisir un motif facultatif (transmis par e-mail) et
  confirmer.
- Chaque étape est auditée (`signup.approved`, `signup.rejected`…).

---

## 5. Active Directory — `/admin/customers/[ref]/ad` (DU-9)

Menu **Relation client > Clients** → fiche client → onglet/section
**Active Directory**. Opérations en écriture contrôlée (`controlled_write`),
**bornées à l'OU de test/site web** ; jamais d'écriture hors périmètre.
Cadrage : [V0.25_AD_FINALISATION.md](V0.25_AD_FINALISATION.md).

- **Lire les groupes effectifs** : groupes directs **et** transitifs de
  l'utilisateur (résolution via `LDAP_MATCHING_RULE_IN_CHAIN`).
- **Renommer un utilisateur** : met à jour `CN`, `sAMAccountName`,
  `displayName` et `UPN` de façon cohérente ; `customer_ad_links` est mis à
  jour. Un `sAMAccountName` déjà pris renvoie `AD_OBJECT_ALREADY_EXISTS`
  (409).
- **Déplacer un utilisateur** : entre `Users` et `Disabled` du même client,
  ou **cross-client** (avec confirmation) — `customer_ad_links` est migré.
  Une référence client inexistante renvoie 404 `PORTAL_DATA_NOT_FOUND`.
- **Changer le mot de passe AD d'un client** : via l'action `/password`,
  disponible **uniquement** si `AD_PASSWORD_CHANGE_ENABLED=true`. Aucun mot
  de passe n'apparaît en clair dans les logs ni l'audit. Rate limit : au
  bout de 3 échecs, verrouillage 15 min sur l'utilisateur.
- **Sortie d'OU en production** : procédure documentée séparément dans
  [AD_PRODUCTION_MIGRATION.md](AD_PRODUCTION_MIGRATION.md).

Toutes les mutations AD sont tracées dans le journal d'audit
(`ad.rename`, `ad.move`, `ad.password_change`…).
