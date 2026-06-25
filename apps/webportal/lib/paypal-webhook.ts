import "server-only";

import { getPayPalAccessToken } from "@/lib/paypal";

const SANDBOX_BASE = "https://api-m.sandbox.paypal.com";
const LIVE_BASE = "https://api-m.paypal.com";

function getBase() {
  return process.env.PAYPAL_MODE === "live" ? LIVE_BASE : SANDBOX_BASE;
}

export function getPayPalWebhookId(): string | null {
  const value = process.env.PAYPAL_WEBHOOK_ID?.trim();
  return value && value.length > 0 ? value : null;
}

export function isWebhookVerificationEnabled(): boolean {
  const raw = process.env.PAYPAL_WEBHOOK_VERIFY?.trim().toLowerCase();
  if (raw === "false" || raw === "0" || raw === "off") {
    return false;
  }
  return true;
}

type WebhookHeaderMap = {
  authAlgo: string | null;
  certUrl: string | null;
  transmissionId: string | null;
  transmissionSig: string | null;
  transmissionTime: string | null;
};

export function extractWebhookHeaders(
  headers: Headers,
): WebhookHeaderMap {
  return {
    authAlgo: headers.get("paypal-auth-algo"),
    certUrl: headers.get("paypal-cert-url"),
    transmissionId: headers.get("paypal-transmission-id"),
    transmissionSig: headers.get("paypal-transmission-sig"),
    transmissionTime: headers.get("paypal-transmission-time"),
  };
}

export type WebhookVerificationOutcome =
  | { ok: true; verified: boolean; reason?: string }
  | { ok: false; reason: string };

export async function verifyPayPalWebhookSignature(
  webhookHeaders: WebhookHeaderMap,
  rawBody: string,
): Promise<WebhookVerificationOutcome> {
  const webhookId = getPayPalWebhookId();
  if (!webhookId) {
    return { ok: false, reason: "PAYPAL_WEBHOOK_ID not configured." };
  }

  if (
    !webhookHeaders.authAlgo
    || !webhookHeaders.certUrl
    || !webhookHeaders.transmissionId
    || !webhookHeaders.transmissionSig
    || !webhookHeaders.transmissionTime
  ) {
    return { ok: false, reason: "Missing PayPal signature headers." };
  }

  let parsedBody: unknown;
  try {
    parsedBody = JSON.parse(rawBody);
  } catch {
    return { ok: false, reason: "Webhook body is not valid JSON." };
  }

  const token = await getPayPalAccessToken();
  const response = await fetch(
    `${getBase()}/v1/notifications/verify-webhook-signature`,
    {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        auth_algo: webhookHeaders.authAlgo,
        cert_url: webhookHeaders.certUrl,
        transmission_id: webhookHeaders.transmissionId,
        transmission_sig: webhookHeaders.transmissionSig,
        transmission_time: webhookHeaders.transmissionTime,
        webhook_id: webhookId,
        webhook_event: parsedBody,
      }),
      cache: "no-store",
    },
  );

  if (!response.ok) {
    return {
      ok: false,
      reason: `PayPal verify endpoint returned ${response.status}.`,
    };
  }

  const data = (await response.json()) as { verification_status?: string };
  const verified = data.verification_status === "SUCCESS";
  return {
    ok: true,
    verified,
    reason: verified ? undefined : `verification_status=${data.verification_status}`,
  };
}
