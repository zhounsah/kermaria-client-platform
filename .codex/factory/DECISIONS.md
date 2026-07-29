# Journal des décisions

Ce fichier contient les décisions durables de l'usine. Une décision ne doit
jamais contenir de secret. Les nouvelles entrées sont ajoutées avec un identifiant
stable `D-YYYYMMDD-NN`, la preuve, la portée et les conséquences.

## D-20260729-01 — Git est la source de vérité

- Décision : l'état courant est reconstruit depuis la branche active, son
  historique et les tests ; aucune conversation ne remplace ces preuves.
- Conséquence : toute reprise commence par les contrôles Git et `STATE.json`.

## D-20260729-02 — Le snapshot reste une référence

- Décision : `backup/snapshot-avant-remise-a-plat-2026-07-29` et `7fceb0d` sont
  consultables en lecture seule, mais ne seront ni fusionnés, ni cherry-pickés,
  ni restaurés globalement.
- Conséquence : chaque comportement est réimplémenté minimalement sur le code
  courant et validé séparément.

## D-20260729-03 — Phases déjà terminées

- Hygiène Git : `7c07b05`, puis corrections `e60a609` et `0c2be86`.
- Retrait des sorties ACL : `8d7125e`.
- Règles multi-agents : `65d3087`.
- Catalogue mock : `3286578`.
- Dernier commit validé : `3286578e3b651f228fadd373da8e4aa445b09aa4`.

## D-20260729-04 — Prochaine phase isolée

- Décision : le refus `PortalService` HTTP 403 est P04 et précède la phase
  distincte des droits de téléchargement.
- Justification : la prochaine phase a été explicitement imposée et le test 403
  existant échoue hors du groupe catalogue mock.

## D-20260729-05 — Commits locaux automatiques

- Décision : une exécution explicitement démarrée ou reprise de l'usine autorise
  les commits locaux atomiques décrits par les phases.
- Limite : aucune opération distante, intégration de branche, réécriture
  d'historique, tag ou déploiement n'est autorisé sans porte humaine.

## D-20260729-06 — Checkpoint runtime séparé

- Décision : `STATE.json` peut rester le seul fichier modifié entre deux commits
  de phase et n'est pas inclus dans ces commits fonctionnels.
- Justification : l'état doit enregistrer le hash du commit qui vient d'être
  créé sans rendre le commit autoréférent.

## Modèle d'entrée

```text
## D-YYYYMMDD-NN — Titre

- Phase : Pxx
- Contexte : faits vérifiés
- Options : options réellement possibles
- Décision : choix explicite
- Conséquences : effet sur la feuille de route, les tests et les blockers
- Auteur/validation humaine : requis seulement pour une porte humaine
```
