# V0.40 / V0.40.1 - Synchronisation KoXo privee, validee et non destructive

## Objet

V0.40 ajoute une chaine privee `webportal -> api-internal -> PowerShell -> CSV -> KoXo`
sans SMB cote site, sans secret reel dans le depot, sans execution KoXo cote site,
et sans creation automatique de la vraie tache planifiee.

V0.40.1 fige explicitement la regle mot de passe :

- aucun mot de passe n'est exporte vers KoXo, ni dans le JSON, ni dans le CSV ;
- `password_hash` en base SQL reste un hash local non reversible ;
- l'alignement du mot de passe avec l'infrastructure Windows continue a se faire
  au moment du `set-password` puis, pour les evolutions futures, via les flux
  dedies portail <-> AD, pas via KoXo ;
- KoXo reste limite a la synchronisation des identites et metadonnees
  utilisateurs.

## Architecture retenue

1. `apps/webportal/app/api/internal/koxo/users/route.ts`
   expose un endpoint BFF prive protege par bearer token, HTTPS et allowlist IP optionnelle.
2. `apps/api-internal/Program.cs` expose `GET /internal/koxo/users` et les routes admin
   `GET /internal/admin/koxo` + `POST /internal/admin/koxo/validate`.
3. `apps/api-internal/Services/KoxoExportService.cs` charge, trie, valide et audite le
   payload JSON KoXo sans reparation silencieuse.
4. `scripts/koxo/Sync-KoXoClients.ps1` consomme le JSON prive, applique les garde-fous,
   genere un CSV 13 colonnes, calcule le hash, valide la relecture, remplace la cible
   de facon sure, puis peut lancer `KoXoAdm.exe /Synchro=CLIENTS.xml`.
5. `scripts/koxo/Install-KoXoScheduledTask.ps1` documente et simule la tache planifiee ;
   aucune creation reelle n'est effectuee depuis le depot.

## Donnees exportees

Chaque utilisateur exporte contient exactement 7 champs JSON :

- `civilite`
- `nom`
- `prenom`
- `dateNaissance`
- `identifiantUnique`
- `groupeSecondaire`
- `email`

Le CSV genere 13 colonnes avec `;` comme separateur :

1. `civilite`
2. `nom`
3. `prenom`
4. `dateNaissance`
5. `identifiantUnique`
6. `groupeSecondaire`
7. `email`
8. vide
9. vide
10. vide
11. vide
12. vide
13. vide

La premiere ligne contient l'en-tete exact KoXo :

`Civilite;Nom;Prenom;DateNaissance;IdentifiantUnique;GroupeSecondaire;Email;Telephone;TelephoneMobile;Fax;PageWeb;ChampLibre;Fonction`

## Ce que KoXo fait de ces champs (verifie en reel le 2026-08-03)

### `groupeSecondaire` pilote l'OU — et la cree si besoin

KoXo place l'identite dans l'OU nommee d'apres ce champ, **et cree cette OU si
elle n'existe pas**. C'est le seul levier de placement annuaire : l'application
ne deplace aucune identite elle-meme.

| Cas | Valeur publiee |
|---|---|
| Essai de demonstration en cours | `CLI-DEMO` |
| Compte converti en client reel | le code `CLI-XXXXXX` reserve a la creation |
| Client reel ordinaire | sa reference client, qui nomme deja son OU |

### `identifiantUnique` revient dans `employeeNumber`

KoXo reporte l'identifiant du CSV (`CLI-NNNNNN`) dans l'attribut AD
**`employeeNumber`**. C'est la **seule cle de rattachement fiable** entre une
identite creee par KoXo et l'utilisateur portail : le nom subit une
translitteration et le `sAMAccountName` est derive par KoXo, donc aucun des deux
n'est predictible cote application. `DemoProvisioningService` s'en sert pour
ecrire le lien `customer_ad_links` manquant.

### Le CSV fait autorite, mais ne porte pas les permissions

Une synchronisation reconcilie l'annuaire sur le CSV. **Retirer une ligne est
donc une instruction** : KoXo desactive les comptes absents du fichier. En
revanche l'appartenance aux groupes `GG_*` n'est **pas** pilotee par le CSV,
elle reste du ressort de l'API — une synchronisation ne peut donc pas defaire
une revocation d'essai echu.

