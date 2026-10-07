---
name: site-ux-cms-data-requests
description: "Refonte de l'accueil, constructeur de pages, demandes de données et approbation automatique après e-mail."
---

# Vitrine, constructeur et demandes de données (2026-10-05)

**État courant : `v2.1.0` déployée en PROD le 2026-10-07 depuis le tag
`09737e2`.** La note `docs/releases/V2.1.0.md` contient les empreintes,
la sauvegarde, les migrations 098–101, les contrôles et le retour arrière.
Le correctif Web `v2.1.0-hotfix.1` (`0f7d652`) remplace ensuite les trois
libellés génériques des capacités VPS par `vCPU`, `Go de RAM` et `Go SSD` sans
changer les valeurs ni la version affichée ; voir
`docs/releases/V2.1.0_HOTFIX_1.md`.
Les paragraphes ci-dessous retracent la préparation et la recette DEV avant
cette livraison ; leurs constats « PROD inchangée » sont historiques.

Recette DEV complémentaire du 2026-10-07 : les comptes fictifs admin et client
se connectent via le BFF. L'admin a publié puis restauré la mise en page
`/admin/catalog` sur MariaDB DEV (versions 0→1→2) ; retirer son éditeur
obligatoire rend 400. Une demande de données fictive est lisible avec son
document par son auteur, mais pas sans session (401) ni sous la session admin
via le chemin client (403) ; sa notification et son échéance à un mois sont
présentes. Un POST sans session/CSRF ou avec un détail invalide rend 401/403/400
sans créer de demande. Le mode d'inscription réel reste manuel et sa page
adapte les champs particuliers sur mobile. La boîte connectée n'appartient
pas à l'allowlist `home.bzh` de l'API DEV ; la livraison d'e-mails et
l'isolation avec un second client attendent des accès de test distincts.
Le rendu visuel authentifié de l'éditeur attend une session navigateur active.

Le 2026-10-07, le propriétaire a autorisé l'usage de ses boîtes Gmail et
Outlook. Le modèle de vérification non personnalisé disait encore que l'équipe
examinerait toujours la demande ; le texte par défaut a été rendu neutre et
livré dans l'API DEV (DLL SHA-256
`F0A30DDD51283910700919BFA6B7D3B658450522821ACE16ADD5E3B4E2FCB216`).
Les deux adresses ont été ajoutées temporairement à l'allowlist DEV, sans
joker, puis le JSON initial a été restauré (SHA-256
`EDA93A0F9F9A8B29BE654D0EF7D8CD0A3A1E71F75DEB86FE53A82DD964E8C2AC`)
pendant l'attente d'un second accord hCaptcha. Le premier jeton de test a
expiré avant l'envoi : aucun e-mail ni compte Gmail/Outlook n'a été créé.
La confirmation hCaptcha est requise au moment de chaque nouvelle résolution ;
la définition du mot de passe d'un nouveau compte reste un geste utilisateur.

Suite de la recette du 2026-10-07 : l'adresse Gmail initiale appartenait déjà
à un client DEV ; l'envoi générique n'a créé ni nouvelle demande ni e-mail.
Avec les accords hCaptcha spécifiques, une inscription fictive Outlook a été
soumise, son e-mail vérifié, puis approuvée manuellement par le BFF admin. Le
titulaire a défini son mot de passe et la session client a affiché « Recette
Outlook ». Un alias Gmail fictif, distinct, a ensuite été inscrit ; le réglage
admin a été activé pour la vérification de cet e-mail, qui a approuvé le compte
et envoyé un seul message de validation. Le même lien relu après retour au
mode manuel n'a dupliqué ni la demande ni le message. Le titulaire a défini
le mot de passe et le tableau de bord DEV affiche « Recette GmailAlias ».
La liste d'envoi DEV a été restaurée à son empreinte initiale et
`signup_auto_approve=false` (version 2). Une demande fictive créée par Outlook
est invisible pour le premier client DEV, et l'alias Gmail voit une liste
vide et une page 404 sur la demande Outlook. L'échec partiel réel d'envoi
et l'inspection visuelle de l'éditeur admin restent hors de cette preuve.

Le propriétaire a fourni deux captures de « Mise en page » DEV. Une session
admin a ensuite confirmé l'aperçu de l'accueil et sa mise à jour quand le
titre du brouillon change, sans publication. Une photo trop présente gênait la
lecture dans la colonne étroite ; le CSS de l'aperçu a été corrigé et livré
sur SRV-12 dans
`webportal-dev-uxcms-preview-contrast-20261007-190125` (archive SHA-256
`1CA4E38DA3D3A096E460326EB7DADD6FDD7E7F3BE7B6FBEFC805962EA1A754C8`).
Le titre initial et l'état sans brouillon ont été restaurés. Le test smoke API
de l'échec d'envoi et du rejeu différé de l'approbation automatique passe ;
une panne SMTP réelle en DEV n'a pas été provoquée.

