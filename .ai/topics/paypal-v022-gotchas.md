---
name: paypal-v022-gotchas
description: "Pièges identifiés pendant l'implémentation V0.22/V0.22.1 PayPal subscriptions — sérialisation JSON, raw strings C#, test webhook local, immutabilité des plans PayPal. À consulter avant de toucher au flow souscription."
metadata: 
  node_type: memory
  type: project
  originSessionId: 4bb86cc5-435d-4a41-b312-6853c1df5edc
---

Quatre gotchas non-évidents accumulés pendant V0.22 + V0.22.1 (2026-06-25 → 2026-06-26) qui économisent du temps de debug.

**1. `System.Text.Json` et acronymes PayPal.** La naming policy CamelCase par défaut transforme `PayPalPlanId` → `payPalPlanId` (P majuscule au milieu) lors de la sérialisation vers le BFF TS, alors que les types shared utilisent `paypalPlanId` (tout en bas-de-casse pour le prefix). Résultat : tout consommateur TS lit `undefined`. Mettre `[property: JsonPropertyName("paypalPlanId")]` sur tous les champs C# avec un acronyme PayPal (et idem pour toute future API tierce avec un acronyme intercalé). Voir `apps/api-internal/Contracts/SubscriptionContracts.cs` et `CommercialContracts.cs`.

**2. Raw strings C# + concat SQL.** `"""..."""` n'ajoute pas de `\n` final, donc `BaseSelect + """WHERE ..."""` produit `commercial_offer_idWHERE` comme un seul token et MariaDB refuse en erreur de syntaxe. Toujours soit insérer `"\n" +` entre les moitiés, soit inliner une seule raw string. Voir `apps/api-internal/Data/Repositories/MariaDbSubscriptionRepository.cs` (les 4 méthodes Get*Async).

**3. PAYPAL_MODE = univers séparés.** Un plan PayPal créé en sandbox n'existe pas en live et vice-versa. V0.22.1 a séparé `commercial_offers.paypal_plan_id` en `paypal_plan_id_sandbox` + `paypal_plan_id_live` (migration 016). Toujours résoudre l'ID actif via `PayPalRuntimeConfiguration.IsLive` côté C# ou `getPayPalMode()` côté BFF, jamais lire directement `paypal_plan_id` qui n'existe plus. La même prudence s'appliquera à toute autre intégration tierce avec ses propres credentials sandbox/prod.

**Why:** ces 3 bugs ont chacun pris 30+ minutes à diagnostiquer parce qu'ils produisent des symptômes muets (button caché, requête refusée silencieusement par MariaDB, lookup retourne null).

**How to apply:**
- Avant tout nouveau record C# avec un champ PayPal/Stripe/etc., vérifier les serialisations camelCase et annoter avec `[JsonPropertyName]` si besoin.
- Toute concat de raw string C# pour SQL : préférer une seule raw string ou expliciter le `"\n"`.
- Toute nouvelle config sandbox/live : penser au stockage en deux colonnes, lecture via `PayPalRuntimeConfiguration` ou équivalent.

**4. Tester un webhook PayPal en local sans tunnel HTTPS.**
- Mettre `PAYPAL_WEBHOOK_VERIFY=false` dans `.local.env.ps1` puis **redémarrer le BFF Next.js** (les env vars sont lues au boot, pas via hot reload).
- POST avec `Invoke-RestMethod` vers `http://localhost:3000/api/webhooks/paypal`. Payload minimum :
  ```json
  { "id": "WH-LOCAL-...", "event_type": "BILLING.SUBSCRIPTION.ACTIVATED",
    "resource": { "id": "I-..." } }
  ```
- Pour `PAYMENT.SALE.COMPLETED`, c'est `resource.billing_agreement_id` (pas `resource.id`) qui sert au lookup.
- `BPCE_INTEGRATION_MODE=mock` + `EMAIL_INTEGRATION_MODE=mock` requis pour aller jusqu'à la facture + email, sinon l'event passe `failed` à l'étape BPCE.
- Toujours changer l'`id` de l'event PowerShell entre 2 essais — sinon idempotence (`UNIQUE event_id`) renvoie le statut précédent sans re-exécuter.

**5. dotnet run + Edit C# = binaire stale.** Quand `dotnet run` tourne, `dotnet build` ne peut pas remplacer le DLL (locked). Le build affiche des warnings MSB3026 et continue, mais le binaire en cours est inchangé. Si un comportement ne matche pas le source code, c'est presque toujours ça. Solution : Ctrl+C, `dotnet run` à nouveau. Pas besoin de clean.

Voir [[roadmap-current]] pour le statut V0.22 et la documentation d'implémentation [docs/V0.22_SUBSCRIPTIONS.md](docs/V0.22_SUBSCRIPTIONS.md).
