# P10 — Vitrine et tunnel pack/contact

```factory-phase
{
  "id": "P10",
  "order": 12,
  "title": "Vitrine et tunnel pack contact signup",
  "kind": "application",
  "dependencies": ["P10A"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "feat(webportal): restaurer le tunnel public des packs",
  "allowedPaths": ["apps/webportal/app/page.tsx", "apps/webportal/app/offres/page.tsx", "apps/webportal/app/contact/page.tsx", "apps/webportal/app/signup/page.tsx", "apps/webportal/app/globals.css", "apps/webportal/components/ContactForm.tsx", "apps/webportal/components/PublicPackCard.tsx", "apps/webportal/components/PublicPackComparisonTable.tsx", "apps/webportal/components/PublicPackOverviewGrid.tsx", "apps/webportal/components/PublicShell.tsx", "apps/webportal/components/SignupForm.tsx", "apps/webportal/lib/public-packs.ts", "apps/webportal/scripts/verify-commercial-foundation-contract.mjs", "apps/webportal/scripts/verify-signup-contract.mjs"],
  "validations": [
    { "name": "commercial-contract", "executable": "npm.cmd", "arguments": ["run", "test:commercial"] },
    { "name": "signup-contract", "executable": "npm.cmd", "arguments": ["run", "test:signup"] },
    { "name": "managed-content-contract", "executable": "npm.cmd", "arguments": ["run", "test:managed-content"] },
    { "name": "web-check", "executable": "npm.cmd", "arguments": ["run", "check:web"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": ["HG-PUBLIC-CONTRACT", "HG-BUSINESS"]
}
```

Conserver une sélection de pack dans le tunnel public sans introduire les
mutations `GET` exclues. Les snapshots de pack visibles et les paramètres de
contact/signup doivent être cohérents et bornés aux offres publiques connues.
