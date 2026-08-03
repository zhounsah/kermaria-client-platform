# Brief Codex — Politique de sauvegarde, restauration et suppression

## Objectif

Clarifier sur le site Zachary IT :

- la fréquence des sauvegardes ;
- la durée de rétention ;
- les conditions de restauration ;
- la suppression après résiliation ;
- la localisation des données et sauvegardes.

L'objectif est d'avoir des informations cohérentes entre les offres, le catalogue, les CGV et la politique de confidentialité, sans promettre plus que l'infrastructure réelle.

## Règle impérative

Avant publication, vérifier que les engagements ci-dessous sont techniquement vrais.

Valeurs proposées, à confirmer :

```yaml
backup_frequency: quotidienne, au moins une fois par période de 24 h
backup_retention: 30 jours glissants
included_customer_restore: 1 par mois avec option de sauvegarde
support_response_target: 1 jour ouvré, sans garantie de rétablissement
active_data_deletion: 7 jours calendaires après fin du service
backup_expiration: au plus tard 30 jours après suppression des données actives
production_location: Bretagne, France
backup_location: France
second_geographic_site_guaranteed: false
```

Ne pas publier une valeur non confirmée. En cas de conflit avec l'existant ou l'infrastructure réelle, conserver le code fonctionnel et signaler précisément le point bloquant.

## Travail demandé

1. Inspecter le dépôt et identifier :
   - pages des offres et packs ;
   - catalogue ;
   - CGV ;
   - politique de confidentialité ;
   - composants ou données partagés contenant les caractéristiques des offres.

2. Centraliser les valeurs récurrentes si l'architecture du projet le permet, afin d'éviter les divergences entre pages.

3. Ajouter une présentation courte sur les offres concernées.

4. Ajouter les clauses détaillées dans les CGV ou dans une section dédiée liée depuis les CGV.

5. Réserver la politique de confidentialité à la conservation des données administratives et personnelles traitées par Zachary IT. Ne pas y mélanger inutilement les caractéristiques commerciales des sauvegardes client.

6. Vérifier la cohérence des noms de packs, options, durées d'engagement et formulations existantes.

7. Ne pas modifier le style visuel global, les tarifs ou le fonctionnement métier hors de ce périmètre.

## Texte court pour les offres

À afficher uniquement lorsqu'une sauvegarde est incluse ou souscrite :

> Sauvegarde automatique quotidienne avec rétention glissante de 30 jours. Une demande de restauration par mois est incluse. Les données modifiées depuis la dernière sauvegarde réussie peuvent ne pas être récupérables.

Pour une offre sans sauvegarde incluse :

> Sauvegarde disponible en option. Sans option active, la récupération des données après suppression, altération ou défaillance n'est pas garantie.

Ajouter un lien visible vers les conditions détaillées.

## Texte détaillé à intégrer

### Sauvegardes

Les services pour lesquels une option de sauvegarde est expressément incluse ou souscrite font l'objet de sauvegardes automatiques. En l'absence d'une option de sauvegarde active, Zachary IT ne garantit pas la possibilité de récupérer les données après leur suppression, leur altération ou la défaillance du support principal.

Les sauvegardes sont exécutées quotidiennement, au moins une fois par période de vingt-quatre heures. Les données créées ou modifiées depuis la dernière sauvegarde réussie peuvent ne pas être récupérables.

Les sauvegardes réussies sont conservées pendant une période glissante maximale de trente jours. Les sauvegardes les plus anciennes sont automatiquement supprimées. Le nombre de points de restauration disponibles peut varier en cas d'échec, de maintenance, d'interruption technique ou de corruption constatée.

Une sauvegarde réduit le risque de perte de données, mais ne constitue pas une garantie absolue de récupération intégrale dans toutes les circonstances.

### Restaurations

Les restaurations rendues nécessaires par un incident relevant de l'infrastructure exploitée par Zachary IT sont réalisées sans frais supplémentaires.

Lorsqu'une option de sauvegarde est active, une demande de restauration par mois est incluse en cas de suppression ou de mauvaise manipulation imputable au client. Une demande supplémentaire, complexe ou portant sur un volume important peut faire l'objet d'un devis préalable.

Zachary IT s'efforce de commencer le traitement d'une demande complète dans un délai d'un jour ouvré. Il s'agit d'un objectif de prise en charge et non d'une garantie de rétablissement dans un délai déterminé. Tout niveau de service garanti doit être prévu par écrit dans une offre ou un contrat spécifique.

### Fin du service et suppression

Le client doit récupérer les données qu'il souhaite conserver avant l'expiration de son accès.

Sauf accord écrit contraire, les données actives hébergées pour le compte du client sont supprimées dans un délai maximal de sept jours calendaires suivant la fin effective du service.

Les copies résiduelles présentes dans les sauvegardes ne sont plus utilisées à des fins opérationnelles et sont supprimées automatiquement à l'expiration de leur période de rétention, au plus tard trente jours après la suppression des données actives.

Les documents comptables, contractuels, techniques ou probatoires soumis à une obligation légale de conservation peuvent être archivés séparément pendant la durée applicable.

### Localisation

Les données principales sont hébergées sur une infrastructure exploitée par Zachary IT en Bretagne, en France. Les sauvegardes sont hébergées en France.

La localisation précise des équipements et les détails sensibles de l'architecture ne sont pas publiés pour des raisons de sécurité.

Sauf engagement contractuel spécifique, l'offre standard ne garantit pas la conservation d'une copie sur un second site géographiquement distinct.

## Vocabulaire à respecter

Ne pas présenter comme une sauvegarde indépendante :

- le RAID ;
- une réplication sur le même site ;
- un snapshot local ;
- une corbeille ;
- l'historique des versions d'un service.

Éviter les termes absolus :

- « aucune perte possible » ;
- « récupération garantie » ;
- « sauvegarde en temps réel », sauf mécanisme réel correspondant ;
- « haute disponibilité » ou « géo-redondance », sauf engagement réellement assuré.

## Critères d'acceptation

- Les informations courtes figurent sur chaque offre concernée.
- Les conditions détaillées sont accessibles en un clic depuis les offres.
- Les valeurs sont identiques partout.
- Les offres sans sauvegarde incluse l'indiquent explicitement.
- Aucun délai de restauration garanti n'est inventé.
- La production en Bretagne et les sauvegardes en France sont distinguées.
- L'absence de second site géographique garanti est clairement indiquée.
- La suppression des données actives et l'expiration des sauvegardes sont différenciées.
- Les CGV, le catalogue et les noms des packs restent cohérents.
- Le build, les tests et le lint réussissent.
- Le rapport final liste les fichiers modifiés et les éventuels engagements restant à confirmer.

## Livrable attendu de Codex

- modifications minimales et ciblées ;
- aucune refonte hors périmètre ;
- tests ou vérifications exécutés ;
- résumé final très court ;
- liste explicite des valeurs non vérifiées avant publication.
