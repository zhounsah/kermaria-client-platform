# P17 — Packaging et service SRV-12

```factory-phase
{
  "id": "P17",
  "order": 18,
  "title": "Packaging reproductible et service WEBPORTAL SRV-12",
  "kind": "infrastructure-source",
  "dependencies": ["P16"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "feat(infra): ajouter le packaging et le service SRV-12",
  "allowedPaths": ["scripts/r740xd-vm/deploy-srv12.py", "scripts/r740xd-vm/build-webportal-package.ps1", "scripts/r740xd-vm/srv12/**", "tests/infra/test_srv12.py", "tests/infra/test-webportal-package.ps1"],
  "validations": [
    { "name": "python-syntax-srv12", "executable": "python", "arguments": ["-m", "py_compile", "scripts/r740xd-vm/deploy-srv12.py"] },
    { "name": "srv12-unit-tests", "executable": "python", "arguments": ["-m", "unittest", "tests.infra.test_srv12"] },
    { "name": "package-tests", "executable": "powershell", "arguments": ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "tests/infra/test-webportal-package.ps1"] },
    { "name": "web-build", "executable": "npm.cmd", "arguments": ["run", "build:web"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": ["HG-NETWORK", "HG-DEPLOY", "HG-PROD-DEPENDENCY"]
}
```

Le constructeur doit prouver la présence du standalone, de `.next/static`, de
`public`, du wrapper, d'un dossier de logs et des métadonnées attendues. Les
archives générées restent ignorées et aucun service distant n'est touché.
