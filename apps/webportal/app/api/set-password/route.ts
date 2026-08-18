import "server-only";

import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { getPortalArea } from "@/lib/public-route-config";
import { getPortalRequestOriginFromHeaders } from "@/lib/public-routes";
import {
  checkRateLimit,
  getRequestIdentifier,
} from "@/lib/rate-limit";
import { callInternalSignup } from "@/lib/signup-server";

type SetPasswordRequestBody = {
  token?: unknown;
  password?: unknown;
  confirmPassword?: unknown;
  flow?: unknown;
};

// Parcours de definition de mot de passe. `flow` choisit l'endpoint et la
// borne haute du mot de passe — rien d'autre. Il n'autorise rien : c'est le
// jeton, et le `purpose` verifie cote API, qui decident. Un parcours inconnu
// est refuse plutot que ramene au signup : un repli silencieux enverrait un
// jeton d'utilisateur supplementaire vers l'endpoint d'inscription.
type SetPasswordFlow = {
  upstreamPath: string;
  maxPasswordLength: number;
};

const ADDITIONAL_USER_FLOW = "billing-v2-additional-user";

type SetPasswordRequestFormat = "json" | "form";

type SetPasswordPresentationCode =
  | "PASSWORD_SET"
  | "TOKEN_INVALID"
  | "TOKEN_EXPIRED"
  | "INVALID_PASSWORD"
  | "INVALID_REQUEST"
  | "RATE_LIMITED"
  | "SET_PASSWORD_REQUEST_TOO_LARGE"
  | "SET_PASSWORD_UNAVAILABLE";

type SetPasswordPayload = {
  code: string;
  message: string;
  correlation_id: string;
};

class SetPasswordBodyError extends Error {
  constructor(readonly kind: "invalid" | "too_large") {
    super(kind);
  }
}

