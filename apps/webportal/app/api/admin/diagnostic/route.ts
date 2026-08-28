import type { DiagnosticConfigurationAdminView } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminGet } from "@/lib/admin-bff";

export function GET(request: NextRequest) {
  return handleAdminGet<DiagnosticConfigurationAdminView>(
    request,
    "/internal/admin/diagnostic/configuration",
  );
}
