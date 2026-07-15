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
- `GET /api/cart`
- `POST /api/cart/items`
- `POST /api/cart/items/remove`
- `POST /api/cart/confirm`
- `GET /api/checkout/summary`
- `POST /api/checkout/subscriptions/items`
- `POST /api/checkout/subscriptions/items/remove`
- `POST /api/checkout/subscriptions/confirm`
- `GET /api/commercial-documents`
- `GET /api/commercial-documents/{id}`
- `POST /api/commercial-documents/{id}/payment-method`
- `GET /api/downloads`
- `GET /api/downloads/{id}/file`
- `GET /api/admin/overview`
- `GET /api/admin/activity`
- `GET /api/admin/customers`
- `GET /api/admin/support-requests`
- `GET /api/admin/service-requests`
- `GET /api/admin/catalog`
- `POST /api/admin/catalog`
- `PATCH /api/admin/catalog/{id}`
- `GET /api/admin/content`
- `GET /api/admin/content/{key}`
- `PATCH /api/admin/content/{key}`
- `GET /api/admin/download-categories`
- `POST /api/admin/download-categories`
- `PATCH /api/admin/download-categories/{id}`
- `DELETE /api/admin/download-categories/{id}`
- `GET /api/admin/downloads`
- `POST /api/admin/downloads`
- `GET /api/admin/downloads/{id}`
- `PATCH /api/admin/downloads/{id}`
- `DELETE /api/admin/downloads/{id}`
- `POST /api/admin/downloads/{id}/file`
- `DELETE /api/admin/downloads/{id}/file`
- `GET /api/admin/commercial-documents`
- `POST /api/admin/commercial-documents`
- `GET /api/admin/commercial-documents/{id}`
- `PATCH /api/admin/commercial-documents/{id}`
- `POST /api/admin/commercial-documents/{id}/lines`
- `PATCH /api/admin/commercial-documents/{id}/lines/{lineId}`
- `POST /api/admin/commercial-documents/{id}/share`
- `POST /api/admin/commercial-documents/{id}/cancel`
- `POST /api/admin/commercial-documents/{id}/issue`
- `GET /api/admin/commercial-documents/{id}/invoice`
- `GET /api/admin/commercial-documents/{id}/invoice/pdf`
- `POST /api/payment/paypal/create`
- `GET /api/payment/paypal/return`
- `GET /api/admin/support-requests/{id}`
- `PATCH /api/admin/support-requests/{id}/status`
- `POST /api/admin/support-requests/{id}/notes`
- `POST /api/admin/support-requests/{id}/messages`
- `GET /api/admin/service-requests/{id}`
- `PATCH /api/admin/service-requests/{id}/status`
- `POST /api/admin/service-requests/{id}/notes`
- `POST /api/admin/service-requests/{id}/messages`
- `GET /api/admin/public-pack-catalog`
- `PATCH /api/admin/public-pack-catalog`
- `GET /api/admin/sessions`
- `GET /api/admin/audit-logs`

Les routes suivantes appartiennent à `API-INTERNAL`. Elles sont privées, non
publiées par le reverse proxy et jamais appelées directement par le navigateur :

- `GET /health`
- `GET /health/live`
- `GET /ready`
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
- `GET /internal/portal/content/{key}`
- `GET /internal/admin/content`
- `GET /internal/admin/content/{key}`
- `PATCH /internal/admin/content/{key}`
- `GET /internal/admin/download-categories`
- `POST /internal/admin/download-categories`
- `PATCH /internal/admin/download-categories/{id}`
- `DELETE /internal/admin/download-categories/{id}`
- `GET /internal/admin/downloads`
- `POST /internal/admin/downloads`
- `GET /internal/admin/downloads/{id}`
- `PATCH /internal/admin/downloads/{id}`
- `DELETE /internal/admin/downloads/{id}`
- `POST /internal/admin/downloads/{id}/file`
- `DELETE /internal/admin/downloads/{id}/file`
- `GET /internal/admin/commercial-documents`
- `POST /internal/admin/commercial-documents`
- `GET /internal/admin/commercial-documents/{id}`
- `PATCH /internal/admin/commercial-documents/{id}`
- `POST /internal/admin/commercial-documents/{id}/lines`
- `PATCH /internal/admin/commercial-documents/{id}/lines/{lineId}`
- `POST /internal/admin/commercial-documents/{id}/share`
- `POST /internal/admin/commercial-documents/{id}/cancel`
- `POST /internal/admin/commercial-documents/{id}/issue`
- `GET /internal/admin/commercial-documents/{id}/invoice`
- `GET /internal/admin/commercial-documents/{id}/invoice/pdf`
- `POST /internal/portal/commercial-documents/{id}/payment-confirm`
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
- `GET /internal/portal/cart`
- `POST /internal/portal/cart/items`
- `POST /internal/portal/cart/items/remove`
- `POST /internal/portal/cart/confirm`
- `GET /internal/portal/checkout/summary`
- `POST /internal/portal/checkout/subscriptions/items`
- `POST /internal/portal/checkout/subscriptions/items/remove`
- `POST /internal/portal/checkout/subscriptions/confirm`
- `GET /internal/portal/support-requests`
- `GET /internal/portal/support-requests/{id}`
- `POST /internal/portal/support-requests/{id}/messages`
- `GET /internal/portal/service-requests`
- `GET /internal/portal/service-requests/{id}`
- `POST /internal/portal/service-requests/{id}/messages`
- `GET /internal/portal/commercial-documents`
- `GET /internal/portal/commercial-documents/{id}`
- `POST /internal/portal/commercial-documents/{id}/payment-method`
- `GET /internal/portal/downloads`
- `GET /internal/portal/downloads/{id}/file`
- `GET /internal/portal/notifications`
- `POST /internal/portal/notifications/{id}/read`
- `POST /internal/portal/notifications/read-all`
- `POST /internal/portal/support-requests`
- `POST /internal/portal/service-requests`
- `GET /internal/portal/public-pack-catalog`
- `GET /internal/admin/public-pack-catalog`
- `PATCH /internal/admin/public-pack-catalog`
- `GET /internal/ad/health`
- `POST /internal/ad/change-password`
- `POST /internal/ad/create-user`
- `POST /internal/ad/add-user-to-group`
- `POST /internal/ad/remove-user-from-group`

