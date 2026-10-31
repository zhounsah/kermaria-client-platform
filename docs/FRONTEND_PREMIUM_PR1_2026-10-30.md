# Frontend public premium — audit et recette locale du 30 octobre 2026

## Base examinée

Travail effectué dans le worktree de la PR #1, branche `codex/zachary-it-refonte-ready`. Cette branche portait déjà une refonte et des modifications non commités (Assistance & maintenance, navigation, mouvement). Aucun changement n'a été reporté depuis le worktree `main` et aucune livraison DEV ou PROD n'a été lancée par cette tâche.

Le WebPortal utilise Next.js 16.4.3 App Router, React 19.2.7, TypeScript, CSS natif et Lucide. Les polices Inter et JetBrains Mono sont locales. L'identité réunit le logo officiel, le bleu cobalt, l'encre sombre, le fond clair et des accents lime. Le parcours public alterne un hero typographique, les publics servis, la méthode, les services, le choix d'offre et le contact. Les pages métier privées restent dans le même WebPortal, séparées par le routage et le BFF.

Le contenu public peut venir des pages de repli ou du constructeur CMS via `SitePageFrame`. Le hero commun `VitrineHero` couvre les deux. L'architecture reste `browser → WEBPORTAL/BFF → API-INTERNAL → MariaDB` ; aucune intégration interne directe n'a été ajoutée au navigateur.

## SEO et accessibilité

Les titres et liens restent dans le HTML, avec un seul H1 sur l'accueil. Les métadonnées, URL canoniques, robots, sitemap, données structurées et en-têtes `noindex` des routes privées sont conservés. Le mouvement est décoratif : `prefers-reduced-motion` affiche directement le contenu et une commande permet de le mettre en pause pour la session. Le menu mobile et le focus de navigation restent utilisables au clavier.

## Ajustements de cette tâche

- L'écran d'introduction ne capte plus les pointeurs ni le défilement. Une action clavier, pointeur ou molette termine l'effet immédiatement.
- Les boucles permanentes de l'orbite, du bandeau de services, des filets, du dégradé et du CTA ont été retirées. Les transitions de contexte, le titre et quelques signaux limités restent animés.
- La colonne du bloc CMS qui contient le hero utilise `minmax(0, 1fr)` et son contenu `min-width: 0`, ce qui supprime un débordement horizontal observé à 1280 px.
- Neuf Agent Skills réutilisables décrivent le design, React/Next, l'accessibilité/SEO, Playwright, le hero, le récit au défilement, le mouvement, les composants interactifs et le budget de performance. WebGL/3D n'a pas été retenu : aucun besoin de profondeur interactive ne le justifie. CSS et Web Animations API couvrent les effets retenus sans GSAP, Motion, Lenis ou Three.js.
- Playwright est ajouté en dépendance de développement. Deux profils Chromium, desktop et Pixel 7, vérifient l'accueil, le défilement, la navigation, le mouvement réduit, la pause et le débordement. Les captures de recette restent locales dans `test-results/` et sont ignorées par Git.

## Vérifications

`npm run check:web` : typecheck shared, lint, typecheck WebPortal et build Next.js réussis. Le lint conserve trois avertissements préexistants hors de ce périmètre.

`npm run test:seo`, `test:brand`, `test:public-site-quality` et `apps/webportal test:navigation` : réussis.

`apps/webportal test:assistance`, `test:public-copy`, `test:managed-content` et `test:ux` : réussis.

`npm --prefix apps/webportal run test:e2e` : 4 scénarios réussis sur desktop et mobile. La première exécution a détecté le débordement CMS, corrigé avant la seconde recette. Captures desktop et mobile examinées.

La recette navigateur tourne avec le mock de développement, sans API-INTERNAL réelle. Elle ne valide donc pas les tarifs Billing V2, les paiements, l'authentification, le rendu d'un CMS distant ou une mesure de Core Web Vitals en production. Les prérequis SEO et la sécurité de routage sont vérifiés par les contrats de dépôt ; aucune livraison n'a eu lieu.

## Dynamisme éditorial livré sur DEV

Une passe supplémentaire a utilisé les skills `motion-design`, `scroll-storytelling`, `interactive-components`, `animation-performance`, `premium-hero` et `playwright`, avec trois revues Terra High en lecture seule. Les constats retenus comme valides étaient le rail dupliqué et figé, les déplacements de texte au survol, l'absence de réponse équivalente au focus, les View Timelines appliquées aux tarifs/formulaires et les marges manquantes dans la variante CMS `.site-page-core`. La troncature exacte du rail n'était d'abord qu'incertaine, puis le nouveau rendu mobile a confirmé la lisibilité des quatre domaines. Le test mobile de pause a d'abord échoué parce que le menu était refermé au rechargement : **FAUX POSITIF** de recette, corrigé dans le test. L'introduction opaque de moins de deux secondes répond à la demande explicite du commanditaire ; elle laisse passer les interactions et se termine sur action.

