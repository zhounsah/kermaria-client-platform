import { NextRequest } from "next/server";

import { handleAdminGet } from "@/lib/admin-bff";
import type { SignupAdminDetail } from "@/lib/internal-api";

type RouteContext = { params: Promise<{ id: string }> };

export async function GET(request: NextRequest, context: RouteContext) {
  const { id } = await context.params;
  return handleAdminGet<SignupAdminDetail>(
    request,
    `/internal/admin/signups/${encodeURIComponent(id)}`,
  );
}
