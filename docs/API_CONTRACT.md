# Contrat d'API

## Surfaces

Le navigateur accède uniquement à `WEBPORTAL` :

- `GET /api/health`
- `GET /api/health/live`
- `GET /api/health/ready`
- `POST /api/auth/login`
- `POST /api/auth/logout`
- `POST /api/auth/revoke-other-sessions`
- `GET /api/auth/me`
- `POST /api/support-requests`
- `POST /api/support-requests/{id}/messages`
- `POST /api/service-requests`
- `POST /api/service-requests/{id}/messages`
- `GET /api/notifications`
- `POST /api/notifications/{id}/read`
- `POST /api/notifications/read-all`
- `GET /api/catalog`
- `GET /api/commercial-documents`
- `GET /api/commercial-documents/{id}`
- `GET /api/admin/overview`
- `GET /api/admin/activity`
- `GET /api/admin/customers`
- `GET /api/admin/support-requests`
- `GET /api/admin/service-requests`
- `GET /api/admin/catalog`
- `POST /api/admin/catalog`
- `PATCH /api/admin/catalog/{id}`
- `GET /api/admin/commercial-documents`
- `POST /api/admin/commercial-documents`
- `GET /api/admin/commercial-documents/{id}`
- `PATCH /api/admin/commercial-documents/{id}`
- `POST /api/admin/commercial-documents/{id}/lines`
- `PATCH /api/admin/commercial-documents/{id}/lines/{lineId}`
- `POST /api/admin/commercial-documents/{id}/share`
- `POST /api/admin/commercial-documents/{id}/cancel`
- `GET /api/admin/support-requests/{id}`
- `PATCH /api/admin/support-requests/{id}/status`
- `POST /api/admin/support-requests/{id}/notes`
- `POST /api/admin/support-requests/{id}/messages`
- `GET /api/admin/service-requests/{id}`
- `PATCH /api/admin/service-requests/{id}/status`
- `POST /api/admin/service-requests/{id}/notes`
- `POST /api/admin/service-requests/{id}/messages`
- `GET /api/admin/sessions`
- `GET /api/admin/audit-logs`

Les routes suivantes appartiennent à `API-INTERNAL`. Elles sont privées, non
publiées par le reverse proxy et jamais appelées directement par le navigateur :

- `GET /health`
- `GET /health/live`
- `GET /health/ready`
- `POST /internal/auth/sessions`
- `GET /internal/auth/session`
- `DELETE /internal/auth/sessions/current`
- `POST /internal/auth/sessions/revoke-others`
- `GET /internal/admin/overview`
- `GET /internal/admin/activity`
- `GET /internal/admin/customers`
- `GET /internal/admin/support-requests`
- `GET /internal/admin/service-requests`
- `GET /internal/admin/catalog`
- `POST /internal/admin/catalog`
- `PATCH /internal/admin/catalog/{id}`
- `GET /internal/admin/commercial-documents`
- `POST /internal/admin/commercial-documents`
- `GET /internal/admin/commercial-documents/{id}`
- `PATCH /internal/admin/commercial-documents/{id}`
- `POST /internal/admin/commercial-documents/{id}/lines`
- `PATCH /internal/admin/commercial-documents/{id}/lines/{lineId}`
- `POST /internal/admin/commercial-documents/{id}/share`
- `POST /internal/admin/commercial-documents/{id}/cancel`
- `GET /internal/admin/support-requests/{id}`
- `PATCH /internal/admin/support-requests/{id}/status`
- `POST /internal/admin/support-requests/{id}/notes`
- `POST /internal/admin/support-requests/{id}/messages`
- `GET /internal/admin/service-requests/{id}`
- `PATCH /internal/admin/service-requests/{id}/status`
- `POST /internal/admin/service-requests/{id}/notes`
- `POST /internal/admin/service-requests/{id}/messages`
- `GET /internal/admin/sessions`
- `GET /internal/admin/audit-logs`
- `GET /internal/portal/summary`
- `GET /internal/portal/profile`
- `GET /internal/portal/services`
- `GET /internal/portal/invoices`
- `GET /internal/portal/service-catalog`
- `GET /internal/portal/catalog`
- `GET /internal/portal/support-requests`
- `GET /internal/portal/support-requests/{id}`
- `POST /internal/portal/support-requests/{id}/messages`
- `GET /internal/portal/service-requests`
- `GET /internal/portal/service-requests/{id}`
- `POST /internal/portal/service-requests/{id}/messages`
- `GET /internal/portal/commercial-documents`
- `GET /internal/portal/commercial-documents/{id}`
- `GET /internal/portal/notifications`
- `POST /internal/portal/notifications/{id}/read`
- `POST /internal/portal/notifications/read-all`
- `POST /internal/portal/support-requests`
- `POST /internal/portal/service-requests`
- `GET /internal/ad/health`
- `POST /internal/ad/change-password`
- `POST /internal/ad/create-user`
- `POST /internal/ad/add-user-to-group`
- `POST /internal/ad/remove-user-from-group`

