# Processus persistant de l'usine

## Contrat de l'orchestrateur

Le thread principal est l'unique orchestrateur. Il lit l'état, distribue les
rôles, arbitre l'ownership des fichiers, fait exécuter les validations, crée les
commits locaux autorisés et enchaîne immédiatement les phases. Il ne produit pas
de code applicatif lui-même lorsque le rôle `implementer` ou `fixer` est actif.

Une demande explicite « démarrer l'usine » ou « reprendre l'usine » autorise les
commits locaux atomiques prévus ici. Elle n'autorise jamais push, merge, rebase,
cherry-pick, tag ou déploiement.

## Premier démarrage

L'infrastructure doit d'abord avoir été relue et committée manuellement par son
propriétaire. Depuis la racine du dépôt, le thread principal exécute ensuite :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .codex/factory/scripts/validate-global.ps1 -FactoryOnly
powershell -NoProfile -ExecutionPolicy Bypass -File .codex/factory/scripts/update-state.ps1 -Action StartPhase
```

Il charge immédiatement les rôles d'analyse de P04 et déroule le processus
jusqu'à une porte humaine ou à `DELIVERED`. Il ne rend pas la main entre deux
phases réussies.

## Reprise dans une nouvelle session

Une nouvelle session n'a besoin d'aucun historique de conversation. Elle doit :

1. lire intégralement `AGENTS.md`, `ROADMAP.md`, `PROCESS.md`,
   `HUMAN_GATES.md`, `DECISIONS.md`, `BLOCKERS.md` et `STATE.json` ;
2. lire la définition de `STATE.json.currentPhase` et les rôles nécessaires ;
3. exécuter :

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .codex/factory/scripts/check-git-state.ps1 -Mode Resume
   ```

4. si `interruption.active` vaut `true`, enregistrer la reprise avec
   `update-state.ps1 -Action Resume` ;
5. continuer depuis `currentStep`, sans répéter une étape dont la preuve est
   encore valide et dont les fichiers n'ont pas changé ;
6. ne demander l'utilisateur que si `blocker.active` vaut `true` et que le cas
   relève précisément de `HUMAN_GATES.md`.

`STATE.json` est un checkpoint runtime versionné à l'installation de l'usine,
mais volontairement modifiable pendant son exécution. Entre deux commits de
phase, lui seul peut rester modifié. Il n'est jamais ajouté au commit applicatif
de la phase ; cela évite qu'un commit contienne un hash autoréférent et préserve
l'atomicité fonctionnelle.

## Machine à états

```text
READY_TO_RUN
  -> PRECHECK
  -> ANALYSIS
  -> PLAN
  -> PRODUCTION
  -> INTEGRATION
  -> QA
       -> READY -> COMMIT -> ADVANCE -> PRECHECK (phase suivante)
       -> FIX -> RE_QA --+
       -> HUMAN_GATE
       -> BLOCKED
  -> FINAL_AUDIT
  -> DELIVERY
  -> DELIVERED
```

Toute sortie non planifiée passe d'abord par `INTERRUPTED`. Aucune phase
réussie ne retourne au thread utilisateur entre `COMMIT`, `ADVANCE` et le
`PRECHECK` suivant.

## Déroulement obligatoire d'une phase

### 1. PRECHECK

- Vérifier racine, branche, HEAD non détachée, index, worktree, ascendance de
  `origin/main` et dernier commit validé.
- Refuser la branche de sauvegarde comme branche courante.
- Vérifier les dépendances et l'allowlist de fichiers de la phase.
- En cas d'écart inattendu, enregistrer l'interruption avant tout arrêt.

### 2. ANALYSIS

- Charger `analyst`, `test-designer` et `security-reviewer` en lecture seule.
- Les analyses peuvent être parallèles.
- Comparer le code actuel, `origin/main` et les hunks pertinents du snapshot,
  sans restaurer un blob complet.
- Produire des constats traçables : preuve, impact, fichiers et test attendu.

### 3. PLAN

