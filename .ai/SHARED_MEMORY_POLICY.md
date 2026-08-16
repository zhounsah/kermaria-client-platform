# Politique de mémoire partagée

Cette arborescence est la **source durable commune** à Codex et Claude Code.

## Règles

1. `MEMORY.md` reste court : état actuel, index et points de vigilance.
2. Les détails durables vont dans `topics/*.md`.
3. L'historique Codex importé est dans `archive/` : il est consultable, mais ne doit jamais être pris comme état courant sans revalidation.
4. Une information issue du code, des tests ou d'un système réellement interrogé prime sur la mémoire.
5. En cas de contradiction, préférer l'information **la plus récente ET vérifiée**, pas simplement la plus récente.
6. À la fin d'une tâche, promouvoir dans `.ai/` uniquement les découvertes qui seront utiles à une autre session ou à l'autre agent.
7. Ne jamais enregistrer de mot de passe, token, clé API, clé privée, cookie de session ou chaîne de connexion contenant un secret.
8. Les valeurs sensibles découvertes dans une ancienne mémoire doivent être remplacées par `[REDACTED]`; si elles sont encore valides, les considérer comme compromises et les faire tourner.

## Statuts recommandés

- `current` : vérifié et encore applicable.
- `revalidate` : probablement utile mais dépend de l'état live / d'une version / d'un hôte.
- `historical` : contexte ancien, utile pour comprendre une décision mais pas pour décrire l'état actuel.

## Fin de tâche

Avant de terminer une tâche importante :

- mettre à jour le topic concerné ;
- corriger/supprimer les affirmations devenues fausses ;
- mettre à jour `MEMORY.md` si l'index ou l'état courant change ;
- lancer `scripts/check-memory-secrets.ps1` ;
- ne pas dupliquer un détail déjà documenté dans le dépôt : mettre plutôt un pointeur vers le fichier source.
