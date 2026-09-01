import type { BillingV2VpsManualProvisioningPayload } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { controlledAdminError, handleAdminMutation } from "@/lib/admin-bff";
import { parseBillingV2VpsManualProvisioningPayload } from "@/lib/bff-payloads";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";

export async function POST(
  request: NextRequest,
  context: { params: Promise<{ id: string }> },
) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { id } = await context.params;
  if (!isTechnicalRequestId(id)) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Identifiant de demande VPS invalide.",
      correlationId,
    );
  }

  const payload = parseBillingV2VpsManualProvisioningPayload(
    await readJsonSafely(request),
  );
  if (!payload) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Les métadonnées de mise en service sont invalides.",
      correlationId,
    );
  }

  return handleAdminMutation<
    BillingV2VpsManualProvisioningPayload,
    Record<string, unknown>
  >(
    request,
    `/internal/admin/billing-v2/vps/technical-reviews/${encodeURIComponent(id)}/manual-provisioning`,
    "POST",
    payload,
  );
}

function isTechnicalRequestId(value: string) {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
}

async function readJsonSafely(request: NextRequest): Promise<unknown> {
  try {
    return await request.json();
  } catch {
    return null;
  }
}
