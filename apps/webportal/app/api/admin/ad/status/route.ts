import type { AdminAdStatus } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminGet } from "@/lib/admin-bff";

export function GET(request: NextRequest) {
  return handleAdminGet<AdminAdStatus>(
    request,
    "/internal/admin/ad/status",
  );
}
