# Refonte adaptative de `/diagnostic`

Date : 28 août 2026  
Projet : `kermaria-client-platform`  
Branche : `main`

## 1. État initial audité

Le chantier a été ouvert sans reset, stash ni suppression des changements déjà présents dans le worktree.

Au début de l'intervention :

```text
HEAD        = 3ee507eab977069b5cd887706926b9f01a36751e
origin/main = 3ee507eab977069b5cd887706926b9f01a36751e
```

Le worktree contenait déjà un chantier Storefront / Lot C, notamment des modifications de pages Services, de `globals.css`, de `storefront-content.ts`, de `ContactForm.tsx` et plusieurs fichiers non suivis. Ces changements ont été considérés comme préexistants et conservés.

## 2. Architecture de `/diagnostic` avant refonte

La route publique `/diagnostic` :

1. chargeait le catalogue public Billing V2 via `getBillingV2FormulesCatalog()` ;
2. rendait `PublicDiagnosticWizard` ;
3. conservait les réponses uniquement dans l'état React côté navigateur ;
4. transformait les réponses en `DiagnosticAnswers` ;
5. exécutait `recommendOffer()` côté WebPortal ;
6. envoyait uniquement une sélection de codes catalogue à `/api/formules/devis` afin que Billing V2 calcule le prix ;
7. orientait les cas hors standard vers `/contact`.

Le diagnostic ne créait pas de lead, ne persistait pas ses réponses dans une table métier et ne modifiait aucun objet Billing ou provider.

### Contact et anti-spam

Le formulaire de contact existant utilise :

```text
WebPortal /api/contact
    ↓
API-INTERNAL /internal/public/contact-message
    ↓
EmailDispatchService
    ↓
e-mail + journalisation email_messages
```

Le BFF `/api/contact` applique :

- validation des champs ;
- longueur maximale ;
- validation d'adresse e-mail ;
- rate-limit de 5 soumissions sur 5 minutes par identifiant de requête ;
- validation de `formuleCode` contre les presets Billing publiés lorsqu'il est présent.

Il n'y a actuellement ni hCaptcha ni honeypot sur ce formulaire. Il n'existe pas non plus de table de leads dédiée au diagnostic.

## 3. Problèmes constatés

Le wizard historique était construit autour de quatre thèmes fixes :

```text
Profil → Données → Accès → Reprise
```

Son modèle convenait principalement à la sauvegarde et, dans une moindre mesure, aux accès distants. Il donnait une impression incohérente lorsqu'un visiteur arrivait depuis une page comme :

- UniFi ;
- messagerie professionnelle ;
- gestion DNS et domaines ;
- infogérance VPS ;
- hébergement web.

La route, les métadonnées SEO et le contenu d'introduction étaient également centrés sur la sauvegarde/stockage.

## 4. Architecture retenue

La nouvelle architecture reste entièrement dans WebPortal.

```text
page Service
    ↓
mapping slug → contexte diagnostic
    ↓
/diagnostic?context=<contexte>
    ↓
allowlist stricte
    ↓
introduction adaptée
    ↓
questions progressives et conditionnelles
    ↓
orientation
    ├─ cas représentable par Billing V2 → formule existante + devis serveur
    └─ cas à cadrer → contact prérempli
```

Aucune migration SQL n'est nécessaire.

Aucune évolution d'API n'est nécessaire pour cette version.

## 5. Contrat de transmission du contexte

Le contrat public est :

```text
/diagnostic?context=<identifiant>
```

Identifiants autorisés :

```text
backup
remote-access
network
messaging
domain-dns
server
web-hosting
general
```

Le paramètre est traité par une allowlist fermée dans `lib/diagnostic-context.ts`.

Un paramètre :

- absent ;
- vide ;
- inconnu ;
- forgé ;
- contenant une tentative d'ajouter une valeur Billing ;

retombe sur `general`.

Le CMS ne choisit pas directement l'identifiant de contexte : les pages Services transmettent leur contexte via un mapping de code WebPortal à partir du slug de page. Cela évite qu'un contenu administrable puisse transformer le comportement commercial.

## 6. Regroupement des pages Services

| Pages Services | Contexte |
| --- | --- |
| `sauvegarde-externalisee`, `supervision-nas` | `backup` |
| `vpn-entreprise`, `bureau-windows-distance` | `remote-access` |
| `unifi`, `firewall` | `network` |
| `messagerie-professionnelle` | `messaging` |
| `gestion-dns-domaines` | `domain-dns` |
| `vps`, `infogerance-vps`, `maintenance-linux`, `supervision-informatique` | `server` |
| `hebergement-web`, `maintenance-wordpress`, `cloudflare-waf` | `web-hosting` |

`/services/domaines-messagerie` agrège volontairement plusieurs sujets. Son CTA global n'impose donc pas de contexte : il renvoie vers le parcours général.

