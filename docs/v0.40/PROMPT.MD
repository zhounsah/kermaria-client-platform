Tu es un ingénieur logiciel senior chargé d’intégrer une synchronisation automatique entre une application web existante et KoXo Administrator.

Avant toute modification, analyse entièrement le dépôt :

- langage et framework ;
- architecture frontend/backend ;
- modèles de données ;
- base de données ;
- formulaire d’inscription ;
- système de génération des numéros utilisateurs ;
- système de génération des codes de référence client ;
- authentification et rôles ;
- variables d’environnement ;
- tâches asynchrones éventuelles ;
- journalisation ;
- tests ;
- conventions du projet ;
- infrastructure de déploiement.

Ne suppose pas la stack technique. Adapte l’implémentation à l’existant.

# Contexte

KoXo Administrator est installé sur :

KERMARIA-SRV-21.clients.home.bzh

Le fichier CSV lu par KoXo se trouve localement sur ce serveur :

C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\clients.csv

Le fichier de réponses KoXo existe déjà :

CLIENTS.xml

La commande fonctionnelle est :

KoXoAdm.exe /Synchro=CLIENTS.xml

La synchronisation KoXo a déjà été testée manuellement avec succès.

# Architecture imposée

Le site public ne doit pas :

- accéder au partage administratif C$ ;
- utiliser SMB pour écrire sur SRV-21 ;
- posséder un compte administrateur du domaine ;
- exécuter directement KoXo Administrator ;
- connaître les identifiants Windows du serveur ;
- disposer d’un accès entrant au réseau Active Directory.

L’architecture retenue est la suivante :

1. le site stocke les informations clients dans sa base de données ;
2. le site expose un endpoint API HTTPS privé qui retourne les utilisateurs KoXo sous forme de JSON ;
3. un script PowerShell local sur KERMARIA-SRV-21 appelle cet endpoint ;
4. le script valide intégralement les données ;
5. le script génère localement le fichier CSV compatible KoXo ;
6. le script remplace le fichier clients.csv de manière sûre ;
7. le script lance KoXoAdm.exe /Synchro=CLIENTS.xml ;
8. une tâche planifiée Windows exécute régulièrement ce script.

Flux attendu :

Base de données du site
        ↓
API JSON HTTPS privée
        ↓
Script PowerShell sur KERMARIA-SRV-21
        ↓
Validation des données
        ↓
Génération locale de clients.csv
        ↓
Remplacement atomique
        ↓
KoXoAdm.exe /Synchro=CLIENTS.xml
        ↓
Active Directory CLIENTS.HOME.BZH

# Partie 1 — Cohérence du formulaire d’inscription

Vérifier que le formulaire d’inscription, les validations backend et les modèles de base de données contiennent les champs nécessaires.

Correspondance attendue :

- Civilité :
  champ civilité du formulaire ;

- Nom :
  champ nom ;

- Prénom :
  champ prénom ;

- DateNaissance :
  champ date de naissance ;

- IdentifiantUnique :
  numéro utilisateur généré par le site ;

- GroupeSecondaire :
  code de référence généré par le site ;

- Email :
  adresse e-mail du client ;

- Fonction :
  non utilisée.

Les champs nécessaires doivent être persistés dans la base de données.

Ne jamais recalculer IdentifiantUnique à partir du nom, du prénom ou de l’e-mail.

IdentifiantUnique doit être :

- unique ;
- stable ;
- immuable ;
- non réutilisable ;
- conservé pendant toute la durée de vie du compte.

Une modification du nom, du prénom, de l’e-mail ou du groupe ne doit pas changer IdentifiantUnique.

GroupeSecondaire doit correspondre exactement au code attendu dans KoXo, par exemple :

CLI-TEST

Vérifier comment ce code de référence est actuellement généré.

Ne pas inventer une nouvelle logique si une logique fiable existe déjà.

# Partie 2 — Endpoint API JSON privé

Créer un endpoint backend privé, par exemple :

GET /api/internal/koxo/users

