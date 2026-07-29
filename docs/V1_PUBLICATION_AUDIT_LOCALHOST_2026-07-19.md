# Audit publication V1 - localhost:3000

Date d'audit : 2026-07-19  
Surface vérifiée : webportal local `http://localhost:3000`

## Conclusion

Le site n'est pas encore viable pour une publication V1 publique en l'état.

Le socle technique est encourageant : la vitrine se rend correctement après redémarrage du runtime, les pages principales répondent, le parcours `/offres -> /signup?pack=...` transporte bien le pack choisi, `/api/health/ready` est sain, `typecheck:webportal` passe et `build:webportal` compile.

Mais plusieurs éléments restent bloquants pour une V1 publique :

- le runtime web avait crashé pendant l'audit initial ;
- le lint webportal échoue ;
- toutes les routes renvoient `X-Robots-Tag: noindex, nofollow` ;
- la politique de confidentialité contient encore un placeholder public ;
- le formulaire d'inscription expose des libellés internes de version et d'infrastructure ;
- quelques textes publics ont des formulations ou accents à corriger ;
- le premier viewport mobile est dense et repousse l'action principale trop bas.

## Vérifications réalisées

Routes vérifiées au rendu navigateur :

- `/`
- `/offres`
- `/contact`
- `/signup`
- `/signup?pack=pack-dossier-securise&commitment=1&payment=monthly`
- `/set-password`
- `/a-propos`
- `/mentions-legales`
- `/politique-confidentialite`
- `/cgv`

Commandes exécutées :

```powershell
curl.exe -i --max-time 10 http://localhost:3000/api/health/ready
npm run typecheck:webportal
npm run lint:webportal
npm run build:webportal
```

Résultats :

- `GET /api/health/ready` : OK, `configuration=healthy`, `api_internal=healthy`.
- `npm run typecheck:webportal` : OK.
- `npm run build:webportal` : OK, build Next.js compilé et pages générées.
- `npm run lint:webportal` : KO, 27 erreurs et 1 warning.
- Navigateur desktop : page d'accueil, offres et signup pack visibles, sans erreur console.
- Navigateur mobile 390px : pas d'overflow horizontal détecté, mais header très chargé et CTA principal repoussé en bas du premier écran.

## Blocants avant V1 publique

### 1. Runtime web à stabiliser avant recette

Symptôme observé : `localhost:3000` acceptait les connexions mais ne renvoyait aucune page au début de l'audit. Après redémarrage du runtime, le rendu est redevenu correct.

Recommandations :

- ajouter un préflight de publication qui vérifie `GET /`, `/offres`, `/signup`, `/api/health/live` et `/api/health/ready` avec timeout court ;
- garder les logs Next.js du runtime dans l'artefact de recette ;
- documenter la procédure de redémarrage et le critère de succès avant bascule ;
- ne pas publier si une route HTML reste suspendue même quand `/api/health/live` répond.

### 2. Lint webportal en échec

Le lint échoue notamment sur :

- `apps/webportal/app/admin/signups/[id]/page.tsx`
- `apps/webportal/app/downloads/page.tsx`
- `apps/webportal/app/password/page.tsx`
- `apps/webportal/app/signup/page.tsx`
- `apps/webportal/components/AdminDownloadForm.tsx`
- `apps/webportal/components/HeaderCartDrawer.tsx`
- `apps/webportal/components/SignupForm.tsx`

Points principaux :

- apostrophes JSX non échappées ;
- `HeaderCartDrawer.tsx` appelle `setState` synchroniquement dans un `useEffect` ;
- dépendance manquante dans un `useEffect`.

Recommandations :

- corriger les apostrophes JSX avec `&apos;` ou reformuler les chaînes ;
- reprendre `HeaderCartDrawer` pour éviter le reset d'état synchrone dans l'effet de changement de route ;
- relancer `npm run lint:webportal` jusqu'au vert ;
- considérer le lint vert comme prérequis strict de V1.

### 3. Header global `noindex, nofollow`

Toutes les pages testées renvoient :

```http
X-Robots-Tag: noindex, nofollow
```

Source identifiée : `apps/webportal/next.config.ts`.

Impact :

- acceptable pour un portail privé, une recette ou un staging ;
- non adéquat pour une vitrine V1 publique devant être indexable ;
- incohérent si `/`, `/offres`, `/a-propos`, `/contact`, `/mentions-legales`, `/politique-confidentialite` et `/cgv` doivent être publiées comme site public.

Recommandations :

- rendre le header conditionnel selon l'environnement ou le hostname ;
- conserver `noindex, nofollow` sur les routes privées (`/dashboard`, `/admin`, espace client, routes portail) ;
- autoriser l'indexation seulement sur les routes vitrine publiques validées ;
- ajouter un test de non-régression sur les headers SEO des routes publiques et privées.

### 4. Politique de confidentialité incomplète

La page `/politique-confidentialite` contient encore :

```text
Contenu placeholder. La version définitive sera publiée avant la mise en production (V1.0 RC).
```

Elle contient aussi une adresse à compléter :

```text
[adresse e-mail à compléter]
```

Recommandations :

- remplacer le placeholder par une version relue et finalisée ;
- renseigner l'adresse de contact RGPD réelle ;
- vérifier la cohérence avec hCaptcha, e-mail, cookies de session, conservation des données, facturation et comptes client ;
- faire relire les pages légales avant publication.

