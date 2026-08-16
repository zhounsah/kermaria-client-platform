# Mémoire partagée Codex + Claude Code

Ce paquet a été construit à partir des deux archives fournies le 2026-08-15.

## Installation dans le dépôt Kermaria

1. Copier le dossier `.ai/` à la racine du dépôt.
2. Fusionner le contenu de `AGENTS.memory-snippet.md` dans l'`AGENTS.md` existant (ne pas écraser ses autres règles).
3. Fusionner `CLAUDE.memory-snippet.md` dans `CLAUDE.md`.
4. Copier `scripts/check-memory-secrets.ps1` dans le dossier `scripts/` du dépôt.
5. Lancer :

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\check-memory-secrets.ps1
```

6. Committer `.ai/`, les changements `AGENTS.md` / `CLAUDE.md` et le script.

## Utilisation

Codex lit `AGENTS.md`, qui lui demande de consulter `.ai/MEMORY.md`. Claude Code importe `AGENTS.md` et `.ai/MEMORY.md` via `CLAUDE.md`. Les deux agents travaillent donc sur la même mémoire durable versionnée.

Ne pas faire charger automatiquement `archive/codex-memory-full.md` : utiliser `rg` pour retrouver un détail précis.

Voir `.ai/MERGE_REPORT.md` pour les contradictions et arbitrages détectés.
