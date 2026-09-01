import { NextRequest } from "next/server";

import { controlledAdminError, handleAdminMutation } from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";

export async function POST(
  request: NextRequest,
  context: { params: Promise<{ id: string }> },
) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { id } = await context.params;
  if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(id)) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Identifiant de demande VPS invalide.",
      correlationId,
    );
  }

  return handleAdminMutation<undefined, Record<string, unknown>>(
    request,
    `/internal/admin/billing-v2/vps/technical-reviews/${encodeURIComponent(id)}/manual-provisioning/activate`,
    "POST",
  );
}
