import type { DirectoryOverview } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminGet } from "@/lib/admin-bff";

export function GET(request: NextRequest) {
  return handleAdminGet<DirectoryOverview>(
    request,
    "/internal/admin/settings/directory",
  );
}