Cet endpoint doit retourner uniquement les utilisateurs qui doivent exister dans KoXo.

Ne pas générer le CSV côté site.

Le site doit retourner des données JSON structurées.

Exemple de réponse :

{
  "schemaVersion": 1,
  "generatedAt": "2026-07-30T21:30:00+02:00",
  "userCount": 1,
  "users": [
    {
      "civilite": "Mme",
      "nom": "GOUZERH LE BOURBON",
      "prenom": "Marie-Cécile",
      "dateNaissance": "2000-01-01",
      "identifiantUnique": "CLI-000001",
      "groupeSecondaire": "CLI-TEST",
      "email": "marie-cecile.gouzerhlebourbon@clients.home.bzh"
    }
  ]
}

Utiliser un format de date ISO strict :

YYYY-MM-DD

L’endpoint doit :

- récupérer les utilisateurs depuis la base ;
- trier les utilisateurs de manière déterministe ;
- contrôler les doublons ;
- retourner un compteur exact ;
- retourner une version de schéma ;
- retourner une date de génération ;
- refuser les données incohérentes ;
- ne jamais retourner de mot de passe ;
- ne jamais exposer d’informations inutiles.

# Partie 3 — Validation métier côté site

Avant de retourner les données, valider :

- civilité présente et autorisée ;
- nom non vide ;
- prénom non vide ;
- date de naissance valide ;
- IdentifiantUnique non vide ;
- IdentifiantUnique unique ;
- GroupeSecondaire non vide ;
- e-mail valide ;
- absence de doublon d’utilisateur ;
- absence de valeurs manifestement corrompues.

Ne pas inventer ou remplacer silencieusement une valeur manquante.

En cas d’utilisateur invalide, choisir une politique explicite et documentée :

- soit refuser entièrement l’export avec une erreur ;
- soit exclure l’utilisateur et retourner une section errors.

Privilégier le refus global si les données produites peuvent entraîner une synchronisation KoXo incorrecte.

Exemple d’erreur structurée :

{
  "error": "KOXO_EXPORT_VALIDATION_FAILED",
  "message": "Un ou plusieurs utilisateurs sont invalides.",
  "invalidUsers": [
    {
      "identifiantUnique": "CLI-000005",
      "fields": ["email"]
    }
  ]
}

Ne pas retourner inutilement le nom complet ou la date de naissance dans les messages d’erreur.

# Partie 4 — Sécurisation de l’API

L’endpoint ne doit pas être accessible publiquement sans authentification.

Implémenter une authentification machine-à-machine adaptée au projet existant.

Solution minimale acceptable :

Authorization: Bearer <token-long-et-aléatoire>

Le token doit être stocké dans une variable d’environnement côté site, par exemple :

KOXO_EXPORT_API_TOKEN

Ne jamais placer le token :

- dans Git ;
- dans le frontend ;
- dans le code source ;
- dans les logs ;
- dans la réponse JSON ;
- dans les fichiers de test réels.

Comparer les secrets de manière sûre si la stack le permet.

Ajouter si possible :

- restriction d’adresse IP ;
- limitation du débit ;
- journalisation des accès ;
- rotation du token ;
- refus des requêtes non HTTPS ;
- en-têtes de sécurité ;
- délai maximal de traitement.

Documenter les protections réellement mises en œuvre.

# Partie 5 — Script PowerShell local sur SRV-21

Créer dans le dépôt un script PowerShell destiné à être copié sur KERMARIA-SRV-21, par exemple :

scripts/koxo/Sync-KoXoClients.ps1

Le script doit être compatible avec Windows PowerShell 5.1, sauf si l’environnement du serveur impose PowerShell 7.

Chemins cibles :

$KoXoExe =
C:\Program Files\KoXo Dev\KoXoAdm\KoXoAdm.exe

$KoXoDirectory =
C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro

$FinalCsv =
C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\clients.csv

$ResponseFile =
CLIENTS.xml

Le script doit suivre exactement ce processus :

