import { NextRequest } from "next/server";
import { handleAdminMutation } from "@/lib/admin-bff";

export async function POST(
  request: NextRequest,
  context: { params: Promise<{ customerId: string }> },
) {
  const { customerId } = await context.params;
  return handleAdminMutation(
    request,
    `/internal/admin/billing-v2/provisioning-readiness/${encodeURIComponent(customerId)}/review`,
    "POST",
  );
}
