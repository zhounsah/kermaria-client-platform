# AUDIT_FINDINGS — Zachary IT v2.0.2.5 / Billing V2.1

Revue conduite sur la branche `claude/zachary-it-audit-reliability-8282c1`,
base `349bf2e` (v2.0.2.5). Aucune production, infrastructure, donnée réelle,
migration ni secret n'a été touché.

**Statut** : `CORRIGÉ` = corrigé et vérifié dans cette branche ·
`DOCUMENTÉ` = constat réel, correction non faite (décision humaine requise ou
risque supérieur au bénéfice) · `VÉRIFIÉ OK` = suspicion levée après contrôle,
aucune action.

| ID | Sévérité | Domaine | Problème | Impact | Action | Statut |
| -- | -------- | ------- | -------- | ------ | ------ | ------ |
| F01 | P0 | Tests / release | Le projet de tests .NET ne compile plus depuis `9978cc8` : `SignupService` a reçu `IAuthenticationService` sans mise à jour des 5 sites de construction | `npm run test:api`, `npm run validate` et `npm run build` échouent à cette étape depuis les releases v2.0.1 → v2.0.2.5 ; toute la suite backend était morte | Argument fourni par un vrai `AuthenticationService` branché sur le magasin mock déjà partagé avec l'inscription | CORRIGÉ |
| F02 | P1 | Admin / contenus | `Customized` comparait un gabarit stocké (LF, trimmé par `TryNormalize`) au littéral C# (CRLF imposé par `.gitattributes`) | Un modèle restauré à sa valeur par défaut restait affiché « personnalisé », définitivement | Comparaison après la même normalisation que l'écriture, sur e-mails, notifications et snippets | CORRIGÉ |
| F03 | P1 | SEO | 4 titres publics répétaient la marque : le gabarit du layout ajoutait un second « \| Zachary IT » | Titres jusqu'à 76 caractères avec marque en double dans les résultats de recherche | Marque retirée du titre de page ; garde-fou étendu à toute position et au seed CMS, plus plafond de longueur | CORRIGÉ |
| F04 | P1 | Sécurité / contact | `/api/contact` recopiait le `message` amont au navigateur : « adresse de destination non configurée », erreur SMTP brute, et l'adresse de la boîte interne en cas de succès | Divulgation d'état de configuration serveur et de détail SMTP à un visiteur anonyme de la vitrine | Message client stable + `logBffFailure` corrélé ; l'API cesse de mettre le détail sur le fil | CORRIGÉ |
| F05 | P1 | Tests / Billing | 10 suites présentes dans le binaire de test n'étaient lancées par aucun script ; `--billing-v2-refund` et `--billing-v2-public-catalog` n'étaient atteignables par rien | Le cœur de remboursement a été livré avec des tests que personne n'exécutait | Suites sans base ajoutées à `validate` ; suites à base groupées sous `test:billing:mariadb` | CORRIGÉ |
| F06 | P1 | Tests | 6 scripts npm et 3 suites webportal hors de `npm run validate`, dont **toute** la famille Billing | Le garde-fou de release ne couvrait pas la facturation | Ajoutés à `validate` ; alias racine créés pour `public-copy`, `client-vps`, `local-navigation` | CORRIGÉ |
| F07 | P1 | Tests | 3 contrats en échec sur HEAD par obsolescence : ils épinglaient un texte source refactoré, pas un comportement | `validate` rouge sans défaut réel ; le contrat managed-content **exigeait** du vocabulaire interne dans la copie publique | Réécrits en assertions de comportement (résolution de zone portail, redirection et continuation, dont open-redirect et segment surnuméraire) | CORRIGÉ |
| F08 | P1 | Vitrine | `app/api/signup/route.ts` répondait « La configuration Billing V2 choisie n est pas valide. » sur l'inscription publique (vocabulaire interne + apostrophe manquante) | Le prospect lit un nom de système interne dans un message d'erreur | Message orienté client ; contrat de copie publique généralisé à toutes les surfaces non-admin | CORRIGÉ |
| F09 | P1 | SEO | `/services`, `/services/[category]` et `/tarifs` répondaient 200 avec canonical indexable et corps « temporairement indisponible » quand le contenu était illisible | Soft-404 : une panne d'API pouvait entrer dans l'index à la place de la page | `noindex, follow` sur exactement la branche qui rend l'`ErrorState` | CORRIGÉ |
| F10 | P2 | Contact | Lien retour intitulé « Retour aux formules » pointant vers `/offres` ; accroche restreinte à la sauvegarde alors que les liens `?formule=` viennent des packs | Libellé contredisant sa destination ; prospect venu d'une page réseau ou messagerie lit une invitation qui l'exclut | Libellé corrigé, accroche élargie aux quatre univers, bloc « Ce qui se passe ensuite » factuel | CORRIGÉ |
| F11 | P2 | SEO / GEO | Cinq rendus publics affichent des FAQ administrables ; aucun balisage `FAQPage` | Les moteurs de réponse devaient déduire les couples question/réponse du texte libre | `faqPageJsonLd` sur les cinq rendus, entrées incomplètes écartées, FAQ vide → aucun balisage | CORRIGÉ |
| F12 | P2 | SEO / GEO | Le nœud `LocalBusiness` décrivait la seule sauvegarde alors que `/services` publie 4 univers et 15 pages | La seule description lisible par machine contredisait le catalogue du site | Description alignée sur la taxonomie publiée + `knowsAbout` (15 sujets, un par page réelle, contrôlé par contrat) | CORRIGÉ |
| F13 | P2 | Tests / Billing | 11 `ReasonCode` de remboursement ne faisaient l'objet d'aucune assertion | Une régression transformant un refus en autorisation passait silencieusement | 12 tests ajoutés (objet inexistant, montants invalides, coordonnées provider, double remboursement, refund observé sur un autre paiement, `canceled`, barrières SQL/Stripe, non-fonctionnalité « remboursement partiel ») ; vérifiés par mutation | CORRIGÉ |
| F14 | P2 | Accessibilité | Le résultat d'envoi du formulaire de contact n'était annoncé que par `aria-live` ; le focus restait sur le bouton | Un visiteur au clavier pouvait ne pas percevoir l'issue en bas d'un formulaire long | `FormMessage` accepte une `ref` et prend le focus après soumission | CORRIGÉ |
| F15 | P2 | UX formulaire | Aucun champ n'indiquait son caractère facultatif | Le visiteur ne sait pas ce qu'il peut omettre | « Sujet (optionnel) », convention déjà utilisée côté admin | CORRIGÉ |
| **F16** | **P2** | **Positionnement** | Le titre et la meta description de l'accueil positionnent l'entreprise sur la **seule sauvegarde** ; `/services` en vend cinq familles | Un prospect cherchant réseau, messagerie ou hébergement ne reconnaît pas Zachary IT depuis le résultat de recherche | Non corrigé : le titre porte le ciblage SEO actuel, c'est une **décision commerciale** | DOCUMENTÉ |
| **F17** | **P2** | **Cohérence éditoriale** | Trois mots pour le même objet commercial sur la vitrine : « pack », « formule », « offre » ; `/offres` emploie les trois | Le prospect ne sait pas si ce sont trois choses ou une | Non corrigé : choisir le terme unique est une **décision commerciale** | DOCUMENTÉ |
| **F18** | **P2** | **Marque / contact** | L'adresse publiée en données structurées et en mentions légales est `zhounsah@home.bzh`, domaine interne, pas le domaine de marque | Rend le contact moins crédible ; aucune adresse `@zachary-it.fr` n'existe dans le dépôt | Non corrigé : créer une boîte est une **action d'infrastructure** hors périmètre | DOCUMENTÉ |
| **F19** | **P1** | **Tests / preuve** | 9 suites Billing exigent `BILLING_V2_TEST_MARIADB_CONNECTION` : schéma financier, checkout natif, identités additionnelles, changement M→L, downgrade différé, crash/concurrence, refus one-time, Stripe indéterminé, schéma liens AD | Le verrouillage InnoDB, les migrations et la concurrence réelle **ne sont pas exercés** ; la preuve s'arrête à la persistance mock | Non exécutables ici : aucune base de test accessible. Regroupées et nommées (`test:billing:mariadb`) au lieu d'être invisibles | DOCUMENTÉ |
| F20 | P3 | Dette technique | `app/globals.css` fait 11 304 lignes en un seul fichier | Coût de navigation et risque de collision entre surfaces | Non corrigé : un découpage est un changement d'architecture, hors périmètre d'une correction sûre | DOCUMENTÉ |
| F21 | P3 | Dette technique | La page `/contact` réutilise la classe `signup-steps-card` | Nom trompeur : restyler l'inscription modifie le contact | Non corrigé : réutiliser la ligne visuelle existante était l'objectif ; renommer touche deux surfaces | DOCUMENTÉ |
| F22 | P3 | Dette technique | 5 fichiers TS/TSX portent un BOM UTF-8 (`client-api.ts`, `admin-bff.ts`, `PublicPriorityServicePage.tsx`, 2 pages admin) | Incohérence d'encodage ; un commit dédié avait déjà retiré le BOM de `package.json` | Non corrigé : sans effet fonctionnel, et un reformatage massif brouillerait la revue | DOCUMENTÉ |
| F23 | P3 | SEO | Le pied de page public affiche « Version v2.0.2.5 » | Numéro de version interne visible du client | Non corrigé : c'est un marqueur de déploiement volontaire, utilisé par les contrôles post-release | DOCUMENTÉ |
| F24 | P3 | Contenu | Le gabarit e-mail par défaut `signup_verification` signe « Kermaria », pas « Zachary IT » | Le client reçoit un e-mail signé d'un autre nom que la marque du site | Non corrigé : le texte est administrable en base et peut déjà être surchargé en production ; corriger le seul seed donnerait une fausse impression de correction | DOCUMENTÉ |
| F25 | — | Sécurité | Suspicion : `userMessageFor` (`lib/client-api.ts`) relaie le message serveur pour les 5xx | Contrôle effectué : les messages 5xx d'API-INTERNAL sont génériques et orientés client (« Le service de données est temporairement indisponible. », « Une erreur interne est survenue. ») | Aucune action | VÉRIFIÉ OK |
| F26 | — | Accessibilité | Suspicion : 3 `outline: none` dans `globals.css` | Contrôle effectué : les trois sont appariés à une règle `:focus` explicite ; `lang="fr"`, liens d'évitement sur les deux shells, aucune `<img>` sans `alt`, 35 règles `focus-visible`, 4 blocs `prefers-reduced-motion` | Aucune action | VÉRIFIÉ OK |
| F27 | — | SEO / sécurité | Contrôle en conditions réelles : en-têtes (CSP `frame-ancestors`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, COOP/CORP), `robots.txt` par hôte, 301 `www` → apex, 301 hôte client → public pour une page vitrine | Tout conforme | Aucune action | VÉRIFIÉ OK |
| F28 | — | Formulaires | Contrôle en conditions réelles de `/api/contact` : champs vides, e-mail invalide, code de formule forgé (`../../etc/passwd`), code inconnu, JSON malformé, limitation de débit à 5 requêtes | Tous les cas répondent correctement, avec `correlation_id` | Aucune action | VÉRIFIÉ OK |