### Autres attributs renseignes

`sn`, `givenName`, `displayName`, `mail`, `userPrincipalName`,
`personalTitle` (`Mme` / `M.`), `pager` (date de naissance),
`physicalDeliveryOfficeName` / `division` / `department` (groupe secondaire),
`homeDirectory`, `homeDrive`, `scriptPath`, et l'appartenance au groupe portant
le nom du groupe secondaire.

### Code de sortie et fin de processus

`KoXoAdm.exe` renvoie **1 meme en cas de succes** (defaut connu, non corrige ;
en interactif il faut valider deux ou trois fois). **Ne pas se fier au code de
sortie** : le script s'appuie sur les marqueurs `LogSuccessful`,
`LogAcceptedMarker`, `LogCompletionMarker` et `LogBlockingError` du journal KoXo.

**Ne pas se fier davantage a la fin du processus.** `KoXoAdm.exe` peut terminer
son travail — journal complet, `Fin de l'operation` ecrite — puis **ne jamais
rendre la main**. Constate sur SRV-21 le 2026-08-04 a 21:32 : le journal portait
`Parametre accepte`, l'`Ajout/Modification` des deux utilisateurs et
`Fin de l'operation` a 21:32:27, mais le processus tournait toujours ; le script
l'a tue au bout de 90 s et a journalise `KoXo sync failed` alors que la
synchronisation avait reussi. Le receveur webhook remontait donc un echec pour
une synchronisation correcte.

Le depassement de `KOXO_SYNC_TIMEOUT_SECONDS` est donc traite **exactement comme
un code de sortie non nul** : le processus est tue, puis le journal KoXo recent
est consulte.

