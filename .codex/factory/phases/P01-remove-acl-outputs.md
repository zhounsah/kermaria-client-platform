# P01 — Retrait des sorties ACL

```factory-phase
{
  "id": "P01",
  "order": 1,
  "title": "Retrait des sorties ACL",
  "kind": "repository",
  "dependencies": ["P00"],
  "initialStatus": "DONE",
  "requiresCommit": true,
  "commitMessage": "chore(repo): retirer les sorties ACL temporaires",
  "allowedPaths": ["tmp/ad-acl-backups/**", "tmp/dsacls-help.txt"],
  "validations": [
    { "name": "diff-check", "executable": "git", "arguments": ["diff", "--check"] }
  ],
  "humanGates": []
}
```

Objectif atteint par `8d7125e` : les exports ACL et DSACLS ne sont plus suivis.
