# P12 — Tiroir panier

```factory-phase
{
  "id": "P12",
  "order": 13,
  "title": "Navigation et accessibilité du tiroir panier",
  "kind": "application",
  "dependencies": ["P11"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "fix(webportal): stabiliser le tiroir panier",
  "allowedPaths": ["apps/webportal/components/HeaderCartDrawer.tsx", "apps/webportal/scripts/verify-cart-contract.mjs"],
  "validations": [
    { "name": "cart-contract", "executable": "npm.cmd", "arguments": ["run", "test:cart"] },
    { "name": "subscriptions-contract", "executable": "npm.cmd", "arguments": ["run", "test:subscriptions"] },
    { "name": "web-check", "executable": "npm.cmd", "arguments": ["run", "check:web"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": []
}
```

Stabiliser ouverture, fermeture, navigation et focus sans changer les mutations
de panier, les prix ou les règles de souscription.
