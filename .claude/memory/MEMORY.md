# Memory index

- [Topologie production courante](deployment-topology.md) - SRV-11 edge/TLS, SRV-12 WEBPORTAL, SRV-13 API-INTERNAL, SRV-06 MariaDB ; domaines zachary-it.fr / dashboard.zachary-it.fr / administration.zachary-it.fr.
- [Migration domaines 2026-08-20](../../docs/DOMAIN_MIGRATION_2026-08-20.md) - source de verite pour canoniques, redirects legacy et webhooks.

- [Historique - Hardware R740xd bloquait la prod](infra-r740xd-blocker.md) - blocage leve depuis juillet 2026 ; conserver uniquement comme contexte historique.
- [Snapshot roadmap au 2026-07-03](roadmap-current.md) — V0.15–V0.30 partiel livrés ; V0.24 infra staging debout, reste Briques 1/2/3 (recette, audit, doc prod). V1.0 beta 1/RC hardware-gated R740xd.
- [API facturation BPCE](bpce-invoicing-api.md) — URLs, clé "Test API (RDC-07)", rate limits, env supposé prod ; le refresh token ne vit que dans un secret applicatif.
- [PayPal V0.22 gotchas](paypal-v022-gotchas.md) — pièges JSON acronymes, raw strings C# concat, dual sandbox/live, test webhook local, dotnet stale DLL.
- [Workflow préférences](workflow-preferences.md) — travailler sur `main` directement (pas de worktree), les branches cassent Next.js en dev.
- [Checklist livraison version](version-release-checklist.md) — à chaque Vx.y/Vx.y.z, MAJ README + ROADMAP + doc dédiée + refs croisées + mémoire AVANT commit/push.
- [Historique - Topologie KERMARIA-SRV-01/02/07](deployment-topology.md) - ancien staging Windows/IIS ; ne plus utiliser pour choisir les cibles de production.
