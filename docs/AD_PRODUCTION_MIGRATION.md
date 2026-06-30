# Procedure de sortie de `OU=TEST_SITE_WEB` vers une OU de production

Statut : **document de procedure**, livre dans la V0.25 (brique 3),
**execute uniquement en V1.0 RC** apres livraison du R740xd et validation
de la V1.0 beta 1. Aucune action prod ne doit etre prise sur la base de
ce document avant ces deux prerequis.

Document redige le 2026-06-30 a partir du cadrage
[V0.25_AD_FINALISATION.md](V0.25_AD_FINALISATION.md).

## Objet

Toute l'integration Active Directory en place (V0.18 a V0.25) est bornee
a l'OU de test :

```text
OU=TEST_SITE_WEB,DC=home,DC=bzh
```

Sous cette racine, le code attend la convention :

```text
OU=<CUSTOMER_REFERENCE>,OU=10_Customers,OU=TEST_SITE_WEB,DC=home,DC=bzh
  +-- OU=Users      (utilisateurs actifs)
  +-- OU=Groups     (groupes du client)
  +-- OU=Disabled   (utilisateurs desactives)
```

Cette convention est materialisee dans
[apps/api-internal/Services/ActiveDirectory/ActiveDirectoryPathScope.cs](../apps/api-internal/Services/ActiveDirectory/ActiveDirectoryPathScope.cs)
(`BuildCustomerOuDn`, `BuildUsersOuDn`, `BuildGroupsOuDn`,
`BuildDisabledOuDn`, `ExtractCustomerReference`). La racine
`OU=TEST_SITE_WEB,DC=home,DC=bzh` est en revanche **hardcodee** dans
[apps/api-internal/Data/Configuration/AdRuntimeConfiguration.cs](../apps/api-internal/Data/Configuration/AdRuntimeConfiguration.cs)
sous la forme :

```csharp
private const string RequiredTestOuRoot =
    "OU=TEST_SITE_WEB,DC=home,DC=bzh";
```

et le check `configurationValid` refuse toute autre valeur de
`AD_CLIENTS_OU_DN`. La sortie vers une OU de production passe donc
**obligatoirement par une modification de code**, pas par une simple
reconfiguration d'environnement.

## Prerequis

### Hardware

- R740xd livre, racke, alimente, accessible reseau (cf.
  [README.md](../README.md) section infrastructure et
  `infra-r740xd-blocker` cote memoire).
- V1.0 beta 1 livree et validee : services tournent, certificats poses,
  pare-feu en place, sauvegardes MariaDB en cours.

### AD de production

- une racine OU dediee a l'application est creee dans l'AD prod (ex :
  `OU=KERMARIA_CLIENTS,DC=<prod-domain>,DC=<tld>`) ;
- la convention `OU=10_Customers` est reproduite directement sous cette
  racine (le code n'accepte pas un autre nom de conteneur, cf.
  `ExtractCustomerReference` qui compare litteralement la chaine
  `10_Customers`) ;
- un compte de service prod dedie (different de celui de test) est cree
  avec les droits :
  - lecture sur la racine et tous les sous-arbres,
  - ecriture restreinte sur la racine (create/move/rename
    user et groupe ; reset de mot de passe utilisateur),
  - **aucun droit hors de la racine prod**.
- la racine prod est sauvegardee (snapshot AD) AVANT toute action.

### Logiciel

- branche de release V1.0 RC contenant la modification de code decrite
  ci-dessous, validee en revue ;
- `AD_DOMAIN`, `AD_CLIENTS_OU_DN`, `AD_SERVICE_ACCOUNT_USERNAME`,
  `AD_SERVICE_ACCOUNT_PASSWORD` prepares dans le coffre secrets prod ;
- `AD_INTEGRATION_MODE` peut etre bascule a `read_only` avant
  `controlled_write` pour une validation lecture-seule prealable.

## Modifications de code requises avant bascule

Une seule modification structurelle est necessaire. Sans elle, le
service refuse de demarrer en prod meme si la config est correcte.

