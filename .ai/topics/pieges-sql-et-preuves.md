---
name: pieges-sql-et-preuves
description: "Pièges payés en août 2026 et invisibles des suites mock : littéral brut C# qui colle deux mots SQL, comparaison NULL-safe <=> dans une migration de convergence, UPDATE qui s'abstient sans bruit, transfert silencieux d'un lien AD, identité AD par objectGUID et non sAMAccountName. Statut : current."
metadata:
  node_type: memory
  type: feedback
  status: current
  source: "transcripts Claude 2026-08-16 à 2026-08-18 (sessions 0714096b, 0f0ae3c0, a8d459ed), revérifiés dans le code le 2026-09-17"
---

# Pièges SQL et limites de preuve

**Pourquoi c'est important :** les suites de tests tournent en persistance
mock et **n'exécutent aucun SQL**. Les défauts ci-dessous passaient au vert.

**Comment l'appliquer :** pour tout SQL composé ou toute migration de
données, imprimer le SQL final, et exiger une preuve sur une base MariaDB
jetable (voir `BILLING_V2_TEST_MARIADB_CONNECTION` /
`API_INTERNAL_TEST_MARIADB_CONNECTION`). Un test ajouté doit être vu
**échouer** une fois, en réintroduisant le défaut.

## Littéral brut C# et concaténation

Un raw string literal `"""…"""` **ne garde pas** le saut de ligne qui
précède le délimiteur fermant. `"SELECT 1" + UserSlotEntitlementSource` a
produit `SELECT 1FROM …` : la lecture des places et quatre compteurs
étaient inexécutables (2026-08-18). Mettre un espace ou un saut de ligne
explicite à la jonction.

## Migration de convergence : `<=>`, pas `=`

Pour ne réécrire que les lignes qui divergent de l'état cible :
`WHERE NOT (col1 <=> 'x' AND col2 <=> NULL …)`, sur **tous** les champs
gérés. Avec `=`, une seule valeur cible `NULL` rend le prédicat `NULL` et
l'UPDATE ne touche plus rien. Tester un seul champ témoin laisse passer
les lignes à moitié migrées. Exemple : `064_billing_v2_provisioning_rule_semantics.sql`.

## Un UPDATE qui s'abstient ne dit rien

Quand une garde (cardinalité, liste de paliers) fait sauter une ligne
ambiguë, rien ne le signale. Livrer une requête de contrôle en pied de
migration (`HAVING COUNT(...) <> 1` → aucune ligne attendue). Sans elle,
un cas ambigu passe pour migré. Ne pas mettre `status` dans le périmètre
d'une migration de données : activer une règle reste une décision
d'exploitation.

## Liens AD : jamais de transfert silencieux

`UpsertPortalUserLinkAsync` pouvait réécrire `portal_user_id` sur la
**seule** ligne d'un `objectGUID` appartenant à un autre utilisateur du
même client, sans violer aucune contrainte UNIQUE. Corrigé (`61be98b`) : le
propriétaire est lu sous le même `FOR UPDATE`, puis refus
(`AmbiguousAdLinkException`). Une contrainte UNIQUE satisfaite ne prouve
pas qu'aucune attribution n'a été écrasée.

## Identité AD : objectGUID

Le provisioning corrèle par `objectGUID`. Le `sAMAccountName` n'est unique
que dans un domaine : avec lui, un homonyme du domaine enfant recevait les
droits. `objectSid` **peut changer légitimement** lors d'un déplacement
entre domaines de la même forêt (l'ancien part dans `sIDHistory`). Seul
`objectGUID` est stable. `..._IDENTITY_SID_MISMATCH` signale donc deux
lectures incohérentes, avec un refus **temporaire**, pas une usurpation.
