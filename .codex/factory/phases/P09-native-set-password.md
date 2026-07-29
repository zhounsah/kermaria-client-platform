# P09 — Set-password natif

```factory-phase
{
  "id": "P09",
  "order": 10,
  "title": "Fallback natif set-password",
  "kind": "application",
  "dependencies": ["P08"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "fix(webportal): restaurer le formulaire natif set-password",
  "allowedPaths": ["apps/webportal/app/api/set-password/route.ts", "apps/webportal/app/set-password/page.tsx", "apps/webportal/app/password/page.tsx", "apps/webportal/components/SetPasswordForm.tsx", "apps/webportal/scripts/verify-signup-contract.mjs"],
  "validations": [
    { "name": "signup-contract", "executable": "npm.cmd", "arguments": ["run", "test:signup"] },
    { "name": "forms-contract", "executable": "npm.cmd", "arguments": ["--prefix", "apps/webportal", "run", "test:forms"] },
    { "name": "web-check", "executable": "npm.cmd", "arguments": ["run", "check:web"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": ["HG-PUBLIC-CONTRACT", "HG-AD-REAL"]
}
```

Présenter succès, token invalide/expiré, rate-limit et erreur serveur après un
POST natif, sans changer le contrat JSON ni effectuer d'accès AD réel pendant la
phase. `HG-AD-REAL` ne s'active que si une validation exige effectivement AD.
