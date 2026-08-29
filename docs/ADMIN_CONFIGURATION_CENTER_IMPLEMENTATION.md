# Centre de configuration administrateur — Spécification d'implémentation

> Statut : **architecture retenue / prête à implémenter**  
> Portée : `kermaria-client-platform`  
> Destination : back-office `/admin`  
> Langue UI et documentation : français  
> Date de cadrage : 2026-08-28

## 1. Objet

Le projet contient aujourd'hui de nombreuses valeurs administrables réparties entre :

- variables d'environnement et `api-internal.config.json` ;
- constantes C# et TypeScript ;
- tables MariaDB déjà administrables ;
- contenu géré par le CMS ;
- règles métier ou techniques dispersées ;
- intégrations externes ;
- configuration AD / KoXo ;
- paramètres de sécurité et feature flags.

L'objectif de ce chantier est de créer un **Centre de configuration** cohérent dans le back-office afin de centraliser l'administration de ce qui doit réellement être administrable, tout en gardant une frontière stricte entre :

1. les paramètres métier dynamiques ;
2. les paramètres runtime nécessitant un redémarrage ;
3. les secrets ;
4. les invariants applicatifs qui doivent rester dans le code ;
5. les contenus déjà gérés dans des modules spécialisés.

Ce chantier ne doit **pas** devenir un éditeur générique de `.env`.

Le résultat attendu est un panneau de contrôle de la plateforme, lisible par un administrateur, audité, typé, validé et fail-closed sur les opérations sensibles.

---

## 2. Règles d'architecture à préserver

Avant toute modification, lire et respecter `AGENTS.md` ainsi que `.ai/MEMORY.md` et les topics pertinents.

Les invariants suivants sont non négociables :

- flux applicatif : `browser -> WEBPORTAL/BFF -> API-INTERNAL -> MariaDB` ;
- `apps/webportal` ne contacte jamais directement MariaDB, AD, KoXo, Veeam, BPCE ou une autre intégration interne ;
- `apps/api-internal` reste la seule autorité serveur pour MariaDB, AD, SMTP et intégrations ;
- les mutations admin passent par le BFF, CSRF, authentification admin et API-INTERNAL ;
- `packages/shared` ne contient aucun secret ni URL interne ;
- aucun secret n'est renvoyé au navigateur ;
- aucune migration n'est exécutée automatiquement par une requête ;
- pas de DDL depuis le compte applicatif ;
- pas de suppression destructive AD ;
- pas de restauration globale d'un worktree ou d'un snapshot ;
- conserver les changements non liés présents dans le worktree ;
- commencer par `git status --short`, branche, HEAD et diff ;
- ne pas commit, push, taguer ou déployer sans demande explicite de l'utilisateur.

Le chantier diagnostic peut être encore présent dans le worktree au démarrage. **Ne pas écraser, restaurer ou mélanger involontairement ces changements.** Si nécessaire, travailler dans un worktree dédié.

---

## 3. Arborescence fonctionnelle retenue

La navigation cible est la suivante :

```text
Administration
└── Configuration
    ├── Vue d'ensemble
    │
    ├── Site & entreprise
    │   ├── Identité
    │   ├── SEO global
    │   ├── Coordonnées
    │   └── Domaines / URLs
    │
    ├── Messages & communications
    │   ├── E-mails
    │   ├── Notifications portail
    │   └── Textes système
    │
    ├── Diagnostic
    │   ├── Parcours & questions
    │   ├── Règles de recommandation
    │   ├── Résultats
    │   └── Simulateur
    │
    ├── Inscriptions
    │
    ├── Sécurité
    │   ├── Sessions
    │   ├── Connexion
    │   └── Mots de passe
    │
    ├── Active Directory & KoXo
    │   ├── Autorités
    │   ├── Périmètres d'écriture
    │   ├── OU / groupes
    │   ├── Provisioning
    │   └── Synchronisation KoXo
    │
    ├── Facturation
    │   ├── Coordonnées de règlement
    │   ├── Fiscalité
    │   ├── Billing V2
    │   └── Feature flags
    │
    ├── Démonstrations
    │   ├── Modèles
    │   └── Conversion
    │
    ├── Intégrations
    │   ├── SMTP
    │   ├── Stripe
    │   ├── PayPal
    │   ├── BPCE
    │   ├── Veeam
    │   ├── hCaptcha
    │   └── KoXo
    │
    ├── Infrastructure
    │   ├── API-INTERNAL
    │   ├── MariaDB
    │   ├── Stockage
    │   ├── Journalisation
    │   └── Configuration runtime
    │
    └── Historique & audit
```

Route racine cible :

```text
/admin/settings
```

Sous-routes recommandées :

```text
/admin/settings/site
/admin/settings/messages
/admin/settings/diagnostic
/admin/settings/signup
/admin/settings/security
/admin/settings/directory
/admin/settings/billing
/admin/settings/demo
/admin/settings/integrations
/admin/settings/infrastructure
/admin/settings/audit
```

Il n'est pas nécessaire de créer une route pour chaque sous-section dès le premier lot. Une page par grand domaine avec onglets/cartes est acceptable si elle reste lisible.

---

## 4. Principe fondamental : quatre classes de configuration

Chaque paramètre exposé doit appartenir explicitement à l'une des catégories suivantes.

### 4.1 `dynamic`

Paramètre métier pouvant changer à chaud et être relu par API-INTERNAL sans redémarrage.

Exemples :

- coordonnées de règlement ;
- limites d'inscription ;
- TTL fonctionnels ;
- certains libellés ;
- certains toggles non critiques ;
- textes administrables ;
- règles du diagnostic ;
- modèles de démonstration ;
- templates de communication.

Ces paramètres sont persistés en MariaDB et prennent effet après validation/enregistrement.

### 4.2 `restart_required`

Paramètre d'exploitation ou d'infrastructure chargé au démarrage.

Exemples :

- URL API interne ;
- racine de stockage ;
- certains timeouts d'intégration ;
- configuration de journalisation ;
- certains modes runtime historiques tant qu'ils ne sont pas refactorés dynamiquement.

L'UI peut les afficher et, uniquement si une voie d'écriture sûre est implémentée, permettre leur modification avec le badge **Redémarrage requis**.

La première version peut rester en lecture seule pour ces valeurs.

### 4.3 `secret`

Secret strictement serveur.

Exemples :

