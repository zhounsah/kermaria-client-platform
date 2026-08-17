---
name: billing-v2-koxo-storage-targets
description: "Phase 3A Billing V2 (2026-08-17) : topologie KoXo extraite et partagée, résolution fail-closed des cibles de quota (GiB→MiB), provider réel toujours dormant. Corrige la fausse causalité « le stockage personnel crée le compte annuaire »."
metadata:
  node_type: memory
  type: project
---

## Ce qui est livré (local, non poussé)

Préparation du provider KoXo Storage. **Rien n'est appliqué** : les hard-stops
de `BillingV2ProvisioningService` restent en place, `DormantBillingV2KoxoStorageProvider`
reste enregistré dans `Program.cs`, et le nouveau resolver n'est branché nulle
part. Voir [[koxo-api-ne-cree-plus]] et [[koxo-fiche-utilisateur-maitre]].

- `KoxoDirectoryTopology` (`apps/api-internal/Services/`) : nommage KoXo pur —
  `PrimaryGroupClients` / `PrimaryGroupDemo`, `DemoGroupPrefix`,
  `ResolveSecondaryGroup`, `ResolvePrimaryGroup`, forme `CLI-NNNNNN`.
  `KoxoExportService` l'utilise désormais et n'expose plus que des alias.
- `BillingV2KoxoStorageTargetResolver` (`Services/Provisioning/`) : fonction
  pure, sans I/O, qui traduit un `BillingV2StorageQuotaPlan` en cible KoXo.

## Correction d'un fait qui était faux dans le code

Les commentaires affirmaient que **le stockage personnel crée le compte
annuaire**. C'est faux et ça a été corrigé.

`MariaDbKoxoRepository.ListExportCandidatesAsync` exige
`ad_link.portal_user_id IS NOT NULL` pour tout client réel : un client payant
ordinaire **a déjà son `customer_ad_links`** avant même de partir dans le CSV.
Seul un essai (`is_demo = TRUE AND demo_kind = 'trial'`) est exporté sans lien,
et c'est alors KoXo qui crée le compte, adopté ensuite par `employeeNumber`.

Le prérequis `PersonalStorageRequired` reste en place, mais c'est une **règle
d'ordre voulue** (pas d'accès VPN/RDS vers un poste inexistant), pas une
dépendance technique de création d'identité.

## Invariants du resolver

- Unité : le plan reste en **GiB** (catalogue), la cible KoXo est en **MiB**,
  conversion `checked(valeur * 1024)` faite à un seul endroit. Débordement ou
  quota ≤ 0 = refus, jamais de rebouclage silencieux.
- Identité utilisateur : `portal_users.id` → `koxo_unique_identifier`
  (`CLI-NNNNNN`) → `employeeNumber` → objet AD. Exactement **un**
  `customer_ad_link` (via `GetUserLinksByPortalUserIdAsync`), même client, et
  cohérence sur le **triplet** `objectGUID` / `objectSID` / `sAMAccountName`
  avec la résolution par `employeeNumber`. Aucun rapprochement par nom : KoXo
  translittère le nom et dérive lui-même le `sAMAccountName`.
- Utilisateur non matérialisé = **bloquant**. Le provider de quota ne crée
  aucune identité : un seul propriétaire de la création, la chaîne KoXo.
- Refus **global** : une seule ligne douteuse refuse toute la résolution.
- Deux quotas visant le même objet = refus (sinon le résultat dépend de l'ordre
  de lecture).
- `BillingV2KoxoQuotaPolicy` classe les transitions ; **une réduction est
  déclarée non applicable** (abaisser sous l'occupation réelle bloque
  l'utilisateur sans rien libérer). Modélisé, pas encore branché.

## Reste à faire en 3B

Endpoint SRV-21, fiche XML, `RepairUser` / `RepairSecondaryGroup`, lecture du
quota courant (nécessaire pour appliquer la règle de non-réduction), puis
branchement du resolver derrière un drapeau. Rappel :
`KoXoAdm.exe` sort en **code 1 même en succès**, se fier aux marqueurs de
journal.
