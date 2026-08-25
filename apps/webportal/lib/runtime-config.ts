import "server-only";

const LOCAL_HOSTNAMES = new Set(["localhost", "127.0.0.1", "::1"]);
let missingInternalApiWarningWritten = false;

export function isPayPalConfigured(): boolean {
  return (
    !!process.env.PAYPAL_CLIENT_ID?.trim()
    && !!process.env.PAYPAL_CLIENT_SECRET?.trim()
  );
}

export function isStripeSecretKeyCompatible(
  mode: string | undefined,
  key: string | undefined,
): boolean {
  const normalizedMode = mode?.trim().toLowerCase();
  const normalizedKey = key?.trim() ?? "";

  if (normalizedMode === "test") {
    return normalizedKey.startsWith("sk_test_")
      || normalizedKey.startsWith("rk_test_");
  }

  if (normalizedMode === "live") {
    return normalizedKey.startsWith("sk_live_")
      || normalizedKey.startsWith("rk_live_");
  }

  return false;
}

export function isStripePublishableKeyCompatible(
  mode: string | undefined,
  key: string | undefined,
): boolean {
  const normalizedMode = mode?.trim().toLowerCase();
  const normalizedKey = key?.trim() ?? "";

  return normalizedMode === "test"
    ? normalizedKey.startsWith("pk_test_")
    : normalizedMode === "live"
      ? normalizedKey.startsWith("pk_live_")
      : false;
}

export function isStripeConfigured(): boolean {
  return (
    isStripeSecretKeyCompatible(
      process.env.STRIPE_MODE,
      process.env.STRIPE_SECRET_KEY,
    )
    && isStripePublishableKeyCompatible(
      process.env.STRIPE_MODE,
      process.env.STRIPE_PUBLISHABLE_KEY,
    )
  );
}

/** Ouvre le parcours de changement de mot de passe. Le drapeau est lu au même
 *  endroit par la page /password et par le profil, pour que l'espace client
 *  n'annonce jamais un parcours que l'API interne refusera. */
export function isBillingV2AuthoritativeCheckoutBffEnabled(): boolean {
  return (
    process.env.BILLING_V2_AUTHORITATIVE_CHECKOUT_BFF_ENABLED
      ?.trim()
      .toLowerCase() === "true"
  );
}

export function isPasswordChangeEnabled(): boolean {
  return (
    process.env.AD_PASSWORD_CHANGE_ENABLED?.trim().toLowerCase() === "true"
  );
}

export function getBillingConfig() {
  return {
    iban: process.env.BILLING_IBAN?.trim() || null,
    bic: process.env.BILLING_BIC?.trim() || null,
    paypalUrl: process.env.BILLING_PAYPAL_URL?.trim() || null,
    transferLabel: process.env.BILLING_TRANSFER_LABEL?.trim() || "Zachary HOUNSA-HOUNKPA EI",
  };
}

export class ServerRuntimeConfigurationError extends Error {
  constructor(variableName: string) {
    super(`Configuration serveur invalide : ${variableName}.`);
  }
}

export function getInternalApiUrl() {
  const configuredUrl = process.env.INTERNAL_API_URL?.trim();

  if (!configuredUrl) {
    if (process.env.NODE_ENV === "production") {
      throw new ServerRuntimeConfigurationError("INTERNAL_API_URL");
    }

    if (!missingInternalApiWarningWritten) {
      console.warn(
        "INTERNAL_API_URL absente : fallback mock local réservé au développement.",
      );
      missingInternalApiWarningWritten = true;
    }

    return undefined;
  }

  let parsedUrl: URL;
  try {
    parsedUrl = new URL(configuredUrl);
  } catch {
    throw new ServerRuntimeConfigurationError("INTERNAL_API_URL");
  }

  if (!["http:", "https:"].includes(parsedUrl.protocol)) {
    throw new ServerRuntimeConfigurationError("INTERNAL_API_URL");
  }

  const allowLocalUrl =
    process.env.ALLOW_LOCAL_INTERNAL_API_URL?.trim().toLowerCase() === "true";
  if (
    process.env.NODE_ENV === "production"
    && LOCAL_HOSTNAMES.has(parsedUrl.hostname)
    && !allowLocalUrl
  ) {
    throw new ServerRuntimeConfigurationError("INTERNAL_API_URL");
  }

  return configuredUrl.replace(/\/+$/, "");
}

export function getInternalServiceHeaders(): Record<string, string> {
  const token = process.env.SERVICE_AUTH_TOKEN?.trim();

  if (process.env.NODE_ENV === "production" && isPlaceholderSecret(token)) {
    throw new ServerRuntimeConfigurationError("SERVICE_AUTH_TOKEN");
  }

  return token && !isPlaceholderSecret(token)
    ? { "X-Service-Auth": token }
    : {};
}

export function getKoxoExportApiToken() {
  const token = process.env.KOXO_EXPORT_API_TOKEN?.trim();
  if (!token || isPlaceholderSecret(token)) {
    throw new ServerRuntimeConfigurationError("KOXO_EXPORT_API_TOKEN");
  }

  return token;
}

export function getKoxoExportAllowedIps() {
  return (process.env.KOXO_EXPORT_ALLOWED_IPS ?? "")
    .split(/[;,\r\n]+/)
    .map((value) => value.trim())
    .filter(Boolean);
}

export function shouldRequireKoxoExportHttps() {
  const configured = process.env.KOXO_EXPORT_REQUIRE_HTTPS?.trim().toLowerCase();
  if (!configured) {
    return process.env.NODE_ENV === "production";
  }

  return configured !== "false";
}

export function validateServerRuntimeConfiguration() {
  getInternalApiUrl();
  getInternalServiceHeaders();

  if (
    process.env.NODE_ENV === "production"
    && process.env.SESSION_COOKIE_SECURE?.trim().toLowerCase() === "false"
  ) {
    throw new ServerRuntimeConfigurationError("SESSION_COOKIE_SECURE");
  }
}

function isPlaceholderSecret(value: string | undefined) {
  if (!value) {
    return true;
  }

  const normalized = value.toLowerCase();
  return (
    ["password", "changeme", "change-me", "test", "dev-local-token"].includes(
      normalized,
    )
    || normalized.startsWith("test")
    || normalized.includes("replace_with")
    || normalized.includes("replace-with")
    || normalized.includes("example")
    || normalized.includes("placeholder")
  );
}
