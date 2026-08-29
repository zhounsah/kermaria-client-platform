import type { SettingsPermissionOverview } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminGet } from "@/lib/admin-bff";

export function GET(request: NextRequest) {
  return handleAdminGet<SettingsPermissionOverview>(
    request,
    "/internal/admin/settings/permissions",
  );
}
