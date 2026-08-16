---
name: workflow-preferences
description: "How the user prefers to collaborate — branching strategy, commit style, and session workflow preferences for this project."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 316dd2c1-620c-4ba1-833b-0b5d317971ba
  modified: 2026-08-06T12:43:20.448Z
---

Travailler directement sur `main` plutot que dans un worktree de branche separee pour les features solo sans revue de PR.

**Why:** Les worktrees separent les `node_modules` du repo principal, ce qui casse Next.js/Turbopack en dev (impossible de trouver `next/package.json`). Le test de l'UI devient plus complique. Pour ce projet mono-dev, les branches ne sont pas utiles.

**Cause racine confirmee (2026-08-03) :** meme avec un `npm install` fait DANS le worktree, `next build` (Turbopack) ECHOUE sous Windows sur une erreur MAX_PATH : le prefixe `.claude/worktrees/<nom>/` ajoute ~55 car. et fait depasser 260 car. sur les manifests generes des routes profondes (ex. `app/api/admin/customers/[customerReference]/ad/groups/[groupSamAccountName]/members/[userSamAccountName]/route_client-reference-manifest.js`). En revanche `tsc --noEmit` (typecheck:shared + typecheck:webportal) et `eslint` (lint:webportal) tournent SANS PROBLEME dans le worktree. Donc pour verifier du frontend en worktree : se fier a typecheck+lint ; le `build:web`/dev ne passera que dans le checkout principal (chemin plus court). Le backend .NET (dotnet build/test) fonctionne, lui, dans le worktree.

**Contournement build confirme (2026-08-04) :** `npx next build --webpack` depuis `apps/webportal` PASSE dans le worktree (le builder webpack n'ecrit pas les manifests par-route trop longs de Turbopack). Utile pour valider un build complet sans quitter le worktree. Une jonction Windows vers un chemin court (`mklink /J C:\kwt <worktree>`) ne sert a rien : Turbopack resout le lien vers le chemin reel et rate quand meme.

**Nuance verifiee le 2026-08-05 :** ce qui echoue, c'est le lien, pas le chemin court. Un **vrai repertoire** court fait passer le build Turbopack par defaut : `scripts/pack-webportal-release.ps1 -GitRef <tag> -WorktreePath C:\wbr` extrait le snapshot git dans `C:\wbr`, y lance `npm ci` + `next build`, et produit l'archive sans toucher a MAX_PATH. C'est la voie a privilegier pour fabriquer un paquet de release depuis un worktree — pas besoin de basculer sur `main` ni de retomber sur `--webpack`. Le script nettoie `-WorktreePath` en sortie.

**Impasse a ne pas retenter (2026-08-06) :** deplacer la sortie du build vers un chemin court via `distDir` ne marche pas non plus — Turbopack refuse tout `distDir` qui sort du `projectPath` (`Invalid distDirRoot: … should not navigate out of the projectPath`). La jonction et le `distDir` sont donc deux culs-de-sac ; seule la copie dans un **vrai** repertoire court fonctionne, cf. le paragraphe precedent.

**Piege a verifier en debut de session (vecu le 2026-08-05) :** le dossier de
reference est `C:\Users\zhounsah\Documents\Dev\kermaria-client-platform` — c'est
lui qui contient le vrai `.git`. Il s'est retrouve checkout sur une branche morte
(`chore/remise-a-plat-agentique`, 0 commit unique, 44 de retard) pendant que
`main` etait checkout dans un worktree cache `C:\kmw` cree le 2026-08-03. D'ou la
sensation, cote utilisateur, de « ne jamais savoir ou j'en suis » : ouvrir son
dossier habituel montrait un etat fige. Remis d'aplomb le 2026-08-05 (dossier
principal rebascule sur `main`, `C:\kmw` supprime). Un worktree qui accapare
`main` empeche aussi tout `git checkout main` ailleurs — c'est le symptome a
reconnaitre. Reflexe : `git worktree list` + `git rev-parse --abbrev-ref HEAD`
avant de conclure quoi que ce soit sur l'etat du depot. Il restait ~15 autres
worktrees (`_deploy_*`, `_koxo_*`, releases) non nettoyes.

**How to apply:**
- Toujours commencer par `git checkout main` et travailler directement dessus.
- Si Claude Code cree automatiquement un worktree, merger en fast-forward dans `main` des que le travail est fini et supprimer le worktree.
- Ne pas proposer de branche feature sauf si l'utilisateur le demande explicitement (ex: "fais-le dans une branche").
