# OVERNIGHT_REPORT — Zachary IT v2.0.2.5 / Billing V2.1

Branche `claude/zachary-it-audit-reliability-8282c1`, base `349bf2e`.
12 commits. Aucune production, infrastructure, donnée réelle, migration
destructive ni secret n'a été touché. Aucun `push`, tag ni déploiement.

---

## Executive summary

**Le produit était en meilleur état que ses garde-fous.** Le code applicatif —
architecture des hôtes, SEO technique, en-têtes de sécurité, honnêteté du
contenu — est de bonne qualité. Ce qui était cassé, c'est la chaîne de
validation censée le protéger : **la suite de tests .NET ne compilait plus
depuis la veille**, ce qui rendait `npm run validate` et `npm run build`
rouges à travers cinq releases (v2.0.1 → v2.0.2.5) sans que personne le voie,
et **dix suites de tests n'étaient lancées par aucun script**, dont la totalité
de la famille Billing et le cœur de remboursement livré la veille.

Une fois la compilation rétablie, une assertion existante a immédiatement
révélé un bug réel resté invisible : un modèle de communication restauré à sa
valeur par défaut restait marqué « personnalisé », définitivement.

Trois défauts sérieux ont été trouvés côté client : `/api/contact` renvoyait au
navigateur d'un visiteur anonyme des textes d'exploitation — état de
configuration serveur, **réponse SMTP brute**, adresse de la boîte interne ;
quatre titres publics affichaient la marque en double ; et trois pages
répondaient 200 avec une canonical indexable et un corps « temporairement
indisponible », soit un soft-404 quand l'API est en panne.

Deux écarts commerciaux importants sont **documentés et non corrigés**, parce
qu'ils relèvent d'une décision humaine : l'accueil positionne l'entreprise sur
la seule sauvegarde alors que le site vend cinq familles de services, et la
vitrine emploie trois mots — « pack », « formule », « offre » — pour le même
objet.

---

## État final

| Étape | Résultat |
| --- | --- |
| `check:secrets` | PASS — aucun motif sensible détecté |
| `typecheck:shared` | PASS |
| `typecheck:webportal` | PASS |
| `lint:webportal` (eslint) | PASS — 0 problème |
| `build:api` (.NET Release) | PASS — 0 erreur |
| `build:web` (Next.js) | PASS — 0 erreur |
| `test:api` (smoke API-INTERNAL) | PASS |
| `test:billing` (17 suites .NET + contrat webportal) | PASS |
| Contrats webportal | **39 / 39 PASS** (4 échouaient sur la base) |
| Suites .NET exigeant une base | **9 non exécutées — voir « Décisions »** |

**Avant / après sur la base `349bf2e`** : contrats webportal 35/39 →
**39/39** ; suites .NET **0 exécutable** (projet non compilable) → **17 + smoke
exécutées et vertes**.

### Ce qui n'a pas pu être lancé, et pourquoi

Neuf suites exigent `BILLING_V2_TEST_MARIADB_CONNECTION` : schéma du cœur
financier, schéma checkout natif, schéma identités additionnelles, schéma liens
AD, intégration changement M→L, downgrade différé, crash et concurrence, refus
des frais one-time, Stripe indéterminé. Aucune base de test n'est accessible
depuis cet environnement, et la politique du projet interdit une MariaDB
locale. **Elles refusent correctement de se déclarer vertes sans base** — je ne
prétends pas qu'elles passent. Elles étaient jusqu'ici atteignables uniquement
en invoquant la DLL à la main ; elles sont désormais regroupées sous
`npm run test:billing:mariadb`.

Conséquence à retenir : **le verrouillage InnoDB, les migrations et la
concurrence réelle ne sont pas exercés.** La preuve s'arrête à la persistance
mock.

