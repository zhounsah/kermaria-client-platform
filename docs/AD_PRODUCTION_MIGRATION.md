# Procedure de sortie de `OU=TEST_SITE_WEB` vers `clients.home.bzh`

Statut : **document de procedure**, mis a jour le 2026-07-18 apres la
creation du domaine enfant `clients.home.bzh`. Ce document decrit la
bascule des **operations AD admin actuelles** hors de l'OU de test. Il ne
decrit pas, a lui seul, la future refonte V0.38 du signup et du modele de
donnees.

Documents lies :

- [`V0.25_AD_FINALISATION.md`](V0.25_AD_FINALISATION.md)
- [`v0.38/V0.38_SITE_AD_ALIGNMENT.md`](v0.38/V0.38_SITE_AD_ALIGNMENT.md)
- [`v0.38/V0.38_KOXO_SIGNUP_INTEGRATION.md`](v0.38/V0.38_KOXO_SIGNUP_INTEGRATION.md)

## 1. Objet

Jusqu'ici, les operations Active Directory exposees par le portail
(`search`, `create`, `rename`, `move`, `groups`, `password`) ont ete
recettees sous :

```text
OU=TEST_SITE_WEB,DC=home,DC=bzh
```

L'objectif de cette procedure est de faire basculer ces operations vers
une racine dediee dans :

```text
OU=Clients,DC=clients,DC=home,DC=bzh
```

Important :

- cette procedure concerne d'abord les operations AD admin existantes ;
- le signup V0.26 reste mono-utilisateur et sans creation AD automatique ;
- la convergence des donnees site -> AD est traitee a part en V0.38.

## 2. Verite actuelle dans le code

Le depot n'est plus bloque par un garde-fou hardcode "test only".

Le code actuel supporte deja :

- `AD_DOMAIN`
- `AD_CLIENTS_OU_DN`
- `AD_REQUIRED_OU_ROOT`
- `AD_ALLOWED_ROOTS`

Le scope AD est resolu par
`apps/api-internal/Data/Configuration/AdRuntimeConfiguration.cs`.

Les services AD courants :

- `LdapActiveDirectoryService`
- `MockActiveDirectoryService`

utilisent encore `OU=TEST_SITE_WEB,DC=home,DC=bzh` comme **fallback** si la
configuration est absente ou incomplete, mais ce n'est plus une contrainte
structurelle du code.

Autre limite toujours presente : `customer_ad_links` reste un modele centre
sur le `customer`, pas sur le `portal_user`. La bascule de racine AD est
possible, mais elle ne vaut pas encore alignement V0.38 complet.

Autre point important pour la cible retenue : certains chemins admin
historiques ont ete penses autour de `OU=10_Customers` et de groupes
locaux par client. L'arborescence documentaire retenue maintenant est plus
simple (`OU=Clients/<CUSTOMER_REFERENCE>/Users|Disabled`) et suppose
l'alignement progressif de ces chemins avant activation definitive.

## 3. Cible logique

Le domaine cible est :

```text
clients.home.bzh
```

La structure logique retenue est :

```text
OU=Clients,DC=clients,DC=home,DC=bzh
  +-- OU=<CUSTOMER_REFERENCE>
      +-- OU=Users
      +-- OU=Disabled
```

Les groupes de securite restent, eux, dans le domaine parent :

```text
OU=SecurityGroups,OU=Kermaria,DC=home,DC=bzh
  +-- CN=GG_VPN
  +-- CN=GG_RDS
  +-- CN=GG_Radio
```

L'architecture cible suppose donc :

- une segregation par `customerReference`
- les sous-OUs `Users` et `Disabled`
- aucune distinction `client pro` / `client simple` dans le nom des OUs
- des groupes de securite centralises dans le domaine parent

Le DN de racine retenu pour les comptes clients web est :

```text
OU=Clients,DC=clients,DC=home,DC=bzh
```

## 4. Prequis

### Infrastructure

- domaine enfant `clients.home.bzh` cree et resolvable
- racine `OU=Clients,DC=clients,DC=home,DC=bzh` creee dans ce domaine
- conteneur de groupes
  `OU=SecurityGroups,OU=Kermaria,DC=home,DC=bzh` cree ou valide
- compte de service dedie avec droits limites a cette racine
- verification des ACL sur la racine et ses sous-arbres
- verification des ACL de gestion de membres sur les groupes du domaine
  parent
- sauvegarde AD avant bascule

### Application

- branche de deploiement contenant le code AD courant
- valeurs de configuration preparees :
  - `AD_DOMAIN=clients.home.bzh`
  - `AD_CLIENTS_OU_DN=OU=Clients,DC=clients,DC=home,DC=bzh`
  - `AD_REQUIRED_OU_ROOT=OU=Clients,DC=clients,DC=home,DC=bzh`
  - `AD_ALLOWED_ROOTS=OU=Clients,DC=clients,DC=home,DC=bzh`
