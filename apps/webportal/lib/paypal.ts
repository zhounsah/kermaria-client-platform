import "server-only";

const SANDBOX_BASE = "https://api-m.sandbox.paypal.com";
const LIVE_BASE = "https://api-m.paypal.com";

function getBase() {
  return process.env.PAYPAL_MODE === "live" ? LIVE_BASE : SANDBOX_BASE;
}

function getCredentials() {
  const clientId = process.env.PAYPAL_CLIENT_ID?.trim();
  const clientSecret = process.env.PAYPAL_CLIENT_SECRET?.trim();
  if (!clientId || !clientSecret) {
    throw new Error("PAYPAL_CLIENT_ID ou PAYPAL_CLIENT_SECRET non configur?s.");
  }
  return { clientId, clientSecret };
}

let cachedToken: string | null = null;
let tokenExpiresAt = 0;

export async function getPayPalAccessToken(): Promise<string> {
  if (cachedToken && Date.now() < tokenExpiresAt) {
    return cachedToken;
  }

  const { clientId, clientSecret } = getCredentials();
  const credentials = Buffer.from(`${clientId}:${clientSecret}`).toString(
    "base64",
  );

  const response = await fetch(`${getBase()}/v1/oauth2/token`, {
    method: "POST",
    headers: {
      Authorization: `Basic ${credentials}`,
      "Content-Type": "application/x-www-form-urlencoded",
    },
    body: "grant_type=client_credentials",
    cache: "no-store",
  });

  if (!response.ok) {
    throw new Error(`?chec de l'authentification PayPal : ${response.status}`);
  }

  const data = await response.json();
  cachedToken = data.access_token as string;
  tokenExpiresAt = Date.now() + (data.expires_in - 60) * 1000;
  return cachedToken;
}

export type CreateOrderResult = {
  orderId: string;
  approveUrl: string;
};

export async function createPayPalOrder(
  amountCents: number,
  currency: string,
  returnUrl: string,
  cancelUrl: string,
  invoiceReference: string,
): Promise<CreateOrderResult> {
  const token = await getPayPalAccessToken();
  const amount = (amountCents / 100).toFixed(2);

  const response = await fetch(`${getBase()}/v2/checkout/orders`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      intent: "CAPTURE",
      purchase_units: [
        {
          reference_id: invoiceReference,
          description: `Reglement facture ${invoiceReference}`,
          amount: {
            currency_code: currency,
            value: amount,
          },
        },
      ],
      payment_source: {
        paypal: {
          experience_context: {
            payment_method_preference: "IMMEDIATE_PAYMENT_REQUIRED",
            landing_page: "LOGIN",
            user_action: "PAY_NOW",
            return_url: returnUrl,
            cancel_url: cancelUrl,
          },
        },
      },
    }),
    cache: "no-store",
  });

  if (!response.ok) {
    const err = await response.text();
    throw new Error(`Creation ordre PayPal echouee : ${response.status} ${err}`);
  }

  const data = await response.json();
  const approveLink = (data.links as Array<{ rel: string; href: string }>).find(
    (l) => l.rel === "payer-action",
  );

  if (!approveLink) {
    throw new Error("Lien d'approbation PayPal introuvable dans la r?ponse.");
  }

  return { orderId: data.id as string, approveUrl: approveLink.href };
}

export type CaptureResult = {
  status: string;
  captureId: string | null;
};

export async function capturePayPalOrder(
  orderId: string,
): Promise<CaptureResult> {
  const token = await getPayPalAccessToken();

  const response = await fetch(
    `${getBase()}/v2/checkout/orders/${orderId}/capture`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
        "Content-Type": "application/json",
      },
      cache: "no-store",
    },
  );

  if (!response.ok) {
    const err = await response.text();
    throw new Error(`Capture PayPal echouee : ${response.status} ${err}`);
  }

  const data = await response.json();
  const capture = data.purchase_units?.[0]?.payments?.captures?.[0];

  return {
    status: data.status as string,
    captureId: capture?.id ?? null,
  };
}

export function isPayPalConfigured(): boolean {
  return (
    !!process.env.PAYPAL_CLIENT_ID?.trim()
    && !!process.env.PAYPAL_CLIENT_SECRET?.trim()
  );
}

export type PayPalEnvironment = "sandbox" | "live";

export function getPayPalMode(): PayPalEnvironment {
  return process.env.PAYPAL_MODE === "live" ? "live" : "sandbox";
}