- mots de passe SQL ;
- `SERVICE_AUTH_TOKEN` ;
- mot de passe LDAP ;
- clé Stripe secrète ;
- secret webhook Stripe ;
- secret PayPal ;
- refresh token BPCE ;
- mot de passe SMTP ;
- tokens KoXo ;
- mot de passe Veeam ;
- clé hCaptcha secrète ;
- clé de protection des mots de passe KoXo.

**Interdictions :**

- ne jamais afficher la valeur ;
- ne jamais la renvoyer dans un DTO ;
- ne jamais la mettre dans un attribut HTML ;
- ne jamais la journaliser ;
- ne jamais la stocker dans `application_settings` en clair.

L'UI affiche uniquement un état :

```text
Non configuré
Configuré
Invalide
À vérifier
```

Une future fonctionnalité de remplacement de secret doit être une mutation spécifique, jamais un champ générique.

### 4.4 `code_invariant`

Valeur ou comportement qui reste dans le code.

Exemples :

- identifiants techniques de services ;
- noms de rôles ;
- codes internes de statuts ;
- règles cryptographiques ;
- validation de signature webhook ;
- logique d'autorisation ;
- frontières de sécurité LDAP ;
- calcul de prix ;
- invariants fiscaux de calcul ;
- chemins BFF ;
- règles de routage de sécurité.

On peut rendre leur **libellé** administrable si utile, mais pas leur contrat technique.

---

## 5. Socle de données cible

### 5.1 `application_settings`

Créer une table générique uniquement pour les réglages simples et typés.

Schéma cible conceptuel :

```text
application_settings
- setting_key             PK
- category
- value_json
- value_type
- version
- updated_by
- created_at
- updated_at
```

Ne pas permettre la création libre d'une clé depuis l'UI.

Les clés disponibles viennent d'un **registre fermé dans le code**.

Chaque définition du registre doit au minimum contenir :

```text
key
label
description
category
type
default value
validation
classification: dynamic | restart_required | secret | code_invariant
risk: low | medium | high | critical
editable
restartRequired
sensitive
```

Types minimaux :

```text
bool
int
string
email
url
enum
json
```

Les valeurs inconnues de la table doivent être ignorées ou rejetées de manière sûre ; elles ne doivent pas produire de comportement arbitraire.

### 5.2 Historique

Créer une stratégie d'historique cohérente, soit via :

```text
application_setting_revisions
```

soit via le mécanisme d'audit existant si celui-ci permet de reconstruire avant/après.

Une modification de configuration doit enregistrer :

```text
timestamp
actor
setting/template/rule
ancienne valeur non secrète
nouvelle valeur non secrète
correlation_id
résultat
```

Pour les secrets : jamais de valeur avant/après, uniquement un état de mutation.

### 5.3 Tables spécialisées

Ne pas forcer tous les domaines dans `application_settings`.

Créer ou réutiliser des tables spécialisées pour :

```text
email_templates
notification_templates
diagnostic_* ou diagnostic_configuration versionnée
demo_content_templates
```

Billing V2 doit continuer d'utiliser ses tables existantes.

Le CMS et l'éditorial doivent continuer d'utiliser leurs systèmes existants.

---

## 6. Vue d'ensemble `/admin/settings`

La landing page doit être un tableau de bord de configuration, pas une liste plate de clés.

Afficher des cartes par domaine avec :

- état global ;
- nombre de paramètres modifiables ;
- avertissements ;
- redémarrage requis éventuel ;
- intégrations configurées/non configurées ;
- erreurs de validation ;
- accès à la section.

Exemple :

```text
Active Directory & KoXo
Autorité identité : KoXo
Accès API : lecture + groupes de services
Webhook : opérationnel
1 avertissement
```

```text
E-mails
Mode : live
SMTP : configuré
7 templates
Dernière erreur : aucune
```

```text
Billing V2
Nouvelles souscriptions : activées
Provider executor : activé
Provisioning : désactivé
2 flags critiques
```

Réutiliser le design system admin existant.

---

## 7. Site & entreprise

### 7.1 Identité

Aujourd'hui certaines valeurs sont en code, notamment :

- nom commercial ;
- dénomination juridique.

Créer une source administrable cohérente pour les valeurs qui ont vocation à changer sans déploiement, avec fallback code sûr.

Candidats :

```text
brand_name
legal_name
contact_email
support_email
phone
address_line_1
address_line_2
postal_code
city
country
```

Ne pas dupliquer ce qui est déjà correctement géré dans le contenu légal si une source unique peut être réutilisée.

### 7.2 SEO global

Rendre administrables au minimum :

```text
default_site_title
default_site_description
```

Les metadata spécifiques d'une page éditoriale restent dans le système éditorial concerné.

Les valeurs SEO doivent être validées : longueur raisonnable, texte brut, pas de HTML arbitraire.

### 7.3 Domaines / URLs

Les domaines et routages sont sensibles car liés à la sécurité, canonicalisation et cookies.

Première version : **lecture seule**.

Afficher notamment :

- domaine public canonique ;
- dashboard ;
- administration ;
- wiki public/interne ;
- portfolio ;
- alias connus.

Ne pas construire un éditeur générique de `public-route-config.ts`.

---

## 8. Messages & communications

### 8.1 Templates e-mail

Les templates transactionnels actuellement codés doivent devenir administrables.

Templates connus au cadrage :

```text
invoice_issued
payment_reminder
payment_confirmed
contact_form
signup_verification
account_approved
account_rejected
```

Créer une table spécialisée conceptuellement :

```text
email_templates
- template_key PK
- display_name
- subject_template
- body_template
- enabled
- version
- updated_by
- created_at
- updated_at
```

Prévoir historique/révisions.

Première version : **texte brut uniquement**.

Ne pas convertir le transport SMTP en HTML dans ce chantier sauf nécessité explicite et solution de sanitization robuste.

Fonctions UI :

- édition sujet ;
- édition corps ;
- liste des variables disponibles ;
- insertion d'une variable ;
- aperçu ;
- validation ;
- restauration du template par défaut ;
- historique ;
- envoi de test si l'infrastructure existante permet une implémentation sûre.

Syntaxe recommandée :

```text
{{contactName}}
{{verificationUrl}}
{{setPasswordUrl}}
{{customerReference}}
```

Chaque template possède une **whitelist fermée de variables**.

Interdictions :

- moteur d'expressions arbitraires ;
- accès aux variables d'environnement ;
- exécution de code ;
- reflection dynamique ;
- includes de fichiers ;
- variable inconnue silencieusement acceptée.