`npm run validate` n'a pas été lancé d'un seul tenant : il appelle `build:web`,
qui échoue dans un worktree Windows sur MAX_PATH (Turbopack). Chaque étape a
été exécutée séparément, le build via `next build --webpack`, contournement
déjà documenté pour ce dépôt.

---

## Corrections effectuées

### 1. La suite de tests .NET ne compilait plus (P0)

**Problème.** `9978cc8` a ajouté `IAuthenticationService` à `SignupService`
sans mettre à jour les cinq sites de construction dans les smoke tests.
**Impact.** `npm run test:api`, `npm run validate` et `npm run build` échouaient
à cette étape depuis les releases v2.0.1 à v2.0.2.5 : toute la suite backend
était morte, silencieusement.
**Correction.** L'argument manquant est fourni par un **vrai**
`AuthenticationService` branché sur le magasin mock déjà partagé avec
l'inscription, plutôt que par un bouchon qui réussit toujours : le parcours VPS
en libre-service ouvre une session juste après avoir créé le compte, et c'est
ce comportement qui mérite d'être couvert.
**Validation.** Le projet compile ; `test:api` passe.

### 2. Un modèle restauré restait « personnalisé » (P1)

**Problème.** Les écritures passent par `TryNormalize`, qui convertit CRLF en
LF et trim. Les gabarits par défaut sont des littéraux C# dans des fichiers que
`.gitattributes` fixe en CRLF. Le drapeau `Customized` comparait les deux
formes en `StringComparison.Ordinal`.
**Impact.** Un modèle restauré à sa valeur exacte revenait marqué
« personnalisé » — et le restait, chaque comparaison ultérieure butant sur le
même écart. Vrai pour les e-mails, les notifications et les snippets.
**Correction.** Comparaison après la même normalisation que l'écriture, en un
seul point.
**Validation.** L'assertion existante de `VerifyCommunicationTemplatesAsync`
échouait dès que le projet a recompilé ; elle passe.

### 3. `/api/contact` renvoyait du détail d'exploitation au public (P1)

**Problème.** Le BFF recopiait le `code` et le `message` amont dans la réponse
servie au visiteur : `NO_RECIPIENT` annonçait « L'adresse de destination du
formulaire de contact n'est pas configurée », un échec de remise renvoyait
`delivery.ErrorMessage`, c'est-à-dire **la réponse SMTP brute** — hôte, échec
d'authentification, boîte cible. La réponse de succès contenait « Message
transmis à \<boîte interne\> ».
**Impact.** Divulgation d'état de configuration serveur et de détail
d'infrastructure à un visiteur anonyme de la vitrine.
**Correction.** Le BFF rend une phrase stable qui dit quoi faire, et journalise
le code amont via `logBffFailure` sous le même `correlation_id`. API-INTERNAL
cesse de mettre le détail sur le fil : il reste dans le journal d'e-mails et un
nouveau `LogError`.
**Validation.** Vérifié en conditions réelles contre une API sans destinataire
configuré : la réponse ne contient plus ni code amont, ni détail, ni adresse.
Contrat de test étendu, avec une assertion côté API en défense en profondeur.

### 4. Marque en double dans quatre titres publics (P1)

**Problème.** `parseStorefrontPageContent` retire un « | Zachary IT » **final**
pour que le gabarit du layout l'ajoute une fois. Quatre titres plaçaient la
marque hors de portée de ce nettoyage — dont le titre CMS de `/tarifs`, au
milieu de la chaîne, produisant 76 caractères avec la marque deux fois.
**Correction.** Marque retirée du titre ; le garde-fou, qui ne cherchait qu'un
suffixe, rejette maintenant la marque à toute position et s'applique aussi au
seed CMS, avec un plafond de longueur.
**Validation.** Vérifié en réel : chaque page publique porte la marque
exactement une fois.

### 5. Soft-404 sur panne de contenu (P1)

