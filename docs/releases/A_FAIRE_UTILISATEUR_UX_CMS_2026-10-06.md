# À faire par le propriétaire du site — prochaine revue

Le code, les tests techniques, les sauvegardes et les déploiements sont pris en
charge par l'équipe de réalisation. Cette liste ne contient que les décisions
et accès qui appartiennent au propriétaire du site.

1. **Relire la vitrine DEV** sur `https://dev.zachary-it.fr/` : accueil,
   services, offres, configuration d'une offre, panier, tarifs, diagnostic,
   inscription, contact, pied de page et page « Demander mes données ».
   Signaler les formulations inexactes, les
   prestations réellement proposées, les coordonnées et les liens à corriger.
   Dans **Administration → Mise en page**, juger aussi si l'aperçu du brouillon
   permet de préparer ces pages sans aide technique. Les montants affichés
   viennent du catalogue : ne pas les saisir dans le CMS.
2. **Valider les textes juridiques et de confidentialité** : coordonnées du
   responsable, canal de contact, durées de conservation, mentions légales et
   politique de confidentialité. La page de demande de données et les réponses
   types doivent être relues par la personne responsable de ces engagements.
3. **Valider la revue métier et fixer la fenêtre de production** pour
   `v2.1.0`. Le commit, le tag, le push et le déploiement ont été autorisés
   **une fois cette revue validée** ; confirmer explicitement quand cette
   condition est remplie. Pour les migrations, approuver séparément le couple
   exact `BASE-SQL-01.home.bzh / kermaria`. L'approbation antérieure portait
   uniquement sur `kermaria_dev`. L'équipe réalisera ensuite la sauvegarde,
   les migrations, les paquets et le contrôle des services SRV-13 puis SRV-12.
4. **Supprimer le fichier de session de recette DEV**
   `%TEMP%\kermaria-formula-admin-session-20261007.clixml` sur RDC-07. Il a
   été créé pour un contrôle d'administration ; la revue automatique a refusé
   sa suppression par commande. Il n'a pas été versionné ni livré aux serveurs.

Ces décisions ne demandent aucune intervention manuelle sur les serveurs ni
dans MariaDB. Les preuves DEV et les limites actuelles sont dans
`DEV_UX_CMS_2026-10-05.md`.
