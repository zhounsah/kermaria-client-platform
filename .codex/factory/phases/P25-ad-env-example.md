# P25 — Exemple de configuration AD

```factory-phase
{
  "id": "P25",
  "order": 25,
  "title": "Exemple de configuration Active Directory",
  "kind": "configuration",
  "dependencies": ["P24"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "chore(config): aligner l'exemple Active Directory",
  "allowedPaths": [".env.example", "tests/config/test-config-boundaries.ps1"],
  "validations": [
    { "name": "config-tests", "executable": "powershell", "arguments": ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "tests/config/test-config-boundaries.ps1"] },
    { "name": "ad-security-contract", "executable": "npm.cmd", "arguments": ["run", "test:ad-security"] },
    { "name": "secrets-check", "executable": "npm.cmd", "arguments": ["run", "check:secrets"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": ["HG-AD-REAL", "HG-SECRET", "HG-BUSINESS"]
}
```

L'exemple ne contient que des placeholders non sensibles et reflète la décision
P24. Il ne doit jamais être utilisé pour deviner un DN, un compte ou une ACL de
production.
