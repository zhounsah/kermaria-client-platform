# Pxx — Titre de phase

## Définition machine

Chaque fichier de phase contient exactement un bloc `factory-phase` JSON. Les
scripts le lisent sans interpréter le texte Markdown.

```factory-phase
{
  "id": "Pxx",
  "order": 99,
  "title": "Titre",
  "kind": "application",
  "dependencies": ["Pyy"],
  "initialStatus": "PENDING",
  "requiresCommit": true,
  "commitMessage": "type(scope): intention atomique",
  "allowedPaths": ["chemin/exact", "dossier/**"],
  "validations": [
    {
      "name": "validation-ciblee",
      "executable": "npm.cmd",
      "arguments": ["run", "test:exemple"]
    },
    {
      "name": "diff-check",
      "executable": "git",
      "arguments": ["diff", "--check"]
    }
  ],
  "humanGates": []
}
```

## Objectif

Décrire un seul résultat observable, sans copier un fichier du snapshot.

## Preuves à lire

- code courant et tests existants ;
- diff ciblé `origin/main..backup/...` ;
- contrats et documentation directement concernés.

## Critères d'acceptation

- comportement attendu et cas négatifs ;
- aucune modification hors `allowedPaths` ;
- validations de la définition toutes vertes ;
- constats QA classés et aucun `VALIDE` ouvert ;
- commit local exact, sans `STATE.json`.

## Risques et portes humaines

Lister les risques propres à la phase et uniquement les codes définis dans
`HUMAN_GATES.md`. Une porte conditionnelle n'est activée que si son déclencheur
précis est rencontré.

## Rapport de phase

Le rapport avant commit contient : diff, tests, résultats, constats classés,
risques résiduels, statut Git et message de commit. Après commit, `STATE.json`
enregistre le hash et avance sans demander de confirmation.