### Levee du garde-fou `RequiredTestOuRoot`

Fichier :
[apps/api-internal/Data/Configuration/AdRuntimeConfiguration.cs](../apps/api-internal/Data/Configuration/AdRuntimeConfiguration.cs).

Remplacer :

```csharp
private const string RequiredTestOuRoot =
    "OU=TEST_SITE_WEB,DC=home,DC=bzh";

// ...

var configurationValid = !requiresDirectoryConfiguration
    || (
        domain is not null
        && clientsOuDn is not null
        && string.Equals(
            clientsOuDn,
            RequiredTestOuRoot,
            StringComparison.OrdinalIgnoreCase)
        && serviceAccountUsername is not null
        && serviceAccountPassword is not null
    );
```

par une variante qui :

1. enleve le check d'egalite stricte avec `RequiredTestOuRoot` ;
2. exige que `AD_CLIENTS_OU_DN` soit non vide, normalise, et termine par
   au moins un `DC=` (regle minimale syntaxique) ;
3. journalise au demarrage la racine effective utilisee (sans secret) ;
4. conserve la liste de racines autorisees sous forme d'allowlist
   explicite (env var `AD_ALLOWED_ROOTS=OU=...` en CSV) plutot qu'une
   seule valeur hardcodee, pour empecher une mauvaise configuration
   silencieuse (par exemple un `AD_CLIENTS_OU_DN=DC=home,DC=bzh` qui
   donnerait acces a tout le domaine).

La PR de levee de garde-fou est livree avec :
- tests unitaires `RuntimeConfigurationValidator` couvrant : DN vide,
  DN syntaxiquement invalide, DN hors allowlist, DN dans l'allowlist ;
- mise a jour de
  [docs/V0.19_AD_SECURITY_HARDENING.md](V0.19_AD_SECURITY_HARDENING.md)
  et de [docs/SECURITY.md](SECURITY.md) pour mentionner la nouvelle
  variable `AD_ALLOWED_ROOTS` ;
- mise a jour de
  [scripts/verify-ad-security-contract.mjs](../scripts/verify-ad-security-contract.mjs)
  pour valider l'allowlist.

Aucune autre modification du code AD n'est requise :
`ActiveDirectoryPathScope` est deja parametrable et fonctionne avec
n'importe quelle racine valide.

## Strategie de coexistence "test + prod"

Decision a prendre avant la bascule : faut-il garder l'OU de test
operationnelle pour audit/regression pendant que la nouvelle OU prod
sert les nouveaux clients ?

Deux options :

### Option A : bascule franche (recommandee)

- A J-bascule, `AD_CLIENTS_OU_DN` est reconfigure vers la racine prod.
- Les objets de test restent en place dans `OU=TEST_SITE_WEB` (non
  supprimes pour audit) mais ne sont plus accessibles par
  l'application : l'application ne lit/ecrit plus que dans la racine
  prod.
- Les `customer_ad_links` existantes pointant vers l'OU de test
  deviennent **orphelines fonctionnellement** : le code resoudra ces
  liens en echec ("objet AD introuvable dans l'OU configuree") et
  affichera "lien AD invalide" cote `/admin/customers/[ref]`.
- Une action admin (script ou ecran "re-lier") est requise pour chaque
  client deja en prod qui doit avoir un compte AD : reprovisionner ou
  re-lier dans la nouvelle OU.

**Pour :** simple, pas de double config, pas de logique conditionnelle.
**Contre :** brutal pour les clients existants ; necessite un script de
re-provisioning.

### Option B : double-OU transitoire (non implementee, evaluer en V1.0 RC)

- ajout d'une colonne `customers.ad_ou_override VARCHAR(1000) NULL`
  (nouvelle migration `018_customer_ad_ou_override.sql`) ;
- `ActiveDirectoryPathScope` devient instanciable par appel a partir de
  la valeur effective `customer.ad_ou_override ?? config.ClientsOuDn` ;
- les anciens clients gardent leur OU de test, les nouveaux clients
  arrivent sous la nouvelle OU prod ;