Le rail contient maintenant quatre libellés uniques et visibles, en quatre colonnes puis deux sur mobile, avec une entrée séquentielle unique. Les trois repères du héros apparaissent à leur tour. La section Méthode trace un filet lime au défilement et ses numéros avancent brièvement. Les cartes d'audience, de services et d'orientation donnent un retour au survol et au focus par des filets et des flèches, sans modifier leur padding. Les pages Services reçoivent des accents similaires. Les animations au défilement restent réservées aux sections éditoriales ; les cartes de prix, formulaires et actions de commande restent stables. Sans View Timeline ou avec mouvement réduit, les contenus restent visibles.

Validation locale : `npm run check:web` réussi, trois avertissements lint préexistants ; contrats Assistance, SEO, contenus gérés, qualité publique, navigation et garde-fou secrets réussis. `npm --prefix apps/webportal run test:e2e` : **8/8 scénarios** sur Chromium desktop et mobile, incluant les quatre domaines du rail, la stabilité du padding des lignes au focus et la persistance de la pause après rechargement. Le mock local ne valide pas les intégrations internes.

Release Web DEV active : `/opt/kermaria/releases-dev/webportal-dev-dynamic-story-pr1-20261030`. Archive `C:\Users\zhounsah\Documents\Dev\_artifacts\dynamic-story-pr1-dev-20261030\release.tar.gz`, SHA-256 `C2533838B8C760CC85CE7B97DC295E8A73B9A295C451D8FAB81A9209A8F6630E`. Script de bascule et retour automatique vers `webportal-dev-home-intro-pr1-20261030-v2` dans le même dossier. La recette navigateur DEV a constaté les quatre domaines du rail, une marge de 51 px sur desktop et 20 px sur mobile pour la Méthode CMS, quatre catégories et six problèmes sur Services, 36 cartes Tarifs sans animation de View Timeline, aucune erreur console et aucun débordement sur les vues vérifiées. La pause et le mouvement réduit désactivent les nouveaux effets. Un échantillon laboratoire initial LCP/CLS sur desktop était aberrant (5,77 s / 0,867) ; cinq chargements desktop suivants ont donné 80 à 292 ms et CLS 0. Cela ne constitue pas une mesure terrain ni une preuve de performance générale. Aucun formulaire, panier ou paiement n'a été soumis. PROD, API et base n'ont pas été modifiés.

## Pourquoi s'abonner — hub Assistance DEV

Le hub `/assistance-maintenance` comporte désormais un bandeau éditorial entre les deux cartes d'audience et les étapes de choix. Il rappelle que la prestation à la carte convient à un besoin ponctuel, puis explique l'intérêt de l'abonnement par trois éléments présents dans les formules : 15/45/90 minutes d'assistance à distance chaque mois, un périmètre d'ordinateurs ou de postes défini, et des contrôles ou une maintenance selon le niveau. Deux liens conduisent aux pages Particuliers et Professionnels. Le contenu ne promet ni économie, ni intervention illimitée, ni délai de réponse et n'introduit aucun prix statique. Le catalogue Billing V2 reste la source des montants et le mode devis reste actif.

Validation locale : `npm run check:web`, `test:assistance`, `test:public-site-quality` et `test:e2e` réussis. La suite Playwright compte maintenant 10 scénarios sur Chromium desktop et mobile ; le nouveau test vérifie la section, ses trois bénéfices, l'absence de débordement et le lien Particuliers. La capture mobile est lisible en une colonne. Navigateur DEV : section visible, aucune erreur console observée, lien Particuliers vers les prix 9,90/19,90/39,90 € TTC et lien Professionnels vers 19/39/69 € HT indicatifs par poste. Aucun formulaire ni paiement n'a été soumis.

Release Web DEV active : `/opt/kermaria/releases-dev/webportal-dev-assistance-value-pr1-20261030`. Archive `C:\Users\zhounsah\Documents\Dev\_artifacts\assistance-value-pr1-dev-20261030\release.tar.gz`, SHA-256 `E55C5E8F6E553F17D8D153E7F43AC50ECDB95E56E9CA0C256F8892929E9EABFE`. Script de bascule et retour automatique vers `webportal-dev-dynamic-story-pr1-20261030` dans le même dossier. Seul le WebPortal DEV a été redémarré ; PROD, API, base et configuration DEV n'ont pas été modifiés.
