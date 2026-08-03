import type { DemoProfileSummary } from "@kermaria/shared";
import { NextRequest } from "next/server";

import {
  controlledAdminError,
  handleAdminMutation,
} from "@/lib/admin-bff";
import { parseDemoProfilePayload } from "@/lib/bff-payloads";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const payload = parseDemoProfilePayload(await readJson(request));
  if (!payload) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Le profil de démonstration fourni est invalide.",
      correlationId,
    );
  }

  return handleAdminMutation<typeof payload, DemoProfileSummary>(
    request,
    "/internal/admin/demo/profiles",
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
