import type { PortalNotificationSummary } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handlePortalGet } from "@/lib/portal-bff";

export const dynamic = "force-dynamic";

export function GET(request: NextRequest) {
  return handlePortalGet<PortalNotificationSummary[]>(
    request,
    "/internal/portal/notifications",
  );
}