## Risques résiduels des corrections

- **F03 / titres** : les titres de production sont administrables en base. La
  correction du seed ne les atteint pas. Les deux titres concernés
  (`storefront:tarifs`, et tout titre saisi avec la marque au milieu) doivent
  être corrigés depuis `/admin/content`.
- **F11 / FAQPage** : le contenu FAQ est administrable. Le constructeur
  n'émet ni `Offer` ni prix, mais rien n'empêche un rédacteur d'écrire un
  montant dans une réponse. Les règles Google interdisent le contenu
  promotionnel dans un `FAQPage`.
- **F09 / noindex** : une panne d'API prolongée désindexerait temporairement
  les pages concernées. C'est le comportement voulu — indexer la panne est
  pire — mais il faut le savoir avant de conclure à une chute de trafic.
- **F13 / remboursement partiel** : le test exige que
  `BillingV2RefundPolicy` n'expose qu'une seule méthode publique. L'ajout d'un
  utilitaire public légitime le fera échouer. C'est délibéré : le message
  explique qu'il faut d'abord revoir la clé d'idempotence.
- **F06 / durée de `validate`** : la commande est nettement plus longue
  (≈ 17 exécutions .NET supplémentaires). Le compromis est assumé : la
  facturation non testée au moment de la release est le risque supérieur.