## Socle commercial V0.15

Les routes commerciales restent strictement dans le flux
`Navigateur -> WEBPORTAL/BFF -> API-INTERNAL -> MariaDB`.

Contraintes :

- `GET /api/catalog` et `GET /internal/portal/catalog` exposent uniquement des
  offres non sensibles et administrables.
- `GET /api/commercial-documents` et `GET /api/commercial-documents/{id}`
  retournent seulement les documents du client de la session et uniquement
  lorsqu'ils ont été partagés.
- Les mutations `/api/admin/catalog*` et `/api/admin/commercial-documents*`
  exigent toujours `internal_admin`.
- Les montants commerciaux sont validés et stockés en centimes entiers.
- `document_type` est borné à `quote_draft`, `billing_draft` ou
  `informational_invoice`.
- `status` est borné à `draft`, `pending_review`, `shared_with_customer` ou
  `cancelled`.
- `shared_with_customer` ne signifie jamais facture officielle.
- Le disclaimer par défaut est
  `Document informatif - ne constitue pas une facture officielle.`
- Aucune route ne génère de PDF légal, paiement, e-mail réel ou numérotation
  fiscale définitive.

## Conventions

- HTTPS obligatoire hors développement local.
- JSON pour les requêtes et réponses.
- `X-Correlation-Id` accepté, généré si absent et renvoyé.
- `X-Data-Source: mariadb|mock` sur les lectures portail.
- `X-Portal-Session` est ajouté uniquement par le BFF vers `API-INTERNAL`.
- Le navigateur ne lit et ne construit jamais `X-Portal-Session`.
- `X-Service-Auth` est ajouté par le BFF et exigé sur `/internal/*` en
  Production. Il n'est jamais transmis au navigateur.
- Erreurs sans trace, secret, topologie SQL ou détail AD.
- Les health checks n'affichent ni URL, ni host SQL, ni valeur de configuration.

Format d'erreur :

```json
{
  "code": "INVALID_REQUEST",
  "message": "La demande est incomplète ou invalide.",
  "correlation_id": "identifiant-de-correlation"
}
```

## Health checks V0.9

`GET /health/live` et `GET /api/health/live` retournent HTTP 200 si le
processus correspondant répond. Ils ne vérifient ni MariaDB ni AD.

`GET /health/ready` retourne :

- HTTP 200 si la configuration est valide et MariaDB répond lorsque
  `SQL_PROVIDER=mariadb` ;
- HTTP 503 si la configuration Development demande MariaDB sans être complète
  ou si MariaDB est indisponible.

Le champ `checks.ad` expose uniquement le mode (`disabled` par défaut).

`GET /api/health/ready` appelle la readiness API depuis le serveur Next.js. Il
retourne HTTP 503 si la configuration WEBPORTAL est invalide ou API-INTERNAL
injoignable. `INTERNAL_API_URL` n'apparaît jamais dans la réponse.

`GET /health` et `GET /api/health` restent conservés pour compatibilité.

## Authentification V0.9

`POST /api/auth/login` accepte :

```json
{
  "email": "demo.user@example.invalid",
  "password": "valeur-injectée-localement"
}
```

Le BFF appelle `POST /internal/auth/sessions`. API-INTERNAL vérifie le hash du
mot de passe, génère un token aléatoire et persiste uniquement son hash. Le BFF
place le token brut dans un cookie `HttpOnly`, `SameSite=Lax`, `Secure` en
production. La réponse navigateur ne contient jamais le token.