Préparation release du 2026-10-07 : `npm run validate` passe sur le worktree
avec l'environnement de validation normal. Un premier essai sous
`APP_ENV=Development` global avait échoué sur le smoke de base indisponible :
le garde DEV refusait correctement sa base fictive. L'installateur Web DEV
résout désormais le chemin du fichier de configuration après l'entrée du
script, évitant l'échec de son paramètre par défaut sous PowerShell 5.1.
Le propriétaire a autorisé le commit, le tag et le déploiement **après** la
validation de la revue métier ; cette condition et la fenêtre de livraison
restent ouvertes. La base PROD `kermaria` n'a pas été touchée et attend une
autorisation distincte sur son couple hôte/base.

Audit du constructeur : `GET /internal/admin/page-layout/revisions` et
`GET /internal/admin/site-media` omettaient le contrôle `content.publish`
appliqué au chargement de l'éditeur, à la publication, à la restauration et à
l'envoi d'une image. Constat **VALIDE** pour les administrateurs à droits
restreints. Les deux lectures sont alignées dans `Program.cs`, smoke tests
réussis, API DEV livrée (DLL SHA-256
`047BBC1F86223DF9B5A649CED12D18EBB0868E977516F6C02AFA6DE01D1D1C3A`).
Le compte admin autorisé charge encore l'historique et la médiathèque via le
BFF après cette livraison. Aucun compte restreint DEV n'a servi à vérifier le
refus en situation réelle. PROD inchangée.

- L'accueil public et le footer ont des blocs initiaux administrables via
  `/admin/page-builder`. Les autres pages gardent leur autorité existante dans
  le bloc `route_content`, obligatoire pour préserver les actions métier. Les
  mises en page sont versionnées ; une publication est immédiate après
  validation serveur, avec comparaison de version et restauration.
- La navigation client/admin se replie sous 1020 px pour laisser apparaître le
  contenu dès le premier écran mobile ; Échap referme le menu et rend le focus
  au bouton.
- Les sous-ensembles latins WOFF2 variables d'Inter et JetBrains Mono sont
  intégrés au dépôt avec leurs licences OFL. `next/font/local` remplace
  `next/font/google` après un échec reproductible du build réseau ; un build
  complet sans accès à Google Fonts a réussi.
- Les pages Services publiques simplifient les anciennes formulations connues
  par projection exacte, y compris les titres visibles et SEO ; un texte CMS
  personnalisé n'est pas remplacé. Les libellés de liens et de fil d'Ariane
  sont en langage courant, sans changer les slugs ni le catalogue Billing.
- Balayage mobile local de treize pages publiques : statuts 200, un H1 par
  page, aucun débordement ni erreur JavaScript. Les catégories Réseau et
  Hébergement, `/offres` et les quatre fiches d'offre n'affichent plus les
  sigles VPN/VPS/CMS/RDS historiques dans la lecture principale ; la projection
  ne remplace que les textes exacts connus. Les quatre prix d'offre restent
  11,90 / 15,80 / 36,70 / 48,50 euros par mois issus du catalogue mock.
- Les quinze fiches de service publiques ont ensuite été contrôlées sur
  mobile : statuts 200, aucun débordement, et plus de sigle VPN/VPS/CMS/RDS/
  NAS/DNS/WAF ni de vocabulaire d'exploitation ciblé dans le contenu principal
  ou la description SEO des valeurs historiques. La projection est exacte et
  préserve les textes CMS personnalisés ; elle ne prouve pas le contenu stocké
  en production.
- Les images du CMS sont publiques et stockées par API-INTERNAL (PNG/JPEG/WebP,
  5 Mo, description). Le BFF livre les octets ; les contenus Billing V2 restent
  l'autorité des prix et de la capacité à commander. Un montant chiffré saisi
  dans un bloc libre est refusé à la publication.
- `/formules` expose maintenant l'introduction, les offres Billing V2 et le
  conseil comme widgets séparés. Le catalogue est requis ; une réorganisation
  publiée puis restaurée a conservé les quatre prix affichés à l'identique en
  navigateur local.
