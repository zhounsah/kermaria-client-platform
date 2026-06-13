import type { AdminCustomerSummary } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminGet } from "@/lib/admin-bff";

export function GET(request: NextRequest) {
  return handleAdminGet<AdminCustomerSummary[]>(
    request,
    "/internal/admin/customers",
  );
}
