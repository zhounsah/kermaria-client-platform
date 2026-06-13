# Contrat d'API

## Surfaces

Le navigateur accède uniquement à `WEBPORTAL` :

- `GET /api/health`
- `POST /api/auth/login`
- `POST /api/auth/logout`
- `GET /api/auth/me`
- `POST /api/support-requests`
- `POST /api/service-requests`

Les routes suivantes appartiennent à `API-INTERNAL`. Elles sont privées, non
publiées par le reverse proxy et jamais appelées directement par le navigateur :

- `GET /health`
- `POST /internal/auth/sessions`
- `GET /internal/auth/session`
- `DELETE /internal/auth/sessions/current`
- `GET /internal/portal/summary`
- `GET /internal/portal/profile`
- `GET /internal/portal/services`
- `GET /internal/portal/invoices`
- `GET /internal/portal/service-catalog`
- `GET /internal/portal/support-requests`
- `POST /internal/portal/support-requests`
- `POST /internal/portal/service-requests`
- `GET /internal/ad/health`
- `POST /internal/ad/change-password`
- `POST /internal/ad/create-user`
- `POST /internal/ad/add-user-to-group`
- `POST /internal/ad/remove-user-from-group`

## Conventions

- HTTPS obligatoire hors développement local.
- JSON pour les requêtes et réponses.
- `X-Correlation-Id` accepté, généré si absent et renvoyé.
- `X-Data-Source: mariadb|mock` sur les lectures portail.
- `X-Portal-Session` est ajouté uniquement par le BFF vers `API-INTERNAL`.
- Le navigateur ne lit et ne construit jamais `X-Portal-Session`.
- Erreurs sans trace, secret, topologie SQL ou détail AD.
- Identité service-à-service obligatoire avant production.

Format d'erreur :

```json
{
  "code": "INVALID_REQUEST",
  "message": "La demande est incomplète ou invalide.",
  "correlation_id": "identifiant-de-correlation"
}
```

## Authentification V0.7

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
    "status": "active"
  },
  "expiresAt": "2026-06-13T12:00:00.0000000Z"
}
```

`GET /api/auth/me` retourne `authenticated: false` sans session valide, ou les
informations minimales ci-dessus. Il ne retourne ni token, ni hash, ni secret.

`POST /api/auth/logout` révoque la session dans API-INTERNAL puis supprime le
cookie. La suppression locale reste effectuée si l'API est indisponible.

Les endpoints internes sont réservés à `WEBPORTAL` :

- `POST /internal/auth/sessions` crée une session ;
- `GET /internal/auth/session` valide `X-Portal-Session` ;
- `DELETE /internal/auth/sessions/current` révoque la session courante.

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
| `test` | Non dans cette V0.7 | Non, `AD_REAL_CHANGE_NOT_ENABLED` |
| `enabled` | Non dans cette V0.7 | Non, validation supplémentaire requise |

Le contrat de changement de mot de passe exige `targetDistinguishedName`,
`currentPassword` et `newPassword`. Ces deux mots de passe ne doivent jamais
être persistés ou journalisés. Toute cible hors `AD_CLIENTS_OU_DN` est refusée.

Les opérations de groupe appliquent `AD_ALLOWED_GROUPS` comme allowlist stricte
et refusent les groupes administratifs. La création utilisateur et les
modifications de groupe restent non opérationnelles.

## Codes d'erreur

- `INVALID_REQUEST` : JSON ou champs invalides ;
- `INVALID_CREDENTIALS` : identifiants invalides, sans indiquer si le compte existe ;
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