| Journal recent | Resultat |
|---|---|
| prouve le succes (marqueurs attendus, pas d'erreur bloquante) | statut `completed_after_timeout`, `TimedOut = $true`, journalisation en niveau `warning`, la synchronisation est un succes |
| ne prouve rien (absent, incomplet ou erreur bloquante) | erreur `KoXo process timed out after N seconds.` apres une journalisation de niveau `error` |

## Variables d'environnement

### Webportal / BFF

- `KOXO_EXPORT_API_TOKEN`
- `KOXO_EXPORT_ALLOWED_IPS` optionnelle, liste separee par `;`
- `KOXO_EXPORT_REQUIRE_HTTPS` par defaut `true` hors local

### Script PowerShell

- `KOXO_API_URL`
- `KOXO_API_TOKEN`
- `KOXO_ALLOW_INSECURE_HTTP` optionnelle, `false` par defaut, reservee a la recette technique hors HTTPS
- `KOXO_CSV_ENCODING` optionnelle, `utf8bom` par defaut ; toute autre valeur
  expose a une perte d'accents, voir la section « Encodage »
- `KOXO_MIN_USER_COUNT`
- `KOXO_MAX_USER_DROP_PERCENT`
- `KOXO_SYNC_TIMEOUT_SECONDS`
- `KOXO_LOG_DIRECTORY`
- `KOXO_KOXO_LOG_GLOB`
- `KOXO_BACKUP_RETENTION_COUNT`

Valeur validee en recette SRV-21 pour les journaux KoXo :

- `KOXO_KOXO_LOG_GLOB=C:\Program Files\KoXo Dev\KoXoAdm\Data\Logs\*.log`

## Utilisation locale / simulation

### Validation admin sans KoXo

1. Ouvrir `/admin/koxo`
2. verifier les compteurs et l'aperÃ§u JSON
3. lancer `Tester la validation`
4. corriger les erreurs listees tant que le statut reste `validation_failed`

### DryRun PowerShell

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\koxo\Sync-KoXoClients.ps1 `
  -CsvTargetPath C:\Temp\koxo\users.csv `
  -WorkingDirectory C:\Temp\koxo\work `
  -DryRun
```

Le mode `DryRun` :

- prend le verrou
- appelle l'API HTTPS ou consomme la charge injectee en test
- valide le JSON
- genere le CSV temporaire
- relit et reverifie le CSV
- journalise l'operation
- n'ecrase jamais le fichier cible
- n'actualise pas l'etat precedent

### Execution cible SRV-21

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\koxo\Sync-KoXoClients.ps1 `
  -CsvTargetPath "C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\clients.csv" `
  -WorkingDirectory "C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\work" `
  -LaunchKoxo `
  -KoxoExecutablePath "C:\Program Files\KoXo Dev\KoXoAdm\KoXoAdm.exe" `
  -KoxoWorkingDirectory "C:\Program Files\KoXo Dev\KoXoAdm" `
  -KoxoSyncArgument "/Synchro=CLIENTS.xml"
```

Quand `-LaunchKoxo` est active :

- le CSV est ecrit et relu avant tout lancement KoXo
- `KoXoAdm.exe` est lance localement sur SRV-21
- le script attend la fin du processus avec le timeout configure
- un code retour non nul reste tolere si le journal KoXo recent prouve une
  fin d'operation correcte avec les marqueurs attendus
- un depassement du timeout reste tolere aux memes conditions : le processus est
  tue, puis le journal tranche (`completed_after_timeout` en niveau `warning`)
- les journaux KoXo recents peuvent etre relus via `KOXO_KOXO_LOG_GLOB`

## Procedure cible SRV-21

1. definir les variables d'environnement KoXo sur SRV-21
2. verifier que `KOXO_API_URL` pointe vers le BFF HTTPS prive
3. tester le script en `-DryRun`
4. verifier les logs locaux et le hash produit
5. verifier la lecture des journaux KoXo via `KOXO_KOXO_LOG_GLOB`
6. executer ensuite sans `-DryRun` et avec `-LaunchKoxo`
7. seulement apres recette exploitable, utiliser `Install-KoXoScheduledTask.ps1`
   en dehors du depot, avec confirmation explicite

Pour une recette technique ponctuelle avant exposition HTTPS, `KOXO_ALLOW_INSECURE_HTTP=true`
peut etre active explicitement sur SRV-21. Cette option ne doit pas rester active
en cible durable.

Recette reelle confirmee le 2026-07-30 sur SRV-21 :

- appel prive BFF KoXo via bearer token ;
- generation locale du CSV `clients.csv` ;
- backup automatique du CSV precedent ;
- lancement de `KoXoAdm.exe /Synchro=CLIENTS.xml` ;
- code retour KoXo `1` tolere si le journal recent confirme :
  `Parametre accepte`, `Ajout/Modification`, `Fin de l'operation`.

## Remplacement sur disque et rollback

- ecriture dans un fichier temporaire
- validation immediate de relecture
- remplacement sur la cible avec backup
- retention bornee des backups
- rollback manuel = remettre en place le dernier `.bak`

## Encodage

Valeurs supportees par le module :

- `utf8`
- `utf8bom`
- `unicode`
- `ascii`
- `latin1`

**Valeur par defaut du module : `utf8bom`** (2026-08-04). Le defaut precedent
`utf8` sans marque d'ordre d'octets est relu en ANSI par KoXo : `LAUMAILLÉ`
arrive alors dans l'annuaire sous la forme `LAUMAILLÃ‰`. Le defaut est
desormais sur par lui-meme : `KOXO_CSV_ENCODING` absente de l'environnement
d'execution ne peut plus reintroduire la corruption.

Deux garde-fous accompagnent ce defaut :

- `Write-KoxoTextFile` relit le fichier avec le meme encodage et **echoue** si
  un caractere a ete perdu — `ascii` et `latin1` remplacent silencieusement par
  `?` ce qu'ils ne savent pas representer ;
- l'encodage effectivement utilise est journalise (`csv_encoding`) et renvoye
  dans le resultat de `Invoke-KoxoSync` (`CsvEncoding`).

### Diagnostiquer une majuscule accentuee perdue

Deux causes distinctes, qui **se cumulent** et se distinguent a la signature :

| Constat | `LAUMAILLÉ` devient | Cause | Correction |
|---|---|---|---|
| `corrompu_encodage` | `LAUMAILLÃ‰` | CSV relu en ANSI par KoXo | `KOXO_CSV_ENCODING=utf8bom` cote machine de synchronisation |
| `translittere` | `LAUMAILLE` | KoXo normalise le caractere | reglage KoXo, ou reprise de `sn` apres synchronisation |
| `corrompu_puis_translittere` | `LAUMAILLA‰` | les deux : ANSI donne `Ã‰`, puis KoXo retire l'accent du `Ã` | corriger l'encodage **d'abord**, le reste ne se mesure qu'ensuite |

Mesure sur SRV-21 le 2026-08-04, avant puis apres correction de l'encodage :

| | `KOXO_CSV_ENCODING` | journal KoXo | `sn` | constat |
|---|---|---|---|---|
| 21:08 | `utf8` | `Roselyne LAUMAILLA‰` | `4c … 41 e2 80 b0` | `corrompu_puis_translittere` |
| 21:32 | `utf8bom` | `Roselyne LAUMAILLE` | `LAUMAILLE` | `translittere` |

La valeur `utf8bom` documentee le 2026-08-03 n'avait jamais ete appliquee sur le
serveur : `KOXO_CSV_ENCODING` y valait toujours `utf8` en variable **Machine**,
qui prime sur le defaut du module.

**Conclusion, desormais mesuree et non plus supposee** : encodage corrige, KoXo
translittere les majuscules accentuees (`LAUMAILLÉ` → `sn=LAUMAILLE`). Aucune
option de `CLIENTS.xml` ne pilote ce comportement.

### Aucun reglage du CSV ne conserve un accent

Six essais reels le 2026-08-04, tous aboutissant a `sn=LAUMAILLE` :

| CSV envoye | encodage | octets de l'accent | `sn` obtenu |
|---|---|---|---|
| `LAUMAILLÉ` | `utf8bom` | `c3 89` | `LAUMAILLE` |
| `LAUMAILLÉ` | `latin1` | `c9` (ANSI natif) | `LAUMAILLE` |
| `LAUMAILLÉ` | `unicode` | `c9 00` (UTF-16LE) | `LAUMAILLE` |
| `Laumaillé` | `utf8bom` | `c3 a9` | `LAUMAILLE` |

Deux enseignements :

- **l'encodage est hors de cause** : `latin1` ne fait intervenir aucune
  conversion UTF-8, l'accent y est un caractere natif du jeu ANSI, et il est
  rabote quand meme. La translitteration est **en aval du decodage**, dans le
  traitement du nom par KoXo ;
- **KoXo force la majuscule sur le champ `Nom`** : envoye en casse normale,
  `Laumaillé` ressort en `LAUMAILLE`. C'est cette mise en capitales qui
  desaccentue. Le contournement « saisir les noms en casse normale », propose
  dans les versions precedentes de ce document, **ne fonctionne pas** — il n'a
  jamais ete teste.

Conserver une majuscule accentuee dans l'annuaire ne peut donc **pas** se jouer
sur le contenu ni sur l'encodage du CSV. Le seul levier restant est de reprendre
`sn` / `displayName` **apres** la synchronisation, l'identite etant retrouvable
par son `employeeNumber`.

Le `sAMAccountName` reste de toute facon translittere en ASCII par KoXo
(`roselyne.laumaille`), ce qui est le comportement voulu et ne prejuge pas de la
valeur de `sn`.

Pour trancher sur donnees reelles, sans rien ecrire ni dans le CSV ni dans
l'annuaire :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\koxo\Test-KoxoAccentHandling.ps1 `
  -CsvPath "C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\clients.csv"
```

Le script relit le CSV consomme par KoXo, retrouve chaque identite par son
`employeeNumber`, affiche les octets reellement ecrits et rend un constat par
ligne. Une identite creee avant une correction d'encodage conserve son
`sAMAccountName` d'origine : la comparer sans rejouer une synchronisation donne
un faux positif.

## Deploiement des scripts sur la machine de synchronisation

`scripts/koxo/Deploy-KoxoScripts.ps1` remplace la copie manuelle fichier par
fichier, qui avait laisse le 2026-08-04 un module vieux de cinq jours sur SRV-21
pendant que la documentation decrivait l'etat du depot.

```powershell
# Voir ce qui divergerait, sans rien ecrire
.\scripts\koxo\Deploy-KoxoScripts.ps1 -DryRun

# Deployer, poser les variables et relancer le receveur
.\scripts\koxo\Deploy-KoxoScripts.ps1 `
  -Settings @{ KOXO_CSV_ENCODING = 'utf8bom' } `
  -RestartReceiver
```

Ce que le script garantit :

- **liste explicite** de fichiers deployes. Le dossier cible heberge aussi
  `CLIENTS.xml` (configuration de KoXo), `koxo-webhook-token.txt` (le secret),
  `clients.csv`, `backups\`, `Logs\` et `work\` : une copie en bloc les
  detruirait. Ces noms sont **proteges**, le script refuse de demarrer si la
  liste a deployer en contient un ;
- **comparaison insensible aux fins de ligne**. `*.ps1` n'est pas couvert par
  `.gitattributes` : git rend du CRLF a la sortie alors que la cible peut porter
  du LF. Comparer les octets bruts signalerait une derive permanente sur des
  fichiers identiques ;
- **sauvegarde horodatee** dans `backups\deploy-<horodatage>\` avant tout
  ecrasement ;
- **verification apres copie** : empreinte exacte **et** analyse syntaxique du
  fichier arrive, pour attraper une copie tronquee ;
- **variables Machine `KOXO_*`** posees et verifiees, car elles priment sur les
  defauts du module — deployer le module sans corriger la variable ne change
  rien au comportement. Les valeurs dont le nom contient `TOKEN`, `SECRET` ou
  `PASSWORD` ne sont jamais affichees ;
- **redemarrage du receveur** via `-RestartReceiver`. Sans lui, un changement de
  variable reste sans effet : le processus garde son bloc d'environnement, et le
  script emet un avertissement explicite dans ce cas ;
- **validation finale** par une synchronisation `-DryRun` qui prouve que le
  deploiement est vivant, et rend l'encodage et la presence du BOM ;
- **inventaire de la derive** : les scripts presents sur la cible mais absents du
  depot sont listes. C'est ainsi qu'a ete repere
  `Start-KoxoSyncWebhookReceiver-8042.cmd`, depuis rapatrie.

### `Start-KoxoSyncWebhookReceiver-8042.cmd`

Lanceur manuel du receveur : il lit `koxo-webhook-token.txt` place a cote et
demarre `Start-KoxoSyncWebhookReceiver.ps1`. Le port se passe en premier
argument, `8042` par defaut. Les chemins viennent de `%~dp0`, il fonctionne donc
aussi bien depuis le depot que depuis le dossier cible.

> La tache planifiee `Kermaria-KoXoWebhookReceiver-8042` **n'appelle pas** ce
> fichier : elle invoque `powershell.exe` directement. Le lanceur sert aux
> demarrages manuels et de reference pour reconstruire la tache.

La version qui trainait sur SRV-21 etait **inoperante** : elle portait `` `$t ``
au lieu de `$t`, fuite d'echappement PowerShell de l'outil qui l'avait generee.
`` `$ `` etant un dollar litteral, la variable n'etait jamais creee et le jeton
transmis valait la chaine « $t ». La version du depot est corrigee et un test
Pester interdit la reapparition de cet echappement.

## Permissions minimales

- lecture HTTPS sur le BFF prive
- ecriture sur le dossier cible CSV
- ecriture sur le dossier de logs locaux
- lecture sur le glob de journaux KoXo si active
- eventuellement droit de creation de tache planifiee si l'installation est
  finalement confirmee hors depot

## Mise en production controlee

Avant une vraie activation :

- renseigner un vrai `KOXO_EXPORT_API_TOKEN` hors depot
- confirmer l'encodage attendu par KoXo
- confirmer le chemin reel du CSV cible
- confirmer le compte d'execution SRV-21
- confirmer l'intervalle de planification
- valider la retention des backups et des logs
- faire une premiere execution `DryRun`
- faire une premiere execution manuelle hors heures sensibles

