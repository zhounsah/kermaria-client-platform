export const DEFAULT_SESSION_COOKIE_NAME = "kermaria_portal_session";
const allowedSameSiteValues = new Set(["lax", "strict", "none"]);

export function getSessionCookieName() {
  const configuredName = process.env.SESSION_COOKIE_NAME?.trim();

  return configuredName && /^[A-Za-z0-9_-]{1,64}$/.test(configuredName)
    ? configuredName
    : DEFAULT_SESSION_COOKIE_NAME;
}

export function isSessionCookieSecure() {
  const configuredValue = process.env.SESSION_COOKIE_SECURE
    ?.trim()
    .toLowerCase();

  if (process.env.NODE_ENV === "production" && configuredValue === "false") {
    throw new Error(
      "Configuration serveur invalide : SESSION_COOKIE_SECURE.",
    );
  }

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
    sameSite: getSessionCookieSameSite(),
    secure: isSessionCookieSecure(),
    path: "/",
  };
}

export function validateSessionCookieConfiguration() {
  getSessionCookieOptions();
}

function getSessionCookieSameSite() {
  const configuredValue = process.env.SESSION_COOKIE_SAME_SITE
    ?.trim()
    .toLowerCase();

  if (!configuredValue) {
    return "lax" as const;
  }

  if (!allowedSameSiteValues.has(configuredValue)) {
    throw new Error(
      "Configuration serveur invalide : SESSION_COOKIE_SAME_SITE.",
    );
  }

  if (configuredValue === "none" && !isSessionCookieSecure()) {
    throw new Error(
      "Configuration serveur invalide : SESSION_COOKIE_SAME_SITE.",
    );
  }

  return configuredValue as "lax" | "strict" | "none";
}
