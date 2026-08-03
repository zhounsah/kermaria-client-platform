import type { DemoAccountCreatedResponse } from "@kermaria/shared";
import { NextRequest } from "next/server";

import {
  controlledAdminError,
  handleAdminMutation,
} from "@/lib/admin-bff";
import { parseDemoAccountCreateRequest } from "@/lib/bff-payloads";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const payload = parseDemoAccountCreateRequest(await readJson(request));
  if (!payload) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Les informations du compte de démonstration sont invalides.",
      correlationId,
    );
  }

  return handleAdminMutation<typeof payload, DemoAccountCreatedResponse>(
    request,
    "/internal/admin/demo/accounts",
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
