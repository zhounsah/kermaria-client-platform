import type { EditorialRevisionDetail } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminGet } from "@/lib/admin-bff";

type RouteContext = { params: Promise<{ revisionId: string }> };

export async function GET(request: NextRequest, context: RouteContext) {
  const { revisionId } = await context.params;
  return handleAdminGet<EditorialRevisionDetail>(
    request,
    `/internal/admin/editorial/revisions/${encodeURIComponent(revisionId)}`,
  );
}