Une variable inconnue doit faire échouer la sauvegarde.

Le runtime doit conserver des templates intégrés au code comme fallback afin qu'une panne SQL ou une ligne manquante ne bloque pas un e-mail critique.

### 8.2 Notifications portail

Externaliser les contenus utilisateurs aujourd'hui produits par `PortalNotificationFactory` :

- titre ;
- message ;
- éventuellement libellés associés aux statuts.

Les identifiants de notification et codes de statut restent des invariants code.

Créer une table `notification_templates` ou un mécanisme spécialisé équivalent.

Prévoir les variables nécessaires, par exemple l'identifiant de demande si utile, avec whitelist stricte.

### 8.3 Textes système

Ne pas externaliser chaque bouton du site.

Créer uniquement un mécanisme de snippets nommés pour les textes réellement opérationnels qui doivent être modifiables sans déploiement.

Exemples possibles :

- confirmation formulaire contact ;
- note de confidentialité courte ;
- messages génériques de fermeture temporaire ;
- certains textes commerciaux récurrents.

Éviter un CMS parallèle au système éditorial existant.

---

## 9. Diagnostic administrable

Le diagnostic est un domaine majeur de ce chantier.

### 9.1 Ce qui doit devenir administrable

- contextes ;
- libellés ;
- titres ;
- introductions ;
- sujets de contact ;
- éligibilité à une formule ;
- questions ;
- ordre ;
- mode simple/multiple ;
- options ;
- options exclusives ;
- conditions d'affichage ;
- textes de résultat ;
- points de recommandation ;
- règles de correspondance vers les besoins Billing V2 ;
- règles de sélection des paliers ;
- conditions qui imposent un échange/devis plutôt qu'une recommandation automatique.

### 9.2 Ne pas créer un moteur de scripting

Les règles doivent être déclaratives et validées.

Pas de JavaScript/C#/expression arbitraire stockée en base.

Préférer une DSL JSON fermée ou des structures relationnelles du type :

```text
condition
- questionId
- operator: equals | includes | one_of | count
- values

action
- recommendationStatus
- serviceCode
- tierCode
- quantity
- guidanceKey
```

Les opérations disponibles sont définies dans le code.

### 9.3 Versioning et publication

Le diagnostic doit pouvoir être modifié sans casser le parcours public en cours.

Prévoir au minimum :

```text
draft
published
```

Une publication doit être atomique : le public voit soit l'ancienne version complète, soit la nouvelle version complète.

### 9.4 Simulateur admin

Créer un simulateur dans `/admin/settings/diagnostic` permettant de :

1. sélectionner un contexte ;
2. répondre comme un utilisateur ;
3. voir les questions conditionnelles ;
4. exécuter le moteur de recommandation ;
5. afficher le résultat final ;
6. afficher la sélection Billing V2 ;
7. afficher les règles qui ont conduit à la décision.

Exemple de sortie :

```text
Besoin détecté
SP = 128 Go
EP = Non
VPN = Oui
RDS = Non

Formule proposée
Pack Accès à distance

Règles appliquées
DIA-REMOTE-012
DIA-STORAGE-004
```

Le simulateur doit utiliser le même moteur que la production, pas une copie frontend distincte.

### 9.5 Autorité commerciale

Le diagnostic ne calcule jamais le prix.

La tarification reste exclusivement l'autorité de Billing V2 côté API-INTERNAL.

---

## 10. Inscriptions

Paramètres candidats dynamiques :

```text
signup_enabled
signup_rate_limit_per_ip_per_hour
signup_rate_limit_per_email_per_24h
signup_verification_token_ttl_hours
signup_password_setup_token_ttl_hours
```

`signup_auto_approve` doit rester désactivé tant que le comportement métier n'a pas été explicitement validé pour la production.

Si exposé dans l'UI, le présenter comme **fonction expérimentale / critique** avec confirmation forte et conserver les garde-fous serveur.

Les validations de bornes existantes doivent être préservées : ne pas rendre possibles des valeurs plus dangereuses simplement parce qu'elles viennent de MariaDB.

Le kill switch d'inscription doit rester vérifié côté API-INTERNAL même si le WebPortal masque le parcours.

---

## 11. Sécurité

### 11.1 Sessions

Candidats :

```text
session_duration_minutes
login_max_failures
login_lockout_minutes
```

Conserver les minimum/maximum existants.

Toute réduction ou augmentation hors plage doit être refusée par API-INTERNAL.

### 11.2 Cookies

Les propriétés de sécurité des cookies, notamment `Secure` et `SameSite`, ne doivent pas devenir de simples toggles dynamiques en production.

Première version : lecture seule dans **Infrastructure / Runtime**.

### 11.3 Mot de passe AD

Intégrer à la section AD / KoXo :

```text
password_change_enabled
password_rate_limit
```

Mais uniquement après le refactor d'autorité décrit ci-dessous.

---

## 12. Active Directory & KoXo — refactor obligatoire

Le modèle actuel `AD_INTEGRATION_MODE=controlled_write` mélange deux notions différentes :

1. KoXo est maître des identités ;
2. API-INTERNAL possède quand même des capacités de modification LDAP directes.

Le Centre de configuration ne doit pas reproduire cette ambiguïté.

### 12.1 Autorités cibles

Modèle fonctionnel retenu :

| Opération | Autorité cible |
|---|---|
| création utilisateur | KoXo uniquement |
| création OU client | KoXo uniquement |
| placement dans hiérarchie KoXo | KoXo |
| mot de passe AD | KoXo uniquement |
| lecture / résolution identité | API-INTERNAL |
| rattachement via `employeeNumber` | API-INTERNAL |
| ajout/retrait groupes de services | API-INTERNAL |
| suppression utilisateur | interdite à API-INTERNAL |
| renommage identité | KoXo de préférence |
| modification attributs d'identité | KoXo de préférence |
| désactivation utilisateur | doit être explicitement définie et auditée |

### 12.2 Nouvelle représentation

Ne pas conserver `controlled_write` comme unique abstraction conceptuelle.

Le modèle peut être implémenté via enums/registry, mais doit représenter séparément :

```text
directory_access
identity_authority
password_authority
group_membership_write_policy
user_lifecycle_write_policy
manual_admin_write_policy
```

Exemple fonctionnel :

```text
Accès annuaire              read_only + service_groups
Autorité identités          koxo
Autorité mots de passe      koxo
Écriture groupes services   enabled
Écriture cycle de vie       disabled
Écriture admin manuelle     disabled
```