**Problème.** `/services`, `/services/[category]` et `/tarifs` répondaient 200
avec canonical indexable et corps « temporairement indisponible ».
**Correction.** `noindex, follow` sur exactement la branche qui rend
l'`ErrorState`. La page reste en 200 pour un humain.
**Validation.** Vérifié contre une API délibérément injoignable.

### 6. Vocabulaire interne sur l'inscription publique (P1)

`app/api/signup/route.ts` répondait « La configuration Billing V2 choisie n est
pas valide. » — nom de système interne, et apostrophe manquante. Remplacé par
« L'offre sélectionnée n'est pas valide. »

### 7. Trois contrats obsolètes remis en phase (P1)

Ils épinglaient un texte source que des refactorings légitimes avaient changé.
Le plus grave : le contrat managed-content **exigeait** que le seed dise
« publiées depuis le catalogue Billing V2.1 » — c'est-à-dire qu'il réclamait le
vocabulaire interne que la copie publique doit justement bannir.

Plutôt que d'assouplir les assertions, les deux premières exercent désormais
les helpers directement (résolution de zone portail sur les cinq zones ;
redirection et continuation, y compris open-redirect et segment surnuméraire),
et la troisième suit l'intention client plus une interdiction nouvelle.

---

## Tests ajoutés

**Remboursement Billing V2.1 — 12 cas, 11 branches de refus jamais exercées.**
La suite couvrait le chemin nominal et quelques refus ; onze `ReasonCode` que
les politiques peuvent produire n'avaient aucune assertion. Ajoutés, un par
risque listé au cahier des charges : BillingEvent absent (objet inexistant) ;
montants nul, négatif et devise vide (données invalides) ; les six coordonnées
provider manquantes, PayPal compris ; abonnement récurrent sans ancre provider,
refusé **dès la demande** et pas seulement à la compensation ; second
remboursement sur une source déjà remboursée (double remboursement, données
anciennes) ; refund non observé chez le provider ; **refund observé sur un
autre paiement** ; statut `canceled`, qui est un échec et non un pending ;
abonnement absent bloquant la compensation ; barrières SQL et passerelle
Stripe du portillon d'exécution.

Un test garde une **non-fonctionnalité** : le remboursement partiel n'existe
pas en V2.1 et la clé d'idempotence porte `full`. Si un partiel est ajouté sans
changer cette clé, un partiel et un intégral sur le même BillingEvent
partageraient une seule clé Stripe, et le second serait résolu par le premier —
soit un client silencieusement non remboursé de la différence.

**Vérifiés par mutation** : relâcher le contrôle de montant et supprimer la
vérification d'identité du paiement font tous deux échouer la suite.

**Contrat de copie publique généralisé.** Il inspectait sept fichiers listés à
la main ; il balaie désormais les 171 fichiers non-administration de `app/` et
`components/`. La régression qu'il doit attraper vient de fichiers **ajoutés**,
ce qu'une liste fixe ne peut pas voir. Seuls les littéraux contenant une espace
sont examinés — un texte rendu est une phrase, une valeur d'état
(`"provisioning"`) est un jeton — ce qui supprime les faux positifs que la
version précédente neutralisait à coups de `replace`. Les deux surfaces
internes légitimes sont nommées avec leur raison. Il a immédiatement trouvé la
fuite du point 6.

**Balisage FAQ.** Assertions sur les cinq rendus, sur le constructeur (entrées
incomplètes écartées, FAQ vide → `null`, ni `Offer` ni prix) et sur `JsonLd`.
Vérifiées par mutation.

**Identité de marque.** Marque interdite à toute position d'un titre de page et
dans le seed CMS ; plafond de longueur ; description de l'entreprise devant
couvrir les cinq univers ; `knowsAbout` contraint à un sujet par page de
service publiée, sans doublon.

**Formulaire de contact.** Le harnais exécutable existant a été étendu :
non-relais du code et du message amont, absence de détail serveur dans le texte
rendu, présence d'une consigne au visiteur, journalisation corrélée, et absence
de toute adresse e-mail dans la réponse de succès.

