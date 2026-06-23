import { NextRequest } from "next/server";

import { controlledAdminError, handleAdminGet } from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";

type BpceIssuedInvoiceInfo = {
  bpceInvoiceId: string;
  fiscalNumber: string | null;
  status: string;
  issueDate: string;
  totalAmountCents: number;
  currency: string;
  pdfAvailable: boolean;
};

type RouteContext = { params: Promise<{ id: string }> };

export async function GET(request: NextRequest, context: RouteContext) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { id } = await context.params;
  if (!/^[A-Za-z0-9-]{1,100}$/.test(id)) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "L'identifiant demandé est invalide.",
      correlationId,
    );
  }

  return handleAdminGet<BpceIssuedInvoiceInfo>(
    request,
    `/internal/admin/commercial-documents/${encodeURIComponent(id)}/invoice`,
  );
}
