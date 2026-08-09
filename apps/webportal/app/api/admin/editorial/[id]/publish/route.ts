import type { EditorialMutationResponse } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminMutation } from "@/lib/admin-bff";

type RouteContext = { params: Promise<{ id: string }> };

export async function POST(request: NextRequest, context: RouteContext) {
  const { id } = await context.params;
  return handleAdminMutation<undefined, EditorialMutationResponse>(
    request,
    `/internal/admin/editorial/${encodeURIComponent(id)}/publish`,
    "POST",
  );
}
