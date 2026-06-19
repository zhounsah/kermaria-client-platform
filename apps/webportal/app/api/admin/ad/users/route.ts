import type { AdDirectoryObjectSummary } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { controlledAdminError, handleAdminGet } from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { isValidPortalIdentifier } from "@/lib/portal-bff";

export function GET(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const query = request.nextUrl.searchParams.get("query")?.trim() ?? "";
  const customerReference =
    request.nextUrl.searchParams.get("customerReference")?.trim() ?? null;

  if (
    !customerReference
    || query.length > 100
    || !isValidPortalIdentifier(customerReference)
  ) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "La recherche Active Directory est invalide.",
      correlationId,
    );
  }

  const params = new URLSearchParams();
  params.set("query", query);
  params.set("customerReference", customerReference);

  return handleAdminGet<AdDirectoryObjectSummary[]>(
    request,
    `/internal/admin/ad/users?${params.toString()}`,
  );
}
