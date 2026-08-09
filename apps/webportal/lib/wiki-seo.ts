import "server-only";

import { headers } from "next/headers";

import {
  getWikiHostKind,
  resolveWikiCanonicalUrl,
} from "@/lib/public-route-config";

export async function getWikiRobots() {
  const headerList = await headers();
  const host = headerList.get("x-forwarded-host") ?? headerList.get("host");
  const kind = getWikiHostKind(host);
  return {
    canonicalHost: kind === "canonical",
    robots:
      kind === "internal"
        ? ({ index: false, follow: true } as const)
        : ({ index: true, follow: true } as const),
  };
}

export function wikiCanonical(pathname: string) {
  return resolveWikiCanonicalUrl(pathname);
}
