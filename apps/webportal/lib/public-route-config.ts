export const PUBLIC_ROUTES = [
  "/",
  "/portfolio",
  "/offres",
  "/diagnostic",
  "/configurer",
  "/solutions",
  "/a-propos",
  "/contact",
  "/mentions-legales",
  "/politique-confidentialite",
  "/cgv",
  "/signup",
  "/set-password",
] as const;

export const PORTFOLIO_URL = "https://portfolio.zacharyhounsa.ovh/";

export type PortalArea = "public" | "client" | "admin" | "local";
export type PortalRole = "client_user" | "internal_admin";

const PORTAL_FAMILIES = {
  "zacharyhounsa.ovh": {
    public: "www.zacharyhounsa.ovh",
    publicAliases: new Set(["zacharyhounsa.ovh", "www.zacharyhounsa.ovh"]),
    // Alias publics servis en 301 vers `public` : un seul hote doit
    // repondre 200, sinon l'indexation se dilue sur deux domaines.
    canonicalRedirects: new Set(["zacharyhounsa.ovh"]),
    client: "dashboard.zacharyhounsa.ovh",
    admin: "administration.zacharyhounsa.ovh",
  },
  "home.bzh": {
    public: "www.home.bzh",
    publicAliases: new Set(["home.bzh", "www.home.bzh", "portail.home.bzh"]),
    canonicalRedirects: new Set(["home.bzh"]),
    client: "dashboard.home.bzh",
    admin: "administration.home.bzh",
  },
} as const;

/**
 * Les validations ACME restent servies par l'hote interroge : les sites de
 * redirection IIS et nginx appliquent deja cette exemption.
 */
const ACME_CHALLENGE_PREFIX = "/.well-known/acme-challenge/";

const UNSAFE_HOST_CHARACTER = /[/\\?#@\u0000-\u001f\u007f]/;

type PortalFamilyName = keyof typeof PORTAL_FAMILIES;

const LOCAL_HOSTNAMES = new Set(["localhost", "127.0.0.1", "::1"]);
const RAW_CONTROL_CHARACTER = /[\u0000-\u001f\u007f]/;
const ENCODED_SEPARATOR_OR_CONTROL =
  /%(?:0[0-9a-f]|1[0-9a-f]|7f|2f|5c)/i;

function parsePortalUrl(value: string | null | undefined): URL | null {
  if (!value) {
    return null;
  }

  try {
    const url = new URL(value);
    if (
      (url.protocol !== "http:" && url.protocol !== "https:")
      || url.username
      || url.password
    ) {
      return null;
    }
    return url;
  } catch {
    return null;
  }
}

function normalizeHostname(hostname: string): string {
  const normalized = hostname.toLowerCase();
  return normalized.startsWith("[") && normalized.endsWith("]")
    ? normalized.slice(1, -1)
    : normalized;
}

function getPortalFamily(hostname: string): PortalFamilyName | null {
  for (const familyName of Object.keys(PORTAL_FAMILIES) as PortalFamilyName[]) {
    const family = PORTAL_FAMILIES[familyName];
    if (
      family.publicAliases.has(hostname as never)
      || family.client === hostname
      || family.admin === hostname
    ) {
      return familyName;
    }
  }

  return null;
}

function isSafePortalPath(pathname: string): boolean {
  return (
    pathname.startsWith("/")
    && !pathname.startsWith("//")
    && !pathname.includes("\\")
    && !RAW_CONTROL_CHARACTER.test(pathname)
    && !ENCODED_SEPARATOR_OR_CONTROL.test(pathname)
  );
}

export function getPortalArea(
  origin: string | null | undefined,
): PortalArea | null {
  const url = parsePortalUrl(origin);
  if (!url) {
    return null;
  }

  const hostname = normalizeHostname(url.hostname);
  if (LOCAL_HOSTNAMES.has(hostname)) {
    return "local";
  }

  const familyName = getPortalFamily(hostname);
  if (!familyName) {
    return null;
  }

  const family = PORTAL_FAMILIES[familyName];
  if (family.publicAliases.has(hostname as never)) {
    return "public";
  }
  if (hostname === family.client) {
    return "client";
  }
  return hostname === family.admin ? "admin" : null;
}

export function resolvePortalAreaUrl(
  origin: string | null | undefined,
  area: PortalArea,
  pathname = "/",
): string | null {
  const url = parsePortalUrl(origin);
  if (!url || !isSafePortalPath(pathname)) {
    return null;
  }

  const hostname = normalizeHostname(url.hostname);
  if (LOCAL_HOSTNAMES.has(hostname)) {
    return `${url.origin}${pathname}`;
  }

  if (area === "local") {
    return null;
  }

  const familyName = getPortalFamily(hostname);
  if (!familyName) {
    return null;
  }

  return `https://${PORTAL_FAMILIES[familyName][area]}${pathname}`;
}

function parseRequestHostname(host: string | null | undefined): string | null {
  if (!host) {
    return null;
  }

  const trimmed = host.trim();
  if (!trimmed || UNSAFE_HOST_CHARACTER.test(trimmed)) {
    return null;
  }

  const url = parsePortalUrl(`https://${trimmed}`);
  return url?.hostname ? normalizeHostname(url.hostname) : null;
}

/**
 * URL canonique absolue vers laquelle rediriger un alias public non
 * canonique (apex sans `www`), chemin et query conserves.
 *
 * Retourne `null` quand l'hote est deja canonique, local, inconnu, ou
 * quand la requete ne doit pas etre redirigee : l'appelant sert alors la
 * reponse normalement.
 */
export function resolveCanonicalPublicUrl(
  host: string | null | undefined,
  pathname: string,
  search = "",
): string | null {
  const hostname = parseRequestHostname(host);
  if (
    !hostname
    || !isSafePortalPath(pathname)
    || pathname.startsWith(ACME_CHALLENGE_PREFIX)
    || RAW_CONTROL_CHARACTER.test(search)
  ) {
    return null;
  }

  const familyName = getPortalFamily(hostname);
  if (!familyName) {
    return null;
  }

  const family = PORTAL_FAMILIES[familyName];
  if (!family.canonicalRedirects.has(hostname as never)) {
    return null;
  }

  const query = search && search !== "?"
    ? (search.startsWith("?") ? search : `?${search}`)
    : "";

  return `https://${family.public}${pathname}${query}`;
}

export function resolvePortalRoleUrl(
  origin: string | null | undefined,
  role: string | null | undefined,
  pathname?: string,
): string | null {
  if (role === "client_user") {
    return resolvePortalAreaUrl(origin, "client", pathname ?? "/dashboard");
  }
  if (role === "internal_admin") {
    return resolvePortalAreaUrl(origin, "admin", pathname ?? "/admin");
  }
  return null;
}

export function isPortalRoleAllowed(
  area: PortalArea | null | undefined,
  role: string | null | undefined,
): role is PortalRole {
  if (area === "local") {
    return role === "client_user" || role === "internal_admin";
  }
  return (
    (area === "client" && role === "client_user")
    || (area === "admin" && role === "internal_admin")
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
