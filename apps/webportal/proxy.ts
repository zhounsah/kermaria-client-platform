import { NextResponse, type NextRequest } from "next/server";

import {
  getWikiHostKind,
  resolveCanonicalPublicUrl,
  resolveWikiRewritePath,
} from "@/lib/public-route-config";

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
  const requestHost = getRequestHost(request);
  // Un seul hote public repond en 200 : les alias (apex sans `www`) sont
  // rediriges en 301 avant tout rendu, chemin et query conserves.
  const canonicalUrl = resolveCanonicalPublicUrl(
    requestHost,
    request.nextUrl.pathname,
    request.nextUrl.search,
  );
  if (canonicalUrl) {
    return NextResponse.redirect(canonicalUrl, 301);
  }

  const wikiHostKind = getWikiHostKind(requestHost);
  if (wikiHostKind) {
    if (
      request.nextUrl.pathname === "/robots.txt"
      || request.nextUrl.pathname === "/sitemap.xml"
    ) {
      const requestHeaders = new Headers(request.headers);
      requestHeaders.set("x-pathname", request.nextUrl.pathname);
      requestHeaders.set("x-wiki-host-kind", wikiHostKind);
      return NextResponse.next({ request: { headers: requestHeaders } });
    }

    const rewritePath = resolveWikiRewritePath(request.nextUrl.pathname);
    if (rewritePath) {
      const rewriteUrl = request.nextUrl.clone();
      rewriteUrl.pathname = rewritePath;
      const requestHeaders = new Headers(request.headers);
      requestHeaders.set("x-pathname", request.nextUrl.pathname);
      requestHeaders.set("x-wiki-host-kind", wikiHostKind);
      return NextResponse.rewrite(rewriteUrl, {
        request: { headers: requestHeaders },
      });
    }
  }

  const requestHeaders = new Headers(request.headers);
  requestHeaders.set("x-pathname", request.nextUrl.pathname);
  return NextResponse.next({ request: { headers: requestHeaders } });
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"],
};