**Zone portail et continuation.** `resolveServicesPortalMode` sur les cinq
zones ; `resolvePortalPublicRedirectUrl` et
`resolveClientCheckoutContinuationPath` sur les cas d'open-redirect
(`//evil.invalid/...`), de segment surnuméraire et de surface interne.

**Câblage.** 10 suites .NET et 6 scripts npm que `validate` n'atteignait pas y
sont entrés ; `--billing-v2-refund` et `--billing-v2-public-catalog`, qui
n'étaient atteignables par rien, sont désormais lancés.

---

## UX / UI

- `/contact` : titre, accroche, bloc « Ce qui se passe ensuite », renvois vers
  `/diagnostic` et `/services`, libellé de retour corrigé.
- Focus déplacé sur le bloc de résultat après soumission ; `FormMessage`
  accepte une `ref` et ne devient focusable que dans ce cas.
- « Sujet (optionnel) », suivant la convention déjà employée côté admin.
- Aucun nouveau design : les blocs ajoutés réutilisent `signup-steps-card` et
  `contact-form-note` de la ligne visuelle existante.
- Rendu vérifié en 375×812 et en desktop ; aucune erreur console.

## Contenu

L'accroche de `/contact` cantonnait la page à la sauvegarde et promettait une
réponse « sous un délai raisonnable » — formule qui n'engage rien tout en ayant
l'air d'engager. Elle est remplacée par une invitation ouverte aux cinq
familles de services. Le bloc d'étapes ne décrit **que** ce que le système fait
réellement et **n'annonce aucun délai** : rien dans le produit ne permet d'en
tenir un.

Aucun chiffre commercial, certification, référence client ni engagement n'a été
inventé. Un cas mérite d'être signalé : j'ai d'abord écrit
`contact@zachary-it.fr` dans un message d'erreur, puis vérifié — cette adresse
n'existe nulle part dans le dépôt. Elle a été retirée au profit d'un renvoi aux
mentions légales.

## Contact

Voir points 3 et `UX_CONTENT_REVIEW.md`. Le vrai sujet était la fuite
d'information ; elle est fermée, testée et vérifiée en conditions réelles.

## Notifications

Le système est cohérent et n'a **pas** été refondu : `FormMessage` (46 usages,
`role="alert"` / `role="status"` selon le ton), `ErrorState` avec référence de
corrélation affichée, `EmptyState`, `LoadingState`, `SubmitButton` avec
`aria-busy` et libellé de chargement.

Une suspicion a été levée après contrôle : `userMessageFor` relaie le message
serveur pour les 5xx, mais les messages 5xx d'API-INTERNAL sont génériques et
orientés client (« Le service de données est temporairement indisponible. »).
Aucune modification n'était justifiée.

## SEO / GEO

Détail dans `SEO_GEO_REVIEW.md`. Corrigé : marque en double, soft-404,
`FAQPage` absent, description machine de l'entreprise limitée à la sauvegarde.
Vérifié conforme : `robots.txt` par hôte, 301 `www` → apex, 301 hôte client →
public, canonicals, sitemap avec `lastmod` réel, absence de spam géographique.

`llms.txt` n'a **pas** été ajouté, et c'est un choix argumenté : le contenu est
administrable et servi dynamiquement, un fichier statique divergerait à la
première modification, et aucun crawler identifié ne le consomme.

## Performance

Fontes en `display: swap`, aucune image sans `alt`, une seule image `next/image`
sur toute la vitrine, build sans avertissement, aucune erreur console. Aucune
micro-optimisation sans bénéfice mesurable n'a été tentée. 84 composants sur
116 sont des composants client : c'est élevé, mais le réduire demande une
analyse par composant qui dépasse une correction sûre.

## Accessibilité

