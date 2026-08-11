import { NextResponse, type NextRequest } from "next/server";

import {
  getWikiHostKind,
  isClientOrAdminPortalHost,
  isPortalApplicationPath,
  isPublicRoute,
  resolveCanonicalPublicUrl,
  resolvePortalPublicRedirectUrl,
  resolveWikiRewritePath,
} from "@/lib/public-route-config";

const INTERNAL_API_TIMEOUT_MS = 3000;

type EditorialSlugResolution =
  | { kind: "missing" }
  | { kind: "present" }
  | { kind: "unknown" };

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

function getInternalServiceHeaders(): Record<string, string> {
  const token = process.env.SERVICE_AUTH_TOKEN?.trim();
  return token ? { "X-Service-Auth": token } : {};
}

function isEditorialSlugCandidate(pathname: string): boolean {
  if (
    isPublicRoute(pathname)
    || isPortalApplicationPath(pathname)
    || pathname !== `/${pathname.slice(1)}`
    || pathname.slice(1).includes("/")
    || pathname.includes(".")
  ) {
    return false;
  }

  return /^[a-z0-9_-]+$/.test(pathname.slice(1));
}

async function readInternalJsonOrNull(url: string): Promise<unknown> {
  const response = await fetch(url, {
    cache: "no-store",
    signal: AbortSignal.timeout(INTERNAL_API_TIMEOUT_MS),
    headers: {
      Accept: "application/json",
      ...getInternalServiceHeaders(),
    },
  });

  if (!response.ok) {
    throw new Error(`Internal editorial probe failed: ${response.status}`);
  }

  return response.json();
}

async function resolveEditorialSlug(
  pathname: string,
): Promise<EditorialSlugResolution> {
  if (!isEditorialSlugCandidate(pathname)) {
    return { kind: "unknown" };
  }

  const internalApiUrl = process.env.INTERNAL_API_URL?.trim()?.replace(/\/+$/, "");
  if (!internalApiUrl) {
    return { kind: "missing" };
  }

  try {
    const redirect = await readInternalJsonOrNull(
      `${internalApiUrl}/internal/public/editorial/redirects?oldPath=${
        encodeURIComponent(pathname)
      }`,
    );
    if (redirect && typeof redirect === "object" && "newPath" in redirect) {
      return { kind: "present" };
    }

    const page = await readInternalJsonOrNull(
      `${internalApiUrl}/internal/public/editorial/seo-pages/${
        encodeURIComponent(pathname.slice(1))
      }`,
    );
    return page ? { kind: "present" } : { kind: "missing" };
  } catch {
    return { kind: "unknown" };
  }
}

function notFoundResponse() {
  return new NextResponse(
    "<!doctype html><html lang=\"fr\"><head><meta charset=\"utf-8\"><meta name=\"robots\" content=\"noindex\"><title>Page introuvable | Zachary IT</title></head><body><main><p>Erreur 404</p><h1>Page introuvable</h1><p>Cette adresse ne correspond à aucune page publique disponible.</p><p><a href=\"/offres\">Voir les offres</a> · <a href=\"/contact\">Expliquer mon besoin</a></p></main></body></html>",
    {
      headers: { "content-type": "text/html; charset=utf-8" },
      status: 404,
    },
  );
}

export async function proxy(request: NextRequest) {
  const requestHost = getRequestHost(request);
  if (
    isClientOrAdminPortalHost(requestHost)
    && request.nextUrl.pathname === "/sitemap.xml"
  ) {
    return new NextResponse("Not found", {
      headers: { "content-type": "text/plain; charset=utf-8" },
      status: 404,
    });
  }

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

  const publicHostRedirectUrl = resolvePortalPublicRedirectUrl(
    requestHost,
    request.nextUrl.pathname,
    request.nextUrl.search,
  );
  if (publicHostRedirectUrl) {
    return NextResponse.redirect(publicHostRedirectUrl, 301);
  }

  if (
    !isClientOrAdminPortalHost(requestHost)
    && !getWikiHostKind(requestHost)
    && (await resolveEditorialSlug(request.nextUrl.pathname)).kind === "missing"
  ) {
    return notFoundResponse();
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
