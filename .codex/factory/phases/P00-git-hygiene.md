# P00 — Hygiène Git

```factory-phase
{
  "id": "P00",
  "order": 0,
  "title": "Hygiène Git",
  "kind": "repository",
  "dependencies": [],
  "initialStatus": "DONE",
  "requiresCommit": true,
  "commitMessage": "fix(git): restaurer l'encodage de gitignore",
  "allowedPaths": [".gitignore"],
  "validations": [
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": []
}
```

Objectif atteint par `7c07b05`, `e60a609` et `0c2be86` : ignorer les artefacts
locaux sans masquer les sources. Aucun travail supplémentaire n'est attendu.
