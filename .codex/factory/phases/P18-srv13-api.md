# P18 — Installation API SRV-13

```factory-phase
{
  "id": "P18",
  "order": 18,
  "title": "Installation API et service SRV-13",
  "kind": "infrastructure-source",
  "dependencies": ["P15"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "feat(infra): ajouter l'installation API SRV-13",
  "allowedPaths": ["scripts/r740xd-vm/srv13/install-api-internal-service.ps1", "scripts/r740xd-vm/srv13/install-dotnet-runtime.ps1", "tests/infra/test-srv13-install.ps1"],
  "validations": [
    { "name": "srv13-install-tests", "executable": "powershell", "arguments": ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "tests/infra/test-srv13-install.ps1"] },
    { "name": "api-build", "executable": "npm.cmd", "arguments": ["run", "build:api"] },
    { "name": "secrets-check", "executable": "npm.cmd", "arguments": ["run", "check:secrets"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": ["HG-NETWORK", "HG-DEPLOY", "HG-PROD-DEPENDENCY"]
}
```

La source exige un compte dédié, vérifie les codes de retour, les ACL, le
stockage persistant et le rollback. `LocalSystem` ne peut pas être le choix par
défaut implicite. La phase ne lance aucun installateur ni service réel.
