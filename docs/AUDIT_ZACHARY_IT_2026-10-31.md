# Zachary IT — audit et corrections locales du 31/10/2026

## Périmètre et état

Cette recette porte sur le worktree `kermaria-client-platform-zachary-ready`, branche `codex/zachary-it-refonte-ready` (base `794653b`). Il contenait déjà des changements non commités de la refonte et de l'assistance. Les corrections ci-dessous restent locales : aucun commit, push, déploiement, changement de tarif, migration, paiement ni envoi d'e-mail n'a été effectué. Le worktree principal `codex/frontend-premium-20261030`, qui porte également des modifications commerciales et API non commitées, n'a pas été modifié par cette mission.

Les 15 pages HTTP 200, 45 liens et huit pages sans exception JavaScript viennent de l'audit initial fourni par le propriétaire. Ces chiffres n'ont pas été remesurés ici. La recette navigateur locale utilise le fallback de développement, sans `API-INTERNAL` : elle ne prouve pas le rendu des catalogues réels, l'authentification ou la facturation sur DEV.

## P0 — conversion et premier affichage

**VALIDE :** la variante d'accueil présente dans le worktree affichait un écran d'introduction plein format avant le hero. Une capture au chargement ne montrait que le titre centré ; le descriptif, le CTA et l'illustration étaient masqués. Le `loading.tsx` reproduisait cette attente avec un titre seul sur fond vide. Le premier écran commercial pouvait donc rester pauvre, en particulier sur mobile.

**Correction :** le hero et son CTA « Demander un devis » sont maintenant accessibles dès le rendu, sans écran d'introduction. La typographie animée, le rail des services, les états de pause et `prefers-reduced-motion` restent en place. Le rendu d'attente reprend le même titre, le descriptif et le CTA à partir d'une source éditoriale locale commune. Fichiers : `app/page.tsx`, `app/loading.tsx`, `app/site-motion.css`, `components/VitrineHero.tsx`, `components/SitePageFrame.tsx`, `lib/home-hero-content.ts` dans `apps/webportal`. Le composant `HomeIntroMotion.tsx`, devenu inutilisé, a été retiré.

**VALIDE dans le worktree principal, déjà corrigé ici :** les cartes Assistance du worktree principal peuvent mener à `/contact` sans conserver le palier choisi. Le worktree de la refonte transporte déjà ce contexte et le contrôle `test:assistance` vérifie les six paliers. Il faut conserver cette correction lors de toute intégration entre branches.

**Parcours commercial vérifié localement :** accueil → devis, hub Assistance → formules particuliers, six paliers → contexte contact (contrat), navigation publique et vocabulaire des CTA (contrat). Les pages `/offres`, `/tarifs` et `/services` dépendent de l'API interne pour leur contenu complet ; la recette intégrée DEV doit confirmer leurs CTA, prix et liens avant livraison.

## P1 — diagnostic, accessibilité, sécurité et SEO

**VALIDE :** dans le formulaire « Être rappelé » du diagnostic, Nom et Téléphone étaient signalés par un astérisque sans attribut `required` sur les champs. Les erreurs renvoyées par le BFF n'étaient pas reliées aux contrôles, notamment au consentement. Les champs portent désormais `required` lorsqu'il s'applique, `aria-invalid` et `aria-describedby` pointant vers un message d'erreur identifié. Le test Playwright parcourt le diagnostic jusqu'au résultat en contexte réseau, ouvre le formulaire et simule une réponse HTTP 400 sans envoi réel. Fichier : `apps/webportal/components/PublicDiagnosticWizard.tsx`.

**VALIDE, déjà corrigé dans ce worktree :** le formulaire de contact lit les `field_errors` dans `response.details` et associe les erreurs aux champs. Le worktree principal lit encore `response.payload`, qui n'existe pas dans `requestBffJson`. Préserver la version du worktree refonte lors de l'intégration. Le test `test:forms` passe, mais aucun e-mail n'a été envoyé.

**VALIDE d'après la réponse HTTPS PROD observée pendant l'audit :** HSTS absent. **VALIDE, durcissement à étudier :** la CSP actuelle borne notamment `frame-ancestors`, `base-uri` et `form-action`, sans politique complète des sources de scripts. **VALIDE mineur sur DEV :** `X-Robots-Tag: noindex, nofollow` est émis deux fois. Les causes sont partagées entre application et proxy ; aucune modification de ces en-têtes n'a été appliquée sans inventaire des ressources, validation des sous-domaines et recette du point de terminaison TLS. Ne pas activer `includeSubDomains` ou `preload` par défaut. Les contrats SEO locaux passent ; la restriction d'accès réseau à DEV et l'absence de fuite de données ne sont pas établies par le seul `noindex`.

