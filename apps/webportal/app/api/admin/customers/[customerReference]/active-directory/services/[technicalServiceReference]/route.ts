import type {
  CorrelationId,
  CustomerAdProvisioningMutationPayload,
  CustomerAdProvisioningMutationResponse,
} from "@kermaria/shared";
import { NextRequest } from "next/server";

import { controlledAdminError, handleAdminMutation } from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { isValidPortalIdentifier } from "@/lib/portal-bff";

type RouteContext = {
  params: Promise<{
    customerReference: string;
    technicalServiceReference: string;
  }>;
};

export async function POST(request: NextRequest, context: RouteContext) {
  const { customerReference, technicalServiceReference } = await context.params;
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );

  if (!isValidPortalIdentifier(customerReference)) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "La ressource demandée est invalide.",
      correlationId,
    );
  }

  if (!/^[A-Za-z0-9._-]{1,100}$/.test(technicalServiceReference)) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Le service technique demandé est invalide.",
      correlationId,
    );
  }

  const payload = await parsePayload(request, correlationId);
  if ("status" in payload) {
    return payload;
  }

  return handleAdminMutation<
    CustomerAdProvisioningMutationPayload,
    CustomerAdProvisioningMutationResponse
  >(
    request,
    `/internal/admin/customers/${encodeURIComponent(customerReference)}/active-directory/services/${encodeURIComponent(technicalServiceReference)}`,
    "POST",
    payload,
  );
}

async function parsePayload(
  request: NextRequest,
  correlationId: CorrelationId,
) {
  const body = await request.json().catch(() => null);
  if (!body || typeof body !== "object" || Array.isArray(body)) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Le payload de provisionning est invalide.",
      correlationId,
    );
  }

  const payload = body as Partial<CustomerAdProvisioningMutationPayload>;
  if (payload.operation !== "activate" && payload.operation !== "remove") {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "L'opération demandée est invalide.",
      correlationId,
    );
  }

  if (
    payload.targetUserSamAccountNames !== undefined
    && payload.targetUserSamAccountNames !== null
    && (!Array.isArray(payload.targetUserSamAccountNames)
      || !payload.targetUserSamAccountNames.every((value) =>
        typeof value === "string"
        && /^[A-Za-z0-9._-]{1,64}$/.test(value.trim())))
  ) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "La sélection d'utilisateurs AD est invalide.",
      correlationId,
    );
  }

  if (
    payload.subscriptionId !== undefined
    && payload.subscriptionId !== null
    && !/^[A-Za-z0-9-]{1,100}$/.test(payload.subscriptionId)
  ) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Le contexte d'abonnement est invalide.",
      correlationId,
    );
  }

  return {
    operation: payload.operation,
    targetUserSamAccountNames:
      payload.targetUserSamAccountNames?.map((value) => value.trim())
        .filter(Boolean)
      ?? null,
    override: payload.override ?? false,
    subscriptionId: payload.subscriptionId ?? null,
  } satisfies CustomerAdProvisioningMutationPayload;
}
