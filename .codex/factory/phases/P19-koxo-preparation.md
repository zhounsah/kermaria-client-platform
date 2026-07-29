# P19 — Préparation KoXo

```factory-phase
{
  "id": "P19",
  "order": 19,
  "title": "Préparation KoXo prospective",
  "kind": "infrastructure-source",
  "dependencies": ["P18"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "feat(infra): préparer l'intégration KoXo en mode sûr",
  "allowedPaths": ["scripts/r740xd-vm/srv13/bootstrap-koxo-exchange.ps1", "tests/infra/test-koxo-bootstrap.ps1"],
  "validations": [
    { "name": "koxo-whatif-tests", "executable": "powershell", "arguments": ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "tests/infra/test-koxo-bootstrap.ps1"] },
    { "name": "secrets-check", "executable": "npm.cmd", "arguments": ["run", "check:secrets"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": ["HG-KOXO", "HG-AD-REAL", "HG-DEPLOY"]
}
```

La source peut être préparée avec paramètres obligatoires, `SupportsShouldProcess`
et tests `-WhatIf`. Tout choix réel de disque, compte, ACL, jonction ou échange
KoXo active la porte humaine avant production ou exécution.
