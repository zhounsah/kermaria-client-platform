# P24 — Documentation AD

```factory-phase
{
  "id": "P24",
  "order": 25,
  "title": "Alignement documentaire Active Directory",
  "kind": "documentation",
  "dependencies": ["P19", "P23"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "docs(ad): réaligner la migration clients.home.bzh",
  "allowedPaths": ["docs/AD_PRODUCTION_MIGRATION.md", "docs/ARCHITECTURE.md", "docs/v0.38/README.md", "docs/v0.38/V0.38_KOXO_AUTOMATION_RUNBOOK.md", "docs/v0.38/V0.38_KOXO_DATA_CONTRACTS.md", "docs/v0.38/V0.38_KOXO_SIGNUP_INTEGRATION.md", "docs/v0.38/V0.38_R740XD_CUTOVER_CHECKLIST.md", "docs/v0.38/V0.38_SITE_AD_ALIGNMENT.md", "README.md"],
  "validations": [
    { "name": "ad-security-contract", "executable": "npm.cmd", "arguments": ["run", "test:ad-security"] },
    { "name": "signup-contract", "executable": "npm.cmd", "arguments": ["run", "test:signup"] },
    { "name": "api-build", "executable": "npm.cmd", "arguments": ["run", "build:api"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": ["HG-AD-REAL", "HG-KOXO", "HG-BUSINESS"]
}
```

L'analyse doit comparer `SignupService`, `ActiveDirectoryPathScope`, la migration
034 et les contrats KoXo. Le DN, les ACL, les groupes et le moment de
provisionnement réels exigent une décision humaine si les sources versionnées ne
les déterminent pas sans ambiguïté.
