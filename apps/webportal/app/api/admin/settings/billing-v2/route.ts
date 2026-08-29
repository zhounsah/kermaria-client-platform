import type { BillingV2ConfigurationOverview } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminGet } from "@/lib/admin-bff";

export function GET(request: NextRequest) {
  return handleAdminGet<BillingV2ConfigurationOverview>(
    request,
    "/internal/admin/settings/billing-v2",
  );
}
