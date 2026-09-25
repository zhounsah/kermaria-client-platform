# Amorcage de l'identite AD du compte client principal

> Etat : **developpe localement, non deploye, migration 096 non appliquee en
> production ni en DEV.** Valide le 2026-09-25 sur une base jetable de SRV-06
> (MariaDB 11.8.6, migrations 001 a 096 par le runner normal, base et compte
> detruits ensuite) : `--primary-identity-bootstrap-schema` vert, rejeu des
> migrations sans effet.

## Invariant metier

Tout compte client principal possede une identite AD dans `CLIENTS.HOME.BZH`.
Les services achetes n'ajoutent ou ne retirent ensuite que des groupes de
securite. Cela vaut pour les trois parcours d'inscription : standard (validation
humaine), panier (Cart) et VPS. Le VPS n'ajoute pas forcement de groupe, mais son
titulaire a une identite comme tout client.

## Ce qui etait casse

| Defaut | Effet |
|---|---|
| **B** — `CompleteSelfServiceAccountAsync` (Cart, VPS) creait `customer` + `portal_user` + `koxo_unique_identifier` sans jamais passer par `ApplyPasswordAsync`. L'exemption « pas besoin d'AD » ecrite pour le VPS (`9978cc8`) a ete heritee par le Cart (`9ee4a77`). | Aucune identite, aucun lien : le provisioning Billing V2 echoue en `BILLING_V2_PROVISIONING_IDENTITY_NOT_LINKED`. |
| **C** — `KoxoExportCandidateQuery` n'exportait un compte sans lien que s'il etait un essai de demo ou un utilisateur additionnel. | En `controlled_write`, meme le signup standard restait bloque : pas de lien → pas d'export → KoXo ne cree rien → `AD_IDENTITY_NOT_READY` a chaque set-password → pas de lien. Et `SetPasswordAsync` refusait de deposer le secret KoXo sans lien a mettre a jour. |

## Le cycle

Table `portal_user_identity_bootstrap` (une ligne par compte principal).

```
awaiting_password ──(mot de passe + secret chiffre, 1 transaction)──▶ koxo_pending
koxo_pending ──(export KoXo : e-mail verifie si requis + secret vivant)──▶ KoXo cree l'objet
koxo_pending ──(adoption par employeeNumber exact + verification stricte)──▶ directory_ready
directory_ready ──(UpsertPortalUserLinkAsync + relecture du lien)──▶ completed
* ──(objet d'un autre client / transfert d'identite / objet invalide)──▶ failed
koxo_pending ──(secret expire, objet absent)──▶ awaiting_password
```

- **Naissance** : la ligne est creee par `ApproveAsync`, dans la transaction
  qui cree le compte. Standard : `awaiting_password`. Cart / VPS : le mot de
  passe saisi est scelle (AES-256-GCM, meme magasin que les utilisateurs
  additionnels) et depose dans cette meme transaction → `koxo_pending`.
- **E-mail** : un compte self-service n'est ni exporte ni cree dans l'annuaire
  avant `portal_users.email_verified_at`. La verification relance la
  synchronisation KoXo et une premiere convergence. Le checkout exige toujours
  l'e-mail verifie.
- **Signup standard** : la validation humaine est conservee. Le set-password
  d'un compte sans lien passe par `SetPasswordForPrimaryIdentityBootstrapAsync`,
  qui ecrit le condensat, retire le jeton, depose le secret et passe
  l'amorcage en `koxo_pending` en une seule transaction, **sans exiger de lien**.
  Le set-password repond `PASSWORD_SET` ; l'identite converge ensuite.
