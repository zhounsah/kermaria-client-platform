# P02 — Règles multi-agents

```factory-phase
{
  "id": "P02",
  "order": 2,
  "title": "Règles Git et multi-agents",
  "kind": "repository",
  "dependencies": ["P01"],
  "initialStatus": "DONE",
  "requiresCommit": true,
  "commitMessage": "docs(agents): encadrer Git et l'orchestration multi-agents",
  "allowedPaths": ["AGENTS.md"],
  "validations": [
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": []
}
```

Objectif atteint par `65d3087` : Git, ownership, revues et restrictions sont
versionnés dans `AGENTS.md`.
