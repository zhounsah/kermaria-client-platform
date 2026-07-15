import type { PortalDownloadCategory } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handlePortalGet } from "@/lib/portal-bff";

export function GET(request: NextRequest) {
  return handlePortalGet<PortalDownloadCategory[]>(
    request,
    "/internal/portal/downloads",
  );
}
