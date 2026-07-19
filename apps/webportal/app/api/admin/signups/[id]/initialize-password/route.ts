import { NextRequest } from "next/server";

import { handleAdminMutation } from "@/lib/admin-bff";

type RouteContext = { params: Promise<{ id: string }> };

export async function POST(request: NextRequest, context: RouteContext) {
  const { id } = await context.params;
  const password = await readPassword(request);
  return handleAdminMutation(
    request,
    `/internal/admin/signups/${encodeURIComponent(id)}/initialize-password`,
    "POST",
    { password },
  );
}

async function readPassword(request: NextRequest): Promise<string | null> {
  try {
    const body = (await request.json()) as { password?: unknown };
    return typeof body.password === "string" ? body.password : null;
  } catch {
    return null;
  }
}
