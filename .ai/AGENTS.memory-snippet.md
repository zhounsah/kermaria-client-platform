# Bloc à intégrer dans AGENTS.md

## Mémoire partagée Codex / Claude Code

Ce dépôt utilise `.ai/` comme mémoire durable commune à tous les agents.

Au début d'une tâche :
1. Lire `.ai/MEMORY.md`.
2. Lire uniquement les `topics/*.md` pertinents.
3. Si nécessaire, rechercher l'historique avec `rg -n -i "<mot-clé>" .ai/archive`.
4. Revalider les faits dépendant de la production, des versions ou de l'infrastructure avant de les utiliser.

À la fin d'une tâche importante :
1. Promouvoir dans `.ai/topics/` les découvertes durables utiles aux sessions futures.
2. Mettre à jour `.ai/MEMORY.md` si l'état courant ou l'index change.
3. Ne jamais mémoriser de secret.
4. Exécuter `powershell -ExecutionPolicy Bypass -File scripts/check-memory-secrets.ps1`.

La mémoire native de l'agent est secondaire : en cas de contradiction, le code / les tests / l'état live puis `.ai/` priment.