## Facturation BPCE V0.20

Les routes BPCE restent strictement dans le flux
`Navigateur -> WEBPORTAL/BFF -> API-INTERNAL -> BPCE`. Le navigateur ne
contacte jamais l'API BPCE.

Contraintes :

- `POST /api/admin/commercial-documents/{id}/issue` exige le role
  `internal_admin`. Le BFF appelle `POST .../issue` cote
  API-INTERNAL qui orchestre customer upsert, draft, validate et cache
  PDF (cf. `docs/V0.20_BPCE_INVOICING.md`).
- `GET .../invoice` retourne les metadonnees de la facture BPCE
  associee (numero fiscal, dates, statut). Aucune cle BPCE n'est exposee.
- `GET .../invoice/pdf` retourne le PDF depuis le cache local. L'API
  BPCE n'est jamais re-interrogee pour servir un PDF cote portail.
- `POST /internal/portal/commercial-documents/{id}/payment-confirm` est
  appele par le flux de retour PayPal apres capture. Il propage
  `mark_as_paid` cote BPCE et passe `commercial_documents.status` a
  `paid`.

Garde-fous :

- aucun appel BPCE n'est realise si `BPCE_INTEGRATION_MODE=disabled` ;
- `BPCE_REFRESH_TOKEN` ne quitte jamais API-INTERNAL ;
- aucun corps de reponse BPCE complet n'est journalise.

## Paiement en ligne PayPal V0.21

Les routes paiement restent strictement dans le flux
`Navigateur -> WEBPORTAL -> PayPal`. PayPal redirige le buyer vers
`/api/payment/paypal/return` apres approbation.

Contraintes :

- `POST /api/payment/paypal/create` exige une session client. Il appelle
  PayPal Orders API v2 cote serveur avec `intent: CAPTURE` (one-shot,
  jamais recurrent) puis retourne `{ orderId, approveUrl }`.
- `GET /api/payment/paypal/return?token={orderId}&documentId={id}` est la
  cible de redirection PayPal. Il capture l'ordre, appelle
  `payment-confirm` cote API-INTERNAL puis redirige vers la facture avec
  un parametre `payment=success`, `cancelled` ou `error`.
- Aucun montant fourni par le navigateur n'est utilise : le montant est
  recalcule cote serveur depuis le document commercial.
- `PAYPAL_CLIENT_SECRET` ne quitte jamais WEBPORTAL et n'est jamais
  journalise.

## Contenus administrables V0.33

Le module de contenus administrables repose sur un **registre ferme** de
cles partage entre `packages/shared`, `WEBPORTAL` et `API-INTERNAL`.

Contraintes :

- cles actuelles : `legal:cgv`, `legal:mentions-legales`,
  `page:a-propos` et `pack-sheet:<publicPackCode>` ;
- aucune creation libre de contenu par API ;
- aucune suppression libre par API ;
- seules `bodyMarkdown` et `versionLabel` sont mutables ;
- `title`, `contentType` et `publicPath` restent figes par la cle ;
- les routes admin exigent `internal_admin` cote BFF et
  `admin.catalog.read` / `admin.catalog.write` cote `API-INTERNAL` ;
- l'ecriture journalise `managed_content.update`.

Comportement des endpoints :

- `GET /internal/portal/content/{key}` : lecture publique interne d'un
  contenu seed ou persiste ;
