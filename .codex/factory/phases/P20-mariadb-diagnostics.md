# P20 — Diagnostics MariaDB

```factory-phase
{
  "id": "P20",
  "order": 20,
  "title": "Diagnostics MariaDB assainis",
  "kind": "infrastructure-source",
  "dependencies": ["P18"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "tooling(infra): ajouter les diagnostics MariaDB durcis",
  "allowedPaths": ["scripts/r740xd-vm/srv06/audit-mariadb-host.py", "scripts/r740xd-vm/srv13/verify-mariadb/**", "tests/infra/test_mariadb_diagnostics.py"],
  "validations": [
    { "name": "python-syntax-mariadb", "executable": "python", "arguments": ["-m", "py_compile", "scripts/r740xd-vm/srv06/audit-mariadb-host.py"] },
    { "name": "mariadb-diagnostic-tests", "executable": "python", "arguments": ["-m", "unittest", "tests.infra.test_mariadb_diagnostics"] },
    { "name": "verify-mariadb-build", "executable": "dotnet", "arguments": ["build", "scripts/r740xd-vm/srv13/verify-mariadb/Kermaria.VerifyMariaDb.csproj", "-c", "Release"] },
    { "name": "secrets-check", "executable": "npm.cmd", "arguments": ["run", "check:secrets"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": ["HG-MARIADB-REAL", "HG-SECRET", "HG-NETWORK"]
}
```

Les erreurs ne révèlent ni credentials ni détails de connexion, la politique TLS
est explicite et les paramètres ne sont pas figés. Les tests utilisent des
doubles ; aucune MariaDB réelle n'est contactée automatiquement.