1. acquérir un verrou afin d’empêcher deux exécutions simultanées ;
2. lire la configuration locale ;
3. récupérer le token API depuis une variable d’environnement Windows ;
4. appeler l’endpoint HTTPS ;
5. vérifier le code HTTP ;
6. vérifier le Content-Type ;
7. parser le JSON ;
8. vérifier schemaVersion ;
9. vérifier userCount ;
10. valider chaque utilisateur ;
11. refuser un export vide non autorisé ;
12. construire le CSV dans un fichier temporaire local ;
13. valider le CSV généré ;
14. sauvegarder l’ancien clients.csv ;
15. remplacer le fichier final de manière atomique ou aussi sûre que possible ;
16. lancer KoXoAdm.exe /Synchro=CLIENTS.xml ;
17. attendre la fin de KoXo ;
18. analyser le code de sortie et les journaux ;
19. journaliser le résultat ;
20. supprimer les fichiers temporaires ;
21. libérer le verrou.

# Partie 6 — Format exact du CSV KoXo

Le CSV doit utiliser le point-virgule comme séparateur :

;

La première ligne doit contenir les en-têtes.

Les colonnes doivent exister dans cet ordre exact :

1. Civilite
2. Nom
3. Prenom
4. DateNaissance
5. IdentifiantUnique
6. GroupeSecondaire
7. Email
8. Telephone
9. TelephoneMobile
10. Fax
11. PageWeb
12. ChampLibre
13. Fonction

En-tête exact :

Civilite;Nom;Prenom;DateNaissance;IdentifiantUnique;GroupeSecondaire;Email;Telephone;TelephoneMobile;Fax;PageWeb;ChampLibre;Fonction

Les colonnes 8 à 13 doivent être présentes mais vides.

Exemple de ligne :

Mme;GOUZERH LE BOURBON;Marie-Cécile;2000-01-01;CLI-000001;CLI-TEST;marie-cecile.gouzerhlebourbon@clients.home.bzh;;;;;;

La colonne Fonction reste vide.

Vérifier précisément que chaque ligne contient 13 colonnes.

Utiliser une vraie logique d’échappement CSV.

Ne pas créer les lignes avec une concaténation naïve de chaînes.

Gérer correctement les valeurs contenant :

- point-virgule ;
- guillemet ;
- virgule ;
- retour à la ligne ;
- apostrophe ;
- caractères accentués ;
- espaces ;
- noms composés ;
- prénoms composés.

# Partie 7 — Encodage

L’encodage doit être configurable.

Prévoir par exemple une variable locale :

KOXO_CSV_ENCODING=windows-1252

ou :

KOXO_CSV_ENCODING=utf-8

L’installation KoXo actuelle doit être testée avec :

- Marie-Cécile ;
- Léa ;
- GOUZERH LE BOURBON ;
- MEDRANO-LEBORGNE ;
- Jean-Bernard ;
- HOUNSA-HOUNKPA.

Ne pas décider arbitrairement que UTF-8 est compatible sans test.

Documenter l’encodage retenu.

# Partie 8 — Garde-fous

Le script ne doit jamais remplacer le CSV existant par un fichier vide ou manifestement incomplet.

Ajouter des garde-fous configurables :

- nombre minimum d’utilisateurs ;
- comparaison avec le nombre précédent ;
- diminution maximale autorisée ;
- refus si userCount ne correspond pas au tableau users ;
- refus si IdentifiantUnique est dupliqué ;
- refus si GroupeSecondaire est vide ;
- refus si l’e-mail est invalide ;
- refus si le nombre de colonnes est incorrect ;
- refus si le JSON est incomplet ;
- refus si schemaVersion est inconnue ;
- refus si la réponse semble être une page HTML ou une erreur ;
- délai maximal de téléchargement ;
- taille maximale de réponse.

Exemple :

KOXO_MIN_USER_COUNT=1
KOXO_MAX_USER_DROP_PERCENT=20