- L'intégrateur classe chaque constat `VALIDE`, `FAUX POSITIF` ou `INCERTAIN`.
- Un constat `INCERTAIN` déclenche une investigation supplémentaire, pas une
  correction spéculative.
- Définir une ownership map sans chevauchement. Plusieurs producteurs ne sont
  permis que pour des ensembles de fichiers réellement indépendants.

### 4. PRODUCTION

- Charger un seul `implementer` par ensemble de fichiers.
- Réimplémenter le comportement minimal depuis le code courant.
- Ne jamais appliquer globalement le snapshot ou un patch historique.
- Ne pas modifier un fichier hors `allowedPaths`.

### 5. INTEGRATION

- Charger `integrator` après les producteurs.
- Examiner le diff assemblé, les contrats partagés et l'absence de modifications
  hors phase.
- Stabiliser le diff avant toute QA indépendante.

### 6. QA

- Charger `qa-engineer` et `code-reviewer`, qui n'ont participé ni à la
  production ni à l'intégration.
- Exécuter `validate-phase.ps1` pour la phase courante.
- Le `security-reviewer` revient si la surface d'attaque ou un contrat de
  sécurité a changé.
- Classer tous les constats avant correction.

### 7. FIX / RE_QA

- Charger `fixer` uniquement avec la liste des constats `VALIDE`.
- Interdire les refactorings opportunistes et les corrections d'éléments
  `FAUX POSITIF` ou `INCERTAIN`.
- Rejouer toute la validation de phase après chaque correction.
- Calculer une empreinte stable des validations en échec et des identifiants de
  constats encore valides.

Le compteur `noProgressCycles` augmente lorsque deux QA successives gardent la
même empreinte, ou lorsqu'aucun test supplémentaire ne passe et aucun constat
valide ne disparaît. Un progrès démontré remet ce compteur à zéro. Au troisième
cycle consécutif sans progrès, l'usine enregistre `QA_NO_PROGRESS_LIMIT`, passe
à `BLOCKED` et sollicite l'utilisateur. Le nombre total de corrections peut
dépasser trois si chaque cycle démontre un progrès réel.

### 8. READY / COMMIT / ADVANCE

Une phase devient `READY` seulement si toutes ses validations réussissent et si
aucun constat valide n'est ouvert. L'orchestrateur doit alors, sans pause :

1. vérifier le diff et l'allowlist avec `check-git-state.ps1 -Mode Ready` ;
2. indexer explicitement les seuls fichiers de la phase, jamais `git add .` ;
3. examiner `git diff --cached --check`, `git diff --cached` et
   `git status --short` ;
4. créer le commit local avec le message exact de la définition de phase ;
5. ne jamais inclure `STATE.json` dans ce commit ;
6. exécuter `update-state.ps1 -Action CompletePhase -Commit <HEAD>` ;
7. passer immédiatement au `PRECHECK` de la phase suivante.

Si la phase terminée est la dernière, `CompletePhase` bascule directement vers
`FINAL_AUDIT`.

## Audit final et livraison

L'audit final est effectué par `final-auditor`, avec une nouvelle passe du
`security-reviewer`, du `code-reviewer` et du `qa-engineer`. Il exécute :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .codex/factory/scripts/validate-global.ps1
```

Le final auditor vérifie aussi l'historique des commits, les décisions, les
blockers, l'absence de fichiers restaurés aveuglément et la concordance entre
code et documentation. Après succès, l'orchestrateur enregistre
`RecordFinalAuditPass`, puis `Deliver`. La livraison est locale et informative ;
elle ne pousse, ne fusionne, ne tague et ne déploie rien.

## Interruption et blocker

Avant toute fin de session non livrée, erreur de capacité, panne d'outil ou
limite de contexte, exécuter :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .codex/factory/scripts/update-state.ps1 `
  -Action Interrupt -Reason "raison factuelle et non sensible"
```

Un blocker doit être reproduit ou confirmé, résumé sans secret dans
`BLOCKERS.md`, puis enregistré dans `STATE.json`. Les échecs ordinaires de test,
les constats corrigibles et les limites temporaires d'un agent ne sont pas des
portes humaines.
