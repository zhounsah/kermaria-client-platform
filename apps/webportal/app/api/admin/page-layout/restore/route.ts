import type { SitePageLayout } from "@kermaria/shared";
import { NextRequest } from "next/server";
import { handleAdminMutation } from "@/lib/admin-bff";

export async function POST(request: NextRequest) {
  const payload: unknown = await request.json().catch(() => null);
  return handleAdminMutation<unknown, SitePageLayout>(request,
    "/internal/admin/page-layout/restore", "POST", payload);
}
