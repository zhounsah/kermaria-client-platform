import assert from "node:assert/strict";

const { findReturnedSubscription } = await import(
  "../lib/subscription-return.ts"
);

const subscriptions = [
  {
    id: "subscription-paypal-existing",
    paypalSubscriptionId: "I-PAYPAL-001",
    stripeSubscriptionId: null,
  },
  {
    id: "subscription-stripe-existing",
    paypalSubscriptionId: null,
    stripeSubscriptionId: "sub_STRIPE_001",
  },
];

assert.equal(
  findReturnedSubscription(
    subscriptions,
    "stripe",
    "sub_STRIPE_001",
  )?.id,
  "subscription-stripe-existing",
  "Un double retour Stripe doit reutiliser l'abonnement local existant.",
);

assert.equal(
  findReturnedSubscription(subscriptions, "stripe", "sub_STRIPE_NEW"),
  null,
  "Un premier retour Stripe sans abonnement local doit rester createur.",
);

assert.equal(
  findReturnedSubscription(subscriptions, "paypal", "I-PAYPAL-001")?.id,
  "subscription-paypal-existing",
  "Le retour PayPal doit conserver sa resolution idempotente existante.",
);

assert.equal(
  findReturnedSubscription(subscriptions, "paypal", "   "),
  null,
  "Un identifiant fournisseur vide ne doit jamais matcher un abonnement.",
);

console.log("Verification idempotence retours abonnement reussie.");
