# P23 — Documentation R740xd

```factory-phase
{
  "id": "P23",
  "order": 25,
  "title": "Documentation cohérente de la cible R740xd",
  "kind": "documentation",
  "dependencies": ["P15", "P16", "P17", "P18", "P19", "P20", "P21"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "docs(infra): harmoniser la cible R740xd",
  "allowedPaths": ["README.md", "docs/ARCHITECTURE.md", "docs/DEPLOYMENT.md", "docs/DEPLOYMENT_R740XD_VM.md", "docs/NETWORK_RULES.md", "docs/PRODUCTION_DEPLOYMENT.md", "docs/v0.38/README.md", "docs/v0.38/V0.38_R740XD_CUTOVER_CHECKLIST.md", "scripts/r740xd-vm/README.md"],
  "validations": [
    { "name": "secrets-check", "executable": "npm.cmd", "arguments": ["run", "check:secrets"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": ["HG-NETWORK", "HG-DEPLOY", "HG-PROD-DEPENDENCY"]
}
```

Séparer explicitement architecture actuelle, source locale réintégrée, cible
future et état de déploiement non vérifié. Aucun serveur n'est déclaré actif ou
validé sans preuve datée obtenue sous porte humaine.
