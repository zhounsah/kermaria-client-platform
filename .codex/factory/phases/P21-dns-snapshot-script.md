# P21 — Snapshot DNS

```factory-phase
{
  "id": "P21",
  "order": 23,
  "title": "Script de snapshot DNS public",
  "kind": "infrastructure-source",
  "dependencies": ["P16"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "tooling(infra): ajouter le snapshotteur DNS",
  "allowedPaths": ["scripts/r740xd-vm/snapshot-public-dns.ps1", "tests/infra/test-dns-snapshot.ps1"],
  "validations": [
    { "name": "dns-snapshot-tests", "executable": "powershell", "arguments": ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "tests/infra/test-dns-snapshot.ps1"] },
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": ["HG-NETWORK"]
}
```

Seul le script reproductible est versionné. Les tests simulent les réponses et
valident le JSON ; toute requête DNS publique réelle nécessite la porte réseau,
et aucun JSON daté ne rejoint le commit.
