export const CSRF_COOKIE_NAME = "kermaria_portal_csrf";
export const CSRF_HEADER_NAME = "X-CSRF-Token";

export function readCookieValue(
  cookieHeader: string | null | undefined,
  cookieName: string,
) {
  if (!cookieHeader) {
    return null;
  }

  for (const part of cookieHeader.split(";")) {
    const [name, ...valueParts] = part.trim().split("=");
    if (name !== cookieName) {
      continue;
    }

    const value = valueParts.join("=").trim();
    return value ? decodeURIComponent(value) : null;
  }

  return null;
}

export function readCsrfTokenFromDocumentCookie() {
  if (typeof document === "undefined") {
    return null;
  }

  return readCookieValue(document.cookie, CSRF_COOKIE_NAME);
}