- **Adoption** (`controlled_write`) : `ResolveUserByEmployeeNumberAsync` avec le
  `CLI-NNNNNN` exact, puis `PrimaryIdentityBootstrapPolicy.ValidateDirectoryObject`
  (type `user`, objectGUID valide, SID de domaine, sAMAccountName ≤ 20, DN dans
  l'OU `OU=<reference client>`, reference client identique si renseignee). Un
  objet desactive est attendu, pas adopte. Aucun rapprochement approchant.
- **Mode mock** : pas de KoXo, l'application cree l'objet simule avec un nom
  deterministe (nom + chiffres du `CLI-NNNNNN`), donc rejouable sans doublon.
- **Conclusion** : `completed` n'est ecrit que si le lien `customer_ad_links`
  de ce compte, de ce client et de cet objectGUID existe (clause SQL de
  l'UPDATE + contrainte CHECK de la migration). Le secret est acquitte juste
  avant.
- **Convergence** : `PrimaryIdentityBootstrapConvergenceWorker` (30 s, lots de
  50), enregistre seulement si les ecritures AD sont actives. Une
  synchronisation KoXo « identite manquante » est redemandee au plus toutes les
  10 minutes par compte : chaque synchronisation KoXo est globale.

## Export KoXo

Troisieme exception de `KoxoExportCandidateQuery`, transcrite par
`PrimaryIdentityBootstrapPolicy.GetExportBlocker` :

- client actif, compte actif, client non demo (donc jamais une vitrine) ;
- etat civil complet et `koxo_unique_identifier` egal des deux cotes ;
- amorcage `koxo_pending` ou `directory_ready` designant ce compte et ce client ;
- `email_verification_required = FALSE` ou `email_verified_at` pose ;
- aucun lien AD utilisateur ;
- secret `koxo_pending_directory_passwords` non expire ;
- aucun cycle d'utilisateur additionnel pour ce compte.

Jamais « tout portal_user sans lien AD ».

## Reprise des comptes existants

Cas vise : compte (par exemple Cart en PROD) avec `koxo_unique_identifier`, sans
`customer_ad_links(user)`, mot de passe clair perdu. **On ne reconstitue jamais
l'ancien mot de passe.**

- **Adresse deja verifiee** : un administrateur appelle
  `POST /api/admin/signups/{id}/identity-recovery`, qui passe par le BFF (CSRF)
  puis par `POST /internal/admin/signups/{id}/identity-recovery` (audit
  `signup.primary_identity_recovery`). La transaction cree l'amorcage au besoin
  en `awaiting_password` et emet un jeton `/set-password` ; le lien part a
  l'adresse du compte. Le nouveau mot de passe fait entrer le compte dans le
  cycle.
- **Adresse non verifiee** : la reprise repond `EMAIL_VERIFICATION_REQUIRED`.
  La verification e-mail cree l'amorcage et envoie elle-meme le lien de
  definition.
- **Rejouable** : chaque demande rend le lien precedent inutilisable. Refus
  explicites : `PRIMARY_IDENTITY_IN_PROGRESS` (secret vivant ou objet deja
  resolu), `PRIMARY_IDENTITY_ALREADY_LINKED`, `PRIMARY_IDENTITY_CONFLICT`
  (amorcage `failed`, arbitrage humain).

Aucun bouton d'interface n'appelle encore cette route.

## Tests

- `dotnet <SmokeTests.dll> --primary-identity-bootstrap` : parcours Cart, standard,
  VPS, annuaire desactive, adoption fail-closed, reprise apres creation KoXo,
  noop sur lien existant, concurrence, anti-transfert, double inscription,
  limitation des declenchements KoXo, reprise des comptes anciens, secret
  expire, et resolution d'identite du provisioning de bout en bout (mock).
- `dotnet <SmokeTests.dll> --primary-identity-bootstrap-schema` : **MariaDB
  jetable** (`BILLING_V2_TEST_MARIADB_CONNECTION`, migrations 001 a 096). Execute
  la vraie requete d'export, les contraintes de la migration 096, le rollback
  de `ApproveAsync` et du set-password apres ecriture, et des courses reelles
  (double validation, double set-password, double reprise : un seul amorcage).
- Base jetable : la migration 066 cree des triggers, refuses a un compte
  cantonne sous binlog. Comme pour la DEV, les creer avec le compte
  d'administration et `DEFINER` = compte temporaire, puis relancer le runner.

## Deploiement (a faire, non fait)

1. Sauvegarde (`npm run backup:mariadb`), puis migration 096 par
   `kermaria_migrator`.
2. Executer la suite schema sur une base jetable clonee avant la production.
3. Deployer SRV-13 puis SRV-12. Le worker demarre si les ecritures AD sont
   actives.
4. Reprendre les comptes existants un par un via la route de reprise.
