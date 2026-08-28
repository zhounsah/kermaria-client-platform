import type { DiagnosticConfigurationRevisionsResponse } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminGet } from "@/lib/admin-bff";

export function GET(request: NextRequest) {
  return handleAdminGet<DiagnosticConfigurationRevisionsResponse>(
    request,
    "/internal/admin/diagnostic/revisions",
  );
}
