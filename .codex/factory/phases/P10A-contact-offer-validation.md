# P10A — Validation serveur de l'offre contact

```factory-phase
{
  "id": "P10A",
  "order": 11,
  "title": "Validation serveur de la référence d'offre contact",
  "kind": "application",
  "dependencies": ["P09"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "fix(webportal): valider les offres du formulaire de contact",
  "allowedPaths": [
    "apps/webportal/app/api/contact/route.ts",
    "apps/webportal/scripts/verify-form-submissions.mjs"
  ],
  "validations": [
    { "name": "forms-contract", "executable": "npm.cmd", "arguments": ["--prefix", "apps/webportal", "run", "test:forms"] },
    { "name": "commercial-contract", "executable": "npm.cmd", "arguments": ["run", "test:commercial"] },
    { "name": "web-check", "executable": "npm.cmd", "arguments": ["run", "check:web"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": ["HG-PUBLIC-CONTRACT", "HG-BUSINESS"]
}
```

Valider côté serveur toute `offerReference` fournie contre le catalogue public
actif. Une référence absente reste autorisée. Une référence inconnue ou inactive
produit une réponse HTTP 400 stable et n'est jamais relayée à l'API interne.

Une indisponibilité du catalogue reste une indisponibilité de service et ne doit
pas être présentée comme une erreur de saisie. Aucun autre comportement du
formulaire contact, refactoring opportuniste ou changement hors allowlist n'est
autorisé.
