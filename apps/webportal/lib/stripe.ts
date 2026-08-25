import "server-only";

import { isStripeSecretKeyCompatible } from "./runtime-config";

const API_BASE = "https://api.stripe.com/v1";

export type StripeEnvironment = "disabled" | "test" | "live";

export function getStripeMode(): StripeEnvironment {
  const raw = process.env.STRIPE_MODE?.trim().toLowerCase();
  return raw === "test" || raw === "live" ? raw : "disabled";
}

function getSecretKey(): string {
  const key = process.env.STRIPE_SECRET_KEY?.trim();
  if (!key) {
    throw new Error("STRIPE_SECRET_KEY non configur?e.");
  }

  if (!isStripeSecretKeyCompatible(getStripeMode(), key)) {
    throw new Error("STRIPE_SECRET_KEY incompatible avec STRIPE_MODE.");
  }

  return key;
}

async function stripeRequest<T>(
  path: string,
  params: Record<string, string>,
): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${getSecretKey()}`,
      "Content-Type": "application/x-www-form-urlencoded",
    },
    body: new URLSearchParams(params).toString(),
    cache: "no-store",
  });

  if (!response.ok) {
    const err = await response.text();
    throw new Error(`Requete Stripe echouee (${path}) : ${response.status} ${err}`);
  }

  return (await response.json()) as T;
}

export type CreateCheckoutSessionResult = {
  sessionId: string;
  approveUrl: string;
};

export async function createStripeOneShotCheckoutSession(
  amountCents: number,
  currency: string,
  description: string,
  successUrl: string,
  cancelUrl: string,
  documentId: string,
): Promise<CreateCheckoutSessionResult> {
  const data = await stripeRequest<{ id: string; url: string | null }>(
    "/checkout/sessions",
    {
      mode: "payment",
      "line_items[0][price_data][currency]": currency.toLowerCase(),
      "line_items[0][price_data][product_data][name]": description.slice(0, 250),
      "line_items[0][price_data][unit_amount]": String(amountCents),
      "line_items[0][quantity]": "1",
      success_url: successUrl,
      cancel_url: cancelUrl,
      "metadata[document_id]": documentId,
      "payment_intent_data[metadata][document_id]": documentId,
    },
  );

  if (!data.url) {
    throw new Error("Stripe n'a pas retourné d'URL de paiement.");
  }

  return { sessionId: data.id, approveUrl: data.url };
}

/*
 * Abonnements et catalogue : plus rien ici.
 *
 * La creation, la relecture et la resiliation d'un abonnement fournisseur sont
 * portees par API-INTERNAL (rail Billing V2 et outbox de resiliation), qui
 * detient les identifiants fournisseur persistes. Le portail n'a plus aucune
 * raison de conclure seul qu'un abonnement a cesse d'etre facturable.
 *
 * Les helpers de creation de produit et de prix Stripe ont disparu avec le
 * catalogue commercial legacy : le rail V2 facture en `price_data` inline et
 * ne depend d'aucun `price_id` externe.
 *
 * Ce qui precede ne sert qu'au paiement PONCTUEL d'un document commercial,
 * qui n'a pas d'abonnement.
 */
