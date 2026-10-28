# Rapport de refonte — Zachary IT

**Date :** 28 octobre 2026

**Branche de revue :** `codex/zachary-it-refonte-ready`, issue de `origin/main` à `dfc31c5`. La première livraison WebPortal DEV correspond au commit applicatif `3e6ddc4` ; la révision animée est décrite ci-dessous.

**Périmètre livré en DEV :** WebPortal et contenus éditoriaux via BFF administrateur. Aucune livraison API, migration SQL ou modification PROD.

## Résultat

### Extension de l'effet à partir de l'enregistrement mobile

Le film fourni le 28 octobre à 14 h 05 montre une ligne de titre qui change par séquences derrière un masque. L'accueil utilise maintenant trois phrases reliées aux prestations réellement présentes : l'informatique « suit votre activité », « sécurise vos accès » et « relie vos équipes ». Le premier message reste le nom accessible du H1 ; les variantes visuelles ne sont pas annoncées en boucle aux lecteurs d'écran. L'emplacement est réservé à hauteur fixe pour éviter que les boutons ou l'illustration ne sautent au changement de phrase. Les titres H1 des autres pages publiques entrent au chargement, et les H2/H3, listes, cartes d'offres, sections de contact et fiches de service avancent au défilement. La connexion et les espaces authentifiés ne reçoivent qu'une courte entrée des titres et panneaux.

Cette extension est réalisée en CSS sans dépendance JavaScript supplémentaire. Une préférence `prefers-reduced-motion: reduce` fige la première phrase et supprime les mouvements. Un titre CMS librement personnalisé reste affiché tel quel. Les contrôles locaux ont couvert les trois positions de la ligne à 390 et 1440 px, les largeurs 320/390/768/1440 px, sept familles de pages intérieures, 32 vues de parcours avec menu/formulaire/filtre et 20 vues axe-core sans violation automatisable. Le film de recette [effet-cyclique-dev-390.mp4](audit-captures/effet-cyclique-dev-390.mp4) montre le cycle et le défilement sur la release DEV finale.

La release WebPortal DEV active est `/opt/kermaria/releases-dev/webportal-dev-zachary-ready-8ed2c7f` (commit `8ed2c7f`, archive SHA-256 `DBC445B72C56F2DD2173CDF8549EA8D56FA8458D0D3DBF734EF807864624E7BA`). L'archive, la release précédente `23e15b2`, le lien PROD et les services ont été contrôlés avant et après la bascule. DEV répond 200 à `/api/health/ready`, son fichier d'environnement garde le même SHA-256, et PROD reste sur `/opt/kermaria/releases/v2.1.1-hotfix.5-dfc31c5-prod` avec son service actif. L'API DEV n'a pas été redémarrée et aucune migration SQL n'a été effectuée.

L'ancien titre persisté dans le constructeur de pages a été aligné par le BFF administrateur avec session, CSRF et version attendue : la mise en page accueil est passée de la version **1 à 2**, avec pour seule différence de bloc « Votre informatique suit votre activité. ». Les snapshots avant/après sont conservés hors Git dans `C:\Users\zhounsah\Documents\Dev\zachary-it-qa\`. Le contrôle intégré qui naviguait ensuite vers la page publique depuis la session admin a échoué parce que l'application a redirigé cette session ; la relecture BFF et une visite anonyme indépendante ont confirmé le titre publié. Une capture ultérieure de l'éditeur montre aussi le nouveau libellé dans son champ.

Après cette livraison, Playwright a contrôlé **20 vues publiques DEV** à 390 et 1440 px : HTTP 200, un H1, pas d'erreur console ni de débordement ; le menu mobile s'ouvre. Les comptes de démonstration ont ouvert `/admin`, `/admin/page-builder` et `/dashboard` sans erreur JavaScript ni débordement mobile. Axe-core ne signale aucune violation automatisable sur ces 20 vues publiques ni sur les six vues authentifiées. Playwright WebKit 27.2 sous Windows a également vérifié le cycle à 390 px et son arrêt avec `prefers-reduced-motion`; ce contrôle n'est pas une recette sur un iPhone physique.

Une mesure Lighthouse mobile supplémentaire (version 13.6.0) donne performance **74**, accessibilité **100**, SEO DEV **69**, LCP **5,74 s**, CLS **0,021**, FCP **2,57 s** et TBT **131 ms**. La mesure unique de la release précédente donnait 75, 5,73 s et 104 ms respectivement ; l'écart ne permet pas de conclure à une régression terrain. Le `noindex` volontaire de DEV explique toujours le score SEO. Le CSS global historique reste la principale ressource à réduire dans un chantier distinct.

