import "server-only";

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
};

// hCaptcha est obligatoire (décision de cadrage V0.26). En développement
// sans clé configurée, on saute la vérification pour rester testable ;
// en production, l'absence de clé fait échouer la requête (fail-closed).
export async function verifyHCaptcha(
  token: string | null,
  remoteIp: string | null,
  correlationId?: string,
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
  // remoteip est optionnel pour hCaptcha. Derrière le reverse-proxy
  // IIS/ARR de SRV-01, l'IP relayée via X-Forwarded-For peut être une IP
  // privée (client sur le LAN) ou celle du proxy — elle diverge alors de
  // l'IP vue par hCaptcha lors de la résolution du widget, ce qui fait
  // rejeter siteverify. On ne transmet donc remoteip que si c'est une IP
  // publique routable ; sinon on vérifie le token seul (comportement
  // standard, remoteip n'étant qu'un signal additionnel).
  const publicIp = toPublicRemoteIp(remoteIp);
  if (publicIp) {
    params.set("remoteip", publicIp);
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
      hostname?: string;
    };
    if (data?.success === true) {
      return { ok: true, code: "CAPTCHA_OK" };
    }
    logHCaptchaFailure({
      correlationId,
      code: "CAPTCHA_FAILED",
      errorCodes: Array.isArray(data?.["error-codes"])
        ? data["error-codes"]
        : [],
      hostname: typeof data?.hostname === "string" ? data.hostname : undefined,
      remoteIpSent: Boolean(publicIp),
    });
    return { ok: false, code: "CAPTCHA_FAILED" };
  } catch {
    logHCaptchaFailure({
      correlationId,
      code: "CAPTCHA_UNAVAILABLE",
      errorCodes: ["request-failed"],
      remoteIpSent: Boolean(publicIp),
    });
    return { ok: false, code: "CAPTCHA_UNAVAILABLE" };
  }
}

type HCaptchaFailureLog = {
  correlationId?: string;
  code: string;
  errorCodes: string[];
  hostname?: string;
  remoteIpSent: boolean;
};

// Journalise l'échec de vérification hCaptcha (error-codes hCaptcha,
// hostname vu par hCaptcha, si un remoteip a été transmis). Écrit sur
// stderr — capté par NSSM AppStderr sur SRV-01 — pour rendre diagnosticable
// un CAPTCHA_FAILED en prod. Ni le secret ni le token ne sont journalisés.
function logHCaptchaFailure(event: HCaptchaFailureLog) {
  process.stderr.write(
    `${JSON.stringify({
      level: "warn",
      event: "hcaptcha_verify_failed",
      surface: "webportal",
      operation: "signup.hcaptcha",
      correlation_id: event.correlationId ?? null,
      code: event.code,
      error_codes: event.errorCodes,
      hostname: event.hostname ?? null,
      remoteip_sent: event.remoteIpSent,
      timestamp_utc: new Date().toISOString(),
    })}\n`,
  );
}

// Ne renvoie l'IP que si elle est publique et routable. Neutralise les
// IP privées, loopback, link-local, CGNAT et IPv6 locales avant de les
// transmettre à hCaptcha comme remoteip (voir verifyHCaptcha).
function toPublicRemoteIp(remoteIp: string | null): string | null {
  if (!remoteIp) {
    return null;
  }
  let ip = remoteIp.trim();
  if (!ip || ip === "unknown") {
    return null;
  }
  // IPv6-mapped IPv4 (::ffff:192.168.0.1) -> on isole la partie IPv4.
  const mapped = ip.match(/^::ffff:(\d{1,3}(?:\.\d{1,3}){3})$/i);
  if (mapped?.[1]) {
    ip = mapped[1];
  }
  // Zone id IPv6 (fe80::1%eth0) -> on retire la zone.
  const zone = ip.indexOf("%");
  if (zone !== -1) {
    ip = ip.slice(0, zone);
  }

  if (ip.includes(".")) {
    return isPublicIpv4(ip) ? ip : null;
  }
  if (ip.includes(":")) {
    return isPublicIpv6(ip) ? ip : null;
  }
  return null;
}

function isPublicIpv4(ip: string): boolean {
  const parts = ip.split(".");
  if (parts.length !== 4) {
    return false;
  }
  const octets = parts.map((p) => Number(p));
  if (octets.some((n) => !Number.isInteger(n) || n < 0 || n > 255)) {
    return false;
  }
  const [a, b] = octets;
  if (a === 0 || a === 127) return false; // "this network" / loopback
  if (a === 10) return false; // 10.0.0.0/8
  if (a === 172 && b >= 16 && b <= 31) return false; // 172.16.0.0/12
  if (a === 192 && b === 168) return false; // 192.168.0.0/16
  if (a === 169 && b === 254) return false; // link-local 169.254.0.0/16
  if (a === 100 && b >= 64 && b <= 127) return false; // CGNAT 100.64.0.0/10
  if (a >= 224) return false; // multicast / réservé
  return true;
}

function isPublicIpv6(ip: string): boolean {
  const lower = ip.toLowerCase();
  if (lower === "::1" || lower === "::") return false; // loopback / unspecified
  if (lower.startsWith("fe80")) return false; // link-local fe80::/10
  if (lower.startsWith("fc") || lower.startsWith("fd")) return false; // ULA fc00::/7
  return true;
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

    const payload = await safeReadJson(upstream);
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
  } catch {
    return unavailable();
  }
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
