export const DEFAULT_SESSION_COOKIE_NAME = "kermaria_portal_session";

export function getSessionCookieName() {
  const configuredName = process.env.SESSION_COOKIE_NAME?.trim();

  return configuredName && /^[A-Za-z0-9_-]{1,64}$/.test(configuredName)
    ? configuredName
    : DEFAULT_SESSION_COOKIE_NAME;
}

export function isSessionCookieSecure() {
  const configuredValue = process.env.SESSION_COOKIE_SECURE?.trim().toLowerCase();

  if (configuredValue === "true") {
    return true;
  }

  if (configuredValue === "false") {
    return false;
  }

  return process.env.NODE_ENV === "production";
}

export function getSessionCookieOptions() {
  return {
    httpOnly: true,
    sameSite: "lax" as const,
    secure: isSessionCookieSecure(),
    path: "/",
  };
}