- `/offres` expose le récit et ses cartes comme bloc de contenu éditable, avec
  validation des champs obligatoires ; l'accès à la configuration, la grille
  simple et le comparatif restent des widgets requis. Le navigateur local
  affiche les mêmes prix Billing V2 sur desktop et mobile, sans débordement.
- `/tarifs` expose séparément l'introduction, le catalogue, les explications,
  les questions, les liens associés et le contact. Le catalogue et le contact
  sont requis ; le rendu local mobile ne déborde pas.
- `/services` public expose six widgets issus du contenu géré existant ; le
  choix par besoin, les catégories et le contact sont requis. Le titre
  historique est rendu inclusif pour les particuliers et professionnels, sans
  remplacer un titre personnalisé. Son rendu initial a été contrôlé sur
  desktop et mobile.
- `/diagnostic` expose un bloc d'introduction éditable (titre, texte, bénéfices)
  et deux widgets requis pour le questionnaire et le résultat. Le bloc
  d'introduction est masqué à l'étape du résultat pour éviter deux H1 ; la
  recommandation et les tarifs restent calculés par les chemins métier. Un
  parcours navigateur mock de 14 questions a atteint le résultat sans erreur
  ni débordement mobile. La première étape affiche « Continuer » avant le
  choix du profil, plutôt que de promettre déjà le résultat.
- `/contact` expose l'introduction et les étapes suivantes comme blocs
  éditables ; le formulaire, le contexte de l'offre et le retour sont des
  widgets obligatoires. Le rendu local mobile a conservé le formulaire et les
  liens vers le diagnostic et les services, sans débordement.
- `/souscrire` expose séparément la présentation, les offres recommandées et
  le choix direct à la carte ; le serveur exige les deux chemins commerciaux
  avant toute publication du layout.
- `/dashboard` client et `/admin` exposent maintenant leurs ensembles comme
  widgets réordonnables. Les services/actions client et les accès détaillés
  admin sont requis. Les données restent chargées par les routes et gardent
  leur contrôle d'accès. Les deux rendus ont été vérifiés à 1440 et 390 px.
- Une page autrefois publiée avec `route_content` est projetée à la lecture
  vers les modules initiaux de sa route, en gardant l'ordre des blocs libres et
  les identifiants uniques. La restauration d'une ancienne révision passe par
  la même conversion ; un smoke test mock couvre lecture, publication et
  restauration. La conversion ne change pas le document stocké avant
  publication.
- Les cinq noms de services historiques connus sont reformulés seulement si
  le texte d'origine est exact ; un nom personnalisé est conservé. Les codes
  de suivi restent dépliables sur la fiche client. Les cinq fiches de service
  mock sont lisibles sans VPN/RDS visibles dans la lecture principale mobile.
- `/signup` expose la reprise contextuelle, le récapitulatif de l'offre et le
  formulaire comme widgets distincts. Le serveur exige le formulaire et la
  sélection, tout en gardant les prix et le contexte dans le flux API/BFF.
  Le formulaire adapte les titres au profil particulier/organisation, testé
  dans le navigateur local. L'ordre initial place le formulaire avant les
  étapes explicatives pour raccourcir l'accès à l'action sur mobile.
- Les quatre descriptions historiques de formules contenant des noms de
  paliers techniques sont présentées en langage d'usage, à égalité de prix et
  de composition. Une description administrée différente reste intacte.
- Les blocs de formulaire acceptent jusqu'à huit champs supplémentaires bornés.
  Les actions restent `contact` et `data_request` ; aucun endpoint libre n'est
  choisi par l'éditeur. L'envoi contact a été exercé en local uniquement avec
  `EMAIL_INTEGRATION_MODE=mock` et un destinataire fictif. Le mode `disabled`
  renvoie une erreur contrôlée 502, conformément à la configuration.
- Les demandes de données sont personnelles à `portal_users.id`, même dans un
  client partagé. La réponse texte et le fichier facultatif restent sous
  `/profile/donnees`; `portal_notifications.user_id` limite leur notification
  au demandeur. La même portée est reproduite dans le mock et vérifiée dans les
  smoke tests ; une notification a été affichée dans l'espace client local.
  Le formulaire couvre les six catégories de droits. Une prolongation motivée
  de deux mois est possible une seule fois avant l'échéance initiale et met à
  jour la date suivie par le client.
- `signup_auto_approve` est activable par l'administrateur mais reste `false`
  par défaut. Seule la vérification e-mail standard déclenche l'approbation
  automatique. `auto_approval_requested` est figé à la vérification et permet
  de reprendre le même lien sans approuver rétroactivement une demande vérifiée
  en mode manuel.
