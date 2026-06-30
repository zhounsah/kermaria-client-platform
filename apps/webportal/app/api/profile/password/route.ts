import type {
  PortalPasswordChangePayload,
  PortalPasswordChangeResponse,
} from "@kermaria/shared";
import { NextRequest } from "next/server";

import {
  controlledPortalError,
  handlePortalPayloadMutation,
} from "@/lib/portal-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const payload = parsePayload(await readJson(request));
  if (!payload) {
    return controlledPortalError(
      400,
      "INVALID_REQUEST",
      "Le mot de passe actuel et le nouveau mot de passe sont obligatoires.",
      correlationId,
    );
  }

  return handlePortalPayloadMutation<PortalPasswordChangePayload>(
    request,
    "/internal/profile/password",
    payload,
  );
}

function parsePayload(value: unknown): PortalPasswordChangePayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<PortalPasswordChangePayload>;
  if (
    typeof candidate.currentPassword !== "string"
    || typeof candidate.newPassword !== "string"
  ) {
    return null;
  }

  const payload: PortalPasswordChangePayload = {
    currentPassword: candidate.currentPassword,
    newPassword: candidate.newPassword,
  };

  if (
    payload.currentPassword.length === 0
    || payload.currentPassword.length > 1024
    || payload.newPassword.length === 0
    || payload.newPassword.length > 1024
    || payload.currentPassword === payload.newPassword
  ) {
    return null;
  }

  return payload;
}

async function readJson(request: NextRequest) {
  try {
    return await request.json();
  } catch {
    return null;
  }
}

// Type guard kept for IDE clarity; the response shape comes from API-INTERNAL.
export type { PortalPasswordChangeResponse };
