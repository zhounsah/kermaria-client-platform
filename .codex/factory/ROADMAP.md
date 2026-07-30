# Feuille de route de l'usine

## Autorité et périmètre

Cette feuille de route reconstruit, groupe fonctionnel par groupe fonctionnel,
les éléments utiles identifiés par l'audit de réintégration du 29 juillet 2026.
Git et les documents versionnés du dépôt restent la source de vérité.

- Base de comparaison : `origin/main` au commit `c470f56bfb99453d1454ab3685ef456ab44d0e45`.
- Branche de travail : `chore/remise-a-plat-agentique`.
- Référence historique en lecture seule :
  `backup/snapshot-avant-remise-a-plat-2026-07-29`, commit
  `7fceb0d141b84ad08306fb8dbe4dad7dc880201d`.
- Le snapshot n'est jamais fusionné, cherry-pické, restauré globalement ou
  considéré comme une implémentation correcte par défaut.
- `STATE.json` est la source de vérité du statut d'exécution. Les statuts
  ci-dessous décrivent l'état initial de l'usine au 29 juillet 2026.

## Statuts

- `DONE` : phase déjà validée et committée.
- `PENDING` : phase ordonnée, pas encore démarrée.
- `ACTIVE` : analyses ou production en cours.
- `QA_FAILED` : défauts validés à corriger.
- `FIXING` : correction bornée aux défauts validés.
- `READY` : validations réussies, commit local atomique à créer immédiatement.
- `BLOCKED` : obstacle technique persistant, enregistré avec ses preuves.
- `HUMAN_GATE` : décision humaine indispensable au sens de `HUMAN_GATES.md`.

## Phases ordonnées

| Ordre | Phase | Statut initial | Dépend de | Résultat attendu |
|---:|---|---|---|---|
| 00 | P00 — Hygiène Git | DONE | — | Artefacts, caches et snapshots locaux ignorés sans masquer les sources. |
| 01 | P01 — Retrait des sorties ACL | DONE | P00 | Exports ACL et sortie DSACLS retirés du suivi. |
| 02 | P02 — Règles multi-agents | DONE | P01 | Règles Git, ownership et revues inscrites dans `AGENTS.md`. |
| 03 | P03 — Catalogue mock | DONE | P02 | Services mock disponibles lorsque les deux repositories sont non persistants. |
| 04 | P04 — Refus `PortalService` HTTP 403 | DONE | P03 | Un service hors périmètre client produit un refus d'accès 403, avec test ciblé. |
| 05 | P05 — Droits de téléchargement | DONE | P04 | Autorisation fondée sur les services actifs, sans élargissement d'accès. |
| 06 | P06A — Baseline lint web | DONE | P05 | Les sept diagnostics web préexistants sont corrigés sans changement fonctionnel ni contournement du lint. |
| 07 | P06 — Mutualisation de la souscription | DONE | P06A | Sections de souscription réutilisées sans changement de contrat. |
| 08 | P07 — Routage canonique des portails | DONE | P06 | `www`, `dashboard` et `administration` convergent vers leur zone canonique. |
| 09 | P08 — Connexion native sans JavaScript | DONE | P07 | POST formulaire et JSON sûrs, redirections bornées, cookies inchangés. |
| 10 | P09 — `set-password` natif | PENDING | P08 | Présentation 303 sûre des succès et erreurs de formulaire natif. |
| 11 | P10 — Vitrine et tunnel pack/contact | PENDING | P09 | Sélection de pack conservée jusqu'au contact ou au signup. |
| 12 | P11 — Reprise du pack dans le dashboard | PENDING | P10 | Sélection en attente reprise sans masquer les erreurs partielles. |
| 13 | P12 — Tiroir panier | PENDING | P11 | Navigation, fermeture et focus stabilisés. |
| 14 | P13 — Textes français et mojibake | PENDING | P12 | Corrections visibles ciblées, aucun identifiant technique accentué. |
| 15 | P14 — Séparation des configurations | PENDING | P13 | Générateurs API et WEBPORTAL empêchent toute fuite de configuration interne. |
| 16 | P15 — Baseline Linux R740xd | PENDING | P14 | Scripts paramétrés, idempotents et sûrs pour réseau, temps et reboot. |
| 17 | P16 — Reverse proxy SRV-11 | PENDING | P15 | Bootstrap nginx/TLS vérifiable avec rollback documenté. |
| 18 | P17 — Packaging et service SRV-12 | PENDING | P16 | Archive WEBPORTAL reproductible et unité de service vérifiable hors déploiement. |
| 19 | P18 — Installation API SRV-13 | PENDING | P15 | Installation .NET/service bornée, compte dédié et rollback vérifiable. |
| 20 | P19 — Préparation KoXo | PENDING | P18 | Source prospective sûre ; toute action réelle reste sous porte humaine. |
| 21 | P20 — Diagnostics MariaDB | PENDING | P18 | Outils de diagnostic à erreurs assainies et politique TLS explicite. |
| 22 | P21 — Snapshot DNS | PENDING | P16 | Script source uniquement ; aucun snapshot daté versionné. |
| 23 | P22 — Documentation V0.39 | PENDING | P10, P11, P13 | Documentation alignée sur le comportement effectivement réintégré. |
| 24 | P23 — Documentation R740xd | PENDING | P15 à P21 | Cible, état courant et actions futures distingués sans ambiguïté. |
| 25 | P24 — Documentation AD | PENDING | P19, P23 | Modèle `clients.home.bzh` aligné sur le code et les décisions humaines. |
| 26 | P25 — Exemple de configuration AD | PENDING | P24 | Exemple cohérent avec DN, ACL et groupes validés ; aucune valeur réelle. |
| 27 | P26 — Sources juridiques canoniques | PENDING | P22 | Doublons racine traités après validation du propriétaire juridique. |

