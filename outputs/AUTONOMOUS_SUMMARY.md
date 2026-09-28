# Résumé autonome

Dernière mise à jour : 2026-09-28.

| Sujet | État | Preuve locale |
| --- | --- | --- |
| Priorité 1 — installer API DEV | Terminé | `2ba96ed`, 18 tests Pester sous Windows PowerShell 5.1 |
| Runtime DEV / PROD | Non touché | aucune connexion de déploiement, aucune écriture distante |
| Priorité 2 — modèle DEV explicite | Terminé | `65c5a19`, template non secret et source `DEV_API_*` explicite |
| Priorité 3 — non-régression runtime | Terminé | `4df041b`, garde API DEV/PROD et smoke ciblé |
| Priorité 4 — stockage headless KoXo | Analyse terminée | `0045409`, blocage UI probable ; test KoXo réel requis avant toute correction |
| Priorité 5 — prochain E2E abonnement | STOP documenté | `48e6c3e`, aucun groupe de service strictement DEV disponible ; décision Zachary requise |

Les WIP préexistants sous `apps/`, `AGENTS.md` et `test*.txt` restent hors du
travail autonome et hors index.
