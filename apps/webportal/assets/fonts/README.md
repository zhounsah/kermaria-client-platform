# Polices locales du portail

Les deux fichiers WOFF2 variables sont les sous-ensembles latins d'Inter
(graisses 100–900) et de JetBrains Mono (graisses 100–800) déjà utilisés par
le portail. Ils sont conservés localement pour que la compilation Next.js ne
dépende pas de `fonts.googleapis.com`.

- Inter : https://github.com/rsms/inter — licence dans `LICENSE-Inter.txt`.
- JetBrains Mono : https://github.com/JetBrains/JetBrainsMono — licence dans
  `LICENSE-JetBrains-Mono.txt`.

Les fichiers de police proviennent des ressources WOFF2 que `next/font/google`
avait placées dans le cache de développement du projet avant le passage à
`next/font/local`. Les textes de licence sont copiés des dépôts officiels.