Les noms exacts de variables/classes sont laissés à l'implémentation si une meilleure intégration au code existant est possible.

### 12.3 Compte LDAP

La production documentée utilise le compte de service :

```text
HOME\svc_api_portal_ad
```

Le mot de passe reste secret.

L'UI affiche :

```text
Compte LDAP : HOME\svc_api_portal_ad
Authentification : compte de service
Secret : configuré
Use current Windows credentials : non
```

Ne jamais afficher le mot de passe.

### 12.4 KoXo

Le parcours signup actuel est :

```text
soumission signup -> MariaDB
vérification e-mail -> API
approbation -> API
trigger KoXo -> webhook
création identité AD -> KoXo
liaison identité via employeeNumber -> API
```

Le mot de passe AD est également appliqué par KoXo dans le modèle retenu.

Le CSV KoXo reste autoritaire sur la population d'identités ; les groupes de services restent pilotés par l'API.

### 12.5 Page admin AD / KoXo

Afficher clairement :

```text
Autorité des identités          KoXo
Autorité des mots de passe      KoXo
Accès API à l'annuaire          Lecture + groupes de services
Création utilisateur par API    Interdite
Mot de passe direct par API     Interdit
Gestion groupes par API         Autorisée
Compte LDAP                     HOME\svc_api_portal_ad
Racines autorisées              ...
Webhook KoXo                    Opérationnel / erreur
Dernière synchronisation        ...
```

### 12.6 Audit des écritures AD

Ajouter une visibilité opérationnelle sur les mutations d'annuaire.

Événement cible :

```text
timestamp
operation
engine: api_internal | koxo
host
actor principal si connu
workflow
customer reference
target user/DN/group
result
correlation_id
```

Ne jamais enregistrer de mot de passe ou token.

Exemples d'opérations :

```text
resolve_user
add_group_member
remove_group_member
sync_identity
move_identity
disable_identity
```

La page doit permettre de répondre simplement à :

> Qui a écrit dans l'AD, quoi, quand, et pour quel workflow ?

---

## 13. Provisioning des services

La topologie commerciale principale doit continuer de venir des tables Billing V2, notamment des règles de provisioning existantes.

Ne pas réintroduire une seconde source d'autorité en `application_settings`.

Les éléments runtime encore nécessaires peuvent rester dans une section avancée :

- DN des groupes ;
- maximum de tentatives ;
- délai de retry ;
- fallbacks techniques hors catalogue.

Les DN doivent être validés par rapport aux racines AD autorisées.

Les mutations de groupe restent auditables et fail-closed.

---

## 14. Facturation

### 14.1 Coordonnées de règlement

Passer en paramètres métier dynamiques les valeurs aujourd'hui de type :

```text
BILLING_IBAN
BILLING_BIC
BILLING_PAYPAL_URL
BILLING_TRANSFER_LABEL
```

Validation :

- IBAN normalisé ;
- BIC normalisé ;
- URL HTTPS pour PayPal ;
- libellé texte borné.

Une modification ne doit jamais réécrire les documents historiques déjà émis.

### 14.2 Fiscalité

Créer une section fiscalité, mais conserver l'autorité des calculs côté API-INTERNAL.

Les mentions fiscales peuvent être administrables si elles restent associées à un régime connu et validé.

Ne jamais rendre le calcul de taxe scriptable.

Un changement de régime doit :

- être fortement audité ;
- avoir une date d'effet ;
- s'appliquer uniquement aux nouveaux calculs/documents ;
- ne pas modifier rétroactivement les factures.

### 14.3 Billing V2

Ne pas déplacer le catalogue Billing V2 dans `application_settings`.

Le Centre de configuration doit fédérer et renvoyer vers l'administration Billing V2 existante.

Il peut afficher un résumé :

- catalogue actif ;
- nombre de services ;
- formules actives ;
- readiness ;
- provider mappings ;
- état du checkout ;
- provisioning.

### 14.4 Feature flags Billing V2

Flags connus :

```text
new subscriptions
authoritative checkout
first real subscription approved
provider outbox
provider executor
provisioning
reconciliation worker
additional user provisioning
generic selection
service fulfillment
subscription changes
Stripe recurring mutation
VPS local provisioning
VPS cloud automation
```

Ne pas les présenter comme une série de switches sans contexte.

Chaque flag doit avoir :

```text
label
description
current state
risk level
dependencies
restart requirement
last change
```

Niveaux :

```text
low
medium
high
critical
```

Pour `high` / `critical`, utiliser confirmation renforcée.

Pour un flag permettant une mutation réelle chez un provider ou dans l'infrastructure, exiger une phrase de confirmation explicite.

Exemple :

```text
ACTIVER PROVISIONING
```

API-INTERNAL doit revalider la confirmation et les dépendances ; ne pas se fier uniquement au frontend.

Une première version peut rendre certains flags critiques **lecture seule** si leur mutation dynamique n'est pas suffisamment sûre.

---

## 15. Démonstrations

### 15.1 Modèles de démonstration

Les modèles actuellement codés dans `DemoContentTemplateRegistry` doivent devenir administrables.

Profils existants au cadrage :

```text
tpe
association
pme-multisite
ad-koxo
```

Créer une table spécialisée telle que :

```text
demo_content_templates
- template_key
- label
- enabled
- version
- updated_by
- created_at
- updated_at
```

et une table d'items/services associée si nécessaire.

Permettre :

- création d'un modèle ;
- activation/désactivation ;
- ordre des services ;
- nom ;
- description ;
- périmètre ;
- aperçu ;
- historique.

Ne pas permettre de créer arbitrairement un type de service inconnu du code si cela contourne les validations métier.

### 15.2 Conversion démo -> client

La destination AD de conversion est sensible.

Présenter l'OU cible dans la section AD / Démonstrations avancée.

Toute modification doit être validée contre `AD_ALLOWED_ROOTS` ou le futur registre équivalent.

---

## 16. Intégrations

Le principe général des pages d'intégration : **observer et tester sans révéler les secrets**.

Chaque intégration doit exposer autant que possible :

```text
mode
configured
healthy
endpoint public/non secret si approprié
timeout
last successful operation
last error summary
last checked
```

### 16.1 SMTP

Afficher :

- disabled/mock/live ;
- host ;
- port ;
- STARTTLS ;
- username ;
- from address ;
- display name ;
- timeout ;
- mot de passe : configuré/non configuré ;
- allowlist live ;
- test d'envoi si sûr.

