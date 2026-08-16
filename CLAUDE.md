@AGENTS.md
@.ai/MEMORY.md

## Mémoire partagée

La source de mémoire durable commune à Claude Code et Codex est le dossier `.ai/`.

- Lire `.ai/MEMORY.md` au début de chaque tâche.
- Lire les fichiers `.ai/topics/` pertinents si nécessaire.
- Considérer l'auto-mémoire native de Claude comme un cache secondaire.
- Lorsqu'une information durable utile aux futures sessions est découverte, la promouvoir dans `.ai/`.
- Ne jamais enregistrer de secret dans la mémoire partagée.