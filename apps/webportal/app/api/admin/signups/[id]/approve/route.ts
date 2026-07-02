import { NextRequest } from "next/server";

import { handleAdminMutation } from "@/lib/admin-bff";

type RouteContext = { params: Promise<{ id: string }> };

export async function POST(request: NextRequest, context: RouteContext) {
  const { id } = await context.params;
  return handleAdminMutation(
    request,
    `/internal/admin/signups/${encodeURIComponent(id)}/approve`,
    "POST",
  );
}