const MIN_PASSWORD_LENGTH = 12;
// Inscription : borne historique du parcours signup.
const MAX_PASSWORD_LENGTH = 200;
// Utilisateur supplementaire Billing V2 : borne du service Phase 4. Les deux
// bornes different reellement cote API ; les aligner ici ferait accepter au
// navigateur un mot de passe que l'API refuserait ensuite.
const MAX_ADDITIONAL_USER_PASSWORD_LENGTH = 128;
const MAX_SET_PASSWORD_BODY_BYTES = 16 * 1024;
const RATE_LIMIT_MAX = 5;
const RATE_LIMIT_WINDOW_MS = 15 * 60 * 1000;

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const format = getSetPasswordRequestFormat(
    request.headers.get("content-type"),
  );

  if (!format) {
    return jsonResponse(
      {
        code: "UNSUPPORTED_MEDIA_TYPE",
        message: "Le format de la demande n'est pas pris en charge.",
        correlation_id: correlationId,
      },
      415,
    );
  }

  if (format === "form" && !isAllowedFormPost(request)) {
    return jsonResponse(
      {
        code: "SET_PASSWORD_FORBIDDEN",
        message: "La demande de définition du mot de passe n'est pas autorisée.",
        correlation_id: correlationId,
      },
      403,
    );
  }

  const identifier = getRequestIdentifier(request);
  const rateDecision = checkRateLimit(
    `set-password:${identifier}`,
    RATE_LIMIT_MAX,
    RATE_LIMIT_WINDOW_MS,
  );
  if (rateDecision.limited) {
    const payload = {
      code: "RATE_LIMITED",
      message: "Trop de tentatives. Réessayez dans quelques minutes.",
      correlation_id: correlationId,
    } satisfies SetPasswordPayload;
    const response = format === "form"
      ? presentationRedirect("RATE_LIMITED", correlationId)
      : jsonResponse(payload, 429);
    response.headers.set("Retry-After", String(rateDecision.retryAfterSeconds));
    return response;
  }

  let body: SetPasswordRequestBody;
  try {
    body = parseSetPasswordBody(
      await readBoundedSetPasswordBody(request),
      format,
    );
  } catch (error) {
    if (error instanceof SetPasswordBodyError && error.kind === "too_large") {
      return format === "form"
        ? presentationRedirect(
            "SET_PASSWORD_REQUEST_TOO_LARGE",
            correlationId,
          )
        : jsonResponse(
            {
              code: "PAYLOAD_TOO_LARGE",
              message: "La demande est trop volumineuse.",
              correlation_id: correlationId,
            },
            413,
          );
    }

    return format === "form"
      ? presentationRedirect("INVALID_REQUEST", correlationId)
      : jsonResponse(
          {
            code: "INVALID_REQUEST",
            message: "Le corps de la requête est invalide.",
            correlation_id: correlationId,
          },
          400,
        );
  }

  const flow = resolveSetPasswordFlow(body.flow);
  if (!flow) {
    return format === "form"
      ? presentationRedirect("INVALID_REQUEST", correlationId)
      : jsonResponse(
          {
            code: "INVALID_REQUEST",
            message: "Le parcours demandé n'est pas pris en charge.",
            correlation_id: correlationId,
          },
          400,
        );
  }

  const token = typeof body.token === "string" ? body.token.trim() : "";
  const password = typeof body.password === "string" ? body.password : "";

  if (!token) {
    return format === "form"
      ? presentationRedirect("TOKEN_INVALID", correlationId)
      : jsonResponse(
          {
            code: "TOKEN_INVALID",
            message: "Lien invalide ou expiré.",
            correlation_id: correlationId,
          },
          400,
        );
  }

  if (
    password.length < MIN_PASSWORD_LENGTH
    || password.length > flow.maxPasswordLength
    || (
      format === "form"
      && (
        typeof body.confirmPassword !== "string"
        || password !== body.confirmPassword
      )
    )
  ) {
    return format === "form"
      ? presentationRedirect("INVALID_PASSWORD", correlationId)
      : jsonResponse(
          {
            code: "INVALID_PASSWORD",
            message: `Le mot de passe doit comporter entre ${MIN_PASSWORD_LENGTH} et ${flow.maxPasswordLength} caractères.`,
            correlation_id: correlationId,
          },
          400,
        );
  }

  // Le corps transmis en amont ne porte pas `flow` : l'endpoint le dit deja,
  // et l'API ne doit jamais deduire d'un champ du navigateur ce qu'elle a le
  // droit de faire.
  const result = await callInternalSignup(
    flow.upstreamPath,
    { token, password },
    correlationId,
  );
  const resultCorrelationId = result.correlationId ?? correlationId;

  if (format === "form") {
    return presentationRedirect(
      toPresentationCode(result.ok, result.code),
      resultCorrelationId,
    );
  }

  return jsonResponse(
    {
      code: result.code,
      message: result.message,
      correlation_id: resultCorrelationId,
    },
    result.ok ? 200 : result.status >= 500 ? 502 : result.status,
  );
}

function resolveSetPasswordFlow(value: unknown): SetPasswordFlow | null {
  if (value === undefined || value === null || value === "") {
    return {
      upstreamPath: "/internal/signup/set-password",
      maxPasswordLength: MAX_PASSWORD_LENGTH,
    };
  }

  if (value === ADDITIONAL_USER_FLOW) {
    return {
      upstreamPath: "/internal/billing-v2/additional-users/password-setup",
      maxPasswordLength: MAX_ADDITIONAL_USER_PASSWORD_LENGTH,
    };
  }

  return null;
}

function getSetPasswordRequestFormat(
  contentType: string | null,
): SetPasswordRequestFormat | null {
  if (!contentType) {
    return null;
  }

  const [mediaType, ...parameters] = contentType
    .split(";")
    .map((part) => part.trim());
  if (
    parameters.some(
      (parameter) => !/^charset\s*=\s*(?:"utf-8"|utf-8)$/i.test(parameter),
    )
  ) {
    return null;
  }

  switch (mediaType.toLowerCase()) {
    case "application/json":
      return "json";
    case "application/x-www-form-urlencoded":
      return "form";
    default:
      return null;
  }
}

