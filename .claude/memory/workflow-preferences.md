---
name: workflow-preferences
description: "How the user prefers to collaborate — branching strategy, commit style, and session workflow preferences for this project."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 316dd2c1-620c-4ba1-833b-0b5d317971ba
---

Travailler directement sur `main` plutot que dans un worktree de branche separee pour les features solo sans revue de PR.

**Why:** Les worktrees separent les `node_modules` du repo principal, ce qui casse Next.js/Turbopack en dev (impossible de trouver `next/package.json`). Le test de l'UI devient plus complique. Pour ce projet mono-dev, les branches ne sont pas utiles.

**How to apply:**
- Toujours commencer par `git checkout main` et travailler directement dessus.
- Si Claude Code cree automatiquement un worktree, merger en fast-forward dans `main` des que le travail est fini et supprimer le worktree.
- Ne pas proposer de branche feature sauf si l'utilisateur le demande explicitement (ex: "fais-le dans une branche").
