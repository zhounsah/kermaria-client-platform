# Assistance & maintenance — contrat Billing V2

## Modèle du catalogue

La gamme est **un service** Billing V2 `ASSISTANCE-MAINTENANCE` (`pricing_model=tiered`, portée `subscription`) avec six paliers publics. Les codes `ASSIST-*` identifient désormais les **paliers**, pas des services distincts. Chaque palier possède son prix mensuel versionné et son profil `manual_delivery`. Les trois paliers particuliers et les trois paliers professionnels restent comparables sur leurs pages dédiées ; le code du palier suit la demande de devis jusqu'au formulaire de contact.

| Palier | Public | Prix TTC mensuel | Prix HT indicatif France |
| --- | --- | ---: | ---: |
| `ASSIST-PERS-ESSENTIEL` | Particuliers | 9,90 € | 8,25 € |
| `ASSIST-PERS-SERENITE` | Particuliers | 19,90 € | 16,58 € |
| `ASSIST-PERS-PREMIUM` | Particuliers | 39,90 € | 33,25 € |
| `ASSIST-PRO-START` | Professionnels, par poste | 22,80 € | 19,00 € |
| `ASSIST-PRO-BUSINESS` | Professionnels, par poste | 46,80 € | 39,00 € |
| `ASSIST-PRO-PREMIUM` | Professionnels, par poste | 82,80 € | 69,00 € |

Les montants HT sont indicatifs au taux normal français de 20 %. Billing V2 et le devis final restent l'autorité pour la TVA et la facturation. Le portail ne calcule ni n'envoie un montant à l'API. Les prestations incluses sont présentées dans les pages de formules ; les minutes non utilisées ne sont pas reportées. La section expliquant l'intérêt du suivi mensuel se place immédiatement après le héros du hub, avant le choix du public.

## Commande, quantité et provisioning

Le service est public en **mode devis** (`public_ordering_mode=quote`, `self_service_orderable=0`). Aucun achat direct, paiement récurrent ou souscription fournisseur n'a été déclenché par cette livraison. Le nombre de postes professionnels est précisé au devis. La limite de quantité du panier direct est aujourd'hui portée par le **service**, pas par le palier : le nouveau service garde une borne sûre de 1. Avant une éventuelle ouverture du panier direct pour plusieurs postes professionnels, ajouter et faire respecter des bornes par palier dans l'API, puis vérifier le calcul des lignes et la facturation. Le contrôle d'interface seul ne suffirait pas.

Aucune règle de provisioning AD ou KoXo ne cible ce service ou ses paliers. Les postes clients externes ne sont pas enrôlés dans l'AD HOME.BZH. Le mécanisme distinct d'identité du compte portail reste inchangé.

## Migrations et correction DEV

`103_assistance_catalog.sql` décrit désormais le modèle correct pour une installation neuve. `104_assistance_tiers_correction.sql` est nécessaire sur DEV parce que l'ancienne 103 y avait déjà été appliquée : le runner enregistre le **nom** de la migration et ne rejoue pas un fichier modifié. Après contrôle de zéro référence contractuelle aux six anciens services, 104 crée le service et ses paliers, puis retire uniquement les six services, leurs prix et profils erronés. Les clés étrangères font échouer cette suppression si des références historiques existent sur une autre base ; il faut alors une migration qui les conserve.

La sauvegarde pré-103 est `C:\Users\zhounsah\Backups\Kermaria\kermaria_dev_pre103_assistance_tables_retry.sql` (SHA-256 `A4DD502DA369433256DFA0C36810C4F7E16A4B14347CE956C5C7A6580A2C3845`). Avant 104, une nouvelle sauvegarde de l'état DEV a été vérifiée : `C:\Users\zhounsah\Backups\Kermaria\kermaria_dev_pre104_assistance_20261030_2155.sql` (1 632 140 octets, 112 tables, SHA-256 `DF17D2322E415FBF8E3960DD30394C179DDEB80D4337F19334ED1948CD36BBFD`). Ces deux dumps excluent une vue et deux triggers faute de privilèges ; une restauration intégrale n'a pas été éprouvée. Ne pas restaurer globalement un dump pour corriger cette gamme : conserver les autres données DEV et les définitions versionnées de la vue/triggers.

`105_assistance_tier_labels.sql` corrige uniquement deux descriptions publiques, avec une garde respectant toute modification ultérieure par l’administration. Après 104 et 105 : un service actif, six paliers actifs et publics, six prix mensuels, six profils de livraison manuelle, zéro ancien service `ASSIST-*`, zéro règle de provisioning AD. Les trois prix TTC particuliers et les trois prix HT indicatifs professionnels ont été relus en base et dans les pages DEV. Le catalogue `/tarifs` présente un seul article avec ses déclinaisons. L'API DEV et PROD, leurs configurations et les parcours de paiement n'ont pas été redéployés par cette correction ; seul le fichier de migration 104 a été copié dans la release API DEV et exécuté via le runner explicite.

Pour toute future activation commerciale, vérifier les contrats, les outils d'exploitation, le suivi des minutes et remises, les garde-fous checkout et un parcours complet de test quote → checkout → règlement → abonnement → facture. La recette actuelle couvre le catalogue et le devis ; elle ne constitue pas une preuve de paiement ni de renouvellement.