```json
{
  "authenticated": true,
  "user": {
    "displayName": "Client démo",
    "email": "demo.user@example.invalid",
    "customerReference": "CLI-DEMO-0060",
    "status": "active",
    "role": "client_user",
    "lastLoginAt": "2026-06-13T11:00:00.0000000Z"
  },
  "expiresAt": "2026-06-13T12:00:00.0000000Z"
}
```

`GET /api/auth/me` retourne `authenticated: false` sans session valide, ou les
informations minimales ci-dessus. Il ne retourne ni token, ni hash, ni secret.

`POST /api/auth/logout` révoque la session dans API-INTERNAL puis supprime le
cookie. La suppression locale reste effectuée si l'API est indisponible.

`POST /api/auth/revoke-other-sessions` conserve la session courante et révoque
les autres sessions non expirées du même utilisateur. La réponse contient
uniquement `revokedCount`.

Après `LOGIN_MAX_FAILURES` échecs consécutifs dans la fenêtre configurée, le
compte est verrouillé pendant `LOGIN_LOCKOUT_MINUTES`. Le code public est
`ACCOUNT_LOCKED` et le message ne confirme pas l'existence du compte.

Les endpoints internes sont réservés à `WEBPORTAL` :

- `POST /internal/auth/sessions` crée une session ;
- `GET /internal/auth/session` valide `X-Portal-Session` ;
- `DELETE /internal/auth/sessions/current` révoque la session courante.
- `POST /internal/auth/sessions/revoke-others` révoque les autres sessions.

## Administration V0.8

Les routes `/api/admin/*` et `/internal/admin/*` exigent une session valide avec
le rôle `internal_admin`. Le BFF vérifie le rôle, puis API-INTERNAL effectue le
même contrôle. Un `client_user` reçoit `ACCESS_DENIED`.

Les vues sont limitées à 100 lignes, ou 10 audits dans l'overview :

- `overview` : compteurs globaux, derniers audits et état AD ;
- `activity` : compteurs de suivi et dix dernières activités publiques, sans
  contenu de message ni note interne ;
- `customers` : références, statuts et compteurs ;
- `support-requests` : suivi des demandes et mutations workflow bornées ;
- `service-requests` : suivi des demandes et mutations workflow bornées ;
- `sessions` : métadonnées prudentes, sans token ni hash ;
- `audit-logs` : événements récents sans payload sensible.

L'adresse source est masquée et le User-Agent est tronqué. Aucune route admin
ne crée, modifie ou supprime un client, une facture ou un rôle. Les seules
mutations de demande sont celles décrites dans le workflow V0.11 ci-dessous.

## Workflow des demandes V0.11

La V0.11 autorise trois mutations admin bornées : changer un statut, ajouter
une note interne et ajouter un message visible du client. Elle ne permet ni
suppression, ni provisioning, ni modification du client ou du service.

Statuts support :

`open`, `in_progress`, `waiting_for_customer`, `resolved`, `closed`,
`cancelled`.

Statuts demande de service :

`received`, `under_review`, `accepted`, `rejected`, `cancelled`, `completed`.

Tous les passages entre statuts valides sont permis. Demander le statut déjà
présent retourne un succès sans ajouter un second événement. Pour le support,
`closed_at` est renseigné pour `closed` et `cancelled`, puis remis à `NULL`
après réouverture.

Payload de changement de statut :

```json
{
  "status": "in_progress"
}
```

Payload de note interne :

```json
{
  "text": "Note opérationnelle sans secret."
}
```

Payload de message public :

```json
{
  "text": "Votre demande est en cours de traitement."
}
```

Les notes et messages contiennent entre 3 et 2 000 caractères. Ils sont
append-only et rendus comme texte brut. Les notes internes ne figurent jamais
dans les DTO client. Les messages publics sont affichés côté client sous
l'identité générique « Équipe Kermaria ».

Les listes admin acceptent les filtres `status`, `priority` pour le support,
`order=newest|oldest|status` et
`attention=to_handle|client_reply`. La limite reste fixée à 100 éléments.

`to_handle` sélectionne les demandes dans un statut de traitement initial ou
dont le dernier message public provient du client. `client_reply` sélectionne
uniquement les demandes dont le dernier message public provient du client.
Une valeur inconnue retourne `400 INVALID_REQUEST`.

## Centre d'activité admin V0.14