Chaque phase possède une définition exécutable dans `phases/`. L'ordre est
strict : même si deux phases seraient techniquement indépendantes, le passage à
la suivante n'a lieu qu'après QA, commit local et mise à jour de l'état.

## Éléments explicitement exclus

Les éléments suivants ne constituent pas des phases de restauration :

- mutations de panier ou checkout en `GET` ;
- redirections externes non bornées ;
- `.artifacts/`, `.codex-tmp/`, `tmp/`, caches Python et sorties de build ;
- archives ou paquets de déploiement sauvegardés ;
- `etat-avant-remise-a-plat.patch` et son patch d'index ;
- snapshots DNS datés, exports ACL et journaux bruts ;
- renommages globaux destinés à accentuer des identifiants techniques ;
- `v0.24.txt` brut, qui n'est pas une source de vérité versionnée.

## Critères globaux de fin

L'usine peut entrer en audit final uniquement si :

1. toutes les phases P00 à P26, incluant P06A, sont `DONE` dans `STATE.json` ;
2. chaque phase nécessitant du code ou de la documentation possède un commit
   local atomique et validé ;
3. aucun blocker ni porte humaine n'est actif ;
4. `npm.cmd run validate` et les contrats complémentaires définis par le
   validateur global réussissent ;
5. `git diff --check` réussit et le worktree ne contient, au plus, que le
   checkpoint contrôlé `STATE.json` ;
6. le reviewer sécurité, le code reviewer, la QA et le final auditor n'ont plus
   de constat `VALIDE` ouvert ;
7. la comparaison finale avec le snapshot confirme que seuls les groupes
   décidés ont été réimplémentés ;
8. aucun push, merge, rebase, tag ou déploiement n'a été effectué.

La livraison consiste en un rapport local, la liste des commits, les preuves de
validation, les blockers résolus ou acceptés, et les actions distantes encore
interdites. Elle ne publie rien automatiquement.
