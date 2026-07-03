---
name: infra-r740xd-blocker
description: "Production deployment of kermaria-client-platform is hardware-gated — the R740xd host has not been delivered yet, so the project stays in test phase on SRV-01/SRV-02 with no real external commitments."
metadata: 
  node_type: memory
  type: project
  originSessionId: 316dd2c1-620c-4ba1-833b-0b5d317971ba
---

Le déploiement de production de `kermaria-client-platform` est bloqué par la livraison du serveur **R740xd**. Tant qu'il n'est pas livré, on reste en **phase de tests** sur les hôtes existants **SRV-01** et **SRV-02**. L'utilisateur refuse explicitement de mettre en place deux VM de préproduction "dans le vent" sur l'infra actuelle.

**Why:** L'infra cible (R740xd) doit héberger la vraie préproduction et la production. Monter une préprod jetable sur SRV-01/02 puis tout rebasculer plus tard est du travail perdu. La V1.0 (premier client réel servi) ne peut pas exister sans cible définitive — domaine, TLS, supervision, sauvegardes, rotation des secrets sont câblés sur la machine finale, pas avant.

**How to apply:**
- Considérer V1.0 comme **hardware-gated** : ne pas proposer de "go live" tant que le R740xd n'est pas listé comme disponible par l'utilisateur.
- Toute version développée avant la cible reste **en phase de tests** : aucune émission externe, aucun e-mail envoyé à un destinataire réel, aucune numérotation fiscale revendiquée comme légale, aucune mutation AD hors `OU=TEST_SITE_WEB`. Les modes (`BPCE_INTEGRATION_MODE`/`PAYPAL_MODE`/`STRIPE_MODE`/`EMAIL_INTEGRATION_MODE`) restent à leur défaut non-live.
- **V0.24 est le sas de stabilisation pré-V1** : ce qui se valide en interne maintenant (Brique 1 recette staging + restauration MariaDB, Brique 2 audit sécurité, Brique 3 doc + procédure prod) est distinct de ce qui attend la cible R740xd (exécution de `docs/PRODUCTION_DEPLOYMENT.md`, bascule des modes en `live`, supervision/sauvegardes/rotation câblées sur l'infra définitive = V1.0 beta 1). Le détail à jour des jalons vit dans [[roadmap-current]], ne pas re-hardcoder de numéros de version ici.
- Si une décision implique d'engager une obligation externe non honorable en test (envoi e-mail réel, numérotation fiscale officielle, AD production), alerter l'utilisateur plutôt que d'avancer.

Voir aussi [[roadmap-current]] pour le détail des jalons.
