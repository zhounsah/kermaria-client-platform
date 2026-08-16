---
name: koxo-accents-majuscules
description: "Majuscules accentuées perdues par KoXo : DEUX causes qui se cumulent (CSV UTF-8 sans BOM relu en ANSI, puis table de translittération KoXo). Mesuré sur SRV-21 le 2026-08-04, sn = LAUMAILLA‰."
metadata: 
  node_type: memory
  type: project
  originSessionId: d79dbe2b-9ae3-4e1a-bc48-9b5d713aee39
  modified: 2026-08-05T21:28:00.590Z
---

Diagnostic réel du 2026-08-04 sur SRV-21 (`clients.home.bzh`), identité
`CLI-000002` créée par KoXo :

- CSV envoyé : `LAUMAILLÉ` → octets `4c 41 55 4d 41 49 4c 4c c3 89`, **sans BOM** ;
- `sn` dans l'annuaire : `LAUMAILLA‰` → octets `… 41 e2 80 b0`.

Ce n'est **pas** une simple corruption d'encodage. Deux mécanismes s'enchaînent :
1. KoXo relit le CSV en **ANSI** → `c3 89` devient `Ã‰` ;
2. sa **table de translittération** rabote ensuite le `Ã` en `A` nu ; le `‰`
   n'étant pas une lettre, il passe intact.

D'où la signature à trois valeurs : `LAUMAILLÃ‰` = encodage seul,
`LAUMAILLE` = translittération seule, **`LAUMAILLA‰` = les deux**. Corriger
l'encodage **d'abord** ; la translittération ne se mesure qu'après.

**Encodage corrigé sur SRV-21 le 2026-08-04** (variable Machine `utf8bom` +
module du dépôt déployé + receveur webhook redémarré). Resynchro : le journal
KoXo passe de `Roselyne LAUMAILLA‰` à `Roselyne LAUMAILLE`, `sn` = `LAUMAILLE`.
**La translittération des majuscules accentuées est donc réelle et mesurée**, et
`CLIENTS.xml` n'a aucune option pour la désactiver. Seul levier restant pour
garder `LAUMAILLÉ` : réécrire `sn`/`displayName` après synchro via
`employeeNumber` — non implémenté à ce jour.

Cause côté dépôt : `KOXO_CSV_ENCODING` valait `utf8` en variable **Machine** sur
SRV-21, et le module avait `utf8` en défaut (corrigé en `utf8bom` le 2026-08-04).
La valeur `utf8bom` « retenue le 2026-08-03 » dans `docs/koxo-sync.md` n'avait
**jamais été appliquée sur le serveur** — la doc décrivait une intention, pas un
état. Voir [[deployment-topology]].

**Sujet clos côté CSV (6 essais réels le 2026-08-04).** `utf8bom`, `latin1`
(accent natif ANSI sur un octet, aucune conversion UTF-8) et `unicode`
(UTF-16LE) donnent tous `sn=LAUMAILLE`. Et `Laumaillé` envoyé **en casse
normale** ressort lui aussi en `LAUMAILLE` : **KoXo force la majuscule sur le
champ `Nom`**, et c'est cette mise en capitales qui désaccentue. Donc : la
translittération est en aval du décodage, aucun encodage ni aucune casse de
saisie ne la contourne, et le contournement « saisir en casse normale » qui
figurait dans `docs/koxo-sync.md` était faux et jamais testé. Ne pas rouvrir ce
sujet par un changement d'encodage.

**CORRECTION du 2026-08-05 : la translittération EST configurable.** Elle ne
l'est simplement pas dans `CLIENTS.xml` (le profil de synchro) mais dans
`C:\Program Files\KoXo Dev\KoXoAdm\Config.xml` (la config globale), qui porte :

```xml
<ProcessCapitalsForNameAndFirstname>1</ProcessCapitalsForNameAndFirstname>
<NameAndFirstnameWithoutAccents>1</NameAndFirstnameWithoutAccents>
```

Ces deux drapeaux expliquent exactement le comportement mesuré (mise en
capitales du `Nom` + désaccentuation). Les passer à `0` est le levier jamais
essayé. La conclusion « aucun encodage ne contourne » reste vraie et le sujet
**encodage** reste clos ; c'est la conclusion « non configurable » qui était
fausse, faute d'avoir lu `Config.xml`. Leçon : chercher les options KoXo dans
`Config.xml` avant de conclure, pas seulement dans le profil de synchro.

Faits KoXo vérifiés au passage, non déductibles du code :
- `CLIENTS.xml` (profil de synchro) n'a aucune option d'accent ni de casse —
  mais `Config.xml` (global) en a deux, voir ci-dessus.
- Le `sAMAccountName` est dérivé du nom **à la création** : une resynchronisation
  ne le change pas (`CLI-000001` a longtemps porté `mariececil.gouzerhle` avec
  `sn=HOUNSA-HOUNKPA`). En revanche **supprimer le compte puis resynchroniser le
  régénère** : le 2026-08-04 les deux comptes ont été purgés puis recréés en
  `zachary.hounsahou` et `roselyne.laumaille`. C'est la voie de réparation d'un
  login né d'un nom corrompu.
- Une requête LDAP émise **depuis une session WinRM** ne remonte rien
  (double saut) : lancer le diagnostic en local sur le DC ou depuis RDC-07 avec
  `-SearchRoot LDAP://clients.home.bzh/DC=clients,DC=home,DC=bzh`.
- Une session WinRM sur SRV-21 **n'hérite d'aucune variable Machine** (env vide,
  pas seulement périmé) : hydrater depuis
  `[Environment]::GetEnvironmentVariables('Machine')` avant tout lancement, sinon
  la synchro échoue sur `KOXO_API_URL` manquant.
- **SRV-21 accumule des processus orphelins.** Un script bloquant lancé au
  travers de WinRM laisse un `wsmprovhost.exe` vivant qui garde son port (un
  receveur 8041 tournait ainsi depuis 2 jours, invisible dans la liste des
  tâches). Et un `KoXoAdm.exe` lancé à la main n'est jamais tué — celui du
  2026-08-04 22:02 a survécu 87 min. Y penser avant de conclure à un conflit de
  port ou de verrou.

**Why:** la note « accents majuscules supprimés par KoXo, non corrigeable côté
application » d'AGENTS.md faisait renoncer d'emblée, alors que la moitié du
problème était une variable d'environnement.

**How to apply:** devant une majuscule accentuée perdue, lire les **octets** de
`sn` avant de conclure, et lancer `scripts/koxo/Test-KoxoAccentHandling.ps1`
(hors session WinRM) qui rend le constat. Ne pas rejouer une synchro en espérant
réparer un login déjà créé.