- `GET /internal/admin/content` : liste admin ;
- `GET /internal/admin/content/{key}` : detail admin ;
- `PATCH /internal/admin/content/{key}` : upsert persistant ;
- `GET/PATCH /internal/admin/public-pack-catalog` : contenu marketing de
  la vitrine packs, distinct des fiches techniques detaillees.

Notes de contrat :

- les cles sont souvent URL-encodees (`legal%3Acgv`) entre l'UI et le
  BFF ; les routes Next les decodent avant validation ;
- le site public ne publie pas de route navigateur `/api/content` :
  les pages publiques appellent ces surfaces via les helpers serveur
  `internal-api.ts` ;
- le rendu Markdown public et admin passe par `react-markdown`, sans
  HTML brut.

## Checkout unifie V0.36

Les routes checkout restent strictement dans le flux
`Navigateur -> WEBPORTAL/BFF -> API-INTERNAL -> MariaDB`.

Contraintes :

- `GET /api/checkout/summary` et
  `GET /internal/portal/checkout/summary` exposent un agregat unique
  `CheckoutSummary` avec :
  - `cart` pour le panier one-shot ;
  - `recurring` pour la selection recurrente facturee ;
  - `totalItemCount` et `hasMixedCheckout` pour l'UI.
- `POST /api/checkout/subscriptions/items` et son endpoint miroir interne
  ajoutent une offre `monthly` active et payable a la selection recurrente.
  Les offres `one_time`, gratuites ou inactives sont refusees via
  `RECURRING_CHECKOUT_OFFER_NOT_ELIGIBLE`.
- `POST /api/checkout/subscriptions/items/remove` retire une selection
  recurrente par `offerId`.
- `POST /api/checkout/subscriptions/confirm` cree :
  - une souscription locale `rail='billing'` et `status='pending_payment'`
    par ligne recurrente ;
  - un document commercial groupe `origin='recurring_checkout'` ;
  - les liaisons `commercial_document_line_subscriptions`.
- `POST /api/cart/confirm` reste reserve au tunnel one-shot et continue de
  produire un document `origin='client_cart'`.
- Le BFF peut retomber sur le seul panier historique si
  `/internal/portal/checkout/summary` n'existe pas encore sur le runtime
  cible (`ROUTE_NOT_FOUND`, `SQL_UNAVAILABLE`, `INTERNAL_ERROR`,
  `INTERNAL_API_UNAVAILABLE`, `INVALID_INTERNAL_RESPONSE`).

## Centre de telechargements client V0.37

Le module telechargements reste strictement dans le flux
`Navigateur -> WEBPORTAL/BFF -> API-INTERNAL -> MariaDB/private storage`.

Contraintes :

- `GET /api/downloads` et `GET /internal/portal/downloads` exigent une
  session client et exposent uniquement des categories non vides avec leurs
  cartes client. Le JSON ne contient ni chemin physique ni URL externe brute.
- `GET /api/downloads/{id}/file` revalide la session client puis appelle
  `GET /internal/portal/downloads/{id}/file`. Le resultat est soit :
  - un `attachment` pour un fichier interne ;
  - une redirection autorisee pour une ressource externe ;
  - `404` si la ressource est inactive ou non autorisee.
- Les droits portail sont calcules uniquement a partir des droits actifs :
  `subscriptions.publicPackCode`, `subscriptions.offerExternalReference` et
  `customer_services.service_type`. Les statuts non actifs ne publient rien.
- `GET/POST/PATCH/DELETE /api/admin/downloads*` et
  `/api/admin/download-categories*` exigent un role `internal_admin`. Les
  mutations BFF reutilisent la protection CSRF deja en place.
- `POST /api/admin/downloads/{id}/file` accepte un upload `multipart/form-data`
  uniquement pour les ressources `internal_file`.
- `DELETE /api/admin/downloads/{id}/file` supprime le binaire prive et la
  ressource repasse `inactive` tant qu'aucun nouveau fichier n'est charge.
- `DELETE /api/admin/download-categories/{id}` renvoie un conflit si la
  categorie est encore referencee.
- Les mutations admin journalisent creation, mise a jour, suppression,
  upload et retrait de binaire ; une delivrance client reussie journalise
  `download.deliver`.

## Choix explicite du virement bancaire V0.36

La selection du virement bancaire reste strictement dans le flux
`Navigateur -> WEBPORTAL/BFF -> API-INTERNAL -> MariaDB`.

Contraintes :

- `POST /api/commercial-documents/{id}/payment-method` appelle
  `POST /internal/portal/commercial-documents/{id}/payment-method`.
- Le payload accepte uniquement `{ "paymentMethod": "manual" }`.
- Le document doit appartenir au client de la session et etre deja `issued`.
- Cette route **n'encaisse rien** : elle enregistre seulement le choix du
  rail et laisse le document en attente de reglement.
