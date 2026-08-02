# Portes nécessitant une décision humaine

## Principe

L'usine continue seule tant qu'une décision peut être prise à partir du code,
des contrats versionnés et de tests locaux déterministes. Elle s'arrête
uniquement dans les cas ci-dessous. Chaque arrêt doit indiquer le code de porte,
la phase, la décision attendue, les options connues, les preuves et l'impact de
l'inaction, sans jamais recopier un secret.

## Portes obligatoires

| Code | Déclencheur précis | Ce que l'usine peut faire avant l'arrêt | Décision attendue |
|---|---|---|---|
| HG-GIT-REMOTE | Tout push, merge, rebase, cherry-pick, tag, force-push ou réécriture d'historique. | Préparer les commits et montrer la commande, sans l'exécuter. | Autorisation explicite et cible exacte. |
| HG-DEPLOY | Tout déploiement, redémarrage, swap, changement IIS/nginx/systemd/service Windows ou publication distante. | Construire et valider localement les scripts et paquets reproductibles. | Environnement, fenêtre, sauvegarde et autorisation d'exécution. |
| HG-AD-REAL | Connexion, lecture sensible ou écriture sur un AD réel ; choix de DN, OU, ACL, groupe, compte de service ou portée KoXo non versionné. | Analyser le code mock et produire un plan sans connexion. | Valeurs et portée approuvées par le propriétaire AD. |
| HG-MARIADB-REAL | Connexion à une MariaDB réelle, backup/restore, migration ou seed sur une base réelle. | Compiler et tester avec mocks ou fixtures locales isolées. | Hôte/base, sauvegarde, fenêtre et autorisation DBA. |
| HG-KOXO | Accès à KoXo, création de jonction, dossiers, ACL, comptes ou échange de données réel. | Écrire des scripts sûrs avec `-WhatIf` et tests statiques. | Topologie, comptes, chemins et accord d'exploitation. |
| HG-NETWORK | Changement DNS, IP, route, pare-feu, proxy, TLS, NTP, reboot ou appel à un serveur/réseau réel. | Vérifier la syntaxe et simuler localement. | Cible, impact, rollback et fenêtre d'intervention. |
| HG-SECRET | Secret, jeton, mot de passe, chaîne de connexion ou URL authentifiée découvert comme exposé ou versionné. | Arrêter la diffusion, ne jamais afficher la valeur et relever seulement le chemin/type. | Rotation, révocation, purge éventuelle et responsable. |
| HG-PUBLIC-CONTRACT | Rupture d'un contrat public : route, payload, statut HTTP, cookie, URL publique, API partagée ou comportement client incompatible. | Documenter les consommateurs et proposer des options compatibles. | Versionnement, période de compatibilité et migration. |
| HG-PROD-DEPENDENCY | Ajout, retrait ou mise à niveau majeure d'une dépendance de production, d'un runtime, d'un service ou d'une VM. | Évaluer licences, vulnérabilités, taille et alternatives. | Choix de dépendance et stratégie de déploiement. |
| HG-DESTRUCTIVE-MIGRATION | Migration destructive ou non rétrocompatible : suppression/renommage de colonne, perte de données, réécriture massive, downgrade impossible. | Préparer une migration additive, un backup et un rollback alternatifs. | Acceptation de perte/indisponibilité et fenêtre DBA. |
| HG-LEGAL | Création, suppression ou modification de CGV, mentions légales, confidentialité, prix juridiquement engageant ou source juridique canonique. | Identifier les doublons et leurs consommateurs sans modifier le fond. | Texte/source approuvé par le responsable juridique. |
| HG-BUSINESS | Ambiguïté métier réelle avec plusieurs comportements plausibles affectant facturation, abonnement, droits, identité, données client ou engagement public. | Produire les options, exemples et impacts testables. | Règle métier choisie et cas limites. |

## Ce qui ne justifie pas une porte humaine

- test local en échec avec diagnostic exploitable ;
- défaut de revue classé `VALIDE` et corrigeable dans l'allowlist ;
- absence d'un sous-agent alors que le thread principal peut poursuivre ;
- cache ou artefact local régénérable ;
- choix de nom interne sans effet contractuel ;
- incident temporaire qui n'a pas atteint trois cycles sans progrès.

## Enregistrement

Une porte active est écrite dans `STATE.json.blocker` avec `type` égal à
`HUMAN_GATE`, puis résumée dans `BLOCKERS.md`. La reprise exige une décision
explicite, enregistrée dans `DECISIONS.md`, suivie de
`update-state.ps1 -Action ClearBlocker` puis `-Action Resume` si nécessaire.
