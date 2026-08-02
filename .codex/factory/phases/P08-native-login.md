# P08 — Connexion native sans JavaScript

```factory-phase
{
  "id": "P08",
  "order": 9,
  "title": "Fallback natif de connexion",
  "kind": "application",
  "dependencies": ["P07"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "fix(webportal): restaurer le formulaire natif de connexion",
  "allowedPaths": ["apps/webportal/app/api/auth/login/route.ts", "apps/webportal/app/login/page.tsx", "apps/webportal/components/LoginForm.tsx", "apps/webportal/scripts/verify-auth-contract.mjs"],
  "validations": [
    { "name": "auth-contract", "executable": "npm.cmd", "arguments": ["--prefix", "apps/webportal", "run", "test:auth"] },
    { "name": "forms-contract", "executable": "npm.cmd", "arguments": ["--prefix", "apps/webportal", "run", "test:forms"] },
    { "name": "web-check", "executable": "npm.cmd", "arguments": ["run", "check:web"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": ["HG-PUBLIC-CONTRACT"]
}
```

Le POST JSON et le POST `application/x-www-form-urlencoded` doivent conserver
les mêmes règles d'authentification. Les erreurs et redirections 303 ne doivent
jamais placer e-mail, token ou cookie dans une URL.