Ne pas exposer le mot de passe SMTP.

### 16.2 Stripe

Afficher :

- disabled/test/live ;
- clé secrète : configurée/non configurée ;
- compatibilité clé/mode ;
- publishable key si déjà publique et utile ;
- webhook configuré ;
- signature verification activée ;
- état de connectivité si une vérification non destructive existe.

Passage en live = risque critique.

### 16.3 PayPal

Afficher :

- sandbox/live ;
- Client ID configuré ;
- Client Secret configuré ;
- webhook configuré ;
- signature verification ;
- état de connexion.

Passage en live = risque critique.

### 16.4 BPCE

Afficher :

- disabled/mock/live ;
- base URL ;
- sender ID ;
- refresh token configuré ;
- timeout ;
- configuration valide ;
- dernier contrôle du sender.

Mode live = risque critique.

### 16.5 Veeam

Afficher :

- collector mode ;
- base URL ;
- username ;
- password configuré ;
- API version ;
- état collecteur ;
- dernière collecte ;
- dernière erreur ;
- connectivité.

Ne pas afficher le mot de passe.

### 16.6 hCaptcha

Afficher :

- site key configurée ;
- secret configuré ;
- inscription activée ;
- état de validation.

### 16.7 KoXo

Afficher :

- webhook URL non secrète ;
- token configuré ;
- timeout ;
- HTTP insecure autorisé/non ;
- état ;
- dernière synchronisation ;
- dernier trigger ;
- dernière erreur.

---

## 17. Infrastructure

### 17.1 API-INTERNAL

Afficher :

- environnement ;
- version/build si disponible ;
- configuration path ;
- uptime ;
- état SQL ;
- état AD ;
- état des intégrations ;
- paramètres nécessitant redémarrage.

### 17.2 MariaDB

Afficher uniquement les données non sensibles :

- provider ;
- host ;
- port ;
- database ;
- username ;
- password configuré ;
- connectivité ;
- migration/schema version ;
- réplication si cette information est déjà disponible côté plateforme sans étendre dangereusement les privilèges.

Ne pas renvoyer de chaîne de connexion complète.

### 17.3 Stockage

Afficher `DOWNLOAD_STORAGE_ROOT` et état d'accès.

Première version en lecture seule.

### 17.4 Journalisation

Afficher :

- niveau global ;
- journal fichier activé ;
- répertoire ;
- niveau fichier ;
- rétention.

La modification peut rester `restart_required`.

### 17.5 Runtime configuration

Créer une vue consolidée des paramètres runtime connus.

Pour chaque ligne :

```text
Paramètre
Valeur non sensible / état
Source : env | json | default | database
Classification
Redémarrage requis
Dernière modification connue
```

Ne pas afficher les secrets.

Le but est de rendre l'exploitation compréhensible, pas d'exposer le contenu brut du fichier JSON.

---

## 18. Contenus existants : fédérer, ne pas dupliquer

Le projet dispose déjà de modules spécialisés :

- `/admin/content` ;
- `/admin/editorial` ;
- `/admin/catalog` / Billing V2 ;
- `/admin/downloads` ;
- `/admin/backups` ;
- `/admin/koxo` ;
- `/admin/email-log` ;
- autres pages métier.

Le Centre de configuration doit créer des liens et résumés vers ces modules lorsque ce sont déjà les bonnes autorités.

Ne pas recréer un second éditeur du CMS.

---

## 19. Présentation commerciale hardcodée

Certaines valeurs de présentation dans `billing-v2-formules.ts` sont encore codées :

- libellés publics de services ;
- bénéfices ;
- slogans/taglines ;
- messages d'indisponibilité checkout.

Faire évoluer progressivement ces données vers une source administrable appropriée :

- attributs commerciaux du catalogue si elles sont liées au service/formule ;
- snippets/messages si elles sont purement rédactionnelles.

Ne jamais déplacer le calcul de prix côté WebPortal.

---

## 20. Permissions admin

Réutiliser le modèle de permissions admin existant.

Prévoir des permissions distinctes si nécessaire :

```text
settings.read
settings.write
settings.security.write
settings.integrations.write
settings.billing.write
settings.directory.write
settings.templates.write
settings.diagnostic.write
```

Ne pas accorder automatiquement toutes les mutations sensibles à un rôle qui possède seulement l'accès au contenu éditorial.

Toute mutation critique doit être revalidée côté API-INTERNAL.

---

## 21. Risque et confirmations

Chaque définition de setting/mutation doit avoir un niveau de risque.

### Low

Exemple : modifier un texte non critique.

Confirmation normale.

### Medium

Exemple : modifier un TTL ou un rate limit dans une plage sûre.

Afficher l'effet.

### High

Exemple : activer une fonctionnalité pouvant affecter les utilisateurs.

Modal + résumé avant/après.

### Critical

Exemples :

- mode provider live ;
- provisioning réel ;
- mutation Stripe récurrente ;
- changement d'autorité AD ;
- écriture cycle de vie utilisateur ;
- modification fiscale structurante.

Exiger :

- permission spécifique ;
- revalidation serveur ;
- texte de confirmation explicite ;
- audit obligatoire ;
- éventuelle dépendance/check de readiness ;
- rejet si l'état cible est incohérent.

---

## 22. API et BFF

Créer des contrats explicites, pas une route universelle dangereuse de type :

```text
PATCH /settings/{anyKey}
```

Une route générique est acceptable uniquement si elle opère sur le **registre fermé** et applique côté API :

- existence de la clé ;
- type ;
- validation ;
- permission ;
- classification ;
- risque ;
- audit ;
- concurrence/version.

Routes conceptuelles possibles :

```text
GET    /internal/admin/settings
GET    /internal/admin/settings/{category}
PATCH  /internal/admin/settings/{key}
GET    /internal/admin/settings/audit

GET    /internal/admin/email-templates
GET    /internal/admin/email-templates/{key}
PATCH  /internal/admin/email-templates/{key}
POST   /internal/admin/email-templates/{key}/preview
POST   /internal/admin/email-templates/{key}/test
POST   /internal/admin/email-templates/{key}/restore-default

GET    /internal/admin/notification-templates
PATCH  /internal/admin/notification-templates/{key}

GET    /internal/admin/diagnostic/configuration
PUT    /internal/admin/diagnostic/draft
POST   /internal/admin/diagnostic/validate
POST   /internal/admin/diagnostic/simulate
POST   /internal/admin/diagnostic/publish

GET    /internal/admin/runtime-status
GET    /internal/admin/integrations/status
GET    /internal/admin/directory/status
```

