# P06A — Remise au vert de la baseline web

```factory-phase
{
  "id": "P06A",
  "order": 6,
  "title": "Remise au vert de la baseline lint web",
  "kind": "application",
  "dependencies": ["P05"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "fix(webportal): restaurer une baseline lint propre",
  "allowedPaths": [
    "apps/webportal/app/admin/signups/`[id`]/page.tsx",
    "apps/webportal/app/downloads/page.tsx",
    "apps/webportal/app/password/page.tsx",
    "apps/webportal/app/signup/page.tsx",
    "apps/webportal/components/AdminDownloadForm.tsx",
    "apps/webportal/components/HeaderCartDrawer.tsx",
    "apps/webportal/components/SignupForm.tsx"
  ],
  "validations": [
    { "name": "lint-no-inline-bypass", "executable": "./node_modules/.bin/eslint.cmd", "arguments": ["apps/webportal/app/admin/signups", "apps/webportal/app/downloads/page.tsx", "apps/webportal/app/password/page.tsx", "apps/webportal/app/signup/page.tsx", "apps/webportal/components/AdminDownloadForm.tsx", "apps/webportal/components/HeaderCartDrawer.tsx", "apps/webportal/components/SignupForm.tsx", "--config", "apps/webportal/eslint.config.mjs", "--no-inline-config", "--max-warnings", "0"] },
    { "name": "admin-contract", "executable": "npm.cmd", "arguments": ["run", "test:admin"] },
    { "name": "downloads-contract", "executable": "npm.cmd", "arguments": ["run", "test:downloads"] },
    { "name": "signup-contract", "executable": "npm.cmd", "arguments": ["run", "test:signup"] },
    { "name": "ux-contract", "executable": "npm.cmd", "arguments": ["run", "test:ux"] },
    { "name": "ad-security-contract", "executable": "npm.cmd", "arguments": ["run", "test:ad-security"] },
    { "name": "cart-contract", "executable": "npm.cmd", "arguments": ["run", "test:cart"] },
    { "name": "subscriptions-contract", "executable": "npm.cmd", "arguments": ["run", "test:subscriptions"] },
    { "name": "web-check", "executable": "npm.cmd", "arguments": ["run", "check:web"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": ["HG-PUBLIC-CONTRACT", "HG-BUSINESS"]
}
```

Corriger exclusivement les diagnostics lint préexistants des sept fichiers,
sans changement fonctionnel volontaire, sans refactoring opportuniste, sans
désactivation ou contournement de règle et sans modification de configuration.

Une correction qui exige un changement fonctionnel, une modification de
contrat public, une désactivation de règle ou un fichier hors allowlist arrête
la phase sur une nouvelle porte humaine avant toute application.