### 5. Texte public trop interne dans le signup

La page `/signup` affiche au public :

- `Avec v0.38, cette étape prepare aussi l'identité cible sous clients.home.bzh.`
- `Si l'écriture AD est active, l'identité clients.home.bzh est finalisée à ce moment-là.`

Impact :

- la référence `v0.38` n'a pas sa place dans une V1 publique ;
- `clients.home.bzh`, AD et "écriture AD" exposent trop la mécanique interne ;
- cela peut inquiéter un client non technique au moment de créer son compte.

Recommandations :

- remplacer par une formulation orientée client, par exemple :
  - "Cette étape prépare votre compte client et les accès associés."
  - "Après validation, vous recevrez un lien pour définir votre mot de passe."
  - "Si vous avez choisi un pack, il restera associé à votre demande."
- garder les détails AD/KoXo uniquement dans les écrans admin ou la documentation interne.

### 6. Corrections rédactionnelles visibles

Occurrences publiques repérées :

- `selectionné` -> `sélectionné`
- `prepare` -> `prépare`
- `Complement d'adresse` -> `Complément d'adresse`
- `/politique-confidentialite` : `ce site n'utilisé aucun service` -> `ce site n'utilise aucun service`

Recommandations :

- faire une passe orthographe complète sur `/`, `/offres`, `/contact`, `/signup`, `/set-password`, `/a-propos`, `/mentions-legales`, `/politique-confidentialite`, `/cgv` ;
- ajouter un grep de garde avant V1 sur `v0.`, `placeholder`, `à compléter`, `selectionn`, `prepare`, `finalis`, `clients.home.bzh`.

## Améliorations recommandées avant publication

### 7. Premier écran mobile trop dense

Sur viewport mobile 390px :

- le header affiche logo, baseline, liens de navigation et CTAs sur deux lignes ;
- le nom `Zachary HOUNSA-HOUNKPA...` est tronqué ;
- le CTA principal de la hero arrive tout en bas du premier écran, le second CTA est sous le pli.

Recommandations :

- simplifier le header mobile : logo + nom court + bouton menu ou navigation réduite ;
- garder un seul CTA prioritaire visible dans le premier écran mobile ;
- raccourcir légèrement le texte hero mobile ou réduire les marges verticales ;
- vérifier `/offres` et `/signup` à 390px et 430px après correction.

### 8. Parcours pack lisible mais conversion encore perfectible

Points positifs :

- `/offres` explique les packs ;
- les liens `Choisir ce pack` transportent bien `pack`, `commitment` et `payment` ;
- `/signup?pack=...` affiche le résumé du pack, l'engagement, le paiement, le tarif et la première échéance.

Point à clarifier :

- le paiement ne se fait pas sur l'écran d'inscription, et le client doit comprendre qu'il finalisera ensuite depuis l'espace client.

Recommandations :

- conserver le résumé pack visible en haut de `/signup` ;
- ajouter une microcopie courte : "Aucun paiement maintenant" / "Vous finaliserez après validation du compte" ;
- éviter les termes internes dans cette explication ;
- vérifier que le mail de confirmation et l'espace client reprennent exactement le même pack.

### 9. Formulaire signup long pour un premier contact

Le formulaire demande dès l'inscription :

- type de structure ;
- raison sociale ;
- adresse complète ;
- identité utilisateur ;
- e-mail ;
- téléphone facultatif ;
- message facultatif.

Ce n'est pas incohérent si l'objectif est une préqualification sérieuse, mais cela peut freiner une conversion depuis une vitrine publique.

Recommandations :

- soit assumer une inscription qualifiée et l'expliquer clairement ;
- soit proposer deux chemins distincts :
  - "Demander un échange" avec formulaire court ;
  - "Créer mon compte" avec formulaire complet ;
- sur mobile, ajouter des sections lisibles : "Structure", "Adresse", "Contact", "Besoin".

## Checklist minimale avant V1

- [ ] Runtime local/staging stable sur `GET /`, `/offres`, `/signup`, `/api/health/live`, `/api/health/ready`.
- [ ] `npm run lint:webportal` vert.
- [ ] `npm run typecheck:webportal` vert.
- [ ] `npm run build:webportal` vert.
- [ ] Politique de confidentialité finalisée, sans placeholder ni champ à compléter.
- [ ] Headers robots adaptés : indexable pour vitrine publique, noindex pour portail privé.
- [ ] Suppression des mentions publiques `v0.38`, `AD`, `écriture AD`, `clients.home.bzh`.
- [ ] Passe orthographe sur les pages publiques.
- [ ] Validation mobile 390px et desktop du parcours `/ -> /offres -> /signup?pack=...`.
- [ ] Recette du formulaire contact et signup avec hCaptcha / e-mail selon l'environnement cible.

## Avis final

État actuel : publiable en recette privée ou beta fermée, pas en V1 publique.

Après correction des blocants ci-dessus, le site a une base solide : la proposition est compréhensible, le parcours pack fonctionne, et le build production passe. La priorité n'est pas de refaire le site, mais de retirer les marqueurs internes, finaliser le légal, rendre le préflight vert et alléger légèrement l'expérience mobile.