Si l’ancien export contient 100 utilisateurs et que le nouveau n’en contient que 10, le script doit refuser le remplacement sauf option explicite de forçage.

Ne pas activer un forçage automatique.

# Partie 9 — Remplacement sûr du fichier

Ne jamais écrire directement dans clients.csv pendant la génération.

Utiliser un fichier temporaire dans le même dossier, par exemple :

clients.csv.new

Processus :

1. générer clients.csv.new ;
2. vider et fermer le flux ;
3. contrôler sa taille ;
4. relire et valider le fichier ;
5. calculer son hash SHA-256 ;
6. sauvegarder clients.csv ;
7. remplacer clients.csv ;
8. supprimer le fichier temporaire après succès.

Conserver des sauvegardes datées, par exemple :

clients-20260730-213000.csv.bak

Conserver les 10 dernières sauvegardes.

Ne supprimer les anciennes sauvegardes qu’après un export réussi.

En cas d’échec avant le remplacement, clients.csv doit rester intact.

# Partie 10 — Lancement de KoXo

Après remplacement du CSV, lancer :

C:\Program Files\KoXo Dev\KoXoAdm\KoXoAdm.exe /Synchro=CLIENTS.xml

Utiliser le répertoire de travail :

C:\Program Files\KoXo Dev\KoXoAdm

Attendre la fin du processus.

KoXo produit des journaux contenant notamment :

- Paramètre accepté : /Synchro=CLIENTS.xml
- Ajout/Modification de l’utilisateur
- Fin de l’opération

Le script doit essayer de vérifier dans les journaux qu’une ligne de fin d’opération a bien été écrite.

Ne pas considérer uniquement le code de sortie du processus comme preuve absolue de réussite si KoXo présente un comportement particulier à la fermeture.

Définir clairement les critères de succès :

- lancement du processus ;
- absence d’expiration ;
- journal KoXo récent ;
- présence de « Paramètre accepté » ;
- présence de « Fin de l’opération » ;
- absence d’erreur bloquante dans le journal.

Prévoir un timeout configurable.

# Partie 11 — Journalisation locale

Créer un dossier local, par exemple :

C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\Logs

Journaliser :

- début du processus ;
- fin du processus ;
- URL appelée sans afficher le token ;
- statut HTTP ;
- version de schéma ;
- date de génération du site ;
- nombre d’utilisateurs reçus ;
- nombre d’utilisateurs validés ;
- nombre d’utilisateurs rejetés ;
- taille du JSON ;
- taille du CSV ;
- hash SHA-256 du CSV ;
- chemin de sauvegarde ;
- chemin final ;
- lancement de KoXo ;
- PID KoXo ;
- code de sortie ;
- résultat de lecture du journal KoXo ;
- durée totale ;
- erreur complète utile ;
- succès ou échec.

Ne jamais journaliser :

- token API ;
- mot de passe ;
- date de naissance complète de chaque utilisateur ;
- informations personnelles inutilement détaillées.

# Partie 12 — Configuration locale du script

Ne pas coder en dur les secrets.

Prévoir des variables d’environnement Windows, par exemple :

KOXO_API_URL
KOXO_API_TOKEN
KOXO_CSV_ENCODING
KOXO_MIN_USER_COUNT
KOXO_MAX_USER_DROP_PERCENT
KOXO_SYNC_TIMEOUT_SECONDS

Exemple :

KOXO_API_URL=https://example.com/api/internal/koxo/users
KOXO_CSV_ENCODING=windows-1252
KOXO_MIN_USER_COUNT=1
KOXO_MAX_USER_DROP_PERCENT=20
KOXO_SYNC_TIMEOUT_SECONDS=300

Le token doit être enregistré localement sur SRV-21 avec une procédure documentée.

Ne pas afficher sa valeur dans la documentation.

# Partie 13 — Tâche planifiée Windows

Fournir un second script PowerShell de déploiement, par exemple :

scripts/koxo/Install-KoXoScheduledTask.ps1

