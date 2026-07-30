# P11 — Reprise du pack dans le dashboard

```factory-phase
{
  "id": "P11",
  "order": 13,
  "title": "Reprise du pack en attente dans le dashboard",
  "kind": "application",
  "dependencies": ["P10"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "feat(webportal): reprendre le pack en attente dans le dashboard",
  "allowedPaths": ["apps/webportal/app/dashboard/page.tsx", "apps/webportal/lib/public-packs.ts", "apps/webportal/scripts/verify-client-ux-contract.mjs"],
  "validations": [
    { "name": "ux-contract", "executable": "npm.cmd", "arguments": ["run", "test:ux"] },
    { "name": "commercial-contract", "executable": "npm.cmd", "arguments": ["run", "test:commercial"] },
    { "name": "web-check", "executable": "npm.cmd", "arguments": ["run", "check:web"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": ["HG-BUSINESS"]
}
```

Le dashboard propose la reprise uniquement lorsqu'une sélection valide existe.
Une erreur de catalogue ou de profil ne doit pas rendre toute la page inutilisable.
