---
name: admin-configuration-center
description: "Centre de configuration administrateur (chantier local, non poussé) : registres fermés, permissions fail-closed (migration 079), atomicité mutation+révision, concurrence fiscale sous verrou, et surtout — les autorités KoXo sont désormais APPLIQUÉES, plus seulement affichées. Corrige la revue finale NO-GO."
metadata:
  node_type: memory
  type: project
---

## État au 2026-08-29

Chantier **local, non poussé, non déployé**. Une revue finale indépendante avait
conclu **NO-GO** ; les huit bloqueurs qu'elle a listés sont fermés dans un lot
correctif unique. La spécification complète est
`docs/ADMIN_CONFIGURATION_CENTER_IMPLEMENTATION.md` ; la section 37 y documente
le lot correctif.

Ce que la revue avait trouvé n'était pas une fonctionnalité manquante mais une
classe unique de défaillances : des garanties **vraies dans la documentation et
dans le code de lecture, mais non tenues au moment de l'écriture**.

## Ce qui fait autorité, et où

- Registres **fermés** dans le code : `SettingsAuditRegistry`,
  `SettingsPermissionRegistry`, `CommunicationTemplateRegistry`,
  `DemoContentTemplateRegistry`, régimes fiscaux. Aucune création libre de clé
  depuis l'interface ; une clé inconnue en base est ignorée sans erreur.
- Modèles de démonstration : bascule **binaire** code ↔ base. Table vide → le
  code fait autorité ; table non vide → la base fait autorité entièrement.
  Aucune fusion : elle produirait des modèles fantômes.
- Permissions du Centre : **fail-closed**. Sans attribution explicite, refus.
  Migration `079_configuration_permissions_fail_closed.sql` — additive, **non
  appliquée** (aucune migration ne s'exécute au démarrage, `kermaria_api` n'a
  pas les droits DDL).

## Le motif d'écriture à ne pas défaire

Partout — paramètres, communications, modèles de démonstration — la mutation et
sa révision sont dans **une seule transaction** :

`BeginTransactionAsync` → `SELECT version, … FOR UPDATE` → écriture → révision →
`CommitAsync`.

Le `FOR UPDATE` sert à deux choses : vérifier la version attendue, et **relire la
valeur remplacée**. Fournie par l'appelant, elle ferait consigner un « avant »
qui n'a jamais existé.

Trois pièges déjà payés :

- une révision écrite **après** la mutation permet une valeur appliquée sans
  trace — indistinguable d'une valeur jamais modifiée, donc la seule défaillance
  qu'un audit ne rattrape pas ;
- un échec de stockage doit **lever** et remonter `*_STORAGE_UNAVAILABLE`.
  Confondu avec un conflit de version, il dit à l'administrateur l'inverse de la
  vérité (« quelqu'un a modifié » au lieu de « rien n'a été enregistré ») ;
- la concurrence fiscale utilise le **nombre de versions** comme version
  optimiste. Lu sur une connexion séparée avant l'insertion, il ne protégeait
  rien : deux administrateurs partis du même écran passaient tous les deux et la
  mention appliquée devenait silencieusement celle dont la date d'effet était la
  plus proche. Le décompte est maintenant pris `FOR UPDATE` dans la transaction
  d'insertion — sur un régime vide, InnoDB verrouille l'intervalle, ce qui est
  exactement ce qui rend un décompte utilisable comme version.

L'amorce des modèles de démonstration est **tout ou rien**, vacuité vérifiée
*dans* la transaction : une amorce partielle laissait une table non vide, donc
faisant autorité, et les modèles manquants devenaient invisibles **et** non
réamorçables.

## Autorités KoXo : appliquées, plus seulement affichées

C'était le point décisif de la revue. Voir [[koxo-ad-password-mastery]],
[[koxo-fiche-utilisateur-maitre]] et [[koxo-api-ne-cree-plus]].

- Le garde est posé **dans `LdapActiveDirectoryService`**, sur les 7 méthodes de
  cycle de vie (`CreateUser`, `DisableUser`, `MoveUserToDisabled`, `RenameUser`,
  `MoveUser`, `ChangeUserPassword`, `SetUserPassword`), et **pas** route par
  route. Raison : une identité doublée ou un mot de passe écrasé ne produit
  aucune erreur au moment où il se produit — une route oubliée ne se
  remarquerait pas. Refus : `409 AD_LIFECYCLE_KOXO_AUTHORITY`.
- Les opérations de **groupe** ne sont pas bloquées : c'est le mandat que l'API
  conserve, et ce sont elles qui ouvrent et ferment réellement l'accès.
- `/internal/profile/password` publie dans `IKoxoPendingPasswordStore`, marque
  la synchronisation `pending`, déclenche le webhook en rattrapage, et répond
  `AD_PASSWORD_CHANGE_PENDING_KOXO`. Avec `ForcePasswords=1`, KoXo réécrit le
  mot de passe depuis la colonne 14 du CSV à chaque passage : une écriture LDAP
  aurait été effacée sans erreur, **après** que le portail a annoncé
  « synchronisé avec Active Directory ». Relais inexploitable →
  `503 KOXO_PASSWORD_HANDOFF_UNAVAILABLE`, avant tout point de non-retour.
- `DemoConversionService` : le déplacement LDAP direct est **supprimé**, pas
  documenté comme exception. L'OU cible vient de `GroupeSecondaire` dans le CSV
  et KoXo la réapplique — le déplacement était hors mandat, sans effet durable,
  et retournait pourtant `identityMoved: true`. La conversion réserve le code de
  groupe réel puis déclenche la synchronisation. Corollaire assumé : l'absence
  de déplacement n'est plus comptée comme conversion partielle sous KoXo, sinon
  aucune conversion ne réussirait en production.
- `DemoProvisioningService` : la révocation d'essai retire les groupes de
  services et **délègue la désactivation du compte à KoXo**. Compter le refus
  LDAP comme un échec ferait rejouer une révocation déjà effective côté accès.

## Ce que les tests prouvent — et ce qu'ils ne prouvent pas

- `MockRevisionFailureSwitch` fait échouer l'écriture de révision **après** le
  contrôle de version et **avant** la publication : le rollback est exercé, pas
  déduit de la lecture du code.
- Pour l'annuaire, l'assertion est « l'écriture n'a pas été **tentée** », pas
  « l'écriture a échoué » : un appel parti et refusé laisse quand même une trace
  d'intention sur un annuaire de production. `RecordingActiveDirectoryService`
  compte les tentatives, le test exige zéro.
- Gardes structurels dans `verify-admin-contract.mjs` et
  `verify-ad-security-contract.mjs`, donc exécutés par `npm run validate`.
- **Limite de preuve** : tout tourne en persistance **mock**. Le comportement de
  verrouillage InnoDB n'est pas exercé. Les dépôts mock sont atomiques par
  construction pour que le test de rollback exerce la forme du code réel — cela
  ne remplace pas une exécution sur MariaDB. Toute validation SQL réelle se
  fait sur le serveur, dans une base temporaire clonée — jamais en local.

## Codes de réponse ajoutés

`SETTINGS_STORAGE_UNAVAILABLE`, `TEMPLATE_STORAGE_UNAVAILABLE`,
`FISCAL_STORAGE_UNAVAILABLE`, `DEMO_TEMPLATE_STORAGE_UNAVAILABLE`,
`AD_LIFECYCLE_KOXO_AUTHORITY`, `AD_PASSWORD_CHANGE_PENDING_KOXO`,
`KOXO_PASSWORD_HANDOFF_UNAVAILABLE`.
