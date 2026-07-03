# Memory index

- [Hardware R740xd bloque la prod](infra-r740xd-blocker.md) — la V1.0 est gated, on reste en phase de tests sur SRV-01/02 jusqu'à livraison.
- [Snapshot roadmap au 2026-07-03](roadmap-current.md) — V0.15–V0.30 partiel livrés ; V0.24 infra staging debout, reste Briques 1/2/3 (recette, audit, doc prod). V1.0 beta 1/RC hardware-gated R740xd.
- [API facturation BPCE](bpce-invoicing-api.md) — URLs, clé "Test API (RDC-07)", rate limits, env supposé prod ; le refresh token ne vit que dans un secret applicatif.
- [PayPal V0.22 gotchas](paypal-v022-gotchas.md) — pièges JSON acronymes, raw strings C# concat, dual sandbox/live, test webhook local, dotnet stale DLL.
- [Workflow préférences](workflow-preferences.md) — travailler sur `main` directement (pas de worktree), les branches cassent Next.js en dev.
- [Checklist livraison version](version-release-checklist.md) — à chaque Vx.y/Vx.y.z, MAJ README + ROADMAP + doc dédiée + refs croisées + mémoire AVANT commit/push.
- [Topologie déploiement KERMARIA-SRV-01/02/07](deployment-topology.md) — WS2022 sans VM : SRV-01 Node+IIS split, SRV-02 API dotnet Service natif, SRV-07 MariaDB test_web ; compte AD partagé, config JSON par app (build-*-config.ps1 + -Override). Runbook docs/DEPLOYMENT_WINDOWS.md.