Ce script doit créer une tâche planifiée :

Nom :

KoXo - Synchronisation CLIENTS

Action :

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Program Files\KoXo Dev\KoXoAdm\Data\CSVSynchro\Sync-KoXoClients.ps1"

Paramètres recommandés :

- exécuter même si l’utilisateur n’est pas connecté ;
- exécuter avec les autorisations maximales ;
- ne pas lancer une seconde instance si la précédente fonctionne encore ;
- interrompre la tâche après un délai raisonnable ;
- relancer après échec ;
- enregistrer l’historique ;
- utiliser un compte de service dédié.

Ne pas utiliser le compte personnel HOME\zhounsah en production.

Prévoir l’utilisation d’un compte de service, par exemple :

CLIENTS\svc-koxo-sync

ou d’un gMSA si l’infrastructure le permet.

Ne pas créer automatiquement un compte Active Directory depuis le dépôt.

Documenter seulement les permissions requises :

- lecture de la configuration locale ;
- accès HTTPS sortant vers le site ;
- écriture dans le dossier CSVSynchro ;
- exécution de KoXoAdm.exe ;
- droits Active Directory et fichiers nécessaires à KoXo ;
- écriture dans le dossier de journaux.

La fréquence doit être configurable.

Pour les tests, prévoir un lancement manuel.

Pour la production, proposer par exemple une exécution toutes les cinq minutes, sans l’activer automatiquement si le projet ne gère pas l’infrastructure.

# Partie 14 — Interface d’administration du site

Si le site possède déjà une interface d’administration, ajouter une section KoXo affichant :

- nombre d’utilisateurs exportables ;
- nombre d’utilisateurs invalides ;
- date du dernier appel API ;
- statut du dernier export demandé ;
- version du schéma ;
- bouton de test de validation des données ;
- aperçu JSON limité et sécurisé ;
- liste des erreurs de validation sans données sensibles excessives.

Ne pas ajouter de bouton permettant au site d’exécuter directement KoXo sur SRV-21.

Le site ne contrôle que les données exposées.

# Partie 15 — Tests backend

Ajouter des tests couvrant au minimum :

1. utilisateur valide ;
2. plusieurs utilisateurs ;
3. tri déterministe ;
4. civilité valide ;
5. civilité invalide ;
6. nom manquant ;
7. prénom manquant ;
8. date invalide ;
9. e-mail invalide ;
10. IdentifiantUnique manquant ;
11. IdentifiantUnique dupliqué ;
12. GroupeSecondaire manquant ;
13. prénom composé ;
14. nom composé ;
15. accents ;
16. apostrophe ;
17. absence de mot de passe dans la réponse ;
18. token absent ;
19. token invalide ;
20. token valide ;
21. structure JSON conforme ;
22. userCount cohérent ;
23. schemaVersion correcte ;
24. stabilité de l’IdentifiantUnique après modification du profil.

# Partie 16 — Tests PowerShell

Ajouter des tests automatisés si Pester est disponible.

Couvrir au minimum :

1. réponse API valide ;
2. code HTTP invalide ;
3. page HTML reçue à la place du JSON ;
4. JSON invalide ;
5. schemaVersion inconnue ;
6. userCount incohérent ;
7. utilisateur invalide ;
8. doublon d’IdentifiantUnique ;
9. export vide ;
10. baisse anormale du nombre d’utilisateurs ;
11. génération CSV correcte ;
12. 13 colonnes exactes ;
13. encodage ;
14. accents ;
15. point-virgule dans un champ ;
16. fichier temporaire ;
17. remplacement réussi ;
18. remplacement échoué ;
19. sauvegarde créée ;
20. conservation des 10 dernières sauvegardes ;
21. verrou concurrent ;
22. timeout KoXo ;
23. journal KoXo trouvé ;
24. journal KoXo absent ;
25. token non écrit dans les logs.

Les tests ne doivent jamais :

- appeler le véritable site de production ;
- modifier le véritable clients.csv ;
- lancer le véritable KoXoAdm.exe ;
- toucher à Active Directory ;
- utiliser de vrais secrets.

