import type { IntegrationsOverview } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminGet } from "@/lib/admin-bff";

export function GET(request: NextRequest) {
  return handleAdminGet<IntegrationsOverview>(
    request,
    "/internal/admin/settings/integrations",
  );
}
