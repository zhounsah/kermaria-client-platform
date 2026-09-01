# Durcissement du refund core — chaîne documentaire BPCE

## Statut au 1er septembre 2026

Le coeur `BillingV2Refund` crée une intention durable, réutilise l'outbox et
le rail Stripe, relit le provider et ne passe un `BillingEvent` à
`settlement_status=refunded` qu'après preuve exacte du PaymentIntent, du montant
et de la devise. Il bloque aussi le renouvellement local et met en file
l'annulation provider d'un abonnement récurrent.

Cette preuve financière est autonome : une indisponibilité BPCE ne doit jamais
annuler, retarder ni réinterpréter un refund Stripe déjà confirmé. La capacité
reste néanmoins **non activable**. BPCE ne porte aujourd'hui que la création et
validation de facture, le PDF et le marquage payé ; il ne porte ni émission
d'avoir ni recherche de facture/avoir par `external_id`. Le refus actuel d'un
refund si `document_status` vaut `issued`, `pending` ou `failed` est donc
intentionnel et fail-closed.

## Pipeline documentaire existant observé

1. `BillingV2DocumentIssuerService` crée, dans une transaction MariaDB, le
   `commercial_document`, son lien `billing_v2_subscription_documents` et les
   snapshots de lignes du `BillingEvent`.
2. Il persiste ensuite une intention dans
   `billing_v2_document_issuance_attempts`, avec référence externe stable,
   avant tout appel BPCE.
3. `InvoiceIssuingService` crée le brouillon, les lignes puis valide la
   facture BPCE (allocation du numéro fiscal), persiste `bpce_invoices` et
   marque la facture réglée. Les statuts documentaires sont alors projetés sur
   le lien de souscription et le `BillingEvent`.
4. Un appel indéterminé est mis en `reconciliation_required` : sans recherche
   BPCE par référence, le retry automatique est interdit afin d'éviter un
   second numéro fiscal. Les transitions locales sont auditées.

Ce pipeline est un modèle de sécurité, pas encore une primitive d'avoir.

## Mécanisme canonique requis pour un avoir

La future migration additive, postérieure à `082`, doit créer une intention
distincte, par exemple `billing_v2_refund_document_corrections`. Elle ne doit
être ni un statut ajouté à `BillingV2Refund`, ni un second statut financier.

| Donnée | Rôle |
| --- | --- |
| `id`, `refund_id`, `billing_event_id` | lien unique au refund confirmé et à l'événement |
| `original_commercial_document_id`, `original_bpce_invoice_id`, `original_fiscal_number` | preuve relue du document corrigé |
| `provider`, `environment`, identifiants et numéro d'avoir | preuve provider de l'avoir |
| `amount_cents`, `currency` | copie contrôlable, vérifiée contre le refund ; jamais un nouveau prix |
| `external_reference`, `idempotency_key_hash` | identité stable de création et réconciliation provider |
| `status`, raisons et horodatages | réparation durable et exploitation |
| `correlation_id` | corrélation de bout en bout |

Les contraintes minimales sont `UNIQUE(refund_id)`, unicité du hash de clé et
unicité `(provider, environment, provider_credit_note_id)` lorsque la référence
provider est présente. La clé externe doit dériver exclusivement de valeurs
immuables :

```text
billing-v2-credit-note|{billing_event_id}|{refund_id}|{original_document_id}
```

Elle peut être envoyée comme `external_id` seulement si BPCE prouve une
recherche ou une unicité équivalente. Sinon un timeout devient
`reconciliation_required`, jamais un nouveau POST.

États proposés :

```text
requested -> in_flight -> provider_created -> confirmed
                    \-> reconciliation_required
                    \-> failed
```

`confirmed` exige l'identifiant BPCE, le numéro d'avoir, le montant et la
devise relus, ainsi que le lien à la facture originale. Le
`BillingEvent.document_status` continue à décrire le document initial ; il ne
doit pas masquer l'avoir. La projection opérationnelle expose séparément le
statut de refund et celui de correction documentaire.

## Flux et reprise requis

```text
facture initiale émise
  -> refund Stripe confirmé
  -> transaction : correction requested + outbox + audit
  -> worker BPCE avec clé stable
  -> avoir créé/validé
  -> refetch, preuve numéro/montant/devise/document original
  -> correction confirmed
```

BPCE indisponible laisse le refund financièrement `refunded` et la correction
en attente, en reprise ou en réconciliation humaine. L'outbox, le bail de
claim et la clé métier autorisent la reprise sans double avoir. Une correction
documentaire n'est jamais finale avant preuve BPCE et ne peut jamais modifier
le produit ni le prix initial.

## Préconditions et readiness

1. BPCE doit fournir et faire tester une primitive d'avoir liée à la facture
   originale et une recherche par référence stable — ou une garantie
   d'idempotence équivalente.
2. Le contrat `IBpceInvoicingService`, son implémentation live, le repository,
   l'outbox et l'audit devront être étendus ensemble ; aucun worker refund ne
   contourne ce rail.
3. Les événements `pending` ou `failed` exigent d'abord la résolution de leur
   émission initiale ; un avoir ne répare pas une facture de résultat inconnu.
4. MariaDB et BPCE test doivent prouver les unicités, claims concurrents, bail
   expiré, timeout après création, refetch, double worker et divergences de
   montant/devise.

La suite `--billing-v2-financial-core-schema` prépare déjà la partie MariaDB
001→082 : intention durable, unicité par événement, outbox, claim concurrent,
processing expiré et projection de confirmation. Elle refuse explicitement de
s'exécuter sans `BILLING_V2_TEST_MARIADB_CONNECTION`; elle ne remplace pas le
test Stripe réel, qui reste une preuve provider séparée.

`BillingV2LifecycleReadiness` décompose désormais : code core, schéma `082`,
preuve Stripe test, correction BPCE et capacité finale. `refunds` n'est READY
que lorsque les quatre premiers le sont. `BILLING_V2_REFUNDS_ENABLED=false` et
`BillingV2LaunchScope.RefundsEnabled=false` restent des verrous d'exécution
indépendants ; ils ne donnent jamais un droit client.

## Conclusions classées

- **VALIDE — indépendance financière :** le refund Stripe confirmé survit à
  BPCE indisponible ; la dette documentaire reste durable et séparée.
- **VALIDE — idempotence :** refund/document original et clé externe stable
  sont le bon domaine métier pour empêcher les doubles avoirs.
- **VALIDE — refus actuel :** sans avoir et recherche BPCE, l'exclusion d'un
  événement documenté évite une incohérence fiscale.
- **BLOQUEUR — BPCE :** aucun contrat actuel ne crée ni ne prouve un avoir
  après timeout ; l'activation générale est interdite jusqu'à son extension.
- **BLOQUEUR — preuves externes :** aucune MariaDB jetable ni clé Stripe test
  n'est configurée dans cette revue ; elles ne sont pas déclarées PASS.
