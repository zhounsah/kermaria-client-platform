import type { CartSummary } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handlePortalGet } from "@/lib/portal-bff";

export async function GET(request: NextRequest) {
  return handlePortalGet<CartSummary>(request, "/internal/portal/cart");
}