export async function createPayPalProduct(
  name: string,
  description: string,
): Promise<string> {
  const token = await getPayPalAccessToken();
  const safeDescription = (description || name).slice(0, 256);

  const response = await fetch(`${getBase()}/v1/catalogs/products`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
      "PayPal-Request-Id": `prod-${Date.now()}-${Math.random()
        .toString(36)
        .slice(2, 10)}`,
    },
    body: JSON.stringify({
      name: name.slice(0, 127),
      description: safeDescription,
      type: "SERVICE",
      category: "SOFTWARE",
    }),
    cache: "no-store",
  });

  if (!response.ok) {
    const err = await response.text();
    throw new Error(`Creation produit PayPal echouee : ${response.status} ${err}`);
  }

  const data = (await response.json()) as { id?: string };
  if (!data.id) {
    throw new Error("PayPal n'a pas retourne d'identifiant de produit.");
  }
  return data.id;
}

type CreatePayPalPlanOptions = {
  billingIntervalMonths?: number;
  setupFeeAmountCents?: number;
};

export async function createPayPalPlan(
  productId: string,
  name: string,
  priceAmountCents: number,
  currency: string,
  options: CreatePayPalPlanOptions = {},
): Promise<string> {
  const token = await getPayPalAccessToken();
  const value = (priceAmountCents / 100).toFixed(2);
  const billingIntervalMonths = options.billingIntervalMonths ?? 1;
  const setupFeeAmountCents = options.setupFeeAmountCents ?? 0;

  const response = await fetch(`${getBase()}/v1/billing/plans`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
      "PayPal-Request-Id": `plan-${Date.now()}-${Math.random()
        .toString(36)
        .slice(2, 10)}`,
    },
    body: JSON.stringify({
      product_id: productId,
      name: name.slice(0, 127),
      status: "ACTIVE",
      billing_cycles: [
        {
          frequency: {
            interval_unit: "MONTH",
            interval_count: billingIntervalMonths,
          },
          tenure_type: "REGULAR",
          sequence: 1,
          total_cycles: 0,
          pricing_scheme: {
            fixed_price: {
              value,
              currency_code: currency,
            },
          },
        },
      ],
      payment_preferences: {
        auto_bill_outstanding: true,
        setup_fee_failure_action: "CONTINUE",
        payment_failure_threshold: 3,
        ...(setupFeeAmountCents > 0
          ? {
              setup_fee: {
                value: (setupFeeAmountCents / 100).toFixed(2),
                currency_code: currency,
              },
            }
          : {}),
      },
    }),
    cache: "no-store",
  });

  if (!response.ok) {
    const err = await response.text();
    throw new Error(`Creation plan PayPal echouee : ${response.status} ${err}`);
  }

  const data = (await response.json()) as { id?: string };
  if (!data.id) {
    throw new Error("PayPal n'a pas retourne d'identifiant de plan.");
  }
  return data.id;
}

export type CreateSubscriptionResult = {
  subscriptionId: string;
  approveUrl: string;
};

export async function cancelPayPalSubscription(
  paypalSubscriptionId: string,
  reason: string,
): Promise<void> {
  const token = await getPayPalAccessToken();

  const response = await fetch(
    `${getBase()}/v1/billing/subscriptions/${encodeURIComponent(
      paypalSubscriptionId,
    )}/cancel`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ reason }),
      cache: "no-store",
    },
  );

  if (response.status === 204) {
    return;
  }

  if (response.status === 422) {
    return;
  }

  const err = await response.text();
  throw new Error(
    `Annulation souscription PayPal echouee : ${response.status} ${err}`,
  );
}

export async function createPayPalSubscription(
  planId: string,
  subscriberEmail: string,
  returnUrl: string,
  cancelUrl: string,
): Promise<CreateSubscriptionResult> {
  const token = await getPayPalAccessToken();

  const response = await fetch(`${getBase()}/v1/billing/subscriptions`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      plan_id: planId,
      subscriber: {
        email_address: subscriberEmail,
      },
      application_context: {
        return_url: returnUrl,
        cancel_url: cancelUrl,
        user_action: "SUBSCRIBE_NOW",
      },
    }),
    cache: "no-store",
  });

  if (!response.ok) {
    const err = await response.text();
    throw new Error(
      `Creation souscription PayPal echouee : ${response.status} ${err}`,
    );
  }

  const data = await response.json();
  const approveLink = (data.links as Array<{ rel: string; href: string }>).find(
    (link) => link.rel === "approve",
  );

  if (!approveLink) {
    throw new Error(
      "Lien d'approbation PayPal introuvable pour la souscription.",
    );
  }

  return {
    subscriptionId: data.id as string,
    approveUrl: approveLink.href,
  };
}
