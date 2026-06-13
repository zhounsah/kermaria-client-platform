# Stack technique recommandée

## Principes de sélection

La stack doit respecter la séparation entre le portail public et les systèmes
internes, rester exploitable sur les deux VM prévues et utiliser des
technologies maintenues. Les versions exactes seront figées dans le squelette
technique et mises à jour selon une politique de support documentée.

## WEBPORTAL

Stack recommandée :

- Next.js avec TypeScript ;
- version LTS maintenue de Node.js ;
- rendu serveur et Backend for Frontend lorsque le parcours le nécessite ;
- application publique hébergée sur Ubuntu Server LTS ;
- exposition uniquement derrière Cloudflare et un reverse proxy HTTPS ;
- validation des entrées côté serveur ;
- sessions sécurisées et protections web appliquées dans le portail.

Next.js permet de regrouper l'interface client et le backend public adapté à
cette interface. TypeScript apporte des contrats explicites et facilite le
partage de types non sensibles. Le navigateur ne doit pas connaître l'adresse
privée d'`API-INTERNAL`.

`WEBPORTAL` ne constitue pas une zone de confiance pour les opérations
d'infrastructure. Il traduit les demandes utilisateur en appels métier
contrôlés vers l'API privée.

Le squelette de phase 2 fixe actuellement Next.js 16.2.9, TypeScript 6.0.3 et
Node.js 24 LTS. Les mises à jour de sécurité compatibles doivent être appliquées
avant chaque livraison.

## API-INTERNAL

Stack recommandée :

- ASP.NET Core en C# ;
- Minimal API pour un premier périmètre réduit et explicite ;
- Web API avec contrôleurs si la complexité des contrats ou des politiques
  transverses le justifie plus tard ;
- hébergement sur Windows Server Core 2022 ou 2025 ;
- service exécuté avec une identité dédiée non administratrice ;
- adaptateurs séparés pour SQL et chaque intégration d'infrastructure.

ASP.NET Core s'intègre naturellement à Windows Server, fournit une base solide
pour l'authentification, l'autorisation, les health checks, la journalisation et
les futurs connecteurs internes. Le choix Minimal API ou contrôleurs ne change
pas la frontière de sécurité : toutes les routes sensibles restent privées.

Le squelette de phase 2 cible .NET 10 LTS et fixe le SDK à 10.0.301. Le SDK
.NET 7 détecté initialement sur le poste est hors support et ne doit pas être
retenu pour la plateforme.

## SQL

La plateforme utilisera le serveur SQL existant. Le moteur exact doit être
confirmé avant toute implémentation ou migration.

La couche d'accès aux données devra :

- être isolée dans `API-INTERNAL` ;
- conserver le modèle métier compatible avec SQL Server, PostgreSQL ou MariaDB
  jusqu'à confirmation du moteur ;
- utiliser une identité applicative dédiée aux droits minimaux ;
- recevoir sa configuration par variable d'environnement ou gestionnaire de
  secrets ;
- ne jamais exposer de chaîne de connexion à `WEBPORTAL`.

## Authentification

La production utilisera un fournisseur d'identité standard compatible avec MFA.
Le fournisseur, le protocole et les politiques de session seront choisis plus
tard après validation des contraintes d'exploitation.

Pendant le développement initial :

- utiliser une authentification mock ou locale ;
- employer uniquement des identités fictives ;
- signaler clairement que ce mode est impropre à la production ;
- empêcher son activation accidentelle dans un environnement de production.

L'authentification des utilisateurs et l'identité technique entre services sont
deux mécanismes distincts.

## Communication entre les VM

`WEBPORTAL` appelle `API-INTERNAL` par HTTPS sur le réseau privé. Chaque appel
doit comporter :

- une identité service-à-service vérifiable ;
- un `correlation_id` propagé entre les composants ;
- un délai d'expiration ;
- une validation stricte de la réponse et des erreurs ;
- aucun secret dans l'URL ou les logs.

Le mécanisme d'identité service-à-service pourra utiliser mTLS ou des jetons de
service courts. Le choix final dépendra de l'infrastructure de certificats et du
fournisseur d'identité retenus.

## Logs et observabilité

Les deux applications produiront des logs structurés, lisibles par une solution
de collecte centralisée. Les événements devront inclure au minimum :

- `timestamp` ;
- `level` ;
- `service` ;
- `environment` ;
- `correlation_id` ;
- type d'événement et résultat ;
- identifiants fonctionnels non sensibles utiles au diagnostic.

Les mots de passe, jetons, secrets, chaînes de connexion et documents complets
ne doivent jamais être journalisés. Les actions sensibles utilisent en plus les
logs d'audit décrits dans `SECURITY.md`.

## Pourquoi WEBPORTAL reste isolé des systèmes internes

`WEBPORTAL` est exposé aux navigateurs et aux requêtes provenant d'Internet. Un
accès direct à SQL, AD, NAS, RDS, VPN ou la facturation augmenterait fortement
l'impact d'une faille web ou d'un vol de session.

L'interdiction de ces accès permet :

- de limiter les mouvements latéraux après compromission du portail ;
- de centraliser les autorisations sensibles dans `API-INTERNAL` ;
- d'utiliser des identités techniques différentes et moins privilégiées ;
- de valider et auditer chaque opération d'infrastructure ;
- de masquer la topologie, les protocoles et les erreurs internes ;
- de changer un connecteur sans exposer ses détails au portail ;
- de restreindre les règles pare-feu selon le principe du moindre privilège.

Le portail obtient uniquement les données métier nécessaires par le contrat de
l'API privée.

## Choix à confirmer avant la production

- politique de mise à jour des versions fixées dans le squelette ;
- choix entre Minimal API et contrôleurs pour la cible finale ;
- moteur et version du serveur SQL existant ;
- bibliothèque d'accès aux données et stratégie de migrations ;
- fournisseur d'identité, protocole, MFA et politiques de session ;
- mécanisme d'identité service-à-service : mTLS ou jetons courts ;
- autorité de certification et gestion du cycle de vie des certificats ;
- reverse proxy, règles Cloudflare et stratégie d'accès à l'origine ;
- gestionnaire de secrets et procédure de rotation ;
- solution de logs, métriques, traces, audit et alertes ;
- ports internes définitifs et règles pare-feu ;
- mécanisme d'hébergement des processus sur Linux et Windows ;
- stratégie de sauvegarde, restauration, déploiement et retour arrière ;
- exigences de disponibilité, rétention, conformité et localisation des
  données.
