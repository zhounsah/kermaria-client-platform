---
name: infra-r740xd-blocker
description: "RÉSOLU (~2026-07-23) : le R740xd est livré et toutes les VM sont actives. La V1.0 n'est PLUS hardware-gated. Historique du blocage conservé ci-dessous."
metadata: 
  node_type: memory
  type: project
  originSessionId: 316dd2c1-620c-4ba1-833b-0b5d317971ba
  modified: 2026-08-03T08:51:55.973Z
---
Etat courant 20/08/2026 : production active sur SRV-11/SRV-12/SRV-13/SRV-06 ; le blocage materiel est clos. Les domaines canoniques sont zachary-it.fr, dashboard.zachary-it.fr et administration.zachary-it.fr. Voir deployment-topology et docs/DOMAIN_MIGRATION_2026-08-20.md.


**MISE À JOUR 2026-08-03 — BLOCAGE LEVÉ.** L'utilisateur confirme avoir reçu le **R740xd il y a ~1,5 semaine (≈ 2026-07-23)** et que **toutes les VM sont actives**. La cible d'infra est donc debout. Conséquences :

- La V1.0 (V1.0 beta 1 = test de déploiement cible, V1.0 RC = prod réelle) **n'est plus hardware-gated** ; le jalon peut avancer.
- L'argument « ne rien monter dans le vent avant la cible » ne tient plus : la cible EST la R740xd.
- La topologie a changé de nature : on passe du bare-metal 3 hôtes sans VM à une **ferme Hyper-V R740xd de ~38 VM** (rôles séparés). Détail à jour dans [[deployment-topology]] (mémoire réécrite le 2026-08-03).
- ⚠️ À confirmer avant d'affirmer : « VM actives » ≠ « apps redéployées dessus ». Vérifier au cas par cas si le webportal/API/DB tournent déjà sur les nouvelles VM (SRV-12/SRV-13/SRV-06…) ou encore sur l'ancien split. Le runbook `docs/DEPLOYMENT_WINDOWS.md` décrit encore l'ancien modèle.
- Les garde-fous « phase de tests » (modes non-live par défaut, pas d'émission externe non voulue, mutations AD cadrées) restent une **décision produit** à conserver tant que l'utilisateur ne bascule pas explicitement en live — mais ce n'est plus imposé par le matériel.

--- HISTORIQUE (périmé, conservé pour contexte) ---

Le déploiement de production était bloqué par la livraison du serveur **R740xd**. Tant qu'il n'était pas livré, on restait en **phase de tests** sur les hôtes existants SRV-01 et SRV-02. L'utilisateur refusait de monter deux VM de préproduction "dans le vent" sur l'infra d'alors.

**Why (historique) :** l'infra cible (R740xd) devait héberger la vraie préprod et la prod ; monter une préprod jetable puis tout rebasculer était du travail perdu.

Voir aussi [[roadmap-current]] pour le détail des jalons.