### Révision visuelle après retour du commanditaire

Le premier rendu a été jugé trop neutre. Après examen de [la référence indiquée](https://enzonaute.me/), l'accueil a été repris avec un titre plus direct, une typographie beaucoup plus présente, une illustration réseau originale, un index des services en mouvement et une alternance de sections ivoire, bleu et bleu nuit. Les entrées, les survols et le défilement sont animés ; `prefers-reduced-motion` supprime ces effets. Les contenus, prix, contacts et parcours métier restent fondés sur les mêmes sources.

`VitrineHero` unifie le rendu par défaut et celui du CMS. Le visuel `network-illustration-motion.webp` pèse environ 48 Ko ; le précédent visuel de 116 Ko, désormais inutilisé, a été retiré. `site-motion.css` contient les mouvements et la nouvelle composition publique au-dessus du socle `site-refonte.css`. La [charte actualisée](charte-site.md) documente les tokens, les choix, les limites et la comparaison avec les concepts.

La première implémentation des apparitions modifiait le DOM avant la fin de l'hydratation ; un avertissement React a conduit à la remplacer par des CSS View Timelines, avec affichage statique sur les navigateurs non compatibles. Un autre essai diminuait l'opacité du texte et échouait au contrôle de contraste pendant l'animation ; les mouvements finaux ne réduisent plus l'opacité. Le bandeau défilant a aussi reçu une contrainte de largeur pour empêcher un débordement sur tablette et ordinateur.

| Vérification de la révision locale | Résultat observé |
| --- | --- |
| `npm run validate` | Réussi après mise à jour du test de contrat pour le héros partagé. Les trois avertissements ESLint préexistants restent sans erreur. |
| Playwright, huit routes × quatre largeurs (320, 390, 768, 1440 px) | 32 vues : HTTP 200, un H1, aucun débordement global ni erreur console. Menu mobile, navigation Offres, formulaire vide et filtre Tarifs exercés. |
| Axe-core, dix routes à 390 et 1440 px | Zéro violation automatisable sur 20 vues publiques locales et 20 vues publiques DEV ; ce contrôle ne remplace pas une revue manuelle WCAG 2.2 AA. |
| Mouvement normal et réduit | `hero-enter`, `rail-flow` et `view()` observés en mode normal ; `none`, `none` et `auto` avec réduction des mouvements. La première ligne de services passe de sa position décalée à sa position finale au défilement. |

Les nouvelles captures locales sont `docs/audit-captures/apres-animation-*.webp` ; les deux captures `apres-animation-dev-accueil-*` montrent la même page avec son CMS réellement publié sur DEV. Elles se comparent aux captures `apres-accueil-*` de la première refonte et `avant-accueil-*` de l'état initial. La compilation locale utilise une API de développement en persistance mock, avec AD, BPCE, e-mail et paiements désactivés.

La release de la première révision animée était `/opt/kermaria/releases-dev/webportal-dev-zachary-ready-23e15b2`, issue du commit `23e15b2`. Son archive `.tar.gz` avait été vérifiée avant et après transfert (SHA-256 `13F9B3655BD20480890C78E09ABF130C129B1D84D85C3084A6C7E2AC1A7ADD3A`). La release intermédiaire `52970b2` a été conservée. Le service DEV était `active`, `/api/health/ready` répondait 200, et le SHA de `/etc/kermaria/webportal-dev.env` restait `8cc1ff4ed17deb2623fbca6ae8ef33a3507969d897899ea7d7d28ea1e8f5b185`. L'API DEV n'avait pas été redémarrée. Le lien PROD restait `/opt/kermaria/releases/v2.1.1-hotfix.5-dfc31c5-prod`, son service `active` et l'accueil PROD répondait 200 ; aucune release ni donnée PROD n'avait été changée.

Sur cette première release animée, Playwright a recontrôlé 20 vues publiques DEV à 390 et 1440 px : HTTP 200, un H1, pas de débordement ni d'erreur console ; le menu mobile s'ouvre. Les comptes de démonstration ont ouvert `/admin`, `/admin/page-builder` et `/dashboard` sans erreur JavaScript ni débordement mobile. Axe-core ne signale aucune violation sur ces six vues authentifiées. Ce parcours ne déclenche ni commande, ni facture, ni mutation sensible.

### Première livraison

L'accueil annonce désormais les prestations et les publics professionnels dès le premier écran. Les pages Services, Offres, Tarifs et Contact utilisent une hiérarchie typographique commune, des listes et séparateurs lisibles, des boutons explicites et moins de cartes décoratives. Les textes commerciaux ont été repris sans inventer de client, prix, engagement ou certification. Les détails et prix des offres restent soumis au catalogue et au moteur serveur.

Le constructeur de pages, les fiches de service et les espaces authentifiés conservent leurs routes et actions. Une page de comparaison VPN/bureau Windows à distance répond maintenant au lien public qui renvoyait 404. Les anciens contenus CMS connus sont projetés uniquement par égalité exacte ; les personnalisations restent prioritaires. Les valeurs administrables concernées ont aussi été publiées sur DEV par le BFF, avec snapshots locaux avant écriture et relecture après chaque changement.

## Fichiers et choix principaux

| Groupe | Modifications |
| --- | --- |
| Direction visuelle initiale | `apps/webportal/app/site-refonte.css` : tokens, mise en page, composants publics, responsive et états de focus. Le premier visuel illustratif de 116 Ko a ensuite été remplacé pendant la révision animée. La charte et la revue des concepts sont dans [charte-site.md](charte-site.md). |
| Parcours publics | Accueil, coque publique, services, offres et tarifs, contact, page de comparaison des accès distants, métadonnées, sitemap et pied de page. Le formulaire signale les champs invalides et y déplace le focus sans envoyer de demande vide. |
| CMS et contenus | Valeurs initiales de l'API pour accueil/contact/pied de page, services et catalogue d'offres, texte « À propos », repli WebPortal et projections exactes d'anciens libellés. Le titre SEO d'un contenu CMS déjà suffixé ne duplique plus la marque. |
| Espace client/admin | Espacements et contrôles homogènes. Le constructeur de pages a des labels visibles pour le type de bloc et le fichier, plus un aperçu nommé et accessible au clavier. |
| Sécurité et dépendances | Next.js et `eslint-config-next` passent de 16.3.8 à 16.4.3 ; `npm audit` ne signale plus de vulnérabilité dans l'arbre installé. Aucun contrat de prix ni frontière BFF/API n'est déplacé. |

L'[audit initial](audit-site.md) classe chaque constat comme valide, faux positif ou incertain avec sa preuve. Les captures locales comparatives sont dans [audit-captures](audit-captures/).

## Vérifications

| Contrôle | Résultat observé | Limite de preuve |
| --- | --- | --- |
| `npm run validate` sur la branche intégrée `origin/main` | Réussi sur le commit applicatif final ; typecheck, lint, build, smoke API et contrats web/Billing V2. | Les smoke API utilisent la persistance mock ; aucune base MariaDB réelle n'a été exercée ici. |
| `npm audit` après installation | 0 vulnérabilité signalée. | C'est l'état du registre au moment du contrôle, pas une garantie future. |
| Playwright local | 32 combinaisons de huit routes à 320, 390, 768 et 1440 px : HTTP 200, un H1, aucun débordement global ni erreur console. Menu mobile, navigation vers Offres, filtre Tarifs et formulaire vide exercés. | API locale en mode mock, sans paiement ni envoi réel. |
| Playwright DEV public | 20 vues sur dix routes à 390 et 1440 px : HTTP 200, un H1, aucun débordement ni erreur console. Le guide d'accès distant répond 200 ; le menu mobile s'ouvre. | Échantillon de routes et de résolutions, pas tous les états du catalogue. |
| Playwright DEV authentifié | Connexion des comptes de démonstration admin et client, `/admin`, `/admin/page-builder` et `/dashboard` chargés. L'éditeur affiche le nouvel accueil ; pas de débordement mobile ni d'erreur JavaScript. | Lecture et navigation seulement ; aucune commande, facturation, remise de fichier ou action admin sensible n'a été déclenchée. |
| axe-core DEV, WCAG 2.0/2.1/2.2 A et AA | 0 violation automatisable sur les 20 vues publiques et six vues client/admin examinées. | Un contrôle manuel au clavier et avec lecteur d'écran reste nécessaire pour conclure à la conformité WCAG 2.2 AA complète. |
| Contact | Un envoi vide affiche trois erreurs associées aux champs requis, `aria-invalid`, et place le focus sur le nom. | Aucun message réel n'a été envoyé. |

### Performance de la première livraison mesurée en DEV

Lighthouse mobile a été exécuté une fois avant la livraison sur `https://dev.zachary-it.fr/`, puis une fois sur la release finale `3e6ddc4`, avec les mêmes options. Ces valeurs sont des **mesures de laboratoire ponctuelles** ; elles ne remplacent pas les Core Web Vitals de visiteurs réels.

| Métrique | Avant | Après |
| --- | ---: | ---: |
| Score performance | 48 | 75 |
| Largest Contentful Paint | 13,39 s | 6,04 s |
| Cumulative Layout Shift | 0,853 | 0,021 |
| First Contentful Paint | 2,26 s | 2,42 s |
| Total Blocking Time | 74 ms | 65 ms |
| Score accessibilité | 100 | 100 |
| Score SEO | 69 | 69 |

Le score SEO DEV de 69 est expliqué par le seul audit Lighthouse en échec, `is-crawlable` : le `noindex` de DEV est volontaire. Le LCP reste lent dans ce profil mobile. Sur la compilation locale, Lighthouse signalait encore le CSS global historique comme ressource bloquante (environ 259 Ko de CSS principal et 95 % de règles non utilisées sur l'accueil), malgré le nouveau visuel léger et prioritaire. Une extraction de ce CSS par familles de routes demanderait une recette supplémentaire de l'ensemble client/admin ; ce lot conserve donc la feuille historique pour les fonctionnalités non refondues. Chrome DevTools MCP n'était pas disponible ; Lighthouse CLI et Playwright ont servi à cette mesure et à la vérification visuelle.

Une exécution Lighthouse mobile supplémentaire sur cette release (`23e15b2`, version 13.6.0) donne performance **75**, accessibilité **100**, SEO **69**, LCP **5,73 s**, CLS **0,021**, FCP **2,41 s** et TBT **104 ms**. Par rapport à l'exécution unique de la première livraison, le score est stable, le LCP est plus court et le TBT plus élevé. Ces écarts de laboratoire ponctuels ne prouvent pas une amélioration ni une régression réelle ; les données terrain restent indisponibles.

## Livraison DEV et limites de déploiement

- Première release WebPortal DEV : `/opt/kermaria/releases-dev/webportal-dev-zachary-ready-3e6ddc4`. Archive `.tar.gz` vérifiée : SHA-256 `A57587D9B0B50D970412313BE791F9DFFC407254A828CE8C1787AC1D8D9449A6`. La release animée active est documentée dans la section de révision ci-dessus.
- Release DEV antérieure conservée : `/opt/kermaria/releases-dev/webportal-dev-zachary-ready-f34c61a`. L'environnement `/etc/kermaria/webportal-dev.env` garde son SHA-256 `8cc1ff4ed17deb2623fbca6ae8ef33a3507969d897899ea7d7d28ea1e8f5b185`. `https://dev.zachary-it.fr/api/health/ready` répond 200.
- Le CMS DEV a reçu des versions publiées de l'accueil, du contact et du pied de page (`0 → 1`), le nouveau texte À propos, la page Services et l'éditorial des offres. Les sauvegardes JSON avant/après sont dans le dossier local de recette `C:\Users\zhounsah\Documents\Dev\zachary-it-qa\`, hors Git. Les mises à jour sont passées par le BFF admin avec session et CSRF ; aucun accès direct à MariaDB n'a été utilisé.
- L'API DEV n'a pas été redémarrée : son runtime annonçait BPCE et e-mail en mode live et `AD_INTEGRATION_MODE=controlled_write`. Son service reste `Running` et son JSON conserve le SHA-256 `85BD80D22B21F6E76DA7BD251BC73E447F91CA9538BECEBB00FB2755A81C350F`. Les nouvelles valeurs par défaut C# de la branche seront disponibles lors d'une livraison API distincte et sécurisée ; les valeurs persistées DEV nécessaires ont été publiées par le BFF.
- La PROD est restée sur `/opt/kermaria/releases/v2.1.1-hotfix.5-dfc31c5-prod`, service WebPortal `active`, service API `Running`, accueil HTTP 200 et ancien titre inchangé. Aucun déploiement, merge, migration ni paiement PROD n'a été effectué.

En cas de régression DEV WebPortal, remettre le lien `/opt/kermaria/webportal-dev` sur la release DEV précédente `/opt/kermaria/releases-dev/webportal-dev-zachary-ready-23e15b2`, puis redémarrer **uniquement** `kermaria-webportal-dev.service` et revérifier `/api/health/ready`. Les contenus CMS ont leurs snapshots locaux et les mises en page leur historique versionné ; une restauration se fait via l'administration après vérification des versions et du périmètre, sans écriture SQL directe. Aucun retour arrière n'a été nécessaire pendant cette livraison.
