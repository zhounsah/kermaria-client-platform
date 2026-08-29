import type { FiscalPolicyAdminView } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminGet } from "@/lib/admin-bff";

export function GET(request: NextRequest) {
  return handleAdminGet<FiscalPolicyAdminView>(
    request,
    "/internal/admin/settings/fiscal-policy",
  );
}