Utiliser des mocks et des répertoires temporaires.

# Partie 17 — Documentation

Créer une documentation claire, par exemple :

docs/koxo-sync.md

Elle doit expliquer :

- l’architecture ;
- le flux complet ;
- les choix de sécurité ;
- les champs du formulaire ;
- les champs JSON ;
- les colonnes CSV ;
- les variables d’environnement ;
- l’encodage ;
- l’installation du script sur SRV-21 ;
- l’installation de la tâche planifiée ;
- la procédure de test manuel ;
- la procédure de retour arrière ;
- les journaux ;
- les erreurs fréquentes ;
- la rotation du token ;
- les permissions Windows ;
- les garde-fous ;
- la procédure de mise en production.

Inclure les commandes PowerShell nécessaires, sans aucun vrai secret.

# Partie 18 — Processus de mise en production

Documenter ce processus exact :

1. déployer l’endpoint API ;
2. tester l’endpoint avec un compte de test ;
3. vérifier le JSON ;
4. configurer le token sur le site ;
5. configurer le même token sur SRV-21 ;
6. installer le script PowerShell ;
7. lancer le script en mode simulation ;
8. générer un CSV temporaire sans remplacer le vrai fichier ;
9. comparer le CSV avec le format KoXo attendu ;
10. lancer un test avec un seul compte ;
11. vérifier les journaux KoXo ;
12. vérifier l’utilisateur dans Active Directory ;
13. relancer sans modification pour vérifier l’idempotence ;
14. tester une modification ;
15. tester un changement de groupe ;
16. tester un utilisateur absent ;
17. vérifier les garde-fous ;
18. créer la tâche planifiée ;
19. lancer manuellement la tâche ;
20. surveiller plusieurs exécutions ;
21. activer la récurrence de production.

Prévoir un mode simulation dans le script :

-DryRun

En mode DryRun :

- appeler l’API ;
- valider les données ;
- générer le CSV temporaire ;
- afficher les contrôles ;
- ne pas remplacer clients.csv ;
- ne pas lancer KoXo.

# Partie 19 — Livrables

À la fin de l’intervention :

1. implémenter le code ;
2. lister les fichiers créés ;
3. lister les fichiers modifiés ;
4. expliquer l’architecture retenue ;
5. fournir l’endpoint final ;
6. fournir un exemple de réponse JSON ;
7. fournir le script PowerShell ;
8. fournir le script d’installation de la tâche planifiée ;
9. fournir la documentation ;
10. fournir les variables d’environnement nécessaires ;
11. fournir les commandes de test ;
12. exécuter les tests ;
13. fournir le résultat des tests ;
14. signaler les points non vérifiables ;
15. ne pas déployer en production ;
16. ne pas écrire sur le véritable serveur KoXo ;
17. ne pas créer la véritable tâche planifiée ;
18. ne pas utiliser de vrais secrets ;
19. ne pas modifier les données réelles.

# Critères d’acceptation

La tâche est réussie uniquement si :

- les champs du formulaire sont cohérents avec KoXo ;
- les données sont correctement persistées ;
- IdentifiantUnique est stable et unique ;
- GroupeSecondaire est correctement généré ;
- l’API est privée et authentifiée ;
- le JSON respecte le schéma ;
- le site ne possède aucun accès SMB vers SRV-21 ;
- le CSV est généré uniquement sur SRV-21 ;
- le CSV contient exactement 13 colonnes ;
- les accents sont correctement encodés ;
- le fichier existant reste intact en cas d’échec ;
- le remplacement utilise un fichier temporaire ;
- les exportations vides ou anormales sont bloquées ;
- les exécutions concurrentes sont bloquées ;
- KoXo est lancé uniquement après validation ;
- les secrets ne figurent pas dans le dépôt ;
- les tests passent ;
- la procédure de déploiement est documentée ;
- aucun déploiement réel n’est effectué.