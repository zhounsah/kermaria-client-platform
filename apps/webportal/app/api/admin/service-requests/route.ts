import type { AdminServiceRequestSummary } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminGet } from "@/lib/admin-bff";

export function GET(request: NextRequest) {
  return handleAdminGet<AdminServiceRequestSummary[]>(
    request,
    `/internal/admin/service-requests${request.nextUrl.search}`,
  );
}
