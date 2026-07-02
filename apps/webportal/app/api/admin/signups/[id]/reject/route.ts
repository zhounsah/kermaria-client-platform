import { NextRequest } from "next/server";

import { handleAdminMutation } from "@/lib/admin-bff";

type RouteContext = { params: Promise<{ id: string }> };

export async function POST(request: NextRequest, context: RouteContext) {
  const { id } = await context.params;
  const reason = await readReason(request);
  return handleAdminMutation(
    request,
    `/internal/admin/signups/${encodeURIComponent(id)}/reject`,
    "POST",
    { reason },
  );
}

async function readReason(request: NextRequest): Promise<string | null> {
  try {
    const body = (await request.json()) as { reason?: unknown };
    if (typeof body.reason === "string") {
      const trimmed = body.reason.trim();
      return trimmed ? trimmed.slice(0, 500) : null;
    }
  } catch {
    // Corps absent ou invalide : refus sans motif.
  }
  return null;
}
