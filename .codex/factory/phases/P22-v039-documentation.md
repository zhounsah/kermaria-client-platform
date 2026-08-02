# P22 — Documentation V0.39

```factory-phase
{
  "id": "P22",
  "order": 24,
  "title": "Documentation V0.39 et audit localhost",
  "kind": "documentation",
  "dependencies": ["P10", "P11", "P13"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "docs(v0.39): documenter le tunnel public réintégré",
  "allowedPaths": ["docs/V0.39_VITRINE_TUNNEL_PUBLIC.md", "docs/V1_PUBLICATION_AUDIT_LOCALHOST_2026-07-19.md", "docs/IMPLEMENTATION_MAP_CURRENT.md", "README.md"],
  "validations": [
    { "name": "web-check", "executable": "npm.cmd", "arguments": ["run", "check:web"] },
    { "name": "commercial-contract", "executable": "npm.cmd", "arguments": ["run", "test:commercial"] },
    { "name": "signup-contract", "executable": "npm.cmd", "arguments": ["run", "test:signup"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": ["HG-PUBLIC-CONTRACT", "HG-BUSINESS"]
}
```

Documenter uniquement les comportements effectivement présents et revalidés.
L'audit localhost reste une preuve historique datée, jamais une preuve staging
ou production. Corriger son whitespace avant restauration de contenu utile.
