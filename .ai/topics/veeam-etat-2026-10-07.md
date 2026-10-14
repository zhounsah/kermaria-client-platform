---
description: "État Veeam observé en lecture seule le 2026-10-07 ; revalider avant toute opération."
---

# Veeam — constat du 7 octobre 2026

Source de détail : [docs/VEEAM_BACKUP_STATUS.md](../../docs/VEEAM_BACKUP_STATUS.md).
Relevé WinRM depuis RDC-07 sur SRV-16 et SRV-01, sans mutation.

- SRV-16 est une VM en cours d'exécution sur SRV-01. Le service Veeam est actif,
  et le module `Veeam.Backup.PowerShell` installé porte la version `13.0.2.29`.
- Le `Backup Configuration Job` est maintenant activé et chiffré, avec 30
  **points** conservés et un dernier résultat `Success`. Le dépôt configuré
  pointe vers un partage CIFS de FS-01. La session WinRM ne pouvait pas le
  lister ; la présence d'un `.bco` exploitable et sa restauration sont inconnues.
  L'ancien constat de job désactivé (septembre 2026) est historique.
- La tâche `Kermaria Veeam Backup Collector` existe ; son lancement du 7
  octobre à 19 h 30 s'est terminé avec le code 0. Le relais Windows PowerShell
  lance `pwsh` et le collecteur. Cela ne démontre ni un envoi accepté par l'API
  ni un affichage client ; le script peut sortir à 0 avec zéro rapport.
- `Get-VBRJob` a renvoyé 23 jobs, dont 18 planifiés. Parmi eux, 18 avaient une
  dernière session réussie, un une session échouée et quatre aucune session
  associée. Le job échoué était `KoXoDATA - OVH 180d`, non planifié, le 3
  octobre. Cette cmdlet avertit qu'elle ne couvre plus les jobs de sauvegarde
  d'ordinateurs ni de copie : ce décompte n'est pas exhaustif.
- Le propriétaire confirme que `F:\KoXoDATA` contient tous les dossiers
  concernés. Le job NAS planifié `KoXoDATA` a cette racine comme objet, sans
  masque d'inclusion ni d'exclusion déclaré. Sa dernière session et son unique
  tâche ont réussi le 7 octobre à 02 h 33. Il s'agit d'un statut commun au job,
  sans preuve de récupérabilité fichier par fichier. Le mapping applicatif
  `(provider, external_job_id)` est unique et ne peut viser qu'un seul
  client/service : il ne représente pas ce job partagé pour tous les clients.
- Ne pas déduire de ces 30 points une rétention client de 30 jours. Les valeurs
  du brief commercial restent proposées, sans validation technique par service.
  Un succès Veeam ne vaut pas test de restauration.

À revalider avant toute décision : fichier `.bco`, copie hors domaine de panne,
restauration isolée, échec KoXoDATA, types de jobs manquants, rapports acceptés,
mappings admin et vue `/backups` d'un client autorisé.
