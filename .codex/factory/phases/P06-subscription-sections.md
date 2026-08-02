# P06 — Mutualisation de la souscription

```factory-phase
{
  "id": "P06",
  "order": 7,
  "title": "Mutualisation des sections de souscription",
  "kind": "application",
  "dependencies": ["P06A"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "refactor(webportal): mutualiser les sections de souscription",
  "allowedPaths": ["apps/webportal/app/souscrire/page.tsx", "apps/webportal/components/SubscribeCatalogSections.tsx"],
  "validations": [
    { "name": "typecheck-webportal", "executable": "npm.cmd", "arguments": ["run", "typecheck:webportal"] },
    { "name": "subscriptions-contract", "executable": "npm.cmd", "arguments": ["run", "test:subscriptions"] },
    { "name": "web-check", "executable": "npm.cmd", "arguments": ["run", "check:web"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": []
}
```

Réutiliser le composant existant sans modifier les règles d'offre, de panier ou
d'abonnement. Le refactoring doit rester comportementalement neutre.
