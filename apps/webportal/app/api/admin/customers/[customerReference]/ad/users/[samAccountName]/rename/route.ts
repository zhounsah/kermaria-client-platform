import type { AdMutationResponse, AdUserRenamePayload } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { controlledAdminError, handleAdminMutation } from "@/lib/admin-bff";
import { parseAdUserRenamePayload } from "@/lib/bff-payloads";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { isValidPortalIdentifier } from "@/lib/portal-bff";

type RouteContext = {
  params: Promise<{ customerReference: string; samAccountName: string }>;
};

export async function POST(request: NextRequest, context: RouteContext) {
  const { customerReference, samAccountName } = await context.params;
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  if (
    !isValidPortalIdentifier(customerReference)
    || !/^[A-Za-z0-9._-]{1,64}$/.test(samAccountName)
  ) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "La ressource demandee est invalide.",
      correlationId,
    );
  }

  const payload = parseAdUserRenamePayload(await readJson(request));
  if (!payload) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Le renommage Active Directory demande est invalide.",
      correlationId,
    );
  }

  return handleAdminMutation<AdUserRenamePayload, AdMutationResponse>(
    request,
    `/internal/admin/customers/${encodeURIComponent(customerReference)}/ad/users/${encodeURIComponent(samAccountName)}/rename`,
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