- Un échec d'envoi après approbation automatique garde
  `approval_email_pending=1`. Le même lien de vérification reprend l'envoi
  après un délai borné sans recréer le compte ; le succès efface l'état en
  attente. Le mock teste échec, attente, rejeu et succès. Le renvoi après
  échec SMTP n'a pas été exercé sur MariaDB DEV.
- Migrations additives `099`, `100`, `101` appliquées à `kermaria_dev` le
  2026-10-05, après sauvegarde physique filtrée et préparée sur SRV-06.
  API DEV SRV-13 puis WebPortal DEV SRV-12 livrés ; PROD inchangée. La
  demande de données avec réponse, document privé et notification, la
  médiathèque et la restauration CMS sont vérifiées en persistance DEV.
  Les tests de renvoi SMTP/KoXo/paiement et une restauration SQL exercée
  restent à faire. Preuves : `docs/releases/DEV_UX_CMS_2026-10-05.md`.
- Une revue des dépendances du build DEV a conduit à Next.js 16.3.8 et
  `eslint-config-next` assorti. Validation globale PASS, second paquet Web DEV
  actif sur SRV-12, Sharp Linux et optimisation WebP contrôlés. L'audit npm
  `--omit=dev` ne signale plus d'avis élevé/critique (un modéré reste).
  La PROD et son lock de déploiement ne sont pas modifiés ; seul le code source
  local porte la mise à jour.
- Le centre de configuration admin affiche maintenant les valeurs d'inscription
  de l'environnement effectivement appliquées lorsqu'aucune ligne n'est
  enregistrée en base. En DEV, `signup_enabled=true` vient du runtime et
  `signup_auto_approve=false` reste le défaut. Correctif API/Web livré en
  binaire seul après validation globale, sans migration ni changement du JSON
  DEV ; preuve dans `docs/releases/DEV_UX_CMS_2026-10-05.md`.
- Les descriptions historiques du catalogue Billing V2 sur `/tarifs` et
  `/formules` étaient encore brutes en DEV (`quota`, `tier`, VPN, RDS, etc.).
  Une projection exacte reformule ces valeurs sans toucher aux montants ni aux
  actions de commande ; une valeur administrée différente garde la priorité.
  Le Web DEV a reçu une quatrième archive avec cette correction. La livraison
  a révélé que l'installateur omettait `KOXO_EXPORT_ALLOWED_IPS` ; le fichier
  DEV antérieur a été restauré à l'identique et l'installateur est corrigé pour
  conserver cette clé puis refuser une liste absente ou vide. Preuves et
  empreintes dans la note de release DEV.
- Validation locale : `test:api`, `check:web`, contrats navigation, copy,
  SEO, managed content, auth/admin et E2E navigateur mock pour publication,
  restauration, médiathèque, demande client, réponse admin et fichier privé.
- Sur la base DEV réelle, la suppression d'un module client requis a rendu 400
  et une sauvegarde fondée sur une ancienne version a rendu 409, sans altérer la
  version publiée. Une session client a reçu 403 sur la lecture du constructeur
  administrateur ; l'absence de session a reçu 401. Après le dernier correctif,
  les services DEV sont toujours prêts et les services PROD inchangés.
- La page d'inscription lit désormais `GET /internal/signup/mode` via
  WebPortal : seuls `enabled` et `autoApprove` effectifs sont exposés, sans
  cache ni session client. Le formulaire se ferme si l'API manque. En DEV
  MariaDB, le mode manuel (`true/false`) est confirmé, sa formulation apparaît
  dans la page publique et l'appel sans authentification de service renvoie
  401. La branche automatique est couverte en tests mock mais n'a pas été
  activée sur la DEV publique faute de boîte e-mail de recette autorisée.
  La cinquième livraison API/Web est décrite dans la note de release DEV.
- La page publique `/demander-mes-donnees` est désormais découpée dans le
  constructeur : introduction, étapes, délai, politique de confidentialité et
  accès obligatoire vers l'espace client ou le contact. La validation serveur
  refuse de supprimer le titre ou le module d'accès. Sur MariaDB DEV, une
  publication en version 1 puis une restauration en version 2 ont conservé le
  contenu initial et les liens ; le rendu mobile à 390 px ne déborde pas.
  La sixième livraison API/Web et ses empreintes sont dans la note de release.
- Un essai de publication concurrente sur MariaDB DEV avec deux requêtes de
  version 2 a donné 200 puis 409 ; la version initiale a été restaurée en
  version 4. Cela vérifie le conflit d'édition sous verrou SQL réel pour cette
  page, sans étendre cette preuve aux autres modèles ou à une restauration de
  sauvegarde physique.
