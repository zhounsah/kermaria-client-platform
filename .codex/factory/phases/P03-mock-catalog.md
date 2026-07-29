# P03 — Catalogue mock

```factory-phase
{
  "id": "P03",
  "order": 3,
  "title": "Catalogue mock non persistant",
  "kind": "application",
  "dependencies": ["P02"],
  "initialStatus": "DONE",
  "requiresCommit": true,
  "commitMessage": "fix(api): restaurer le fallback du catalogue mock",
  "allowedPaths": ["apps/api-internal/Services/ClientServiceCatalogService.cs", "tests/api-internal/Program.cs"],
  "validations": [
    { "name": "api-smoke", "executable": "npm.cmd", "arguments": ["run", "test:api"] },
    { "name": "api-build", "executable": "npm.cmd", "arguments": ["run", "build:api"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": []
}
```

Objectif atteint par `3286578` : les données mock ne sont renvoyées que si les
deux repositories concernés sont non persistants.
