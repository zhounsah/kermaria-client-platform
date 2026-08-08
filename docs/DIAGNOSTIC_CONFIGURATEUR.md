# Diagnostic et configurateur Zachary IT

## Architecture

Le diagnostic public vit dans `apps/webportal/app/diagnostic` et utilise le
moteur pur `apps/webportal/lib/public-diagnostic.ts`. Ce moteur ne lit aucun
prix et ne retourne pas de texte commercial final : il produit des codes metier
types (`needs_vpn`, `storage_unknown`, `requires_quote`, etc.) traduits dans
l'interface par `PublicDiagnosticWizard`.

Le configurateur public vit dans `apps/webportal/app/configurer` et utilise
`apps/webportal/lib/public-configurator.ts` pour normaliser la configuration
demandee depuis l'URL ou le formulaire. Cette couche ne fabrique aucune option
commerciale : elle accepte seulement les identifiants de pack, engagement,
paiement, utilisateurs, stockage et intentions VPN / bureau Windows.

## Source de verite catalogue

`packages/shared` decrit les identifiants et capacites publiques des packs
(`PublicPackManifest.capabilities`) : utilisateurs inclus, stockage inclus,
acces distant, VPN, bureau Windows, sauvegarde et audiences. Aucun prix
commercial n'y est duplique.

Les prix, frais de mise en service, politique fiscale, cadence de facturation
et variantes vendables restent resolus depuis `commercial_offers` cote API
interne, via `CatalogConfigurationService`.

La politique fiscale centrale est `IFiscalPolicy` / `FiscalPolicy` dans
`apps/api-internal/Services/FiscalPolicy.cs`. Elle distingue au minimum :

- `franchise_base` : Zachary IT ne collecte pas la TVA actuellement ; le
  montant payable est egal au montant catalogue et la mention fiscale est
  conservee dans les snapshots ;
- `standard` : un taux positif en basis points est applique pour les parcours
  qui en auront besoin plus tard.

Les composants React ne calculent pas la TVA. Ils affichent le montant et la
mention fournis par l'API (`fiscalRegime`, `fiscalMention`) via
`apps/webportal/lib/fiscal-formatters.ts`.

## Recommandation

Les regles actuelles sont explicites dans `recommendOffer` :

- sauvegarde simple et fichiers distants : Pack Dossier Securise ;
- besoin VPN : Pack Acces a Distance ;
- besoin bureau Windows distant : Pack Bureau Windows a Distance ;
- association, petite structure, plusieurs utilisateurs ou stockage jusqu'a
  64 Go : Pack Pro / Association ;
- volume ou nombre d'utilisateurs hors standard, structure non classee, ou
  bureau Windows multi-utilisateur : `requires_quote`.

Pour modifier une regle, ajuster `public-diagnostic.ts`, ajouter ou adapter le
code metier dans `packages/shared`, puis mettre a jour les libelles UI et le
test `test:diagnostic-configurator`.

## Resolution et securite des prix

Le BFF `/api/configurer/resolve` valide la forme publique puis relaie vers
`/internal/portal/configuration/resolve`. Le resolver API interne recharge
toujours `commercial_offers`, verifie que la variante est active et vendable,
et retourne soit :

- `ok` avec snapshot prix ;
- `requires_different_offer` avec le pack vendable conseille ;
- `requires_quote` quand aucun pack standard ne doit etre force.

Le navigateur ne transmet jamais un prix faisant autorite. Au signup,
`SignupService` recalcule `CatalogConfigurationInput` avec le catalogue courant
et ignore tout snapshot de prix fourni en parallele. Si un tarif ou le regime
fiscal change entre le diagnostic/configurateur et l'inscription, le snapshot
signup reflete le catalogue courant ou la configuration est rejetee.

## Stockage signup

L'inscription conserve deux objets distincts :

- `requestedConfiguration` : intention utilisateur, identifiants et quantites ;
- `resolution` : snapshot commercial resolu au moment de l'inscription,
  incluant offre retenue, prix, regime fiscal, mention fiscale, frais et
  lignes.

En MariaDB, ce snapshot est stocke dans
`signup_pending.catalog_configuration_snapshot_json`, separe du snapshot legacy
`pack_selection_snapshot_json`.

## Format URL

Le configurateur accepte des URLs partageables sans donnees personnelles :

```text
/configurer?pack=pack-acces-distance&commitment=6&payment=monthly&users=1&storage=32&vpn=yes&windows=no
```

Parametres acceptes :

- `pack` : identifiant de pack public ;
- `commitment` : `1`, `6` ou `12` ;
- `payment` : `monthly` ou `upfront` selon l'engagement ;
- `users` : valeur bornee vendable cote UI ;
- `storage` : `8`, `32`, `64` ou absent ;
- `vpn`, `windows` : `yes`, `no` ou absent.

Tout doublon ou valeur invalide est rejete. Les options inconnues sont ignorees
et aucun prix ni donnee personnelle ne doit etre place dans l'URL.
