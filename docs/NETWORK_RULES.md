# Règles réseau provisoires

## Principes

Ces règles décrivent la cible logique avant attribution des adresses, VLAN,
groupes de sécurité et ports applicatifs définitifs.

- Tout flux non explicitement listé et approuvé est refusé par défaut.
- `SRV-11` est le seul composant publie sur Internet et relaie vers
  `WEBPORTAL`.
- `API-INTERNAL` et le serveur SQL ne disposent d'aucune publication Internet.
- Les flux d'administration passent exclusivement par un réseau
  d'administration ou un VPN autorisé.
- Chaque règle doit être limitée aux sources, destinations, protocoles et ports
  strictement nécessaires.
- L'ouverture d'un port ne remplace jamais l'authentification et l'autorisation
  applicatives.

## Flux provisoirement autorisés

| Source | Destination | Protocole / port | Usage et conditions |
|---|---|---|---|
| Internet | `SRV-11` / reverse proxy public | TCP 80/443 | Accès HTTP de redirection puis HTTPS vers les FQDN publics |
| Cloudflare / reverse proxy | `SRV-12` / `WEBPORTAL` | TCP 3000 | Transmission HTTP privée vers le `Next standalone` |
| `SRV-12` / `WEBPORTAL` | `SRV-13` / `API-INTERNAL` | TCP 5000 | Appels privés avec identité service-à-service |
| `API-INTERNAL` | SQL existant | Port SQL à confirmer selon le moteur | Accès avec compte applicatif dédié et chiffrement si disponible |
| `API-INTERNAL` | Contrôleurs de domaine futurs | TCP 636 | LDAPS pour les opérations AD validées |
| `API-INTERNAL` | DNS / Kerberos / LDAP futurs | Ports strictement nécessaires à confirmer et documenter | Uniquement si LDAPS et l'architecture retenue exigent ces dépendances |
| `API-INTERNAL` | NAS / RDS / VPN / facturation futurs | Ports explicitement nécessaires à chaque connecteur | Aucun accès générique ; une règle dédiée par intégration |
| Administration via réseau admin / VPN | `SRV-11` | TCP 22 | SSH d'administration, jamais ouvert directement à Internet |
| Administration via réseau admin / VPN | `WEBPORTAL` | TCP 22 | SSH d'administration, jamais ouvert directement à Internet |
| Administration via réseau admin / VPN | `API-INTERNAL` | TCP 3389 ou WinRM HTTPS TCP 5986, à confirmer | RDP ou WinRM selon les standards d'exploitation |
| VM applicatives | Collecte de logs et supervision | Ports à confirmer | Télémétrie sécurisée vers les destinations approuvées |
| VM applicatives | DNS, synchronisation de temps et mises à jour approuvées | Ports et relais à confirmer | Services d'infrastructure nécessaires à l'exploitation |

L'usage futur de DNS, Kerberos ou LDAP avec Active Directory doit faire l'objet
d'une justification technique. À titre indicatif, les ports standards associés
peuvent inclure DNS TCP/UDP 53, Kerberos TCP/UDP 88 et LDAP TCP 389, mais ils ne
doivent pas être ouverts automatiquement. Les ports dynamiques ou protocoles
supplémentaires restent interdits tant qu'un besoin précis n'est pas validé.

## Flux explicitement interdits

| Source | Destination | Règle |
|---|---|---|
| Internet | `API-INTERNAL` | Interdit, aucune publication ou redirection |
| Internet | `SRV-12` / `WEBPORTAL` | Interdit, aucune publication directe |
| Internet | SQL existant | Interdit |
| Internet | Active Directory | Interdit |
| Navigateur client | `API-INTERNAL` | Interdit |
| `WEBPORTAL` | Active Directory | Interdit |
| `WEBPORTAL` | SQL existant | Interdit |
| `WEBPORTAL` | NAS | Interdit |
| `WEBPORTAL` | RDS | Interdit |
| `WEBPORTAL` | VPN interne | Interdit |
| `WEBPORTAL` | Facturation interne | Interdit |
| Réseau public | SSH, RDP ou WinRM des VM | Interdit |
| Tout composant non autorisé | Interfaces d'administration internes | Interdit |

## Conditions d'activation des flux futurs

Un flux vers AD, NAS, RDS, VPN ou la facturation ne peut être activé qu'après :

1. définition du cas d'usage et du propriétaire ;
2. identification des adresses et ports minimaux ;
3. mise en place d'une identité dédiée au moindre privilège ;
4. validation de l'authentification, du chiffrement et des autorisations ;
5. définition des logs d'audit et alertes ;
6. test dans un environnement contrôlé ;
7. mise à jour de ce document et de la configuration pare-feu suivie.

## Vérification

Les règles effectives devront être comparées régulièrement à ce document. Toute
règle temporaire doit posséder un motif, un responsable et une date
d'expiration. Une règle devenue inutile doit être supprimée.
