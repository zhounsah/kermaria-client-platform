import type { AdminSupportRequestDetail } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminGet } from "@/lib/admin-bff";

type RouteContext = { params: Promise<{ id: string }> };

export async function GET(request: NextRequest, context: RouteContext) {
  const { id } = await context.params;
  return handleAdminGet<AdminSupportRequestDetail>(
    request,
    `/internal/admin/support-requests/${encodeURIComponent(id)}`,
  );
}
