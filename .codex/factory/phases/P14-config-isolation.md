# P14 — Séparation des configurations

```factory-phase
{
  "id": "P14",
  "order": 14,
  "title": "Séparation des configurations API et WEBPORTAL",
  "kind": "tooling",
  "dependencies": ["P13"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "fix(config): renforcer la séparation API et webportal",
  "allowedPaths": ["scripts/build-api-config.ps1", "scripts/build-webportal-config.ps1", "tests/config/**"],
  "validations": [
    { "name": "config-tests", "executable": "powershell", "arguments": ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "tests/config/test-config-boundaries.ps1"] },
    { "name": "secrets-check", "executable": "npm.cmd", "arguments": ["run", "check:secrets"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": ["HG-SECRET", "HG-PUBLIC-CONTRACT"]
}
```

Les générateurs doivent conserver `AD_DOMAIN`, mots de passe, tokens et autres
valeurs internes hors de la configuration WEBPORTAL. Les tests créés dans cette
phase utilisent uniquement des valeurs factices et vérifient les blocklists.