`GET /internal/admin/activity` exige une session `internal_admin` et retourne :

```json
{
  "supportToHandleCount": 2,
  "serviceToHandleCount": 1,
  "recentClientReplyCount": 2,
  "waitingForCustomerCount": 1,
  "activeRequestCount": 4,
  "recentActivities": []
}
```

Les dix activités sont des métadonnées de messages publics : type et référence
de demande, client, sujet, statut, type d'auteur et date. Le texte du message,
les notes internes, tokens et secrets sont exclus du contrat.

Une demande support est « à traiter » si elle est `open`, `in_progress` ou si
son dernier message public vient du client. Une demande de service est « à
traiter » si elle est `received`, `under_review` ou si son dernier message
public vient du client. Aucun statut n'est modifié automatiquement.

Une réponse de mutation contient la référence, le statut courant, l'indication
de changement et le `correlation_id`, sans contenu de note :

```json
{
  "id": "identifiant",
  "reference": "SUP-20260614-EXEMPLE",
  "status": "in_progress",
  "changed": true,
  "correlation_id": "identifiant-de-correlation"
}
```

Les détails client exposent uniquement les événements `created` et
`status_changed`, ainsi que les messages publics. Les détails admin ajoutent
les notes internes. Les mutations admin exigent une session `internal_admin`;
les réponses client V0.13 exigent une session `client_user` propriétaire de la
demande. Ces contrôles sont effectués par le BFF puis par API-INTERNAL.

## Réponses client V0.13

Le client peut ajouter un message public à une demande lui appartenant :

- `POST /api/support-requests/{id}/messages` ;
- `POST /api/service-requests/{id}/messages`.

Le BFF transmet ensuite vers la route privée `/internal/portal/*`. Le
`customer_id` ne figure jamais dans le payload et provient exclusivement de la
session validée par API-INTERNAL.

Payload :

```json
{
  "text": "Voici les informations complémentaires demandées."
}
```

Le texte, après trim, contient entre 3 et 2 000 caractères. Il est stocké dans
`request_public_messages` et rendu comme texte brut. Une demande appartenant à
un autre client est retournée comme introuvable.

Chaque message public expose un auteur contrôlé :

```json
{
  "id": "identifiant",
  "message": "Message public",
  "authorLabel": "Vous",
  "authorType": "client",
  "createdAt": "2026-06-14T20:00:00Z"
}
```

`authorType` vaut `admin` ou `client`. Dans la vue client, ses propres réponses
portent le libellé `Vous`; les messages internes autorisés portent le libellé
`Équipe Kermaria`. La vue admin peut afficher le nom du client auteur.

Une réponse client crée un événement et un audit sans recopier son contenu.
Elle ne crée pas de notification pour le client qui vient de répondre. Aucun
e-mail, temps réel, pièce jointe ou action automatique n'est déclenché.

## Notifications portail V0.12

Les notifications sont internes au portail. Elles sont créées dans la même
transaction MariaDB que l'événement visible qui les déclenche :

- changement réel de statut d'une demande support ;
- changement réel de statut d'une demande de service ;
- ajout d'un message public support ;
- ajout d'un message public service.

Un statut inchangé et une note interne ne créent aucune notification.

Contrat de lecture :

```json
[
  {
    "id": "identifiant",
    "notificationType": "support_status_changed",
    "title": "Mise à jour de votre demande support",
    "message": "Votre demande support est en attente de votre retour.",
    "linkUrl": "/support/identifiant-demande",
    "isRead": false,
    "readAt": null,
    "createdAt": "2026-06-14T12:00:00Z"
  }
]
```

Types autorisés :

- `support_status_changed` ;
- `service_status_changed` ;
- `support_public_message` ;
- `service_public_message`.

Les textes sont synthétiques : le contenu complet d'un message public et les
notes internes ne sont pas recopiés dans la notification.

Réponse de marquage :

```json
{
  "updatedCount": 1,
  "correlation_id": "identifiant-de-correlation"
}
```

API-INTERNAL résout le `customer_id` depuis `X-Portal-Session`. Une notification
absente du client courant retourne une erreur non distinctive
`PORTAL_DATA_NOT_FOUND`. Un `internal_admin` ne peut pas utiliser ces routes
portail.

## Endpoints portail

