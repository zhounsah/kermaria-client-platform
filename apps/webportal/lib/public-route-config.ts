export const PUBLIC_ROUTES = [
  "/",
  "/offres",
  "/a-propos",
  "/contact",
  "/mentions-legales",
  "/politique-confidentialite",
  "/cgv",
  "/signup",
  "/set-password",
] as const;

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