- migration progressive par lots.

**Pour :** zero rupture pour les clients existants.
**Contre :** double config, scope refactore (singleton -> per-request),
risque accru d'erreur de scope, audit plus complexe.

**Decision par defaut : Option A** sauf si, au moment de la bascule, on
a deja plus de 5 clients prod actifs avec lien AD. Tracer la decision
dans la PR de bascule.

## Procedure de bascule (Option A)

A executer fenetre de maintenance annoncee, ordre strict.

### J-7 : preparation

1. Verifier que la PR "levee `RequiredTestOuRoot`" est mergee et
   deployee en preprod (sans changer encore `AD_CLIENTS_OU_DN`).
2. Verifier `npm run test:ad-security` vert sur preprod.
3. Snapshot AD prod (racine cible).
4. `mysqldump` complet de la base prod : table `customer_ad_links`,
   `customers`, `audit_log` au minimum.
5. Communiquer aux clients existants ayant un lien AD : fenetre de
   reprovisioning prevue.

### J-1 : repetition

1. Sur l'environnement de preprod :
   - configurer `AD_CLIENTS_OU_DN` vers une racine prod **factice** de
     repetition (pas la vraie racine prod) ;
   - basculer `AD_INTEGRATION_MODE=read_only` ;
   - redemarrer ;
   - verifier que l'audit log enregistre l'evenement
     `ad.startup.root_changed` (a ajouter dans la PR de levee de
     garde-fou) ;
   - re-provisionner un client de test, verifier le flux.
2. Documenter le timing exact de chaque etape.

### J0 : bascule prod

Dans l'ordre, sans sauter :

1. **Mode lecture seule prealable**
   - `AD_INTEGRATION_MODE=read_only`
   - `AD_CLIENTS_OU_DN=<nouvelle racine prod>`
   - `AD_SERVICE_ACCOUNT_USERNAME=<compte prod>`
   - `AD_SERVICE_ACCOUNT_PASSWORD=<secret prod>` (rotation depuis le
     coffre)
   - redemarrage API-INTERNAL
   - verifier les logs : pas d'exception `RuntimeConfigurationException`,
     log `ad.startup.root_changed` present
   - verifier UI `/admin/customers/[ref]` : la section AD affiche "mode
     read_only" et liste les objets visibles (initialement zero, car la
     racine prod est vide)
2. **Re-provisioning des clients prioritaires**
   - pour chaque client prod existant a re-lier :
     - admin clique "Provisionner AD" (brique 2 V0.25) qui cree l'objet
       sous `OU=Users,OU=<REF>,OU=10_Customers,<racine prod>` ;
     - le lien `customer_ad_links` est mis a jour (nouveau
       `object_guid`, nouveau DN) ;
     - audit `ad.user.created` enregistre.
   - **Note** : passer en `controlled_write` temporairement pour cette
     etape, repasser en `read_only` immediatement apres si du temps
     mort est attendu avant l'ouverture aux nouveaux clients.
3. **Mode normal**
   - `AD_INTEGRATION_MODE=controlled_write`
   - redemarrage
   - smoke test : creation d'un utilisateur, ajout au groupe, reset
     mot de passe (brique 1 V0.25), desactivation, deplacement.

### J+1 a J+7 : surveillance

- monitoring du nombre d'erreurs `ad.*` dans l'audit log ;
- check quotidien des liens orphelins :
  `SELECT customer_id, distinguished_name FROM customer_ad_links
   WHERE distinguished_name LIKE '%TEST_SITE_WEB%'` ;
- les orphelins detectes declenchent un ticket ops.

## Plan de rollback

Si une anomalie bloquante est detectee entre J0 et J+7 :

1. `AD_INTEGRATION_MODE=disabled` (coupe l'integration sans casser le
   reste de l'application : le portail continue de fonctionner sans
   acces AD).
