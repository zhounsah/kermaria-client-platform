import type { AdminAuditLogEntry } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminGet } from "@/lib/admin-bff";

export function GET(request: NextRequest) {
  return handleAdminGet<AdminAuditLogEntry[]>(
    request,
    "/internal/admin/audit-logs",
  );
}
