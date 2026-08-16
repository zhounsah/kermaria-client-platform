---
name: koxo-fiche-utilisateur-maitre
description: "MESURÉ 2026-08-06 : KoXo garde une fiche PAR UTILISATEUR (Data\\Users\\<GROUPE>\\<groupe secondaire>\\<login>.xml) et la réapplique à l'AD à CHAQUE synchro. Toute modification faite directement dans l'AD est écrasée."
metadata: 
  node_type: memory
  type: project
  originSessionId: d79dbe2b-9ae3-4e1a-bc48-9b5d713aee39
  modified: 2026-08-06T11:47:29.599Z
---

Le modèle de groupe primaire ne sert qu'à **semer** un compte à sa création.
Ensuite, KoXo conserve une fiche par utilisateur dans
`Data\Users\<GROUPE PRIMAIRE>\<groupe secondaire>\<login>.xml`, et **la
réapplique à l'annuaire à chaque synchronisation**.

Démonstration faite sur `AllowRDS` :

| Action | Résultat |
|---|---|
| `allowLogon = 1` posé par ADSI sur un compte existant | **écrasé à 0** à la synchro suivante |
| `<AllowRDS>` passé à 1 **dans la fiche utilisateur**, puis synchro | `allowLogon = 1` dans l'AD |

D'où la règle : **ne jamais corriger un attribut piloté par KoXo directement
dans l'AD** — corriger la fiche (IHM ou XML), puis synchroniser. Corriger le
*modèle* ne suffit pas non plus pour les comptes déjà créés.

Ça explique aussi le mot de passe : avec `ForcePasswords=1` et une colonne 14
vide, KoXo journalise « Mot de passe forcé » et `pwdLastSet` change — il
réapplique celui de **sa** base. Le compte reste authentifiable, mais c'est KoXo
qui fait autorité, pas l'annuaire. Voir [[koxo-ad-password-mastery]].

Édition byte-safe obligatoire : ces XML sont en UTF-8 sans BOM et contiennent
`CLIENTS DÉMO` dans leur chemin. Un aller-retour `[xml]` + `.Save()` en
PowerShell 5.1 produit `CLIENTS DÃ‰MO` — le profil vise alors un groupe
inexistant et la synchro devient un no-op silencieux (constaté le 2026-08-06).
Lire/écrire via `[System.IO.File]::ReadAllBytes` + `UTF8Encoding($false)`.
