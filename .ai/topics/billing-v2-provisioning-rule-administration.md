---
name: billing-v2-provisioning-rule-administration
description: "Administration locale des regles de provisioning Billing V2 : routes catalogue, BFF contraint, UI minimale et semantique AD groupe utilisateur."
metadata:
  node_type: memory
  type: project
---

## Regles de provisioning administrees

L'autorite reste exclusivement `billing_v2_provisioning_rules`. Le catalogue
admin expose les regles rattachees a chaque service, avec les routes
`/internal/admin/billing-v2/catalog/provisioning-rules` pour creer, modifier et
desactiver, ainsi qu'une lecture bornee par service.

Le premier modele administre est strict :

- `rule_type = ad_group_membership`;
- `target_type = ad_group`;
- `scope = user`;
- `target_reference` non vide.

La validation API-INTERNAL reutilise `BillingV2ProvisioningRuleSemantics` : un
type inconnu, un couple type/cible incompatible, une portee incoherente, un
service ou palier invalide, et une regle active identique sont refuses. Un
retrait met la regle a `inactive`; il ne supprime pas l'historique.

Le garde `DeploymentEnvironmentGuard` borne aussi les cibles AD de catalogue :
en `APP_ENV=Development`, seules les references finissant par `_DEV` sont
acceptables (par exemple `GG_SERVICE_E2E_DEV`) ; en production, toute
reference `_DEV` est refusee. CREATE et UPDATE appellent ce garde avant toute
ecriture SQL.

Les memberships effectivement ajoutes par Billing V2 sont traces dans
`billing_v2_provisioning_managed_memberships`. Cette trace operationnelle, et
non une liste de configuration, est la seule autorite de retrait : une regle
inactive reste donc deprovisionnable, tandis qu'un groupe manuel jamais ajoute
par Billing V2 ne peut pas etre retire par le moteur.

Le planner demeure le consommateur de la table : son test de regles confirme
qu'un groupe AD est porte par l'identite utilisateur exacte, sans configuration
parallele ni appel annuaire pendant les tests locaux.
