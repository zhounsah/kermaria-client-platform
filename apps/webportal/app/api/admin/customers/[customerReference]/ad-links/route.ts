import type {
  AdLinkMutationResponse,
  CustomerAdLinkPayload,
  CustomerAdLinkSummary,
} from "@kermaria/shared";
import { NextRequest } from "next/server";

import { parseCustomerAdLinkPayload } from "@/lib/bff-payloads";
import { controlledAdminError, handleAdminGet, handleAdminMutation } from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { isValidPortalIdentifier } from "@/lib/portal-bff";

type RouteContext = { params: Promise<{ customerReference: string }> };

export async function GET(request: NextRequest, context: RouteContext) {
  const { customerReference } = await context.params;
  if (!isValidPortalIdentifier(customerReference)) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "La ressource demandee est invalide.",
      resolveCorrelationId(request.headers.get(CORRELATION_HEADER)),
    );
  }

  return handleAdminGet<CustomerAdLinkSummary[]>(
    request,
    `/internal/admin/customers/${encodeURIComponent(customerReference)}/ad-links`,
  );
}

export async function POST(request: NextRequest, context: RouteContext) {
  const { customerReference } = await context.params;
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  if (!isValidPortalIdentifier(customerReference)) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "La ressource demandee est invalide.",
      correlationId,
    );
  }

  const payload = parseCustomerAdLinkPayload(await readJson(request));
  if (!payload) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Le lien Active Directory demande est invalide.",
      correlationId,
    );
  }

  return handleAdminMutation<CustomerAdLinkPayload, AdLinkMutationResponse>(
    request,
    `/internal/admin/customers/${encodeURIComponent(customerReference)}/ad-links`,
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
