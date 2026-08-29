import type { RuntimeOverview } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminGet } from "@/lib/admin-bff";

export function GET(request: NextRequest) {
  return handleAdminGet<RuntimeOverview>(
    request,
    "/internal/admin/settings/runtime",
  );
}
