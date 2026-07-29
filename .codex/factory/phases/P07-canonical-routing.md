# P07 — Routage canonique des portails

```factory-phase
{
  "id": "P07",
  "order": 8,
  "title": "Routage canonique www dashboard administration",
  "kind": "application",
  "dependencies": ["P06"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "fix(webportal): restaurer le routage canonique des portails",
  "allowedPaths": ["apps/webportal/lib/public-route-config.ts", "apps/webportal/lib/public-routes.ts", "apps/webportal/app/page.tsx", "apps/webportal/app/login/page.tsx", "apps/webportal/app/api/auth/login/route.ts", "apps/webportal/components/LoginForm.tsx", "apps/webportal/scripts/verify-auth-contract.mjs"],
  "validations": [
    { "name": "auth-contract", "executable": "npm.cmd", "arguments": ["run", "test:auth"] },
    { "name": "typecheck-webportal", "executable": "npm.cmd", "arguments": ["run", "typecheck:webportal"] },
    { "name": "web-check", "executable": "npm.cmd", "arguments": ["run", "check:web"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": ["HG-PUBLIC-CONTRACT"]
}
```

La matrice hôte/zone/rôle est testée explicitement. Les URL relatives ne sont
pas utilisées lorsqu'elles conserveraient le mauvais hôte, et toute cible de
redirection reste bornée aux portails configurés.
