import type { FiscalPolicyMutationResponse } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminMutation } from "@/lib/admin-bff";
import { invalidCommunicationRequest } from "@/lib/admin-communications-bff";

export async function DELETE(
  request: NextRequest,
  context: { params: Promise<{ id: string }> },
) {
  const { id } = await context.params;
  if (!/^[0-9a-fA-F-]{36}$/.test(id)) {
    return invalidCommunicationRequest(request);
  }
  return handleAdminMutation<undefined, FiscalPolicyMutationResponse>(
    request,
    `/internal/admin/settings/fiscal-policy/mentions/${encodeURIComponent(id)}`,
    "DELETE",
  );
}
