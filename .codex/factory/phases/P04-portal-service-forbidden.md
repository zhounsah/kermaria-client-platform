# P04 — Refus PortalService HTTP 403

```factory-phase
{
  "id": "P04",
  "order": 4,
  "title": "Refus PortalService HTTP 403",
  "kind": "application",
  "dependencies": ["P03"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "fix(api): refuser les services hors périmètre avec HTTP 403",
  "allowedPaths": ["apps/api-internal/Services/PortalService.cs", "tests/api-internal/Program.cs"],
  "validations": [
    { "name": "api-smoke", "executable": "npm.cmd", "arguments": ["run", "test:api"] },
    { "name": "workflow-contract", "executable": "npm.cmd", "arguments": ["run", "test:workflow"] },
    { "name": "api-build", "executable": "npm.cmd", "arguments": ["run", "build:api"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": ["HG-PUBLIC-CONTRACT"]
}
```

## Objectif et acceptation

Un identifiant syntaxiquement valide mais inaccessible au client doit lever le
refus d'accès déjà traduit en HTTP 403 par l'API. Les payloads invalides restent
des erreurs de validation. Le test existant autour de `Program.cs:1081` doit
passer sans élargir l'accès ni modifier d'autre service.

`HG-PUBLIC-CONTRACT` ne s'active que si l'analyse démontre qu'un consommateur
versionné exige le statut précédent ; le test existant en faveur de 403 est une
preuve contraire à examiner en premier.
