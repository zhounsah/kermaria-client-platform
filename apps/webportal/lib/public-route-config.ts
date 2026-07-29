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

export const PORTFOLIO_URL = "https://portfolio.zacharyhounsa.ovh/";

export type PortalArea = "public" | "client" | "admin";

type PortalHostMapping = {
  hosts: string[];
  public: string;
  client: string;
  admin: string;
};

const PORTAL_HOST_MAPPINGS: PortalHostMapping[] = [
  {
    hosts: [
      "www.zacharyhounsa.ovh",
      "zacharyhounsa.ovh",
      "dashboard.zacharyhounsa.ovh",
      "administration.zacharyhounsa.ovh",
    ],
    public: "www.zacharyhounsa.ovh",
    client: "dashboard.zacharyhounsa.ovh",
    admin: "administration.zacharyhounsa.ovh",
  },
  {
    hosts: [
      "www.home.bzh",
      "home.bzh",
      "portail.home.bzh",
      "dashboard.home.bzh",
      "administration.home.bzh",
    ],
    public: "www.home.bzh",
    client: "dashboard.home.bzh",
    admin: "administration.home.bzh",
  },
];

function normalizeAbsoluteUrl(value: string): string | null {
  try {
    const url = new URL(value);
    if (!["http:", "https:"].includes(url.protocol)) {
      return null;
    }
    return url.toString();
  } catch {
    return null;
  }
}

export function resolvePortalAreaUrl(
  currentUrl: string,
  area: PortalArea,
  pathname = "/",
): string {
  const normalized = normalizeAbsoluteUrl(currentUrl);
  if (!normalized) {
    return pathname;
  }

  const url = new URL(normalized);
  const mapping = PORTAL_HOST_MAPPINGS.find((candidate) =>
    candidate.hosts.includes(url.hostname.toLowerCase()),
  );

  if (mapping) {
    url.hostname = mapping[area];
  }

  return new URL(pathname, url).toString();
}

export function resolvePortalRoleUrl(
  currentUrl: string,
  role: "client_user" | "internal_admin",
  pathname?: string,
): string {
  return resolvePortalAreaUrl(
    currentUrl,
    role === "internal_admin" ? "admin" : "client",
    pathname ?? (role === "internal_admin" ? "/admin" : "/dashboard"),
  );
}

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
