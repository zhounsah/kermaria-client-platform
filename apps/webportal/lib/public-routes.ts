import "server-only";

import { headers } from "next/headers";

export const PUBLIC_ROUTES = [
  "/",
  "/offres",
  "/a-propos",
  "/contact",
  "/mentions-legales",
  "/politique-confidentialite",
  "/cgv",
] as const;

// Portfolio statique copié sous apps/webportal/public/portfolio/.
// Le `.html` explicite garde le bon contexte de directory pour que les
// liens relatifs du portfolio (projects/, contact.html, etc.) résolvent.
export const PORTFOLIO_URL = "/portfolio/index.html";

export function isPublicRoute(pathname: string | null | undefined): boolean {
  if (!pathname) {
    return false;
  }

  if (pathname === "/") {
    return true;
  }

  return PUBLIC_ROUTES.some(
    (route) =>
      route !== "/" && (pathname === route || pathname.startsWith(`${route}/`)),
  );
}

export async function getCurrentPathname(): Promise<string | null> {
  const headersList = await headers();
  return headersList.get("x-pathname");
}

export function isVitrinePublicEnabled(): boolean {
  return process.env.PUBLIC_VITRINE_ENABLED?.trim().toLowerCase() === "true";
}

export function isSignupEnabled(): boolean {
  return process.env.SIGNUP_ENABLED?.trim().toLowerCase() === "true";
}

export function getPortalPublicUrl(): string {
  const fromEnv = process.env.PUBLIC_PORTAL_URL?.trim();
  if (fromEnv) {
    return fromEnv.replace(/\/+$/, "");
  }
  return "http://localhost:3000";
}
