# Journal des blockers

`STATE.json.blocker` est la source structurée du blocker actif. Ce journal
conserve les preuves lisibles et les résolutions. Aucun secret, identifiant
d'accès, contenu client ou chaîne de connexion ne doit y être copié.

## État initial — 2026-07-29

Aucun blocker actif. La phase P04 est prête à démarrer.

## Modèle d'entrée

```text
## B-YYYYMMDD-NN — OPEN | RESOLVED — Titre

- Phase et étape : Pxx / STEP
- Type/code : TECHNICAL ou HUMAN_GATE / code stable
- Première occurrence : horodatage UTC
- Dernière occurrence : horodatage UTC
- Empreinte : identifiant stable, sans données sensibles
- Preuves : commandes, erreurs assainies, chemins et tests
- Tentatives : actions bornées déjà essayées
- Décision attendue : uniquement pour une porte humaine
- Résolution : preuve de disparition et validation rejouée
```

Un test en échec n'est pas automatiquement un blocker. Le statut `BLOCKED`
n'est utilisé qu'après trois cycles de correction consécutifs sans progrès, ou
quand aucune action sûre ne peut avancer sans changement externe. Une porte
humaine suit exclusivement `HUMAN_GATES.md`.
