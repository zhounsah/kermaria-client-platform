# Invariants de sécurité Billing

Ces règles ne doivent pas être violées.

1. Aucun montant monétaire en FLOAT ou DOUBLE.
2. Tous les montants sont stockés en centimes entiers.
3. Les prix déjà utilisés par une facture ou un contrat ne sont jamais réécrits.
4. Les anciens Stripe Price IDs et PayPal Plan IDs ne sont jamais modifiés pendant la migration.
5. Une facture historique n'est jamais recalculée avec un nouveau catalogue.
6. Un contrat legacy actif n'est jamais repricé silencieusement.
7. Une migration DB doit être additive et rétrocompatible tant que le legacy existe.
8. Le provisioning ne doit pas être déclenché à partir du nom commercial d'un pack.
9. Le provisioning doit découler des services/tiers effectifs.
10. Toute modification financière doit être idempotente.
11. Les webhooks fournisseurs doivent être idempotents.
12. Toute modification d'abonnement doit être auditée.
13. La création d'un changement et de son événement outbox doit être atomique.
14. Aucun worker externe ne doit être appelé au milieu d'une transaction DB critique.
15. Les downgrades de stockage doivent vérifier la compatibilité avec l'utilisation réelle.
16. Le tier VPN legacy ne doit pas être converti vers un nouveau tier sans information fiable.
17. Un paiement upfront ne doit pas générer de remboursement automatique lors d'une réduction de provisioning.
18. Une capacité déjà prépayée peut être reprovisionnée sans supplément dans la limite du droit acheté.
19. Les prestations ponctuelles ne reçoivent pas une remise d'engagement par défaut.
20. Toute bascule legacy → V2 doit avoir une stratégie de rollback documentée.
