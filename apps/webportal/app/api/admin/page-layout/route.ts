import type { SitePageLayout } from "@kermaria/shared";
import { NextRequest } from "next/server";
import { handleAdminGet, handleAdminMutation } from "@/lib/admin-bff";

export function GET(request: NextRequest) {
  const area = request.nextUrl.searchParams.get("area") ?? "";
  const pageKey = request.nextUrl.searchParams.get("pageKey") ?? "";
  return handleAdminGet<SitePageLayout>(request,
    `/internal/admin/page-layout?area=${encodeURIComponent(area)}&pageKey=${encodeURIComponent(pageKey)}`);
}

export async function POST(request: NextRequest) {
  const payload: unknown = await request.json().catch(() => null);
  return handleAdminMutation<unknown, SitePageLayout>(request, "/internal/admin/page-layout", "POST", payload);
}
