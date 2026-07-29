# P16 — Reverse proxy SRV-11

```factory-phase
{
  "id": "P16",
  "order": 17,
  "title": "Reverse proxy SRV-11",
  "kind": "infrastructure-source",
  "dependencies": ["P15"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "feat(infra): ajouter le reverse proxy SRV-11",
  "allowedPaths": ["scripts/r740xd-vm/deploy-srv11.py", "scripts/r740xd-vm/srv11/**", "tests/infra/test_srv11.py"],
  "validations": [
    { "name": "python-syntax-srv11", "executable": "python", "arguments": ["-m", "py_compile", "scripts/r740xd-vm/deploy-srv11.py"] },
    { "name": "srv11-unit-tests", "executable": "python", "arguments": ["-m", "unittest", "tests.infra.test_srv11"] },
    { "name": "secrets-check", "executable": "npm.cmd", "arguments": ["run", "check:secrets"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": ["HG-NETWORK", "HG-DEPLOY", "HG-PROD-DEPENDENCY"]
}
```

La source doit séparer bootstrap HTTP, activation TLS, test de configuration et
rollback. Aucun `nginx -t`, certificat ou appel SRV-11 réel n'est exécuté sans
porte humaine.
