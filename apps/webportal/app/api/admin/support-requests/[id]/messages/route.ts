import { NextRequest } from "next/server";

import {
  controlledAdminError,
  handleAdminMutation,
} from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { parseRequestTextPayload } from "@/lib/workflow-payloads";

type RouteContext = { params: Promise<{ id: string }> };

export async function POST(request: NextRequest, context: RouteContext) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const payload = parseRequestTextPayload(await readJson(request));
  if (!payload) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Le message doit contenir entre 3 et 2 000 caractères.",
      correlationId,
    );
  }

  const { id } = await context.params;
  return handleAdminMutation(
    request,
    `/internal/admin/support-requests/${encodeURIComponent(id)}/messages`,
    "POST",
    payload,
  );
}

async function readJson(request: NextRequest) {
  try {
    return await request.json();
  } catch {
    return null;
  }
}