2. Si la cause est identifiee comme la nouvelle racine prod :
   - re-mettre `AD_CLIENTS_OU_DN=OU=TEST_SITE_WEB,DC=home,DC=bzh`
   - `AD_INTEGRATION_MODE=read_only` puis `controlled_write` apres
     validation
   - les objets re-provisionnes en prod restent en place (non
     supprimes) ; ils seront repris a la prochaine tentative.
   - les liens `customer_ad_links` re-pointent automatiquement vers
     l'OU de test pour les clients dont le lien n'a pas ete touche ;
     les liens crees pendant la fenetre prod doivent etre nettoyes via
     SQL :
     `DELETE FROM customer_ad_links WHERE linked_at >= '<J0>'
      AND distinguished_name NOT LIKE '%TEST_SITE_WEB%'`
   - audit `ad.rollback.executed` saisi manuellement.
3. Communication aux clients impactes.

Le rollback est **degrade** par construction : tout client provisionne
en prod entre J0 et la decision de rollback perd son lien AD. Ne pas
sous-estimer.

## Controles avant bascule

Checklist a cocher dans la PR de bascule (J-7) :

- [ ] PR "levee `RequiredTestOuRoot`" mergee, deployee en preprod,
      tests verts depuis 7 jours minimum.
- [ ] `AD_ALLOWED_ROOTS` configure et restreint a la racine prod
      cible (et eventuellement la racine de test pour la fenetre de
      bascule).
- [ ] Compte de service prod cree, scope ACL verifie sur la racine
      prod uniquement (NE PAS donner les droits hors racine).
- [ ] Mot de passe du compte de service prod stocke dans le coffre
      secrets prod, jamais en clair dans un repo ou un script.
- [ ] Snapshot AD prod realise et restauration testee.
- [ ] `mysqldump` de prod realise et restauration testee.
- [ ] Sauvegardes MariaDB recentes verifiees
      (cf. [V0.24_STABILISATION_TESTABLE.md](V0.24_STABILISATION_TESTABLE.md)).
- [ ] Repetition J-1 executee sur preprod avec racine factice.
- [ ] Liste des clients a re-provisionner figee et communiquee.
- [ ] Fenetre de maintenance annoncee.
- [ ] `npm run test:ad-security` vert sur la branche de release.

## Audit et points de vigilance

- **Aucune suppression** d'objet AD dans la fenetre de bascule (ni
  cote test, ni cote prod). Tout est `Disabled` ou conserve tel quel.
- **Aucun hard delete** dans `customer_ad_links` sauf dans le cadre
  strict du rollback decrit ci-dessus.
- L'audit log doit montrer une trace continue : avant la bascule, on
  voit des operations sur l'OU de test ; apres, sur la racine prod.
  L'evenement `ad.startup.root_changed` marque la transition et doit
  etre present.
- Les changements de mot de passe AD (brique 1 V0.25) restent disables
  par defaut (`AD_PASSWORD_CHANGE_ENABLED=false`). Ne pas activer en
  meme temps que la bascule d'OU : separer en deux fenetres de
  maintenance distinctes.
- Le mode `live` PayPal/BPCE/EMAIL n'est pas couple a cette procedure :
  ils peuvent etre actives independamment (mais idealement apres
  stabilisation de la bascule AD).

## Limites assumees

- Pas de migration automatique d'objets AD (move d'arbres) : la
  procedure est un **re-provisioning** par client, pas un deplacement
  AD-side de l'ancienne OU vers la nouvelle. Le deplacement AD natif
  est theoriquement possible mais hors scope V1.0 RC (risque eleve,
  GUID/SID change non maitrise selon la methode).
- Pas de procedure multi-domaine ni de trusts AD : un seul domaine
  prod.
- Pas de federation SSO (Azure AD, OIDC) : c'est une migration
  d'integration LDAP existante, pas un changement d'architecture
  d'authentification.
- Pas d'automatisation de la levee de garde-fou `RequiredTestOuRoot` :
  c'est une PR manuelle, revue, mergee, deployee. Pas de feature flag
  pour basculer dynamiquement entre racine test et racine prod sur la
  meme instance (`ActiveDirectoryPathScope` est un singleton).
