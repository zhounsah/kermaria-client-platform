import "server-only";

import { isIP } from "node:net";

import { CORRELATION_HEADER } from "@/lib/correlation";
import {
  getInternalApiUrl,
  getInternalServiceHeaders,
} from "@/lib/runtime-config";

const INTERNAL_TIMEOUT_MS = 10_000;
const HCAPTCHA_VERIFY_URL = "https://hcaptcha.com/siteverify";

export type InternalSignupResult = {
  ok: boolean;
  status: number;
  code: string;
  message: string;
  correlationId?: string;
};

export type HCaptchaOutcome = {
  ok: boolean;
  code: string;
  // `error-codes` renvoyés par hCaptcha siteverify (ex. sitekey-secret-mismatch,
  // invalid-input-response, timeout-or-duplicate). Nécessaires pour diagnostiquer
  // un rejet côté serveur sans les exposer au client.
  errorCodes?: string[];
};

// hCaptcha est obligatoire (décision de cadrage V0.26). En développement
// sans clé configurée, on saute la vérification pour rester testable ;
// en production, l'absence de clé fait échouer la requête (fail-closed).
export async function verifyHCaptcha(
  token: string | null,
  remoteIp: string | null,
): Promise<HCaptchaOutcome> {
  const secret = process.env.HCAPTCHA_SECRET_KEY?.trim();

  if (!secret || isPlaceholderSecret(secret)) {
    if (process.env.NODE_ENV === "production") {
      return { ok: false, code: "CAPTCHA_MISCONFIGURED" };
    }
    return { ok: true, code: "CAPTCHA_SKIPPED_DEV" };
  }

  if (!token) {
    return { ok: false, code: "CAPTCHA_REQUIRED" };
  }

  const params = new URLSearchParams({ secret, response: token });
  // Le x-forwarded-for injecté par le reverse proxy peut contenir une
  // adresse IPv6 lien-local avec zone index (ex. `fe80::1%12`, typique d'un
  // client LAN interne) ou une liste d'IP : hCaptcha rejette ces formats
  // (invalid-remoteip) et fait échouer siteverify alors que le token est
  // valide. On retire donc le zone index `%…` puis on valide via net.isIP
  // (qui renvoie 0 pour une liste ou du garbage). remoteip est optionnel :
  // en cas de doute on l'omet plutôt que de casser la vérification.
  const cleanRemoteIp = remoteIp?.split("%", 1)[0]?.trim();
  if (cleanRemoteIp && isIP(cleanRemoteIp) !== 0) {
    params.set("remoteip", cleanRemoteIp);
  }

  try {
    const response = await fetch(HCAPTCHA_VERIFY_URL, {
      method: "POST",
      cache: "no-store",
      signal: AbortSignal.timeout(8_000),
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: params.toString(),
    });
    const data = (await response.json()) as {
      success?: boolean;
      "error-codes"?: string[];
    };
    return data?.success === true
      ? { ok: true, code: "CAPTCHA_OK" }
      : {
          ok: false,
          code: "CAPTCHA_FAILED",
          errorCodes: data?.["error-codes"],
        };
  } catch {
    return { ok: false, code: "CAPTCHA_UNAVAILABLE" };
  }
}

// Relais anonyme (X-Service-Auth) vers l'API-INTERNAL pour les routes
// publiques de signup. Aucune session n'est requise.
export async function callInternalSignup(
  path: string,
  body: unknown,
  correlationId: string,
): Promise<InternalSignupResult> {
  let internalApiUrl: string | undefined;
  try {
    internalApiUrl = getInternalApiUrl();
  } catch {
    return unavailable();
  }

  if (!internalApiUrl) {
    return unavailable();
  }

  try {
    const upstream = await fetch(`${internalApiUrl}${path}`, {
      method: "POST",
      cache: "no-store",
      signal: AbortSignal.timeout(INTERNAL_TIMEOUT_MS),
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
        ...getInternalServiceHeaders(),
        [CORRELATION_HEADER]: correlationId,
      },
      body: JSON.stringify(body),
    });

    return toResult(upstream, await safeReadJson(upstream), correlationId);
  } catch {
    return unavailable();
  }
}

// Validation non destructive (GET) du jeton de définition de mot de passe.
// Permet à la page /set-password d'afficher l'état « lien invalide / expiré »
// dès le chargement, sans consommer le jeton (la consommation reste le POST
// /internal/signup/set-password). Relais anonyme X-Service-Auth, sans session.
export async function validateSetPasswordToken(
  token: string,
  correlationId: string,
): Promise<InternalSignupResult> {
  let internalApiUrl: string | undefined;
  try {
    internalApiUrl = getInternalApiUrl();
  } catch {
    return unavailable();
  }

  if (!internalApiUrl) {
    return unavailable();
  }

  try {
    const upstream = await fetch(
      `${internalApiUrl}/internal/signup/set-password/validate?token=${encodeURIComponent(token)}`,
      {
        method: "GET",
        cache: "no-store",
        signal: AbortSignal.timeout(INTERNAL_TIMEOUT_MS),
        headers: {
          Accept: "application/json",
          ...getInternalServiceHeaders(),
          [CORRELATION_HEADER]: correlationId,
        },
      },
    );

    return toResult(upstream, await safeReadJson(upstream), correlationId);
  } catch {
    return unavailable();
  }
}

function toResult(
  upstream: Response,
  payload: Record<string, unknown> | null,
  correlationId: string,
): InternalSignupResult {
  return {
    ok: upstream.ok,
    status: upstream.status,
    code:
      typeof payload?.code === "string"
        ? payload.code
        : upstream.ok
          ? "OK"
          : "SIGNUP_REQUEST_FAILED",
    message:
      typeof payload?.message === "string"
        ? payload.message
        : upstream.ok
          ? "Demande traitée."
          : "La demande n'a pas pu être traitée.",
    correlationId:
      typeof payload?.correlation_id === "string"
        ? payload.correlation_id
        : correlationId,
  };
}

function unavailable(): InternalSignupResult {
  return {
    ok: false,
    status: 503,
    code: "INTERNAL_API_UNAVAILABLE",
    message:
      "Le service est temporairement indisponible. Réessayez dans quelques instants.",
  };
}

async function safeReadJson(
  response: Response,
): Promise<Record<string, unknown> | null> {
  try {
    const contentType = response.headers.get("content-type") ?? "";
    if (!contentType.toLowerCase().includes("application/json")) {
      return null;
    }
    return (await response.json()) as Record<string, unknown>;
  } catch {
    return null;
  }
}

function isPlaceholderSecret(value: string) {
  const normalized = value.toLowerCase();
  return (
    ["changeme", "change-me", "test", "placeholder"].includes(normalized)
    || normalized.startsWith("test")
    || normalized.includes("replace_with")
    || normalized.includes("replace-with")
    || normalized.includes("example")
    || normalized.includes("placeholder")
  );
}