function isAllowedFormPost(request: NextRequest): boolean {
  const origin = getPortalRequestOriginFromHeaders(request.headers);
  const area = getPortalArea(origin);
  if (
    !origin
    || (area !== "public" && area !== "client" && area !== "local")
  ) {
    return false;
  }

  const requestOrigin = request.headers.get("origin");
  if (!requestOrigin) {
    return false;
  }

  try {
    const url = new URL(requestOrigin);
    return (
      (url.protocol === "http:" || url.protocol === "https:")
      && !url.username
      && !url.password
      && url.origin === origin
    );
  } catch {
    return false;
  }
}

async function readBoundedSetPasswordBody(request: NextRequest): Promise<string> {
  const declaredLength = request.headers.get("content-length");
  if (declaredLength) {
    if (!/^\d+$/.test(declaredLength)) {
      throw new SetPasswordBodyError("invalid");
    }
    if (Number(declaredLength) > MAX_SET_PASSWORD_BODY_BYTES) {
      throw new SetPasswordBodyError("too_large");
    }
  }

  if (!request.body) {
    return "";
  }

  const reader = request.body.getReader();
  const chunks: Uint8Array[] = [];
  let totalBytes = 0;

  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) {
        break;
      }
      totalBytes += value.byteLength;
      if (totalBytes > MAX_SET_PASSWORD_BODY_BYTES) {
        throw new SetPasswordBodyError("too_large");
      }
      chunks.push(value);
    }
  } finally {
    reader.releaseLock();
  }

  const bytes = new Uint8Array(totalBytes);
  let offset = 0;
  for (const chunk of chunks) {
    bytes.set(chunk, offset);
    offset += chunk.byteLength;
  }

  try {
    return new TextDecoder("utf-8", { fatal: true }).decode(bytes);
  } catch {
    throw new SetPasswordBodyError("invalid");
  }
}

function parseSetPasswordBody(
  body: string,
  format: SetPasswordRequestFormat,
): SetPasswordRequestBody {
  if (format === "json") {
    try {
      const payload = JSON.parse(body) as unknown;
      return payload && typeof payload === "object"
        ? payload as SetPasswordRequestBody
        : {};
    } catch {
      throw new SetPasswordBodyError("invalid");
    }
  }

  const form = new URLSearchParams(body);
  const tokens = form.getAll("token");
  const passwords = form.getAll("password");
  const confirmations = form.getAll("confirmPassword");
  // `flow` est facultatif — son absence est le parcours d'inscription — mais
  // repete il serait ambigu, et une ambiguite sur le parcours choisit
  // l'endpoint a notre place.
  const flows = form.getAll("flow");
  if (
    tokens.length !== 1
    || passwords.length !== 1
    || confirmations.length !== 1
    || flows.length > 1
  ) {
    throw new SetPasswordBodyError("invalid");
  }

  return {
    token: tokens[0],
    password: passwords[0],
    confirmPassword: confirmations[0],
    flow: flows[0],
  };
}

function toPresentationCode(
  ok: boolean,
  code: string,
): SetPasswordPresentationCode {
  if (ok) {
    return code === "PASSWORD_SET"
      ? "PASSWORD_SET"
      : "SET_PASSWORD_UNAVAILABLE";
  }

  switch (code) {
    case "TOKEN_INVALID":
    case "TOKEN_EXPIRED":
    case "INVALID_PASSWORD":
    case "RATE_LIMITED":
      return code;
    default:
      return "SET_PASSWORD_UNAVAILABLE";
  }
}

function presentationRedirect(
  code: SetPasswordPresentationCode,
  correlationId: string,
) {
  const response = new NextResponse(null, {
    status: 303,
    headers: { Location: `/set-password?result=${code}` },
  });
  applyResponseHeaders(response, correlationId);
  return response;
}

function jsonResponse(
  payload: SetPasswordPayload,
  status: number,
) {
  const response = NextResponse.json(payload, { status });
  applyResponseHeaders(response, payload.correlation_id);
  return response;
}

function applyResponseHeaders(response: NextResponse, correlationId: string) {
  response.headers.set("Cache-Control", "no-store");
  response.headers.set(CORRELATION_HEADER, correlationId);
}
