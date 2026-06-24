import "server-only";

const LOCAL_HOSTNAMES = new Set(["localhost", "127.0.0.1", "::1"]);
let missingInternalApiWarningWritten = false;

export function isPayPalConfigured(): boolean {
  return (
    !!process.env.PAYPAL_CLIENT_ID?.trim()
    && !!process.env.PAYPAL_CLIENT_SECRET?.trim()
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
