# Reprise d'une inscription dont l'e-mail n'a pas été reçu

## Incident DEV du 2026-10-03

La demande fictive Noé Valbrume (`6dc55aab-14f7-4d64-91ef-f94bf140d41d`)
reste `email_pending`. L'API a enregistré une annulation de l'envoi SMTP à
23:21:42, sans entrée d'envoi réussi dans le journal e-mail. La résolution
de `ssl0.ovh.net` a ensuite expiré lors d'un diagnostic, puis une nouvelle
résolution et une connexion TCP 587 ont réussi. Cela établit un incident
transitoire observé, pas une preuve de livraison du message initial.

Soumettre à nouveau le formulaire public renvoie volontairement une réponse
non révélatrice pour une adresse déjà connue, sans renvoyer l'e-mail d'une
inscription standard. Il manquait une action de reprise administrateur.

## Correctif

Sur une demande `email_pending`, l'administration propose **Renvoyer l'e-mail
de confirmation**. Le navigateur passe par le BFF protégé par session/CSRF,
puis par `POST /internal/admin/signups/{id}/resend-verification-email`, réservé
aux administrateurs internes et audité (`signup.verification_email_resent`).

- Le destinataire est celui de la demande ; aucune adresse ou URL ne vient
  du corps de la requête.
- Nouveau jeton aléatoire, condensat seul conservé, durée de validité issue
  de la configuration d'inscription existante.
- Rotation conditionnelle atomique limitée à `email_pending`, sans preuve
  e-mail déjà enregistrée, avec au moins une minute depuis la dernière mise
  à jour. Une concurrence de renvois n'élit qu'un envoi.
- L'ancien lien est invalidé. La confirmation contrôle encore son condensat
  et son expiration lors de l'écriture, pour refuser une lecture devenue
  périmée pendant une rotation.
- Aucun compte, mot de passe ni objet AD n'est créé par cette action.
- Échec de livraison remonté explicitement ; nouvelle tentative possible
  après le délai. Aucun jeton n'est renvoyé au navigateur ni à l'audit.
- Pas de migration, pas de DDL et pas d'écriture directe en base par l'agent.

## Validation et preuve attendue

Tests locaux : rotation, refus sur état incompatible ou identifiant absent,
délai entre tentatives, invalidation de l'ancien lien, confirmation du nouveau,
échec SMTP simulé, refus d'une confirmation lue avant rotation et concurrence
des renvois. Ces tests utilisent le repository mock ; ils ne constituent
pas une preuve de verrouillage MariaDB réel.

La preuve DEV finale doit passer par le bouton admin, un événement d'envoi
réussi, la réception/confirmation par l'utilisateur, puis la relecture de
`email_verified` avant approbation. Ne jamais substituer une modification
manuelle de statut ou l'extraction d'un lien depuis les journaux.

## Livraison et renvoi vérifiés

- Tests du renvoi et de l'amorçage AD : PASS ; suite complète smoke API : PASS.
- Contrat signup : 62 contrôles PASS ; types shared/web : PASS ; lint web :
  aucune erreur (un avertissement préexistant dans profile/subscriptions).
- Build web DEV réussi après un premier échec de téléchargement des polices
  Google lors des perturbations réseau. API publiée avec apphost win-x64.
- API DEV mise à jour via l'installateur qui préserve le JSON ; readiness 200.
- Web DEV : `/opt/kermaria/releases-dev/webportal-dev-signup-resend-20261003`,
  readiness privée healthy et nouvelle action visible sur le site DEV.
- Configurations DEV/PROD, DLL API PROD et lien de release Web PROD contrôlés
  inchangés. Aucun commit, tag ou push. Aucune migration.
- Artefacts et preuves hors Git :
  `C:\Users\zhounsah\Backups\Kermaria\signup-resend-dev-20261003`.
  Archive web SHA-256 :
  `C5CF2870AA3A8BC19B926409694D38978BE69A5D63486EFA05AAC9FC7475202E`.
- Bouton utilisé depuis l'administration DEV pour la demande existante.
  Retour « Un nouveau lien de confirmation a été envoyé » et journal e-mail
  `signup_verification`, **sent**, le 3 octobre à **23:43**, corrélation
  `d3b232ee-8715-44ce-b05e-7cb8df0b639c`.

La réception et l'ouverture du lien restent à la main de l'utilisateur.
L'état `sent` prouve la remise SMTP, pas la confirmation de possession de
l'adresse. Demande encore non approuvée au moment de cette preuve.

### Confirmation et approbation

L'utilisateur a confirmé l'adresse ; statut `email_verified` relu dans
l'administration, puis approbation exécutée par l'action normale.
Malgré une erreur affichée pendant l'envoi SMTP, la création a été validée :
statut **Approuvée**, client **DEV-CLI-723NSN**, le 3 octobre à **23:46**,
mot de passe non défini. Ne pas rejouer la création.

L'envoi `account_approved` puis son renvoi ont dépassé le délai SMTP
(`SmtpException`, operation timed out). Les délais actuels de transport sont
10 s côté appel interne et 15 s côté navigateur ; aucun allongement global
ni modification du réseau n'a été appliqué. Le journal ne prouve pas la
livraison du lien de mot de passe.

Relais demandé au titulaire sur l'action existante **Initialisation par mes
soins** de la fiche admin : il saisit, confirme et soumet lui-même le mot de
passe. Ce parcours prévu par le produit finalise l'identité KoXo/AD et évite
de dépendre d'un autre envoi SMTP ; l'agent ne lit ni ne saisit le secret.
