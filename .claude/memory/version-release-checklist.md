---
name: version-release-checklist
description: "Règle de livraison : à chaque version (Vx.y ou Vx.y.z), mettre à jour TOUTE la documentation projet (README.md, docs/ROADMAP.md, doc dédiée docs/Vx.y_*.md) ET la mémoire (MEMORY.md + roadmap-current.md) avant le commit/push. Pas de version livrée sans documentation à jour."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: d0b41c3b-4ff3-4905-9529-990629f89fbd
---

À chaque livraison de version (Vx.y nouvelle ou Vx.y.z hotfix), la doc et
la mémoire doivent être synchronisées avant le commit/push final.

**Why:** demandé explicitement par l'utilisateur le 2026-06-28 — il en a
marre de devoir me rappeler de mettre à jour README/ROADMAP/mémoire à
chaque livraison. Sans rappel, j'oubliais surtout le README.md (les
exemples de commandes y vivent et deviennent rapidement obsolètes) et
parfois les renumérotations en cascade dans `docs/V0.xx_*.md` /
`DEPLOYMENT.md` / `SECURITY.md`. Une doc périmée = un user qui ne sait
plus où on en est.

**How to apply:** à chaque commit qui clôt une version (livraison
fonctionnelle, hotfix, polish), passer cette checklist **avant** le
commit final :

1. **`README.md`** — section "Developpement local", scripts npm,
   variables d'env, liste des modes par défaut. Tout exemple de
   commande doit fonctionner tel quel.
2. **`docs/ROADMAP.md`** — ajouter la version livrée dans "Livré et
   figé", déplacer/renommer les jalons "À venir" si la séquence
   change, ajuster l'intro "Phase de tests : principe" si une borne
   change (versions hardware-gated, etc.).
3. **`docs/Vx.y_*.md`** — doc dédiée à la version (architecture,
   migrations, contrats API, limites assumées, tests). Suit la
   convention des fichiers existants (`V0.20_BPCE_INVOICING.md`,
   `V0.22_SUBSCRIPTIONS.md`, etc.).
4. **Refs croisées** — quand on renumérote (ex. V0.24a → V0.24, V0.24b
   → V1.0 beta 1), faire un `grep -rE "V0\.xx[ab]?" docs/ apps/` et
   propager dans `DEPLOYMENT.md`, `SECURITY.md`, les autres
   `docs/V0.xx_*.md` qui citent la borne.
5. **Mémoire** — mettre à jour [[roadmap-current]] (snapshot complet)
   et le lien dans MEMORY.md (entrée index, doit refléter le dernier
   livré et la prochaine étape).

Si l'utilisateur valide une livraison ("je valide" / "ok pour push"),
considérer cette checklist comme implicite : exécuter avant de
committer ou pousser, même sans demande explicite.

Voir aussi [[roadmap-current]] pour la séquence courante des versions.
