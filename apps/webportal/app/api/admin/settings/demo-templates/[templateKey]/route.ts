import type { DemoContentTemplateMutationResponse } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminMutation } from "@/lib/admin-bff";
import { invalidCommunicationRequest } from "@/lib/admin-communications-bff";

export async function DELETE(
  request: NextRequest,
  context: { params: Promise<{ templateKey: string }> },
) {
  const { templateKey } = await context.params;
  const expectedVersion = Number(
    request.nextUrl.searchParams.get("expectedVersion"),
  );
  if (
    !/^[a-z0-9][a-z0-9-]{1,63}$/.test(templateKey)
    || !Number.isInteger(expectedVersion)
    || expectedVersion < 0
  ) {
    return invalidCommunicationRequest(request);
  }
  return handleAdminMutation<undefined, DemoContentTemplateMutationResponse>(
    request,
    `/internal/admin/settings/demo-templates/${encodeURIComponent(templateKey)}`
      + `?expectedVersion=${expectedVersion}`,
    "DELETE",
  );
}