- L'aperçu du constructeur réutilise maintenant `SitePageFrame` pour les blocs
  éditoriaux dans la largeur du panneau. `preview` rend le conteneur inerte,
  empêche l'envoi par `CmsConfigurableForm`, représente les modules métier par
  des repères, et distingue brouillon et version publiée. `check:web` et
  `test:admin` ont réussi ; la septième release Web DEV est active. Une session
  navigateur admin manquait pour la vérification visuelle complète de l'éditeur.
  Le premier contrôle d'archive a échoué sur le quota tmpfs SRV-12 ; trois
  anciens dossiers de préparation du chantier ont été nettoyés après contrôle
  de chemin, puis l'archive et Sharp ont passé la reprise. Détail et empreintes
  dans la note de release DEV.
- Le parcours public d'offre affichait encore `VPN` et `Support Plus` dans les
  choix et le devis. Les intitulés historiques sont désormais projetés via
  `resolveTierLabel`, `resolveServicePublicLabel` et
  `resolveServicePublicDetail` sur les fiches configurables, le panier, la
  revue avant paiement et le résumé d'inscription ; un détail administré
  différent reste intact. L'aide et les descriptions SEO reprennent les mêmes
  termes courants. La huitième release Web DEV est active, sans changement API
  ni SQL ; sur Chrome, Essentiel donne 15,80 € et Performance 20,80 € après
  recalcul serveur. Les preuves et empreintes sont dans la note de release DEV.
- Les quatre fiches `/formules/<code>` partagent maintenant la mise en page
  `/formules/[code]` du constructeur. Fil d'Ariane, introduction Billing V2 et
  configurateur sont requis ; explication et contact restent déplaçables ou
  éditables. Les quatre fiches DEV ont été relues à 390 px : un titre, les
  choix et le lien de contact sont présents sans débordement. La neuvième
  livraison API/Web et le manifeste source sont dans la note de release DEV.
  `npm run validate` et les builds DEV ont réussi ; la publication admin sur
  MariaDB réelle n'a pas été rejouée pour cette page après refus automatique
  de la commande de recette. Le test API mock vérifie le garde du configurateur.
- Fiche d'offre, panier, revue avant paiement, profil client et catalogue
  administrateur sont maintenant découpés en modules obligatoires plutôt qu'un
  `route_content` unique. Les prix, le consentement avant paiement, les actions
  de sécurité et l'éditeur du catalogue restent dans leurs modules métier ;
  les textes libres n'en deviennent pas l'autorité. La dixième livraison
  API/Web DEV a passé `npm run validate`. La première archive Web plaçait les
  assets au mauvais niveau : les scripts/logo renvoyaient 404. Retour immédiat
  à la release précédente, puis archive corrigée avec scripts/logo 200 et
  panier/revue chargés dans Chrome. À 390 px, fiche d'offre, panier et revue
  ont un seul titre et ne débordent pas. Le rendu authentifié du profil et du
  catalogue reste à vérifier dans la recette conjointe avec le propriétaire.

## Suite nécessaire avant de déclarer le chantier terminé

- Le constructeur ajoute et réordonne des blocs dans les trois espaces, mais
  la plupart des écrans métier existants restent regroupés dans
  `route_content`. La page client « Mes données » a été convertie en trois
  widgets distincts (présentation, formulaire, historique), la liste admin
  des demandes en deux widgets et les deux fiches de détail en widgets
  protégés (résumé, échanges, actions et fichiers). Leur réordonnancement et
  restauration ont été vérifiés en navigateur local ; le fichier client reste
  téléchargeable après conversion. Les autres écrans ne sont pas encore
  déplaçables composant par composant.
- Les pages Services prioritaires ont des titres et introductions simplifiés.
  Des détails techniques persistent dans certains contenus CMS historiques ;
  une revue éditoriale des autres pages et du contenu réellement stocké reste
  nécessaire.
- Les migrations 099–101 et les parcours CMS/données ont été éprouvés en DEV.
  La concurrence SQL du constructeur a été testée sur une page ; aucune
  restauration du backup physique ni essai de concurrence sur les demandes
  de données n'a été mené. La PROD n'a pas reçu ce code.
- Le renvoi sur même lien est testé en mock, mais pas encore éprouvé avec SMTP
  ni une transaction MariaDB réelle. Une panne du stockage juste après un
  envoi SMTP peut laisser l'état de suivi en attente et provoquer un nouveau
  lien lors d'un rejeu ultérieur ; le client doit alors utiliser le dernier
  message reçu.
