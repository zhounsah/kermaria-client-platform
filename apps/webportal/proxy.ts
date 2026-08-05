import { NextResponse, type NextRequest } from "next/server";

import { resolveCanonicalPublicUrl } from "@/lib/public-route-config";

function getRequestHost(request: NextRequest): string | null {
  const forwardedHost = request.headers
    .get("x-forwarded-host")
    ?.split(",")[0]
    ?.trim();

  return (
    forwardedHost
    || request.headers.get("host")?.trim()
    || request.nextUrl.host
    || null
  );
}

export function proxy(request: NextRequest) {
  // Un seul hote public repond en 200 : les alias (apex sans `www`) sont
  // rediriges en 301 avant tout rendu, chemin et query conserves.
  const canonicalUrl = resolveCanonicalPublicUrl(
    getRequestHost(request),
    request.nextUrl.pathname,
    request.nextUrl.search,
  );
  if (canonicalUrl) {
    return NextResponse.redirect(canonicalUrl, 301);
  }

  const requestHeaders = new Headers(request.headers);
  requestHeaders.set("x-pathname", request.nextUrl.pathname);
  return NextResponse.next({ request: { headers: requestHeaders } });
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"],
};
