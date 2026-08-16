# Rapport de fusion — mémoire Codex + Claude Code

Date : 2026-08-15

## Inventaire

- Claude : 22 fichiers thématiques conservés + index d'origine reconstruit.
- Codex : 41 groupes de tâches extraits de la mémoire générée.
- Les dépôts `.git` internes, `raw_memories.md` et les rollouts bruts n'ont pas été injectés dans le contexte courant : ils sont trop bruyants et augmentent le risque de propager des secrets/états obsolètes.
- La mémoire Codex synthétisée et complète a néanmoins été conservée en **archive expurgée** pour recherche ciblée.

## Arbitrages principaux

1. **Roadmap** — le snapshot Claude du 2026-07-09 est historique : son blocage R740xd est contredit par le topic Claude du ~2026-07-23 qui indique le blocage levé.
2. **SEO** — le topic Claude v1.1.12 (2026-08-06) est dépassé pour l'état de release par la preuve Codex v1.3.3.4 (2026-08-11).
3. **hCaptcha** — l'état DUMMY du 2026-07-06 est dépassé : Codex consigne le remplacement de la paire hCaptcha sur SRV-12/SRV-13 le 2026-08-01.
4. **Veeam** — la mention v1.1.14 « partiellement déployée » reste historique et ne doit pas être extrapolée à aujourd'hui sans revalidation live.
5. **Workflow Git** — la préférence de travailler directement sur `main` est conservée, avec la nuance Codex : quand plusieurs sessions travaillent en parallèle, ne jamais écraser/stash/reset les changements concurrents et ne stage que les fichiers intentionnels.
6. **Secrets** — les valeurs sensibles / exemples de mots de passe détectés ont été expurgés dans la copie fusionnée. Les originaux fournis n'ont pas été modifiés.

## Principe retenu

`.ai/MEMORY.md` est l'index commun. Claude et Codex lisent les mêmes fichiers. Les mémoires natives restent disponibles comme caches individuels, mais tout fait durable qui doit survivre au changement d'agent doit être promu dans `.ai/`.

## Groupes Codex les plus récents importés

- 2026-08-11 — kermaria-client-platform / editorial platform, public SEO navigation, and v1.3.3.4 canonicalisation release → `archive/codex-task-groups/2026-08-11-kermaria-client-platform-editorial-platform-public-seo-navigation-and-v1-3-3-4-canonicalisation-release.md`
- 2026-08-10 — kermaria-client-platform / KoXo production synchronization, AD provisioning, and release 1.0.0.8 → `archive/codex-task-groups/2026-08-10-kermaria-client-platform-koxo-production-synchronization-ad-provisioning-and-release-1-0-0-8.md`
- 2026-08-08 — kermaria-client-platform / staged SRV-13 then SRV-12 V1.1 deployment → `archive/codex-task-groups/2026-08-08-kermaria-client-platform-staged-srv-13-then-srv-12-v1-1-deployment.md`
- 2026-08-08 — kermaria-client-platform / public isolated client-space demo → `archive/codex-task-groups/2026-08-08-kermaria-client-platform-public-isolated-client-space-demo.md`
- 2026-08-08 — kermaria-client-platform / diagnostic configurator and central commercial catalog → `archive/codex-task-groups/2026-08-08-kermaria-client-platform-diagnostic-configurator-and-central-commercial-catalog.md`
- 2026-08-08 — kermaria-client-platform / Veeam backup status and release handoff → `archive/codex-task-groups/2026-08-08-kermaria-client-platform-veeam-backup-status-and-release-handoff.md`
- 2026-08-07 — kermaria-client-platform / public offers comparison-table self-service editor → `archive/codex-task-groups/2026-08-07-kermaria-client-platform-public-offers-comparison-table-self-service-editor.md`
- 2026-08-04 — SRV-11 Nextcloud Nginx vhost audit and activation → `archive/codex-task-groups/2026-08-04-srv-11-nextcloud-nginx-vhost-audit-and-activation.md`
- 2026-08-03 — kermaria-client-platform / public backup policy, privacy recovery, and forced release → `archive/codex-task-groups/2026-08-03-kermaria-client-platform-public-backup-policy-privacy-recovery-and-forced-release.md`
- 2026-08-03 — kermaria-client-platform / Stripe test webhook impact diagnosis → `archive/codex-task-groups/2026-08-03-kermaria-client-platform-stripe-test-webhook-impact-diagnosis.md`

## Important

Ce paquet ne remplace pas automatiquement un `AGENTS.md` ou `CLAUDE.md` existant. Utiliser les deux fichiers `*.memory-snippet.md` pour fusionner les blocs avec les instructions déjà présentes dans le projet.
