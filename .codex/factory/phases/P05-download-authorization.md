# P05 — Droits de téléchargement

```factory-phase
{
  "id": "P05",
  "order": 5,
  "title": "Droits de téléchargement par services actifs",
  "kind": "application",
  "dependencies": ["P04"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "fix(api): aligner les droits de téléchargement sur les services actifs",
  "allowedPaths": ["apps/api-internal/Services/DownloadService.cs", "tests/api-internal/Program.cs", "apps/webportal/scripts/verify-downloads-contract.mjs"],
  "validations": [
    { "name": "api-smoke", "executable": "npm.cmd", "arguments": ["run", "test:api"] },
    { "name": "downloads-contract", "executable": "npm.cmd", "arguments": ["run", "test:downloads"] },
    { "name": "api-build", "executable": "npm.cmd", "arguments": ["run", "build:api"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": ["HG-PUBLIC-CONTRACT"]
}
```

Centraliser le calcul des services actifs autorisés sans permettre à un client
d'accéder au téléchargement d'un autre service ou d'un autre client. Les cas
autorisé, inactif, inconnu et cross-customer sont obligatoires.
