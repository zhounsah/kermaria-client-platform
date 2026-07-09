import type { CheckoutRecurringConfirmResponse } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handlePortalPayloadMutationTyped } from "@/lib/portal-bff";

export async function POST(request: NextRequest) {
  return handlePortalPayloadMutationTyped<
    CheckoutRecurringConfirmResponse,
    undefined
  >(
    request,
    "/internal/portal/checkout/subscriptions/confirm",
    undefined,
  );
}
