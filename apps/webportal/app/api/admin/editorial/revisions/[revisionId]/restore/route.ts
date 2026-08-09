import type { EditorialMutationResponse } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminMutation } from "@/lib/admin-bff";

type RouteContext = { params: Promise<{ revisionId: string }> };

export async function POST(request: NextRequest, context: RouteContext) {
  const { revisionId } = await context.params;
  return handleAdminMutation<undefined, EditorialMutationResponse>(
    request,
    `/internal/admin/editorial/revisions/${encodeURIComponent(revisionId)}/restore`,
    "POST",
  );
}
