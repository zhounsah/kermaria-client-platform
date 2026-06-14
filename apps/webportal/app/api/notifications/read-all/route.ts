import { NextRequest } from "next/server";

import { handlePortalMutation } from "@/lib/portal-bff";

export function POST(request: NextRequest) {
  return handlePortalMutation(
    request,
    "/internal/portal/notifications/read-all",
  );
}
