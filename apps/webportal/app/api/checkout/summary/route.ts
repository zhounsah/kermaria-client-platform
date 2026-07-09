import { NextRequest } from "next/server";

import { handlePortalGet } from "@/lib/portal-bff";

export async function GET(request: NextRequest) {
  return handlePortalGet(request, "/internal/portal/checkout/summary");
}