Contrôlé et **conforme** : `lang="fr"`, liens d'évitement sur les deux shells,
aucune `<img>` sans `alt`, 35 règles `focus-visible`, 4 blocs
`prefers-reduced-motion`, et les 3 `outline: none` sont chacun appariés à une
règle `:focus` explicite. Ajouté : gestion du focus sur le résultat du
formulaire de contact, et marquage du champ facultatif.

## Sécurité

**Corrigé** : la fuite de détail d'exploitation sur `/api/contact`, aux deux
extrémités.

**Vérifié conforme en conditions réelles** : en-têtes (CSP `frame-ancestors`,
`X-Frame-Options: DENY`, `Referrer-Policy`, `Permissions-Policy`, COOP, CORP),
isolation `robots` par hôte, limitation de débit du formulaire (5 / 5 min,
déclenchement observé), validation du code de formule contre le catalogue
publié — une chaîne forgée (`../../etc/passwd`) est refusée avant tout usage,
CSRF sur les mutations admin, `SERVICE_AUTH_TOKEN` refusant les valeurs
placeholder hors développement (garde qui a bloqué mon propre banc d'essai,
comme prévu).

Les nouveaux tests ajoutent une couverture d'open-redirect sur le chemin de
reprise de souscription.

---

## Dette technique restante

1. **9 suites Billing non exécutables sans base** — verrouillage InnoDB,
   migrations et concurrence réelle jamais exercés (F19). *Priorité haute.*
2. **Titres administrés en base** — la correction du seed `/tarifs` n'atteint
   pas la valeur servie en production (F03). *Priorité haute, action admin.*
3. **`app/globals.css` : 11 304 lignes** en un fichier (F20). *Moyenne.*
4. **Contenu FAQ administrable non contraint** — rien n'empêche un rédacteur
   d'écrire un prix dans une réponse balisée en `FAQPage`. *Moyenne.*
5. **`signup-steps-card` partagée** entre inscription et contact : le nom ne
   décrit plus son usage (F21). *Faible.*
6. **BOM UTF-8** dans 5 fichiers TS/TSX (F22). *Faible.*
7. **Gabarit e-mail `signup_verification` signé « Kermaria »** et non
   « Zachary IT » (F24) — administrable en base, donc peut-être déjà surchargé
   en production ; à vérifier avant de corriger le seul seed. *Faible.*
8. **`validate` nettement plus long** (≈ 17 exécutions .NET ajoutées).
   Compromis assumé. *Faible.*

---

## Décisions requérant une validation humaine

### D1 — Positionnement de l'accueil

**Contexte.** Le titre servi est « Zachary IT | Sauvegarde informatique à
Guichen (35) » et la meta description ne parle que de sauvegarde, stockage
documentaire et continuité d'activité. `/services` publie quatre univers et
quinze pages : VPS, hébergement, messagerie, DNS, VPN, bureau à distance,
UniFi, firewall, WAF, supervision, maintenance, support.

**Options.** (a) Ne rien changer. (b) Élargir titre et description aux cinq
familles. (c) Élargir la description seule, garder le titre.

**Avantages / risques.** (a) préserve le ciblage actuel sur « sauvegarde
Guichen », mais un prospect réseau ou messagerie ne reconnaît pas l'entreprise
depuis un résultat de recherche — c'est l'écart le plus coûteux
commercialement du site. (b) ouvre la découverte, au prix d'un déplacement du
référencement acquis. (c) compromis : la description est lue par l'humain dans
la SERP, le titre garde son ancrage.

**Recommandation : (c)**, puis mesurer avant d'aller vers (b). J'ai déjà élargi
la description **lisible par machine** (`LocalBusiness`), qui contredisait le
catalogue sans porter de ciblage : c'était une correction, pas un
repositionnement.

### D2 — Un seul mot pour l'objet commercial

**Contexte.** « pack », « formule » et « offre » désignent la même chose ;
`/offres` emploie les trois.

