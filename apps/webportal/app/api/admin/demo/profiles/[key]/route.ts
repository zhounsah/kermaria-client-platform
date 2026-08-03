import { NextRequest } from "next/server";

import { handleAdminMutation } from "@/lib/admin-bff";

type DeleteResponse = { deleted: boolean };

export async function DELETE(
  request: NextRequest,
  context: { params: Promise<{ key: string }> },
) {
  const { key } = await context.params;
  return handleAdminMutation<undefined, DeleteResponse>(
    request,
    `/internal/admin/demo/profiles/${encodeURIComponent(key)}`,
    "DELETE",
  );
}