Les noms exacts peuvent être adaptés aux conventions existantes.

Synchroniser les contrats dans `packages/shared` lorsque nécessaire.

Le WebPortal appelle exclusivement API-INTERNAL via les mécanismes BFF existants.

---

## 23. Concurrence et sauvegarde

Utiliser un mécanisme de version/optimistic concurrency pour éviter qu'un administrateur écrase silencieusement les modifications d'un autre.

Exemple :

```text
version = 12
```

La mutation porte `expectedVersion=12`.

Si la version courante est 13 :

```text
409 SETTINGS_VERSION_CONFLICT
```

L'UI recharge et informe l'utilisateur.

Appliquer la même logique aux templates et configurations versionnées lorsqu'utile.

---

## 24. Cache et lecture dynamique

Les réglages `dynamic` doivent réellement être dynamiques.

Éviter de charger une fois `application_settings` dans un singleton immutable au démarrage.

Mettre en place une abstraction de type :

```text
IApplicationSettingsService
```

avec :

- lecture DB ;
- fallback registre/default ;
- cache borné si nécessaire ;
- invalidation après mutation ;
- comportement fail-safe.

Les services qui doivent réagir à chaud doivent appeler cette abstraction ou consommer un snapshot actualisable.

Pour les réglages critiques, ne pas masquer une indisponibilité SQL par un comportement qui pourrait activer une fonction dangereuse. Le fallback doit être **fail-closed**.

---

## 25. Valeurs par défaut et fallback

Toute configuration nouvelle doit avoir une stratégie de fallback explicite.

Règle générale :

- texte : fallback code ;
- fonctionnalité sensible : fallback `disabled` ;
- permission d'écriture : fallback `denied` ;
- intégration live : fallback `disabled` ;
- template transactionnel : fallback template intégré ;
- règle diagnostic : fallback dernière version publiée valide ;
- secret manquant : fonctionnalité dépendante refusée.

Ne jamais choisir un fallback plus permissif que la configuration absente.

---

## 26. Migration des valeurs existantes

Ne pas casser le déploiement actuel.

Pour chaque paramètre déplacé vers MariaDB :

1. conserver temporairement la lecture de la valeur historique comme fallback ;
2. initialiser la valeur DB de manière déterministe si nécessaire ;
3. basculer l'autorité vers la DB ;
4. documenter la variable runtime devenue obsolète ;
5. supprimer le fallback historique seulement dans un lot ultérieur clairement identifié.

Ne pas migrer toutes les variables en une seule fois.

Les secrets ne sont jamais migrés vers la table générique.

---

## 27. Migrations MariaDB

Avant d'ajouter une migration :

- lister les migrations existantes ;
- choisir le prochain numéro réel ;
- respecter `-- statement-break` ;
- migration additive et rejouable selon les conventions du projet ;
- aucune suppression destructive dans le premier lot ;
- aucun `DELETE` de données de configuration existantes ;
- prévoir index et contraintes utiles ;
- historique/audit dès le socle, pas en correctif tardif.

Avant une migration réelle, suivre la procédure de sauvegarde documentée.

---

## 28. UX admin

### 28.1 Principes

- libellés humains ;
- description de l'effet ;
- afficher la valeur effective ;
- afficher la source ;
- distinguer modifiable / lecture seule ;
- badges `Dynamique`, `Redémarrage requis`, `Secret`, `Critique` ;
- pas de jargon d'environnement quand un libellé métier suffit ;
- afficher le nom technique en secondaire pour l'exploitation.

### 28.2 Dirty state

Tout formulaire d'édition doit protéger contre la perte de modifications :

- bouton Annuler ;
- état non sauvegardé ;
- navigation interne protégée ;
- confirmation avant abandon.

Réutiliser les patterns déjà développés dans l'admin.

### 28.3 Accessibilité

- labels explicites ;
- `aria-describedby` pour les aides/risques ;
- erreurs associées au champ ;
- focus après validation/erreur ;
- ne pas utiliser la couleur comme seul indicateur de risque.

---

## 29. Audit & historique

Créer `/admin/settings/audit` ou intégrer les événements au système d'audit existant avec un filtre Configuration.

Filtres minimum :

```text
période
acteur
catégorie
clé/template/règle
niveau de risque
résultat
correlation_id
```

Événements :

```text
setting_changed
setting_change_rejected
email_template_changed
email_template_restored
diagnostic_draft_changed
diagnostic_published
feature_flag_changed
integration_mode_changed
directory_policy_changed
ad_write_performed
secret_replaced
```

Ne jamais stocker un secret dans le payload d'audit.

---

## 30. Tests obligatoires

### 30.1 API

Tester :

- registre fermé ;
- type invalide ;
- clé inconnue ;
- bornes ;
- permissions ;
- optimistic concurrency ;
- fallback ;
- fail-closed ;
- audit ;
- secrets jamais sérialisés ;
- templates variables autorisées/interdites ;
- publication diagnostic atomique ;
- simulateur diagnostic ;
- politique AD/KoXo ;
- feature flags critiques.

### 30.2 WebPortal

Ajouter/étendre les contrats admin pour vérifier :

- routes protégées ;
- BFF uniquement ;
- CSRF ;
- rendu catégories ;
- badges risque ;
- champs secrets non présents ;
- dirty state ;
- confirmations critiques ;
- erreurs 409 ;
- simulateur diagnostic ;
- liens vers modules fédérés.

### 30.3 Régression

Au minimum selon les fichiers touchés :

```text
npm run typecheck:shared
npm run typecheck:webportal
npm run lint:webportal
npm run build:api
npm run test:api
npm --prefix apps/webportal run test:admin
npm --prefix apps/webportal run test:ad-security
```

Puis `npm run validate` lorsque le lot est suffisamment stabilisé.

Avant clôture :

```text
git diff --check
git status --short
git diff
```

---

## 31. Ordre d'implémentation retenu

### P0 — Socle

1. vérifier l'état Git et isoler les changements non liés ;
2. registre de configuration ;
3. tables `application_settings` + historique/audit ;
4. service de lecture dynamique ;
5. API admin settings ;
6. BFF ;
7. `/admin/settings` ;
8. permissions et niveaux de risque ;
9. vue runtime/integrations en lecture seule ;
10. tests du socle.

### P0 — AD / KoXo

