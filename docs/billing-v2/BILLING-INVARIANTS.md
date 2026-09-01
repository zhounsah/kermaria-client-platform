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

## Cœur financier (Phase 1)

Détail et mécanismes dans `FINANCIAL-CORE.md`.

21. Un `BillingEvent` est une intention financière immuable. Ses montants, sa
    période, ses snapshots et ses lignes ne sont jamais mis à jour.
22. Une correction se fait par un nouvel événement `adjustment` référençant
    l'événement fautif, jamais par mutation d'un événement historique.
23. Une clé d'idempotence n'est jamais réutilisée, y compris après un `void`.
24. `SubscriptionChange` persiste l'intention utilisateur et sert d'ancre
    d'idempotence pour toute opération monétaire.
25. Toute mutation de `subscriptions` s'écrit en compare-and-swap sur `version`.
    Un conflit de version remonte en échec explicite, jamais en no-op.
26. Une `PaymentAttempt` est persistée **avant** tout appel provider, et un retry
    réutilise la même ligne et la même clé provider.
27. Le montant attendu et le montant réellement settled sont deux données
    distinctes. Un règlement n'est un succès que s'ils sont égaux, devise
    comprise ; tout écart produit `amount_mismatch` et bloque la chaîne.
28. Aucun montant facturé n'est déterminé par Stripe ou PayPal.
29. Un webhook provider est un signal, jamais une preuve de paiement suffisante.
    La convergence passe par une relecture de l'objet chez le provider.
30. Aucun provisioning, aucune émission documentaire et aucun passage à `paid` ne
    peut découler de la seule réception d'un événement provider brut.
31. En V2.0, la relation `BillingEvent` ↔ document est 1:1.
32. Une seule autorité numérote les factures.
33. Un `BillingEvent` ne peut pas être annulé si un settlement a réussi ou si un
    document légal a été émis.
34. Un événement `finalized` a au moins une ligne, et la somme de ses lignes
    égale ses totaux, devise comprise.
35. Une ambiguïté de prix applicable est résolue par une règle versionnée
    explicite, sinon elle échoue en fermé. Deux versions d'un même prix ne sont
    jamais sommées comme deux services.
36. Un paiement arrivant après expiration d'un `SubscriptionChange` ne provisionne
    rien automatiquement et part en réconciliation.
37. Un remboursement intégral est une intention durable liée à un `BillingEvent`
    settled et à sa `PaymentAttempt`, jamais une écriture directe de
    `settlement_status=refunded`.
38. Le montant et la devise d'un remboursement viennent exclusivement du
    settlement financial authoritative ; le navigateur et les workflows produit
    ne portent aucun montant de remboursement autoritaire.
39. `settlement_status=refunded` exige une relecture provider réussie, du même
    PaymentIntent, du montant intégral exact et de la même devise. Pending,
    failed, timeout ou webhook seul ne valent jamais refunded.
40. Un remboursement confirmé bloque les renouvellements futurs et enfile une
    annulation provider idempotente ; cette compensation interne est distincte
    de `SelfServiceCancellationEnabled`.
