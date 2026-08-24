import { NextRequest } from "next/server";

import { handleAdminGet } from "@/lib/admin-bff";

export function GET(request: NextRequest) {
  return handleAdminGet(
    request,
    "/internal/admin/billing-v2/catalog/providers",
  );
}
