# P15 — Baseline Linux R740xd

```factory-phase
{
  "id": "P15",
  "order": 15,
  "title": "Baseline Linux R740xd",
  "kind": "infrastructure-source",
  "dependencies": ["P14"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "feat(infra): ajouter la baseline Linux R740xd",
  "allowedPaths": ["scripts/r740xd-vm/configure-linux-baseline.py", "scripts/r740xd-vm/verify-linux-reboot.py", "tests/infra/test_linux_baseline.py"],
  "validations": [
    { "name": "python-syntax-baseline", "executable": "python", "arguments": ["-m", "py_compile", "scripts/r740xd-vm/configure-linux-baseline.py", "scripts/r740xd-vm/verify-linux-reboot.py"] },
    { "name": "baseline-unit-tests", "executable": "python", "arguments": ["-m", "unittest", "tests.infra.test_linux_baseline"] },
    { "name": "secrets-check", "executable": "npm.cmd", "arguments": ["run", "check:secrets"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": ["HG-NETWORK", "HG-DEPLOY", "HG-PROD-DEPENDENCY"]
}
```

Réimplémenter des scripts paramétrés, idempotents, avec dry-run et confirmation
explicite avant réseau ou reboot. La phase valide seulement la source et les
simulations locales ; toute exécution distante active une porte humaine.
