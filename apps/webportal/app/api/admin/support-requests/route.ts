import type { AdminSupportRequestSummary } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminGet } from "@/lib/admin-bff";

export function GET(request: NextRequest) {
  return handleAdminGet<AdminSupportRequestSummary[]>(
    request,
    `/internal/admin/support-requests${request.nextUrl.search}`,
  );
}
