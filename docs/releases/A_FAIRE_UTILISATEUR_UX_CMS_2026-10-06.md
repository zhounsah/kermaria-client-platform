# À faire par le propriétaire du site — après la livraison v2.1.0

Le code, les tests techniques, les sauvegardes et les déploiements sont pris en
charge par l'équipe de réalisation. Cette liste ne contient que les décisions
et accès qui appartiennent au propriétaire du site.

1. **Vérifier l'accès depuis l'extérieur** : ouvrir
   `https://zachary-it.fr/` depuis un téléphone en données mobiles, sans Wi-Fi
   ni VPN. Ouvrir aussi « Demander mes données ». Les contrôles de livraison
   ont réussi depuis le réseau de l'opérateur, mais une sonde externe n'a pas
   pu confirmer la portée Internet.
2. **Relire la vitrine en production** sur `https://zachary-it.fr/` : accueil,
   services, offres, configuration d'une offre, panier, tarifs, diagnostic,
   inscription, contact, pied de page et page « Demander mes données ».
   Signaler les formulations inexactes, les
   prestations réellement proposées, les coordonnées et les liens à corriger.
   Dans **Administration → Mise en page**, juger aussi si l'aperçu du brouillon
   permet de préparer ces pages sans aide technique. Les montants affichés
   viennent du catalogue : ne pas les saisir dans le CMS.
3. **Valider les textes juridiques et de confidentialité** : coordonnées du
   responsable, canal de contact, durées de conservation, mentions légales et
   politique de confidentialité. La page de demande de données et les réponses
   types doivent être relues par la personne responsable de ces engagements.
4. **Supprimer le fichier de session de recette DEV**
   `%TEMP%\kermaria-formula-admin-session-20261007.clixml` sur RDC-07. Il a
   été créé pour un contrôle d'administration ; la revue automatique a refusé
   sa suppression par commande. Il n'a pas été versionné ni livré aux serveurs.

Ces actions ne demandent aucune intervention manuelle sur les serveurs ni
dans MariaDB. Les preuves de livraison et les limites figurent dans
`V2.1.0.md` ; les preuves DEV détaillées sont dans
`DEV_UX_CMS_2026-10-05.md`.
