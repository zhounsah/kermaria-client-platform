# P26 — Sources juridiques canoniques

```factory-phase
{
  "id": "P26",
  "order": 28,
  "title": "Clarification des sources juridiques canoniques",
  "kind": "legal-documentation",
  "dependencies": ["P22"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "chore(legal): clarifier les sources juridiques canoniques",
  "allowedPaths": ["CGV.txt", "mentions-légales.txt", "apps/api-internal/SeedContent/cgv.md", "apps/api-internal/SeedContent/mentions-legales.md", "README.md"],
  "validations": [
    { "name": "api-build", "executable": "npm.cmd", "arguments": ["run", "build:api"] },
    { "name": "secrets-check", "executable": "npm.cmd", "arguments": ["run", "check:secrets"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": ["HG-LEGAL"]
}
```

Cette phase est obligatoirement précédée de `HG-LEGAL`. L'usine peut démontrer
que les fichiers racine sont des doublons et identifier les consommateurs, mais
ne peut ni choisir la source officielle ni modifier/supprimer le fond juridique
sans validation explicite.
