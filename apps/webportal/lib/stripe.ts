import "server-only";

const API_BASE = "https://api.stripe.com/v1";

export type StripeEnvironment = "disabled" | "test" | "live";

export function getStripeMode(): StripeEnvironment {
  const raw = process.env.STRIPE_MODE?.trim().toLowerCase();
  return raw === "test" || raw === "live" ? raw : "disabled";
}

function getSecretKey(): string {
  const key = process.env.STRIPE_SECRET_KEY?.trim();
  if (!key) {
    throw new Error("STRIPE_SECRET_KEY non configurée.");
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
    throw new Error(`Requête Stripe échouée (${path}) : ${response.status} ${err}`);
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

export async function createStripeSubscriptionCheckoutSession(
  priceId: string,
  customerEmail: string,
  successUrl: string,
  cancelUrl: string,
): Promise<CreateCheckoutSessionResult> {
  const data = await stripeRequest<{ id: string; url: string | null }>(
    "/checkout/sessions",
    {
      mode: "subscription",
      "line_items[0][price]": priceId,
      "line_items[0][quantity]": "1",
      customer_email: customerEmail,
      success_url: successUrl,
      cancel_url: cancelUrl,
    },
  );

  if (!data.url) {
    throw new Error("Stripe n'a pas retourné d'URL de souscription.");
  }

  return { sessionId: data.id, approveUrl: data.url };
}

export type StripeCheckoutSession = {
  subscriptionId: string | null;
  customerEmail: string | null;
};

export async function getStripeCheckoutSession(
  sessionId: string,
): Promise<StripeCheckoutSession> {
  const response = await fetch(
    `${API_BASE}/checkout/sessions/${encodeURIComponent(sessionId)}`,
    {
      method: "GET",
      headers: { Authorization: `Bearer ${getSecretKey()}` },
      cache: "no-store",
    },
  );

  if (!response.ok) {
    const err = await response.text();
    throw new Error(`Lecture session Stripe échouée : ${response.status} ${err}`);
  }

  const data = (await response.json()) as {
    subscription?: string | null;
    customer_details?: { email?: string | null } | null;
  };

  return {
    subscriptionId: data.subscription ?? null,
    customerEmail: data.customer_details?.email ?? null,
  };
}

export async function createStripeProduct(
  name: string,
  description: string,
): Promise<string> {
  const data = await stripeRequest<{ id: string }>("/products", {
    name: name.slice(0, 250),
    description: (description || name).slice(0, 500),
  });
  return data.id;
}

export async function createStripePrice(
  productId: string,
  priceAmountCents: number,
  currency: string,
): Promise<string> {
  const data = await stripeRequest<{ id: string }>("/prices", {
    product: productId,
    unit_amount: String(priceAmountCents),
    currency: currency.toLowerCase(),
    "recurring[interval]": "month",
  });
  return data.id;
}
