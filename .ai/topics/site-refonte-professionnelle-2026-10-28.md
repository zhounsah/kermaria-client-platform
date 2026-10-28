---
name: site-refonte-professionnelle-2026-10-28
description: "Audit et refonte B2B de Zachary IT, autorité CMS, recette visuelle, livraison WebPortal DEV et limites de preuve."
---

# Refonte professionnelle Zachary IT — 28 octobre 2026

La branche de revue `codex/zachary-it-refonte-ready` part de `origin/main` à `dfc31c5` pour conserver les correctifs TVA/tarifs postérieurs au `main` local. Les constats et preuves sont dans `docs/audit-site.md`, le système visuel dans `docs/charte-site.md`, les tests et la livraison dans `docs/rapport-refonte-site.md`.

## Autorités à respecter

- L'accueil, le contact et le pied de page ont des mises en page publiées par `SitePageService` et rendues par `SitePageFrame`. Modifier seulement `app/page.tsx` ou `PublicShell` ne suffit pas si un layout CMS est présent. Les autres pages combinent contenu administrable, widgets obligatoires et catalogue serveur.
- Les projections WebPortal (`public-site-editorial.ts`, `storefront-content.ts`, `public-packs.ts`) ne remplacent que des formulations historiques exactes. Une valeur CMS personnalisée garde la priorité. Les défauts C# et les replis locaux sont alignés ; une base déjà peuplée ne change pas quand un seed Markdown ou C# change.
- Les prix, la taxe et le statut de commande restent servis par l'API et le moteur Billing. Le texte éditorial ne doit pas qualifier tous les prix de HT quand les composants montrent du TTC.
- Le lien `/vpn-ou-bureau-a-distance-que-choisir` était 404 en DEV avant la refonte. Une vraie page WebPortal, déclarée publique et au sitemap, le sert maintenant. Garder les liens du CMS et le guide synchronisés.
- Le `title.template` racine ajoute déjà `| Zachary IT` aux pages enfants. Un `seoTitle` CMS peut contenir ce suffixe ; `buildPublicMetadata` le retire avant le template pour éviter le doublon.

## Mesures et pièges de recette

- Le `app/loading.tsx` initial annonçait l'espace client sur la vitrine et laissait voir le footer avant le contenu public ; Lighthouse DEV mesurait un CLS d'environ 0,853. Réserver une hauteur d'écran pendant le chargement ramène le CLS du test DEV à environ 0,021. L'image d'accueil est séparée du texte, déclarée en HTML avec priorité de chargement et optimisée à 116 Ko.
- Sur une seule exécution Lighthouse mobile DEV avant/après, le score performance est passé de 48 à 75 et le LCP de 13,39 s à 6,04 s. Le LCP reste à améliorer. Le score SEO de 69 en DEV vient du `noindex` volontaire, pas d'un défaut à corriger en DEV. Les mesures de laboratoire ne valent pas preuve terrain.
- La recette locale a couvert 32 combinaisons route/résolution ; la recette DEV publique 20 vues et les comptes de démonstration admin/client 6 vues. Axe n'a plus signalé de violation sur ces vues après ajout des labels et du focus à l'éditeur de mise en page. Cela ne certifie pas WCAG 2.2 AA sans vérification manuelle.
- Le CSS global historique reste volumineux et chargé sur l'ensemble du portail. La refonte visuelle est dans `site-refonte.css`. Une extraction par groupes de routes doit être évaluée séparément pour ne pas casser les écrans client/admin.

## Frontière DEV/PROD constatée

Le WebPortal DEV actif est `/opt/kermaria/releases-dev/webportal-dev-zachary-ready-3e6ddc4`; l'environnement DEV existant a été préservé. Les mises en page accueil/contact/footer et les contenus À propos/Services/Offres ont été publiés par le BFF administrateur avec session et CSRF, sauvegardes JSON et relectures. Aucune migration SQL ni écriture directe MariaDB. L'API DEV n'a pas été redémarrée : sa configuration observée avait BPCE/e-mail live et AD `controlled_write`. La PROD est restée sur `v2.1.1-hotfix.5` ; revalider ces états avant toute autre opération.