**Options.** (a) Statu quo. (b) « Formule » partout (déjà le terme du moteur
tarifaire et de `/formules`). (c) « Offre » partout.

**Avantages / risques.** (b) aligne la vitrine sur le vocabulaire interne du
catalogue et minimise le travail de code ; impose de renommer `/offres`, donc
des redirections. (c) est le mot le plus naturel commercialement, mais éloigne
la vitrine du vocabulaire du catalogue.

**Recommandation : (b)**, avec 301 de `/offres` vers `/formules`. À trancher
avant toute campagne : chaque semaine passée multiplie les liens à rediriger.

### D3 — Adresse de contact sur le domaine de marque

**Contexte.** Données structurées et mentions légales publient
`zhounsah@home.bzh`, un domaine interne. Aucune adresse `@zachary-it.fr`
n'existe dans le dépôt.

**Options.** (a) Statu quo. (b) Créer `contact@zachary-it.fr` et la publier.

**Risques.** (a) affaiblit la crédibilité chez un prospect attentif et expose
un nom de domaine interne. (b) demande une boîte, du SPF/DKIM et une mise à
jour des mentions légales.

**Recommandation : (b).** Je n'ai rien changé ici — et j'ai retiré une
occurrence de `contact@zachary-it.fr` que j'avais écrite par erreur dans un
message d'erreur avant de vérifier qu'elle n'existait pas.

### D4 — Base de test pour les 9 suites Billing

**Contexte.** Les invariants les plus critiques de V2.1 — verrouillage,
migrations, concurrence, crash — ne sont prouvés sur aucune base.

**Options.** (a) Statu quo. (b) Base jetable sur SRV-06 avec un compte
cantonné, selon le protocole déjà documenté. (c) Base locale.

**Risques.** (a) laisse la facturation sans preuve de concurrence réelle avant
le premier euro encaissé. (c) est exclu par la politique du projet.

**Recommandation : (b)**, avant toute levée de
`FIRST_REAL_SUBSCRIPTION_APPROVED`.

### D5 — Durée de `npm run validate`

`validate` inclut désormais toute la famille Billing. Si la durée devient un
frein, la scinder en `validate` (rapide) et `validate:release` (complet) est
préférable à en ressortir des suites. **Recommandation : laisser tel quel** et
observer ; l'incident F01 vient précisément d'un garde-fou qu'on ne lançait
plus.

---

## Commits

| Commit | Description |
| --- | --- |
| `13770f6` | `test(api)` — rétablit la compilation des smoke tests ; l'argument manquant est un vrai `AuthenticationService`, pas un bouchon |
| `d37310a` | `fix(admin)` — un modèle restauré n'est plus signalé « personnalisé » (comparaison CRLF/LF) |
| `4f0a78f` | `fix(seo)` — supprime le suffixe de marque en double sur deux pages VPS |
| `270e41d` | `test(webportal)` — remet trois contrats obsolètes en phase, en assertions de comportement |
| `28c2305` | `seo` — balise en `FAQPage` les questions déjà affichées sur cinq rendus |
| `78a0606` | `fix(contact)` — cesse de renvoyer du détail d'exploitation au visiteur, aux deux extrémités |
| `9563007` | `content(contact)` — la page répond aux questions d'un prospect ; focus et champ facultatif |
| `0dd4a71` | `test` — exécute les suites qu'aucun garde-fou n'atteignait ; balayage complet de la copie publique |
| `38ee965` | `test(billing)` — couvre les 11 refus de remboursement jamais exercés, plus la non-fonctionnalité « partiel » |
| `023dd51` | `seo` — la description machine de l'entreprise couvre tout le catalogue ; `knowsAbout` |
| `140f439` | `seo` — `noindex` sur les rendus d'erreur, contre le soft-404 |
| `2697011` | `seo` — supprime la marque répétée dans les titres ; garde-fou étendu au seed CMS |

---

## Rapport chiffré final

