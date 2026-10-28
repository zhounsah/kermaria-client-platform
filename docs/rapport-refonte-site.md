# Rapport de refonte — Zachary IT

**Date :** 28 octobre 2026

**Branche de revue :** `codex/zachary-it-refonte-ready`, issue de `origin/main` à `dfc31c5` ; dernier commit applicatif `3e6ddc4`.

**Périmètre livré en DEV :** WebPortal et contenus éditoriaux via BFF administrateur. Aucune livraison API, migration SQL ou modification PROD.

## Résultat

L'accueil annonce désormais les prestations et les publics professionnels dès le premier écran. Les pages Services, Offres, Tarifs et Contact utilisent une hiérarchie typographique commune, des listes et séparateurs lisibles, des boutons explicites et moins de cartes décoratives. Les textes commerciaux ont été repris sans inventer de client, prix, engagement ou certification. Les détails et prix des offres restent soumis au catalogue et au moteur serveur.

Le constructeur de pages, les fiches de service et les espaces authentifiés conservent leurs routes et actions. Une page de comparaison VPN/bureau Windows à distance répond maintenant au lien public qui renvoyait 404. Les anciens contenus CMS connus sont projetés uniquement par égalité exacte ; les personnalisations restent prioritaires. Les valeurs administrables concernées ont aussi été publiées sur DEV par le BFF, avec snapshots locaux avant écriture et relecture après chaque changement.

## Fichiers et choix principaux

| Groupe | Modifications |
| --- | --- |
| Direction visuelle | `apps/webportal/app/site-refonte.css` : tokens, mise en page, composants publics, responsive et états de focus. `network-office-editorial.webp` remplace l'image d'accueil d'environ 2,14 Mo par un visuel illustratif de 116 Ko. La charte et la revue des concepts sont dans [charte-site.md](charte-site.md). |
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

### Performance mesurée en DEV

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

## Livraison DEV et limites de déploiement

- Release WebPortal DEV active : `/opt/kermaria/releases-dev/webportal-dev-zachary-ready-3e6ddc4`. Archive `.tar.gz` vérifiée : SHA-256 `A57587D9B0B50D970412313BE791F9DFFC407254A828CE8C1787AC1D8D9449A6`.
- Release DEV précédente conservée : `/opt/kermaria/releases-dev/webportal-dev-zachary-ready-f34c61a`. L'environnement `/etc/kermaria/webportal-dev.env` garde son SHA-256 `8cc1ff4ed17deb2623fbca6ae8ef33a3507969d897899ea7d7d28ea1e8f5b185`. `https://dev.zachary-it.fr/api/health/ready` répond 200.
- Le CMS DEV a reçu des versions publiées de l'accueil, du contact et du pied de page (`0 → 1`), le nouveau texte À propos, la page Services et l'éditorial des offres. Les sauvegardes JSON avant/après sont dans le dossier local de recette `C:\Users\zhounsah\Documents\Dev\zachary-it-qa\`, hors Git. Les mises à jour sont passées par le BFF admin avec session et CSRF ; aucun accès direct à MariaDB n'a été utilisé.
- L'API DEV n'a pas été redémarrée : son runtime annonçait BPCE et e-mail en mode live et `AD_INTEGRATION_MODE=controlled_write`. Son service reste `Running` et son JSON conserve le SHA-256 `85BD80D22B21F6E76DA7BD251BC73E447F91CA9538BECEBB00FB2755A81C350F`. Les nouvelles valeurs par défaut C# de la branche seront disponibles lors d'une livraison API distincte et sécurisée ; les valeurs persistées DEV nécessaires ont été publiées par le BFF.
- La PROD est restée sur `/opt/kermaria/releases/v2.1.1-hotfix.5-dfc31c5-prod`, service WebPortal `active`, service API `Running`, accueil HTTP 200 et ancien titre inchangé. Aucun déploiement, merge, migration ni paiement PROD n'a été effectué.

En cas de régression DEV WebPortal, remettre le lien `/opt/kermaria/webportal-dev` sur la release DEV précédente, puis redémarrer **uniquement** `kermaria-webportal-dev.service` et revérifier `/api/health/ready`. Les contenus CMS ont leurs snapshots locaux et les mises en page leur historique versionné ; une restauration se fait via l'administration après vérification des versions et du périmètre, sans écriture SQL directe. Aucun retour arrière n'a été nécessaire pendant cette livraison.
