import "server-only";

import { createHmac, timingSafeEqual } from "node:crypto";

const REPLAY_TOLERANCE_SECONDS = 300;

export function isWebhookVerificationEnabled(): boolean {
  const raw = process.env.STRIPE_WEBHOOK_VERIFY?.trim().toLowerCase();
  if (raw === "false" || raw === "0" || raw === "off") {
    return false;
  }
  return true;
}

function getWebhookSecret(): string | null {
  const value = process.env.STRIPE_WEBHOOK_SECRET?.trim();
  return value && value.length > 0 ? value : null;
}

export type SignatureVerificationOutcome =
  | { ok: true; verified: boolean; reason?: string }
  | { ok: false; reason: string };

export function verifyStripeSignature(
  signatureHeader: string | null,
  rawBody: string,
): SignatureVerificationOutcome {
  const secret = getWebhookSecret();
  if (!secret) {
    return { ok: false, reason: "STRIPE_WEBHOOK_SECRET not configured." };
  }

  if (!signatureHeader) {
    return { ok: false, reason: "Missing Stripe-Signature header." };
  }

  const parts = signatureHeader.split(",").reduce<Record<string, string>>(
    (acc, part) => {
      const [key, value] = part.split("=");
      if (key && value) {
        acc[key.trim()] = value.trim();
      }
      return acc;
    },
    {},
  );

  const timestamp = parts.t;
  const expectedSignature = parts.v1;
  if (!timestamp || !expectedSignature) {
    return { ok: false, reason: "Malformed Stripe-Signature header." };
  }

  const timestampSeconds = Number.parseInt(timestamp, 10);
  if (
    !Number.isFinite(timestampSeconds)
    || Math.abs(Date.now() / 1000 - timestampSeconds) > REPLAY_TOLERANCE_SECONDS
  ) {
    return { ok: true, verified: false, reason: "Signature timestamp outside tolerance window." };
  }

  const signedPayload = `${timestamp}.${rawBody}`;
  const computedSignature = createHmac("sha256", secret)
    .update(signedPayload, "utf8")
    .digest("hex");

  const computedBuffer = Buffer.from(computedSignature, "hex");
  const expectedBuffer = Buffer.from(expectedSignature, "hex");
  const verified =
    computedBuffer.length === expectedBuffer.length
    && timingSafeEqual(computedBuffer, expectedBuffer);

  return { ok: true, verified, reason: verified ? undefined : "signature_mismatch" };
}
