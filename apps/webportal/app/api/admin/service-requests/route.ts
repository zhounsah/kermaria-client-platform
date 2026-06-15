import type { AdminServiceRequestSummary } from "@kermaria/shared";
import { NextRequest } from "next/server";

import {
  buildAdminRequestListPath,
  handleAdminGet,
} from "@/lib/admin-bff";

export function GET(request: NextRequest) {
  const target = buildAdminRequestListPath(request, "service");
  if ("response" in target) {
    return target.response;
  }

  return handleAdminGet<AdminServiceRequestSummary[]>(
    request,
    target.path,
  );
}