**INCERTAIN :** parité de prix TTC/HT, calcul du panier et facture sur le catalogue réel ; connexion, réinitialisation de mot de passe et droits sur DEV ; Stripe test de bout en bout et webhooks ; accessibilité WCAG 2.2 AA exhaustive ; SEO de toutes les URL publiées ; sécurité et dépendances sur l'ensemble du dépôt. Les tests de contrat exécutés ne remplacent pas ces essais intégrés.

## P2 — qualité et tests

**VALIDE :** `test:cart-anonymous-checkout` attendait l'ancien libellé « Reprendre mon VPS » alors que la page affiche « Reprendre mon serveur ». L'assertion a été alignée sur le libellé actuel, sans changement du tunnel de commande. Fichier : `apps/webportal/scripts/verify-anonymous-cart-checkout-contract.mjs`.

Deux scénarios Playwright ciblés ont été ajoutés : présence immédiate du CTA sur l'accueil, et erreurs accessibles du rappel diagnostic avec réponse serveur simulée. Une matrice vérifie l'absence de débordement horizontal à 320, 390, 768, 1024, 1440 et 1920 px sur l'accueil, le hub Assistance, le contact et le diagnostic. Les captures de l'accueil et de l'assistance à 320, 390, 1440 et 1920 px sont produites dans `apps/webportal/test-results/` lors de la recette, dossier temporaire ignoré par Git.

## Résultats de validation

| Contrôle | Résultat | Limite |
| --- | --- | --- |
| `npm run validate` | Réussi : secrets, lint, TypeScript, builds web/API, smoke API, contrats web et Billing V2 | Mock et tests contractuels ; avertissements lint et CA1416 existants |
| `npm --prefix apps/webportal run test:assistance` | Réussi | Catalogue simulé/local |
| `npm --prefix apps/webportal run test:cart-anonymous-checkout` | Réussi après correction du libellé attendu | Aucun checkout réel |
| `npm --prefix apps/webportal run test:seo` et `test:navigation` | Réussis | Configuration locale, pas preuve du proxy live |
| `npm exec playwright test -- --workers=1` depuis `apps/webportal` | 15 réussis, 1 ignoré (matrice exécutée sur Chromium desktop) | Chromium local, fallback sans API interne ; un essai parallèle a échoué une fois sur la persistance de la pause après rechargement, puis le scénario isolé et la suite séquentielle ont réussi |
| `npm audit --omit=dev --audit-level=high` | 0 vulnérabilité signalée | Dépendances de production du lockfile local uniquement ; ce n'est pas un audit applicatif |
| `scripts/check-memory-secrets.ps1` | Réussi | Contrôle de motifs, pas preuve d'absence de tout secret |
| `git diff --check` | Réussi | État de travail préexistant conservé |

Lighthouse et les Core Web Vitals **n'ont pas de mesure avant/après comparable** dans cette mission. Aucun serveur Chrome DevTools MCP ni binaire Lighthouse n'était disponible ; le fallback Next en mode développement ne représente pas les conditions de production. Les objectifs LCP ≤ 2,5 s, CLS ≤ 0,1, INP ≤ 200 ms et les scores 90/95/95 restent des cibles, pas des résultats acquis. Les mesures devront être faites sur une build optimisée, avec appareil, réseau, cache, lieu et nombre de répétitions documentés, puis confrontées aux données terrain si disponibles.

## Avant une mise en production

1. Intégrer les changements de la refonte et de l'assistance sans écraser les modifications commerciales/API du worktree principal ; faire une revue de diff et des prix approuvés.
2. Livrer en DEV seulement après revue du paquet, puis vérifier visuellement les six largeurs sur `/services`, `/offres`, `/tarifs`, `/formules/*`, `/offres/*`, le panier et la souscription avec le catalogue réel.
3. Tester avec des identités fictives autorisées : contact et rappel vers une boîte de test, inscription/connexion/réinitialisation, retour panier, Stripe **test** et webhooks idempotents. Aucun compte client ni moyen de paiement réel.
4. Comparer les montants affichés et ceux retournés par l'API avant checkout ; faire valider tout tarif, TVA, remise, engagement ou condition contractuelle par le propriétaire.
5. Mesurer Lighthouse/CWV sur une build de livraison et examiner console/réseau, liens internes, SEO/canoniques et captures desktop/mobile.
6. Inventorier les ressources nécessaires à la CSP, tester une politique `Report-Only`, puis planifier HSTS au point TLS avec les sous-domaines et le rollback vérifiés. Garder DEV non indexable et privé ; supprimer l'en-tête `X-Robots-Tag` redondant après identification de sa seconde source.
7. Obtenir l'accord explicite du propriétaire pour toute bascule PROD, tarif ou paiement réel. Conserver un chemin de retour et contrôler les en-têtes et parcours après déploiement.
