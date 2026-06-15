import type { CommercialDocumentSummary } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handlePortalGet } from "@/lib/portal-bff";

export function GET(request: NextRequest) {
  return handlePortalGet<CommercialDocumentSummary[]>(
    request,
    "/internal/portal/commercial-documents",
  );
}