## 7. Questions par contexte

### Sauvegarde / protection des données

Le parcours demande :

- ce qu'il faut protéger ;
- le volume approximatif ;
- le type de structure ;
- le nombre de personnes concernées ;
- l'existence d'une sauvegarde distincte ;
- si une restauration a été testée.

Un cas avec serveur, NAS, poste complet ou périmètre mixte est volontairement orienté vers un cadrage.

### Accès distant

Le parcours demande :

- ce que l'utilisateur veut atteindre depuis l'extérieur ;
- le type de structure ;
- le nombre de personnes ;
- si les ressources existent déjà ;
- le nombre de sites lorsque pertinent ;
- les appareils utilisés.

Le visiteur n'a pas à choisir lui-même « VPN » ou « RDS ». Le besoin « environnement Windows complet » peut mener au parcours RDS existant ; fichiers/application interne peuvent mener à l'accès distant existant si le périmètre reste simple.

### Réseau / Wi-Fi / UniFi

Le parcours demande :

- le problème ou l'objectif ;
- l'installation existante ;
- le nombre de sites ;
- un ordre de grandeur du nombre d'appareils.

Le résultat propose un cadrage/audit, pas une sélection de matériel automatique.

### Messagerie professionnelle

Le parcours demande :

- l'objectif ;
- si un domaine existe ;
- le nombre de boîtes ;
- le service de messagerie actuel ;
- les données à reprendre seulement lorsque la situation le justifie.

### Domaine / DNS

Le parcours demande :

- l'objectif ;
- qui contrôle le domaine ;
- les services qui en dépendent ;
- si un service est actuellement bloqué.

Aucun enregistrement DNS technique n'est demandé au client.

### Serveur / VPS / infogérance

Le parcours demande :

- l'objectif ;
- les usages du serveur ;
- si le serveur existe déjà ;
- les accès d'administration uniquement lorsque pertinent ;
- l'état du suivi des mises à jour/sauvegardes ;
- l'impact d'une panne.

### Hébergement web

Le parcours demande :

- nouveau site, migration, reprise, maintenance ou incident ;
- le type de site lorsque connu ;
- l'existence du domaine ;
- les accès uniquement lorsque le site existe ;
- le suivi des sauvegardes et mises à jour.

### Accès direct

`/diagnostic` affiche une page d'orientation avec les sept sujets métier. Le visiteur sélectionne son problème avant d'entrer dans les questions détaillées.

## 8. Éléments communs

Tous les parcours utilisent :

- une introduction contextuelle ;
- une question à la fois ;
- une progression visible ;
- des boutons précédent/suivant ;
- des choix « Je ne sais pas » lorsque réalistes ;
- des questions conditionnelles ;
- un résumé des réponses ;
- une orientation lisible ;
- un formulaire de contact prérempli ;
- aucune obligation de créer un compte.

Lorsqu'une question conditionnelle devient invisible après modification d'une réponse précédente, sa valeur est supprimée de l'état du diagnostic.

## 9. Frontière commerciale et Billing V2

Billing V2 reste l'autorité commerciale.

Le diagnostic :

- ne contient aucun prix en dur ;
- ne calcule aucune TVA ;
- ne contient aucun `serviceCode` ou `tierCode` client ;
- ne rend pas une page non self-service commandable ;
- ne crée pas un deuxième catalogue ;
- n'altère pas les routes `/formules/...`.

Seuls deux contextes peuvent produire une sélection commerciale, parce que le moteur actuel sait réellement les représenter :

```text
backup
remote-access
```

### Sauvegarde

Une recommandation de formule n'est possible que pour un besoin simple portant exactement sur des fichiers.

Ces cas forcent un cadrage :

- ordinateur complet ;
- serveur ;
- NAS ;
- plusieurs types de cibles ;
- cible inconnue.

### Accès distant

Une formule n'est possible que lorsque le besoin est suffisamment clair et représentable par les presets existants.

Ces cas forcent un cadrage :

- plusieurs types d'accès ;
- besoin inconnu ;
- plusieurs sites.

### Tous les autres contextes

`network`, `messaging`, `domain-dns`, `server` et `web-hosting` ne peuvent jamais produire une formule par le moteur adaptatif, même si un utilisateur forge artificiellement des réponses ressemblant à celles de sauvegarde ou d'accès distant.

Lorsqu'une formule est proposée, son prix est demandé à :

```text
POST /api/formules/devis
```

Le serveur recalcule donc le montant à partir du catalogue Billing V2.

## 10. Sécurité et validation

Les garde-fous ajoutés ou conservés sont :

