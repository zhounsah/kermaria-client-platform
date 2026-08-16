---
name: koxo-orphelins-supprimes
description: "RÉSOLU 2026-08-06 : SyncDoNotDeleteUsers passé à 1 sur les deux profils, un orphelin est désormais DÉSACTIVÉ puis RÉACTIVÉ automatiquement s'il revient (cycle mesuré). Avant ça il était supprimé, avec perte du SID."
metadata: 
  node_type: memory
  type: project
  originSessionId: d79dbe2b-9ae3-4e1a-bc48-9b5d713aee39
  modified: 2026-08-06T12:10:21.647Z
---

## L'état actuel (2026-08-06, décidé par ZH)

`<SyncDoNotDeleteUsers>1</SyncDoNotDeleteUsers>` sur `CLIENTS.xml` **et**
`CLIENTS-DEMO.xml`. Motif de ZH : un client qui se désabonne puis revient doit
**conserver son identifiant**.

Cycle mesuré de bout en bout avec les profils réels :

| Passage | CSV | Compte |
|---|---|---|
| A | présent | créé, actif |
| B | retiré | **désactivé**, toujours dans l'annuaire |
| C | de retour | **réactivé automatiquement** |

Le compte qui revient garde `SID`, `sAMAccountName`, `employeeNumber`, dossier
personnel **et mot de passe** (authentification vérifiée). Rien à refaire, et
l'adoption par `employeeNumber` côté API le retrouve tel quel.

## Pourquoi ce fichier existe quand même

Le réglage par défaut était `0`, et il **supprimait** : deux identités de test
ont purement disparu de l'annuaire le 2026-08-06 avant la correction. Le nom
`DisableOrphanedAccounts=1` ne protège de rien, c'est `SyncDoNotDeleteUsers`
qui décide. Si ce drapeau se défait, un export partiel ne désactive pas des
clients : il les **supprime**, et le compte recréé prend un **SID différent**
— ACL de fichiers et accès RDS perdus avec. `BackupDeletedUsersData=1` sauve
les données, pas le compte.

C'est aussi ce qui donne sa valeur au garde-fou `KOXO_ALLOW_EMPTY_CSV`
livré le même jour. Voir [[koxo-groupes-primaires-separes]].