Notes sur 10, justifiées et volontairement sévères. « Avant » = état de la base
`349bf2e`.

| Domaine | Avant | Après | Confiance |
| -------------- | ----: | ----: | -------------------- |
| Fiabilité | 5 | 8 | forte |
| Tests | 3 | 8 | forte |
| Billing | 6 | 7 | **moyenne** |
| UX | 7 | 8 | forte |
| UI | 7 | 7 | forte |
| Contenu | 6 | 7 | moyenne |
| Contact | 4 | 8 | forte |
| Notifications | 7 | 8 | forte |
| SEO | 7 | 9 | forte |
| GEO / IA | 5 | 8 | moyenne |
| Accessibilité | 7 | 8 | forte |
| Performance | 7 | 7 | moyenne |
| Maintenabilité | 6 | 7 | moyenne |
| Sécurité | 6 | 8 | forte |

**Justifications des notes les plus discutables.**

- **Tests 3 → 8.** Le 3 n'est pas sévère à l'excès : la suite backend ne
  compilait pas, dix suites ne tournaient nulle part, et quatre contrats
  échouaient. Ce n'est pas 9 après, parce que les invariants les plus critiques
  de Billing restent prouvés en mock seulement.
- **Billing 6 → 7, confiance moyenne.** Le code est de bonne facture et les
  refus sont maintenant couverts, mais **je n'ai exercé aucune base réelle**.
  Verrouillage, migrations et concurrence restent non prouvés. Monter au-delà
  de 7 supposerait une preuve que je n'ai pas.
- **Contact 4 → 8.** Le 4 tient à la fuite de détail SMTP et d'état de
  configuration vers un visiteur anonyme, pas à l'esthétique.
- **GEO 5 → 8, confiance moyenne.** La description machine de l'entreprise
  contredisait le catalogue du site ; c'est corrigé et les FAQ sont balisées.
  La confiance reste moyenne parce que la façon dont les moteurs de réponse
  exploitent réellement ces signaux n'est pas mesurable depuis le dépôt.
- **UI et Performance inchangés.** Je n'ai pas refondu la ligne visuelle — elle
  est cohérente — et je n'ai pas mesuré de bénéfice de performance : annoncer
  un gain sans mesure serait faux.
- **Sécurité 8 et non 9.** L'architecture est solide et vérifiée en réel, mais
  une revue de sécurité complète du périmètre admin et des intégrations
  provider dépasse ce qui a été fait ici.

---

## Post-audit closure - v2.0.2.6

This section supersedes earlier open-state statements where later validation produced stronger evidence.

- Fresh MariaDB 11.8.6 database bootstrap: migrations `001` through `093` PASS.
- `npm run test:billing:mariadb`: 9/9 MariaDB-backed suites PASS, stderr empty.
- Real MariaDB validation exposed and fixed a test defect in concurrent connection string handling (`e7a0f40`).
- Persisted CMS content was synchronized through migrations `087`-`093`; stale internal Billing wording, public pack/formule wording and mojibake were removed.
- Public contact identity now uses `contact@zachary-it.fr` in public/legal content.
- Public customer vocabulary is standardized on `offre`; internal Billing routes/model names remain unchanged.
- The public footer no longer exposes the application version.
- Homepage positioning now reflects the broader IT service scope.

Final release gates passed: secrets check, both typechecks, full web lint, API build, production WEBPORTAL webpack build (79 pages), API/Billing tests, public content contracts, and all 9 MariaDB-backed Billing suites.

Billing/database-test confidence is now **strong**, not medium: schema migration, InnoDB persistence, locking/concurrency and opt-in integration paths were exercised against MariaDB 11.8.6.

Operational note: migration `066_billing_v2_componentized_pricing` creates triggers and a view. With binary logging enabled, fresh bootstrap requires temporary `SUPER` for trigger creation plus `CREATE VIEW`; these privileges must be removed immediately after the migration window.