- allowlist du paramètre `context` ;
- mapping slug → contexte fermé côté code ;
- aucune donnée commerciale transportée dans le contexte ;
- aucune création de formule depuis un contexte non autorisé ;
- devis toujours calculé côté serveur ;
- `formuleCode` du contact validé contre les presets publiés ;
- validation et rate-limit du formulaire de contact inchangés ;
- aucune migration SQL ;
- aucune modification des règles provider ;
- aucun contournement des règles `selfServiceOrderable`.

## 11. Accessibilité et responsive

Le wizard utilise :

- `fieldset` et `legend` ;
- des `label` associés aux radios/checkboxes ;
- une progression annoncée ;
- un déplacement de focus vers le titre de la question à chaque changement d'étape ;
- des focus visibles ;
- des boutons désactivés tant qu'une réponse obligatoire manque ;
- un affichage adaptatif desktop/tablette/mobile.

Les cartes de choix du parcours général sont des liens natifs avec focus visible.

## 12. Contrats automatisés ajoutés

`verify-diagnostic-contract.mjs` couvre notamment :

- les huit contextes ;
- le fallback d'un contexte inconnu ;
- plusieurs formes de paramètres forgés ;
- l'accès direct général ;
- le mapping de tous les slugs Services ;
- le caractère général de `domaines-messagerie` ;
- les questions conditionnelles ;
- la suppression des réponses devenues cachées ;
- l'absence de jargon interne dans les définitions et résumés ;
- la formule sauvegarde uniquement pour le cas simple fichiers ;
- le refus formule pour poste, serveur, NAS, mixte ou inconnu ;
- le VPN/RDS uniquement lorsque le contexte distant est représentable ;
- le refus formule en multi-site ;
- l'impossibilité pour les contextes réseau, messagerie, domaine, serveur et web de produire une formule ;
- l'absence de codes Billing en dur dans le moteur adaptatif ;
- l'utilisation de `/api/formules/devis` ;
- la conservation du parcours `/formules/{preset}` ;
- la transmission du contexte par les composants Storefront ;
- l'absence du configurateur legacy.

## 13. Fichiers principaux du chantier

```text
apps/webportal/app/diagnostic/page.tsx
apps/webportal/components/PublicDiagnosticWizard.tsx
apps/webportal/components/ContactForm.tsx
apps/webportal/lib/diagnostic-context.ts
apps/webportal/lib/adaptive-diagnostic.ts
apps/webportal/scripts/verify-diagnostic-contract.mjs
apps/webportal/app/globals.css
apps/webportal/components/PublicStorefrontPage.tsx
```

Les composants Lot C `PublicPriorityServicePage.tsx` et `PublicMessagingCategoryPage.tsx` restent dans le worktree existant et sont réutilisés sans refonte de leur mise en page.

## 14. Dette restante

### Anti-spam du contact

Le formulaire de contact dispose d'un rate-limit mais pas de hCaptcha/honeypot. Ce n'est pas bloquant pour cette refonte et ajouter un dispositif anti-bot nécessiterait un chantier séparé.

### Pas de lead persistant

Le diagnostic transmet un e-mail mais ne crée pas de fiche lead structurée en base. C'est cohérent avec l'architecture actuelle, mais une future gestion CRM ou suivi back-office nécessiterait un nouveau contrat API et probablement une migration.

### Gabarit d'e-mail générique

Le message passe par le pipeline du formulaire de contact existant. L'e-mail interne reste donc un « message vitrine » générique. Une identité d'e-mail dédiée au diagnostic serait une évolution API/EmailTemplates séparée, non nécessaire au fonctionnement demandé.

### Brouillon préexistant

`apps/webportal/lib/adaptive-diagnostic.gz.b64` était déjà présent comme fichier non suivi avant le chantier. Il a volontairement été laissé intact conformément à la consigne de ne supprimer aucun changement préexistant.

## 15. Résultats de validation

À compléter après la validation finale du chantier.

- Typecheck ciblé : **OK**
- ESLint ciblé des fichiers concernés : **OK**
- `test:diagnostic` : **OK**
- Lint WebPortal complet : _à exécuter_
- Typecheck final : _à exécuter_
- Build Next.js : _à exécuter_
- Suite de validation existante : _à exécuter_
- Smoke tests `localhost:3000` : _à exécuter_
- Vérification responsive : _à exécuter_
- Vérification accessibilité runtime : _à exécuter_

## 16. Publication

Aucun commit, push, tag ni déploiement n'est réalisé dans ce chantier sans accord explicite.


## Association du diagnostic aux formules

Le résultat commercial d'un diagnostic éligible n'utilise pas un second modèle
de capacités propre au wizard. Le contrat canonique est directement
`BillingV2PublicSelection`.

La chaîne est donc :

```text
réponses client
    -> DiagnosticAnswers
    -> recommendOffer()
    -> BillingV2PublicSelection
    -> formule de base + configuration
    -> /api/formules/devis
```