- `AD_INTEGRATION_MODE=read_only` disponible pour une validation lecture
  seule avant passage en `controlled_write`

### Donnees

- export de `customer_ad_links`
- export de `customers`
- export des journaux d'audit utiles

## 5. Decision de coexistence

Deux strategies restent possibles.

### Option A - bascule franche

- l'application pointe uniquement vers `clients.home.bzh`
- l'ancienne OU de test reste archivee mais n'est plus pilotee
- les liens AD historiques hors nouvelle racine devront etre re-lies ou
  re-provisionnes

C'est l'option recommandee.

### Option B - double racine transitoire

- une partie des clients reste en test
- une partie des clients part sur `clients.home.bzh`

Cette option n'est pas recommandee avec le modele courant, car elle
complexifie trop le scope AD et la lecture de `customer_ad_links`.

## 6. Configuration cible

Exemple de configuration attendue :

```text
AD_DOMAIN=clients.home.bzh
AD_CLIENTS_OU_DN=OU=Clients,DC=clients,DC=home,DC=bzh
AD_REQUIRED_OU_ROOT=OU=Clients,DC=clients,DC=home,DC=bzh
AD_ALLOWED_ROOTS=OU=Clients,DC=clients,DC=home,DC=bzh
AD_INTEGRATION_MODE=read_only
```

`AD_ALLOWED_ROOTS` doit rester une allowlist stricte, bornee a
`OU=Clients,DC=clients,DC=home,DC=bzh`.

## 7. Procedure de bascule

### J-7

1. Creer et valider `OU=Clients,DC=clients,DC=home,DC=bzh`.
2. Creer et tester le compte de service.
3. Verifier les ACL de gestion de membres sur `GG_VPN`, `GG_RDS`,
   `GG_Radio`.
4. Verifier que `npm run test:ad-security` reste vert.
5. Exporter `customer_ad_links`, `customers` et les audits utiles.
6. Snapshot AD de la nouvelle racine.

### J-1

1. Deployer la configuration cible avec `AD_INTEGRATION_MODE=read_only`.
2. Redemarrer l'API.
3. Verifier l'etat AD dans l'admin.
4. Verifier qu'une recherche AD repond depuis la nouvelle racine.
5. Confirmer qu'aucune operation n'echoue pour cause de scope.

### J0

1. Passer en `read_only` sur la nouvelle racine.
2. Verifier la lecture des objets visibles.
3. Basculer ensuite en `controlled_write`.
4. Jouer un client temoin :
   - recherche
   - creation user
   - rattachement a `GG_VPN` / `GG_RDS` / `GG_Radio` dans le domaine parent
   - rename
   - move `Users <-> Disabled`
   - changement de mot de passe si active
5. Re-lier ou re-provisionner les clients prioritaires deja presents.

### J+1 a J+7

- surveiller les erreurs `ad.*`
- lister les `customer_ad_links` encore pointant vers `TEST_SITE_WEB`
- ouvrir un suivi ops pour chaque lien non migre

## 8. Verification minimale

La bascule est consideree saine si :

- l'etat AD admin repond sur la nouvelle racine
- un user temoin peut etre cree dans `clients.home.bzh`
- un user temoin peut etre rattache a un groupe `GG_*` du domaine parent
- le lien MariaDB correspondant est coherent
- aucune operation ne sort des racines autorisees

## 9. Rollback

En cas d'anomalie bloquante :

1. repasser `AD_INTEGRATION_MODE=disabled` ou `read_only`
2. remettre les variables de racine sur l'OU de test
3. redemarrer l'API
4. conserver les objets crees dans `clients.home.bzh` pour analyse
5. ne nettoyer `customer_ad_links` qu'avec une action volontaire et tracee

Le rollback degrade n'efface pas magiquement les objets crees dans le
domaine enfant. Il doit etre pilote comme un incident d'exploitation.

## 10. Ce que cette procedure ne couvre pas

- la creation AD automatique depuis le signup
- le multi-utilisateur pro/association
- le suivi AD par `portal_user`
- la synchronisation continue du mot de passe portail -> AD
- la reprise KoXo

Ces sujets relevent de V0.38 et de :

- [`v0.38/V0.38_SITE_AD_ALIGNMENT.md`](v0.38/V0.38_SITE_AD_ALIGNMENT.md)
- [`v0.38/V0.38_KOXO_SIGNUP_INTEGRATION.md`](v0.38/V0.38_KOXO_SIGNUP_INTEGRATION.md)
- [`v0.38/V0.38_KOXO_DATA_CONTRACTS.md`](v0.38/V0.38_KOXO_DATA_CONTRACTS.md)
