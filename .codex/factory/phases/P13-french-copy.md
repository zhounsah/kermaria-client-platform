# P13 — Textes français et mojibake

```factory-phase
{
  "id": "P13",
  "order": 15,
  "title": "Corrections ciblées des textes français",
  "kind": "application",
  "dependencies": ["P12"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "fix(webportal): corriger les textes français visibles",
  "allowedPaths": ["apps/webportal/app/admin/customers/[customerReference]/page.tsx", "apps/webportal/app/admin/signups/[id]/page.tsx", "apps/webportal/app/downloads/page.tsx", "apps/webportal/app/layout.tsx", "apps/webportal/app/panier/page.tsx", "apps/webportal/app/politique-confidentialite/page.tsx", "apps/webportal/app/profile/page.tsx", "apps/webportal/components/AdminCustomerAdManager.tsx", "apps/webportal/components/AdminDownloadForm.tsx", "apps/webportal/components/AdminRequestFilters.tsx", "apps/webportal/components/AdminSubscriptionProvisioningManager.tsx", "apps/webportal/components/RequestStatusBadge.tsx", "apps/webportal/components/RequestTimeline.tsx", "apps/webportal/components/SessionStatusBadge.tsx", "apps/webportal/components/StatusChangeForm.tsx"],
  "validations": [
    { "name": "typecheck-webportal", "executable": "npm.cmd", "arguments": ["run", "typecheck:webportal"] },
    { "name": "lint-webportal", "executable": "npm.cmd", "arguments": ["run", "lint:webportal"] },
    { "name": "web-check", "executable": "npm.cmd", "arguments": ["run", "check:web"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": []
}
```

Corriger uniquement les chaînes rendues et les mojibakes prouvés. Les routes,
clés JSON, valeurs de statut, variables, types et symboles restent inchangés.
Une revue visuelle locale complète la vérification statique lorsqu'elle est
disponible, sans être présentée comme une preuve staging.
