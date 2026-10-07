import type { SitePageRevision } from "@kermaria/shared";
import { NextRequest } from "next/server";
import { handleAdminGet } from "@/lib/admin-bff";

export function GET(request: NextRequest) {
  const area = request.nextUrl.searchParams.get("area") ?? "";
  const pageKey = request.nextUrl.searchParams.get("pageKey") ?? "";
  return handleAdminGet<SitePageRevision[]>(request,
    `/internal/admin/page-layout/revisions?area=${encodeURIComponent(area)}&pageKey=${encodeURIComponent(pageKey)}`);
}
