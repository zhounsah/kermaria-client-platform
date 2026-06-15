import type { AdminSupportRequestSummary } from "@kermaria/shared";
import { NextRequest } from "next/server";

import {
  buildAdminRequestListPath,
  handleAdminGet,
} from "@/lib/admin-bff";

export function GET(request: NextRequest) {
  const target = buildAdminRequestListPath(request, "support");
  if ("response" in target) {
    return target.response;
  }

  return handleAdminGet<AdminSupportRequestSummary[]>(
    request,
    target.path,
  );
}
