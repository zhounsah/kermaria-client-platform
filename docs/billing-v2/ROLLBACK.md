# Rollback Billing V2

Ce document decrit le rollback applicatif Billing V2 avant toute activation
production. Il ne remplace pas une sauvegarde ni une revue humaine.

## Principes

- Ne jamais supprimer les tables Billing V2 pour rollbacker un flag.
- Ne jamais modifier les lignes legacy `commercial_offers`, subscriptions ou
  factures historiques pour annuler un essai V2.
- Le legacy reste autoritaire tant qu'une validation humaine n'a pas confirme
  que Billing V2 peut traiter le premier vrai nouvel abonnement.
- Les price locks legacy et V2 restent conserves : ils sont des preuves
  contractuelles ou des artefacts d'audit, pas des leviers de rollback.
- Aucun rollback ne doit retirer un droit AD ou Nextcloud accorde par le legacy.
- Ne jamais supprimer un objet Stripe/PayPal comme substitut a un rollback
  applicatif automatise ; toute annulation provider eventuelle est une decision
  humaine hors migration.
- Les routes de resiliation client/admin existantes sont legacy-only. Pour une
  subscription `billing_v2`, elles doivent refuser avant tout appel Stripe/PayPal
  tant qu'un flux V2 dedie, audite et idempotent n'existe pas.
- Aucun `DROP` ou `DELETE` destructif ne doit etre utilise pour masquer un essai
  V2. Les factures historiques ne sont jamais recalculees ni reecrites.

## Rollback Avant Premier Paiement Provider

Si aucun client n'a ete redirige vers Stripe/PayPal V2 :

1. Desactiver les flags BFF/API :
   - `BILLING_V2_AUTHORITATIVE_CHECKOUT_BFF_ENABLED=false`
   - `BILLING_V2_AUTHORITATIVE_CHECKOUT_ENABLED=false`
   - `BILLING_V2_NEW_SUBSCRIPTIONS_ENABLED=false`
   - `BILLING_V2_PROVIDER_OUTBOX_ENABLED=false`
   - `BILLING_V2_PROVIDER_EXECUTOR_ENABLED=false`
   - `BILLING_V2_REFUNDS_ENABLED=false`
   - `BILLING_V2_PROVISIONING_ENABLED=false`
2. Garder `BILLING_V2_CATALOG_SHADOW_MODE` et
   `BILLING_V2_PROVISIONING_SHADOW_MODE` selon le besoin d'observation, ou les
   desactiver si les logs shadow perturbent l'exploitation.
3. Verifier en lecture seule que le checkout public repasse par le chemin legacy.
4. Ne pas purger les lignes V2 locales : elles restent utiles pour revue.

## Rollback Apres Session Provider Creee

Si une session Stripe/PayPal V2 existe mais que le paiement n'est pas confirme :

1. Desactiver immediatement le BFF checkout V2 et l'outbox provider.
2. Ne pas creer de nouvel abonnement legacy a partir de la session V2 sans revue
   humaine.
3. Laisser les evenements outbox non traites en base avec leur statut courant.
4. Comparer la session provider locale avec le Dashboard Stripe/PayPal en mode
   correspondant, sans modifier les anciens Price IDs ou Plan IDs legacy.
5. Si une annulation provider est necessaire, elle doit etre decidee et executee
   manuellement hors migration automatique.

## Rollback Apres Activation Provider V2

Si un webhook/retour provider a active une subscription V2 locale :

1. Couper les flags de checkout et de provisioning V2.
2. Laisser `billing_v2_provider_events` et
   `billing_v2_payment_agreements` comme journal d'audit.
   Laisser egalement `billing_v2_subscription_documents`,
   `billing_v2_document_line_snapshots`, `commercial_documents` et
   `bpce_invoices` comme preuves documentaires si une facture V2 a deja ete
   emise.
   Aucun `DROP` ou `DELETE` destructif ne doit toucher ces journaux ou les
   factures historiques.
3. Ne pas convertir automatiquement la subscription V2 en subscription legacy.
4. Revue humaine obligatoire pour choisir entre :
   - maintenir le contrat en V2 avec V2 desactive pour les nouveaux checkouts ;
   - recreer manuellement un contrat legacy en preservant le prix contractuel ;
   - rembourser/annuler cote provider selon le cas commercial.
5. Tout provisioning deja applique doit etre traite add-only : aucun retrait de
   droit automatique lors du rollback initial.

## Outbox Et Webhooks

- `BILLING_V2_PROVIDER_OUTBOX_ENABLED=false` empeche de nouveaux appels provider.
- Un event outbox `processing` expire peut etre rejoue plus tard avec la meme
  cle d'idempotence si les flags sont rouverts.
- Un provider event deja `processed` reste idempotent.
- Un provider event `failed` ou `skipped` peut etre retraite avec le dernier
  payload stocke, mais ne doit pas ecraser un succes deja traite.

## Verification Read-Only

Avant de declarer le rollback stabilise :

1. Executer les tests locaux Billing et Billing V2.
2. Verifier que `READINESS-CHECKS.sql` reste une lecture seule.
3. Verifier que l'admin `/admin/billing-v2` n'expose pas
   `BILLING_V2_ADMIN_READY_FOR_FIRST_SUBSCRIPTION` si les flags sont fermes.
4. Confirmer qu'aucun deploiement ou migration production n'a ete execute par le
   rollback local.