Le refactor d'autorité AD/KoXo fait partie du socle de sécurité et doit être traité avant de présenter `controlled_write` dans une nouvelle UI.

1. séparer les concepts d'autorité ;
2. préserver le comportement réel actuel : KoXo identité/mot de passe, API groupes ;
3. interdire explicitement les écritures de cycle de vie non nécessaires ;
4. page `/admin/settings/directory` ;
5. audit opérationnel AD ;
6. tests fail-closed.

### P1 — Communications

1. `email_templates` ;
2. fallback code ;
3. éditeur admin ;
4. preview/test ;
5. `notification_templates` ;
6. historique ;
7. tests.

### P1 — Diagnostic

1. modèle versionné ;
2. migration des définitions existantes ;
3. draft/published ;
4. moteur déclaratif validé ;
5. admin ;
6. simulateur ;
7. publication atomique ;
8. régression du parcours public.

### P1 — Inscriptions / sécurité

Basculer progressivement les paramètres réellement dynamiques vers le nouveau service.

### P2 — Facturation / feature flags

1. coordonnées de règlement ;
2. fiscalité sûre ;
3. résumé Billing V2 ;
4. flags avec risque/confirmation ;
5. readiness et dépendances.

### P2 — Démonstrations

Rendre les templates administrables et intégrer la conversion avancée.

### P2 — Intégrations

Construire les consoles SMTP/Stripe/PayPal/BPCE/Veeam/hCaptcha/KoXo en lecture/status puis mutations sûres uniquement quand justifiées.

### P3 — Infrastructure

Consolider runtime, stockage, journalisation, MariaDB et API-INTERNAL. Priorité à l'observabilité ; les écritures runtime peuvent rester hors scope si elles nécessitent une gestion de secrets/restart trop risquée.

---

## 32. Stratégie de livraison

Ne pas faire un unique diff gigantesque sans points de validation.

Implémenter par lots cohérents, chacun compilable/testable.

Ordre recommandé des commits lorsque l'utilisateur autorisera les commits :

```text
1. settings foundation + migrations + contracts
2. admin settings shell + overview
3. AD/KoXo authority refactor + audit
4. email templates
5. notification templates
6. diagnostic configuration + simulator
7. signup/security settings
8. billing/business settings
9. demo templates
10. integrations/runtime status
11. documentation finalisation
```

---

## 33. Documentation à mettre à jour en fin de chantier

Selon les changements réellement effectués :

```text
docs/ARCHITECTURE.md
docs/API_CONTRACT.md
docs/DATA_MODEL.md
docs/GUIDE_ADMIN.md
docs/SECURITY.md
docs/koxo-sync.md
docs/DEPLOYMENT.md
.env.example
.ai/topics/* pertinents
.ai/MEMORY.md si nécessaire selon les règles du dépôt
```

Documenter clairement les variables devenues legacy ou fallback.

---

## 34. Non-objectifs

Ce chantier ne doit pas :

- créer un gestionnaire de secrets maison en base ;
- rendre éditable chaque variable d'environnement ;
- exposer API-INTERNAL à Internet ;
- donner au WebPortal un accès direct à MariaDB ou AD ;
- introduire un moteur de scripting dans le diagnostic ou les e-mails ;
- recalculer les prix côté frontend ;
- remplacer le CMS existant ;
- dupliquer Billing V2 ;
- rendre arbitraires les statuts métier ;
- ajouter des suppressions AD destructives ;
- modifier rétroactivement les factures ;
- activer automatiquement des providers live ;
- transformer un secret en champ affichable ;
- supprimer les garde-fous existants sous prétexte qu'un setting est administrable.

---

## 35. Critères d'acceptation globaux

Le chantier est considéré terminé lorsque :

1. `/admin/settings` existe et respecte l'arborescence retenue ;
2. le registre fermé distingue clairement dynamique/runtime/secret/invariant ;
3. les paramètres simples retenus sont persistés, validés et audités ;
4. les secrets ne sont jamais exposés ;
5. les intégrations disposent d'un état lisible ;
6. AD/KoXo affiche et applique un modèle d'autorité explicite ;
7. la création d'identité et le mot de passe restent sous autorité KoXo ;
8. API-INTERNAL conserve uniquement les écritures AD nécessaires, notamment groupes de services ;
9. les écritures AD sont auditables ;
10. les templates e-mail sont administrables avec fallback et whitelist de variables ;
11. les notifications portail sont administrables ;
12. le diagnostic est versionné, administrable et simulable ;
13. les inscriptions et paramètres de sécurité retenus sont gérables avec bornes ;
14. les coordonnées de règlement ne dépendent plus obligatoirement du runtime ;
15. les feature flags critiques sont présentés avec risque et confirmations ;
16. les modèles de démo ne nécessitent plus une modification de code pour les changements usuels ;
17. le Centre de configuration fédère les modules existants au lieu de les dupliquer ;
18. optimistic concurrency empêche les écrasements silencieux ;
19. toutes les mutations sensibles passent BFF + CSRF + API + permission + audit ;
20. les tests ciblés et la validation globale applicable passent ;
21. la documentation est synchronisée ;
22. aucun changement non lié du worktree n'a été perdu ou restauré.

---

## 36. Directive d'exécution pour l'agent

Cette documentation constitue la décision d'architecture du chantier.

L'agent chargé de l'implémentation doit :

1. lire ce document en entier ;
2. lire `AGENTS.md`, `.ai/MEMORY.md` et les topics utiles ;
3. inspecter l'état actuel réel du dépôt et les implémentations existantes ;
4. adapter les noms/classes/routes exacts aux conventions du projet sans remettre en cause les décisions fonctionnelles ci-dessus ;
5. implémenter directement par lots cohérents ;
6. valider chaque lot ;
7. ne demander une clarification que si une impossibilité factuelle ou une décision irréversible non couverte apparaît ;
8. ne pas effectuer de commit, push, tag ou déploiement sans demande explicite.

La priorité est la sécurité, l'autorité unique des données, l'absence de duplication et l'exploitabilité réelle en production.

---

## 37. Suites de la revue finale indépendante

La revue finale a conclu **NO-GO**. Ce qu'elle a trouvé n'était pas une
fonctionnalité manquante mais une classe de défaillances silencieuses : des
garanties écrites dans la documentation et vraies dans le code de lecture, mais
non tenues au moment de l'écriture. Les huit points sont fermés ci-dessous.

### 37.1 Atomicité mutation + historique

