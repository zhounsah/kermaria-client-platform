import { NextRequest } from "next/server";

import { handlePortalMutation } from "@/lib/portal-bff";

export async function POST(request: NextRequest) {
  return handlePortalMutation(request, "/internal/portal/cart/confirm");
}