Les `GET /internal/portal/*` utilisent MariaDB lorsque `SQL_PROVIDER=mariadb`
et que toutes les variables SQL sont disponibles. En développement uniquement,
ils utilisent le dépôt mock lorsque SQL est absent.

Toutes les routes `/internal/portal/*`, catalogue inclus, exigent une session
valide. API-INTERNAL résout `user_id` puis `customer_id` depuis cette session.
Les payloads ne contiennent jamais de `customerId`.

Les POST acceptent les contrats suivants :

```json
{
  "serviceId": "svc-backup-001",
  "priority": "normal",
  "subject": "Objet non sensible",
  "description": "Description non sensible"
}
```

```json
{
  "catalogItemId": "catalog-vpn",
  "subject": "Demande de service",
  "description": "Description non sensible"
}
```

Réponse persistée :

```json
{
  "reference": "SUP-20260612-EXEMPLE",
  "status": "received",
  "persisted": true,
  "message": "Demande enregistrée.",
  "correlation_id": "identifiant-de-correlation"
}
```

Réponse du fallback :

```json
{
  "reference": "SUP-MOCK-EXEMPLE",
  "status": "mock_received",
  "persisted": false,
  "message": "Demande mock reçue.",
  "correlation_id": "identifiant-de-correlation"
}
```

Une écriture persistée crée aussi un événement `audit_logs`. Aucun e-mail,
devis, contrat, provisioning, paiement ou traitement immédiat n'est déclenché.

Pour le support, `serviceId` doit appartenir au client connecté. Un service
d'un autre client retourne `ACCESS_DENIED`. Une demande de service écrit le
`customer_id` et le `created_by_user_id` issus de la session.

## Active Directory

`GET /internal/ad/health` retourne uniquement :

```json
{
  "mode": "disabled",
  "status": "disabled",
  "configurationValid": true,
  "operationsEnabled": false
}
```

Il ne retourne jamais domaine, OU, compte de service, mot de passe ou topologie.

Comportement des modes :

| Mode | Réseau AD | Mutation réelle |
|---|---|---|
| `disabled` | Non | Non, `AD_INTEGRATION_DISABLED` |
| `mock` | Non | Non; changement de mot de passe refusé, autres actions simulées |
| `test` | Non dans cette V0.8 | Non, `AD_REAL_CHANGE_NOT_ENABLED` |
| `enabled` | Non dans cette V0.8 | Non, validation supplémentaire requise |

Le contrat de changement de mot de passe exige `targetDistinguishedName`,
`currentPassword` et `newPassword`. Ces deux mots de passe ne doivent jamais
être persistés ou journalisés. Toute cible hors `AD_CLIENTS_OU_DN` est refusée.

Les opérations de groupe appliquent `AD_ALLOWED_GROUPS` comme allowlist stricte
et refusent les groupes administratifs. La création utilisateur et les
modifications de groupe restent non opérationnelles.

## Codes d'erreur

- `INVALID_REQUEST` : JSON ou champs invalides ;
- `INVALID_CREDENTIALS` : identifiants invalides, sans indiquer si le compte existe ;
- `ACCOUNT_LOCKED` : connexion temporairement bloquée après plusieurs échecs ;
- `SESSION_REQUIRED` : session absente ;
- `SESSION_INVALID` : token inconnu ou invalide ;
- `SESSION_EXPIRED` : session expirée ;
- `SESSION_REVOKED` : session révoquée ;
- `ACCESS_DENIED` : ressource hors du client connecté ou compte non autorisé ;
- `PORTAL_DATA_NOT_FOUND` : données de démonstration absentes ;
- `SQL_CONFIG_MISSING` : configuration SQL absente hors développement ;
- `SQL_UNAVAILABLE` : MariaDB configurée mais indisponible ;
- `AD_INTEGRATION_DISABLED` : mode AD désactivé ;
- `AD_REAL_CHANGE_NOT_ENABLED` : opération AD réelle non validée ;
- `AD_TARGET_OUTSIDE_ALLOWED_OU` : cible hors OU autorisée ;
- `AD_SCOPE_NOT_ALLOWED` : cible ou groupe non autorisé ;
- `AD_CONFIGURATION_INVALID` : configuration de test incomplète ;
- `ROUTE_NOT_FOUND` : route inconnue ;
- `INTERNAL_ERROR` : erreur interne contrôlée.
