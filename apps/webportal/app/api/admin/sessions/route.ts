import type { AdminSessionSummary } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminGet } from "@/lib/admin-bff";

export function GET(request: NextRequest) {
  return handleAdminGet<AdminSessionSummary[]>(
    request,
    "/internal/admin/sessions",
  );
}