La sélection contient déjà les dimensions nécessaires à l'association avec les
formules :

- stockage personnel ;
- sauvegarde personnelle ;
- espace partagé ;
- sauvegarde partagée ;
- accès sécurisé à distance ;
- bureau Windows à distance ;
- utilisateurs supplémentaires ;
- Support+.

Les codes techniques restent internes. Le résultat public traduit la sélection
en libellés client avant affichage.

Exemple de contrat ajouté aux tests :

```text
À protéger                Fichiers et documents
Volume                    128 Go
Contexte                   Particulier
Utilisateurs               1
Sauvegarde actuelle        En partie
Restauration testée        Jamais

=> Formule de base         Dossier sécurisé
=> Stockage personnel      128 Go
=> Sauvegarde personnelle  Incluse
=> Espace partagé          Non
=> Sauvegarde partagée     Non
=> Accès distant           Non
=> Bureau Windows distant  Non
=> Utilisateurs            1
=> Support renforcé        Non
```

L'état de l'existant, par exemple une sauvegarde partielle ou une restauration
jamais testée, influence les conseils et avertissements. Il ne déclenche pas à
lui seul une formule plus coûteuse.

La dépendance de palier de sauvegarde reste gérée côté API-INTERNAL :
la sauvegarde personnelle suit automatiquement la valeur numérique du palier de
stockage personnel. Une sélection 128 Go + sauvegarde active est donc tarifée
avec la sauvegarde 128 Go sans exposer un second choix de palier au navigateur.

Les quatre familles de formules sont couvertes par le contrat de diagnostic :

- Dossier sécurisé : stockage + sauvegarde personnelle ;
- Accès à Distance : base précédente + accès sécurisé ;
- Bureau Windows à Distance : accès sécurisé + bureau Windows ;
- Pro / Association : petite structure, espace partagé, sauvegarde partagée,
  utilisateur supplémentaire et support renforcé.

Billing V2 reste l'unique autorité de prix et de validité de la sélection.


## Configuration back-office des recommandations

L'association entre un profil diagnostique et sa formule de base n'est plus une
constante TypeScript. Elle est persistée comme contenu administrable structuré
sous la clé `diagnostic:recommendations` et se règle depuis
`/admin/diagnostic`.

Les cinq profils commerciaux actuellement détectés sont :

- sauvegarde simple ;
- accès distant ;
- bureau Windows distant ;
- équipe / structure ;
- équipe + bureau Windows distant.

Pour chacun, l'administrateur choisit une formule issue du catalogue public
Billing V2 courant, ou **Aucun parcours standard — demander un devis**.

La liste proposée dans l'administration est construite dynamiquement depuis le
catalogue Billing V2. Aucune liste de formules n'est dupliquée dans l'interface
d'administration. Une nouvelle formule publique peut donc devenir une cible du
diagnostic sans modification du code du WebPortal.

La frontière d'autorité reste fail-closed :

1. le WebPortal valide la structure de la configuration ;
2. API-INTERNAL revalide la structure lors du `PATCH` ;
3. API-INTERNAL vérifie que chaque `presetCode` non nul existe réellement dans
   le catalogue public Billing V2 courant ;
4. le moteur public recherche de nouveau le preset dans le catalogue au moment
   du diagnostic ;
5. une formule absente, supprimée, dépubliée ou un profil réglé sur `null`
   bascule vers cadrage/devis et n'est jamais proposé au client.

L'éditeur générique `/admin/content/[key]` redirige
`diagnostic:recommendations` vers `/admin/diagnostic`, afin que la configuration
soit manipulée uniquement via le formulaire structuré. Le BFF générique reste
protégé par la validation API-INTERNAL en cas de requête forgée.

Aucune migration SQL n'est nécessaire : la configuration réutilise le stockage
de contenus administrables existant et est seedée avec les correspondances
historiques pour préserver le comportement lors du premier démarrage.

## Validation finale de la configuration administrable

Après raccordement du back-office :

- `lint:webportal` : **OK**
- `typecheck:shared` : **OK**
- `typecheck:webportal` : **OK**
- `test:diagnostic` : **OK**
- `test:formules` : **OK**
- `test:catalog` : **OK**
- `test:managed-content` : **OK**
- `test:public-site-quality` : **OK**
- `build:web` : **OK**
- `build:api` : **OK**
- `test:api` : **OK**
- `git diff --check` : **OK**
- `npm run validate` global : **OK (exit 0)**

Smokes locaux réels sur `localhost:3000` :

- `/admin/backups` : HTTP 200 — la régression
  `getManagedContentRegistry is not defined` est corrigée ;
- `/admin/diagnostic` : HTTP 200 ;
- `/diagnostic?context=backup` : HTTP 200.

Aucun commit, push, tag ni déploiement n'a été effectué.
