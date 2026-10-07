import type { SitePageLayout } from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";
import { handleAdminGet } from "@/lib/admin-bff";
import { handlePortalGet } from "@/lib/portal-bff";
import { getPublicSitePageLayout } from "@/lib/internal-api";

export const dynamic = "force-dynamic";

export async function GET(request: NextRequest) {
  const area = request.nextUrl.searchParams.get("area");
  const pageKey = request.nextUrl.searchParams.get("pageKey") ?? "";
  if (!/^\/[a-z0-9/_\[\].-]*$/.test(pageKey) || pageKey.length > 200)
    return NextResponse.json({ code: "INVALID_REQUEST", message: "Page invalide." }, { status: 400 });
  const path = `/internal/page-layout?area=${area}&pageKey=${encodeURIComponent(pageKey)}`;
  if (area === "admin") return handleAdminGet<SitePageLayout>(request, path);
  if (area === "client") return handlePortalGet<SitePageLayout>(request, path);
  if (area !== "public") return NextResponse.json({ code: "INVALID_REQUEST", message: "Espace invalide." }, { status: 400 });
  const result = await getPublicSitePageLayout(pageKey);
  return NextResponse.json(result.data);
}
