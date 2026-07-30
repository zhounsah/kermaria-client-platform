import { NextRequest } from "next/server";

import { handleAdminGet } from "@/lib/admin-bff";
import type { KoxoAdminDashboard } from "@/lib/internal-api";

export function GET(request: NextRequest) {
  return handleAdminGet<KoxoAdminDashboard | null>(
    request,
    "/internal/admin/koxo",
  );
}
