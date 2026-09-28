# Amorcage de l'identite AD du compte principal (2026-09-25)

> État initial au 2026-09-25 : local, non commité, non déployé. Migration 096
> non appliquée en PROD à cette date. **Mise à jour du 2026-09-28 : le flux et
> la migration 096 sont déployés en DEV et validés par l'E2E identité standard
> complet ; PROD n'a pas été touchée.**
>
> Preuve base réelle initiale : base jetable SRV-06 (11.8.6), migrations
> 001-096 par le runner normal, `--primary-identity-bootstrap-schema` vert
> (rollback + concurrence compris), rejeu des migrations sans effet, base et
> compte detruits. Revele au passage : `ApproveAsync` levait sur double validation
> (lecteur ouvert au rollback), voir [pieges-sql-et-preuves.md](pieges-sql-et-preuves.md).

## Invariant decide par ZH

Tout compte client principal possede une identite AD dans CLIENTS.HOME.BZH,
**VPS compris**. Les services n'ajoutent/retirent ensuite que des groupes.

## Deux defauts corriges

- **B** : `SignupService.CompleteSelfServiceAccountAsync` (Cart + VPS) ne passait
  jamais par `ApplyPasswordAsync`. Exemption voulue pour le VPS (`9978cc8`,
  2026-09-01), heritee sans decision par le Cart (`9ee4a77`, 2026-09-21).
- **C** : `KoxoExportCandidateQuery` n'exportait un compte sans lien que pour
  l'essai demo ou l'utilisateur additionnel. En `controlled_write`, le signup
  standard etait donc lui aussi bloque (`AD_IDENTITY_NOT_READY` perpetuel) : le
  point 5 de [koxo-api-ne-cree-plus.md](koxo-api-ne-cree-plus.md) n'avait jamais
  ete livre. Seconde boucle : `SetPasswordAsync` exige un lien pour deposer le
  secret KoXo.

## Correctif

- Table `portal_user_identity_bootstrap` (migration 096, additive, sans
  backfill). Etats `awaiting_password` → `koxo_pending` → `directory_ready` →
  `completed`, plus `failed`. CHECK : pas de `completed` sans objectGUID ni date
  de liaison.
- La ligne nait dans la transaction de `ApproveAsync`. Cart/VPS : secret scelle
  depose dans cette meme transaction (`SignupApprovalRequest.InitialKoxoSecret`).
- Set-password sans lien : `SetPasswordForPrimaryIdentityBootstrapAsync`
  (condensat + jeton + secret + amorcage en une transaction, sans lien).
- 3e branche d'export : amorcage explicite + e-mail verifie si requis + secret
  non expire + pas de lien + pas de cycle additionnel. Transcrite par
  `PrimaryIdentityBootstrapPolicy`.
- Worker `PrimaryIdentityBootstrapConvergenceWorker` (si ecritures AD actives) ;
  declenchement KoXo « identite manquante » limite a 10 min par compte.
- Reprise des comptes existants : `POST /internal/admin/signups/{id}/identity-recovery`
  (auditee) → jeton `/set-password` ; non verifie → c'est la verification e-mail
  qui cree l'amorcage et envoie le lien.

## Pieges a retenir

- Le depot de liens mock est **statique** (partage par tout le processus) et ne
  connait que les clients `CLI-DEMO-00x` pour `GetCustomerUserLinksAsync`.
- Un `MockSignupRepository` sans `SealSink` fait echouer tout amorcage : le
  cablage DI le fournit, les fixtures de test doivent le fournir aussi (avec le
  MEME magasin que le service, sinon `AttachSealed` ne trouve pas le scelle).
- DEV (`AD_INTEGRATION_MODE=test` lu comme `disabled`) : aucun amorcage ne
  converge, par construction. Voir [dev-environment.md](dev-environment.md).

Doc : `docs/PRIMARY_IDENTITY_BOOTSTRAP.md`.

## E2E DEV du 2026-09-28

Le signup standard Melis Rochedune a confirmé le cycle réel
`awaiting_password → koxo_pending → completed` : password setup normal,
webhook DEV `password_set` en `202`, export `CLIENTS DEV`, synchronisation
KoXo, adoption stricte par `employeeNumber`, création de `customer_ad_links`
et `PRIMARY_IDENTITY_COMPLETED`. Aucun secret, hash ou jeton n'est mémorisé.
Le stockage headless reste hors périmètre et non validé.