- L'encaissement reel converge ensuite sur
  `POST /internal/portal/commercial-documents/{id}/payment-confirm`
  (Stripe / PayPal) ou sur le flux admin `mark-as-paid` (virement).
- Une fois le document marque `paid`, `InvoiceIssuingService` declenche
  a la fois :
  - `CartProvisioningTrigger` pour les documents issus du panier one-shot ;
  - `BilledSubscriptionPaymentTrigger` pour les souscriptions reliees au
    document (premier paiement ou renouvellement).

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
- `status` est borné à `draft`, `pending_review`, `shared_with_customer`,
  `cancelled`, `issued` ou `paid`. `issued` et `paid` sont produits
  uniquement par les flux BPCE/paiement (V0.20/V0.21).
- `shared_with_customer` ne signifie jamais facture officielle.
- Le disclaimer par défaut est
  `Document informatif - ne constitue pas une facture officielle.`
- La numerotation fiscale officielle et les PDF immuables sont produits
  par BPCE en V0.20, jamais en V0.15.

## Conventions

- HTTPS obligatoire hors développement local.
- JSON pour les requêtes et réponses.
- `X-Correlation-Id` accepté, généré si absent et renvoyé.
- `X-Data-Source: mariadb|mock` sur les lectures portail.
- `X-Portal-Session` est ajouté uniquement par le BFF vers `API-INTERNAL`.
- Le navigateur ne lit et ne construit jamais `X-Portal-Session`.
- `X-Service-Auth` est ajouté par le BFF et exigé sur `/internal/*` dans tout
  environnement non `Development`. Il n'est jamais transmis au navigateur.
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

## Health checks V0.16

`GET /health/live` et `GET /api/health/live` retournent HTTP 200 si le
processus correspondant répond. Ils ne vérifient ni MariaDB ni AD.

`GET /ready` est un alias court de `GET /health/ready` pour la supervision.

`GET /health/ready` retourne :

- HTTP 200 si la configuration est valide et MariaDB répond lorsque
  `SQL_PROVIDER=mariadb` ;
- HTTP 503 si la configuration Development demande MariaDB sans être complète
  ou si MariaDB est indisponible.

Le champ `checks.ad` expose uniquement le mode (`disabled` par défaut).

Les réponses API exposent `X-Correlation-Id` et `X-Request-Id` sans inclure de
secret ou de détail de connexion.

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

`GET /internal/admin/ad/status` retourne le mode actif, l'etat de preparation,
les capacites lecture/ecriture et le scope autorise, sans jamais exposer de
mot de passe ni de details de bind.

Les recherches et mutations AD passent uniquement par `API-INTERNAL`, via les
endpoints admin `/internal/admin/ad/*` et
`/internal/admin/customers/{customerReference}/ad/*`.

Comportement des modes :

| Mode | Réseau AD | Mutation réelle |
|---|---|---|
| `disabled` | Non | Non, `AD_INTEGRATION_DISABLED` |
| `mock` | Non | Non, mutations simulées et auditables |
| `read_only` | Oui | Non, `AD_READ_ONLY` |
| `controlled_write` | Oui | Oui, strictement bornée à `OU=TEST_SITE_WEB,DC=home,DC=bzh` |

Les écritures V0.18 autorisées sont limitées à :

- création d'utilisateur de test dans `OU=Users,OU=<CUSTOMER_REFERENCE>,OU=10_Customers,...` ;
- création de groupe dans `OU=Groups,OU=<CUSTOMER_REFERENCE>,OU=10_Customers,...` ;
- ajout / retrait de membre dans le même `customerReference` ;
- désactivation d'utilisateur ;
- déplacement d'un utilisateur désactivé vers `OU=Disabled,OU=<CUSTOMER_REFERENCE>,OU=10_Customers,...` ;
- création / suppression d'un lien MariaDB `customer_ad_links`.

Aucun hard delete AD, reset de mot de passe ou activation d'une OU de
production n'est exposé.

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
- `AD_READ_ONLY` : écriture AD refusée en mode lecture seule ;
- `AD_TARGET_OUTSIDE_ALLOWED_OU` : cible hors OU autorisée ;
- `AD_CROSS_CUSTOMER_FORBIDDEN` : opération AD entre deux clients différents ;
- `AD_GROUP_MEMBER_ALREADY_PRESENT` : membership déjà présent, sans changement ;
- `AD_GROUP_MEMBER_ALREADY_ABSENT` : membership déjà absent, sans changement ;
- `AD_SCOPE_NOT_ALLOWED` : cible ou groupe non autorisé ;
- `AD_CONFIGURATION_INVALID` : configuration de test incomplète ;
- `ROUTE_NOT_FOUND` : route inconnue ;
- `INTERNAL_ERROR` : erreur interne contrôlée.
