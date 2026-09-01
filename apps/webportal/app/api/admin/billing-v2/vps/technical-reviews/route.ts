import { NextRequest } from "next/server";

import { handleAdminGet } from "@/lib/admin-bff";

export function GET(request: NextRequest) {
  return handleAdminGet<Record<string, unknown>[]>(
    request,
    "/internal/admin/billing-v2/vps/technical-reviews",
  );
}