`ApplicationSettingsService`, les communications et les modèles de démonstration
écrivaient la valeur puis, dans une seconde opération, sa révision. Une panne
entre les deux appliquait un changement sans laisser de trace — le seul mode de
défaillance qu'un audit ne peut pas rattraper, puisqu'une valeur modifiée sans
historique est indistinguable d'une valeur jamais modifiée.

Les trois dépôts MariaDB écrivent désormais la mutation et sa révision dans
**une seule transaction**. Le motif est identique partout :

1. `BeginTransactionAsync` ;
2. `SELECT version, … FOR UPDATE` — le verrou sert à deux choses : vérifier la
   version attendue, et **relire la valeur remplacée**, pour que l'historique
   porte ce qui a réellement été écrasé et non ce qu'une lecture antérieure
   avait vu ;
3. l'écriture ;
4. la révision ;
5. `CommitAsync`.

Un échec de stockage **lève** et remonte `*_STORAGE_UNAVAILABLE` : il n'est
jamais confondu avec un conflit de version, qui a une signification opposée
pour l'administrateur (« quelqu'un a modifié entre-temps » contre « rien n'a été
enregistré »).

### 37.2 Concurrence fiscale

Le nombre de versions d'un régime sert de version optimiste. Il était lu sur une
connexion séparée, avant l'insertion : deux administrateurs partis du même écran
passaient tous les deux, et la mention réellement appliquée devenait
silencieusement celle dont la date d'effet était la plus proche.

Le décompte est maintenant pris avec `FOR UPDATE` dans la transaction qui
insère. Sur un régime encore vide, InnoDB verrouille l'intervalle — c'est
précisément ce qui rend un décompte utilisable comme version. `TryAddAsync`
retourne un `FiscalMentionAddOutcome` à trois branches (`Added`,
`VersionConflict`, `EffectiveDateTaken`) : le service ne relit plus rien pour
décider.

### 37.3 Amorce des modèles de démonstration

L'amorce écrivait modèle par modèle. Interrompue à mi-parcours, elle laissait
une table non vide, donc considérée comme faisant autorité par la règle de
bascule binaire : les modèles manquants devenaient invisibles, et l'amorce
n'était plus rejouable puisqu'elle exige une table vide.

`TryImportAsync` fait le tout ou rien, et vérifie la vacuité **dans** la
transaction — la vérifier avant laisserait deux amorces concurrentes voir toutes
les deux une table vide.

### 37.4 Autorités KoXo appliquées, pas seulement décrites

La vue `/admin/settings/directory` affirmait « KoXo fait autorité sur les
identités et les mots de passe » alors que cinq routes de cycle de vie
écrivaient encore directement en LDAP. Le garde est posé dans
`LdapActiveDirectoryService`, sur les sept méthodes de cycle de vie, et non
route par route : une route ajoutée plus tard en hérite. C'était le point
décisif — une identité doublée ou un mot de passe écrasé ne produit aucune
erreur au moment où il se produit, donc une route oubliée ne se remarquerait
pas.

Les opérations de groupe ne sont **pas** bloquées : elles sont le mandat que
l'API conserve, et ce sont elles qui ouvrent et ferment réellement l'accès.

### 37.5 `/internal/profile/password`

La route posait le mot de passe en LDAP. Avec `ForcePasswords=1`, KoXo réécrit
le mot de passe depuis la colonne 14 du CSV à chaque synchronisation : le
secret posé ici aurait été effacé au passage suivant, sans erreur, après que le
portail a affiché « synchronisé avec Active Directory ». Le client perdait
NextCloud, RDS et le VPN en croyant l'inverse.

Sous autorité KoXo, la route publie dans `IKoxoPendingPasswordStore`, marque la
synchronisation `pending`, déclenche le webhook en rattrapage, et répond
`AD_PASSWORD_CHANGE_PENDING_KOXO` — « appliqué à la prochaine synchronisation »,
ce qui est vrai. Un relais inexploitable refuse en
`503 KOXO_PASSWORD_HANDOFF_UNAVAILABLE` avant tout point de non-retour.

### 37.6 `DemoConversionService`

Le déplacement LDAP direct est **supprimé**, pas documenté comme exception :
sous KoXo, l'OU cible est décrite par `GroupeSecondaire` dans le CSV et
réappliquée à chaque synchronisation. Le déplacement était donc hors mandat,
sans effet durable, et retournait pourtant `identityMoved: true`. La conversion
réserve le code de groupe réel — le levier qui existe déjà — puis déclenche la
synchronisation.

Corollaire assumé : l'absence de déplacement n'est plus comptée comme une
conversion partielle sous autorité KoXo, sans quoi aucune conversion ne
réussirait en production. De même, la révocation d'un essai délègue la
désactivation du compte à KoXo et retire les groupes de services elle-même ;
compter le refus LDAP comme un échec ferait rejouer une révocation déjà
effective côté accès.

### 37.7 Tests

Le comportement transactionnel ne se démontre pas en lisant le code. Un
interrupteur de test (`MockRevisionFailureSwitch`) fait échouer l'écriture de
révision **après** le contrôle de version et **avant** la publication, et les
smoke tests vérifient qu'après l'échec la valeur, la version et le nombre de
révisions sont inchangés. La concurrence fiscale est exercée par deux
`AddMentionAsync` réellement concurrents partant de la même version : exactement
un succès, exactement un conflit, une seule ligne stockée.

Pour l'annuaire, l'assertion utile n'est pas « l'écriture a échoué » mais
« l'écriture n'a pas été tentée » : un appel parti et refusé laisse quand même
une trace d'intention sur un annuaire de production. Un
`RecordingActiveDirectoryService` compte les tentatives ; le test exige zéro.

Des gardes structurels sont ajoutés à `verify-admin-contract.mjs` et
`verify-ad-security-contract.mjs`, donc exécutés par `npm run validate` : ils
échouent si une mutation quitte sa transaction, si une interface réexpose une
écriture de révision indépendante, ou si un garde d'autorité disparaît.

### 37.8 Ce qui reste hors de cette correction

- La preuve en base réelle n'a pas été refaite : les suites tournent en
  persistance mock, où le comportement de verrouillage InnoDB n'est pas
  exercé. Les dépôts mock sont atomiques par construction pour que le test de
  rollback exerce bien la forme du code réel, mais cela ne remplace pas une
  exécution sur MariaDB.
- La migration `079_configuration_permissions_fail_closed.sql` reste à appliquer
  explicitement ; aucune migration n'est appliquée au démarrage.
