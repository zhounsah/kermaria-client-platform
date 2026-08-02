import type { ApiError, AuthMeResponse, LoginPayload } from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { ensureCsrfCookie } from "@/lib/csrf-server";
import {
  createInternalSession,
  getInternalApiError,
  revokeInternalSession,
} from "@/lib/internal-api";
import {
  type PortalArea,
  getPortalArea,
  isPortalRoleAllowed,
  resolvePortalAreaUrl,
  resolvePortalRoleUrl,
} from "@/lib/public-route-config";
import { getPortalRequestOriginFromHeaders } from "@/lib/public-routes";
import {
  getSessionCookieName,
  getSessionCookieOptions,
} from "@/lib/session-config";

const MAX_LOGIN_BODY_BYTES = 16 * 1024;

type LoginRequestFormat = "json" | "form";
type LoginArea = Exclude<PortalArea, "public">;
type LoginPresentationCode =
  | "INVALID_CREDENTIALS"
  | "LOGIN_REQUEST_TOO_LARGE"
  | "LOGIN_UNAVAILABLE"
  | "PORTAL_ROLE_MISMATCH";

class LoginBodyError extends Error {
  constructor(readonly kind: "invalid" | "too_large") {
    super(kind);
  }
}

export async function POST(request: NextRequest) {
  const origin = getPortalRequestOriginFromHeaders(request.headers);
  const area = getPortalArea(origin);
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );

  if (!origin || !area || area === "public") {
    return portalLoginForbidden(correlationId);
  }

  const format = getLoginRequestFormat(request.headers.get("content-type"));
  if (!format) {
    return unsupportedMediaType(correlationId);
  }

  if (format === "form" && !isSameOriginFormPost(request, origin)) {
    return portalLoginForbidden(correlationId);
  }

  let payload: unknown;

  try {
    const body = await readBoundedLoginBody(request);
    payload = parseLoginPayload(body, format);
  } catch (error) {
    if (error instanceof LoginBodyError && error.kind === "too_large") {
      return format === "form"
        ? redirectToLogin(origin, area, "LOGIN_REQUEST_TOO_LARGE", correlationId)
        : payloadTooLarge(correlationId);
    }

    return format === "form"
      ? redirectToLogin(origin, area, "INVALID_CREDENTIALS", correlationId)
      : invalidCredentials(correlationId);
  }

  if (!isLoginPayload(payload)) {
    return format === "form"
      ? redirectToLogin(origin, area, "INVALID_CREDENTIALS", correlationId)
      : invalidCredentials(correlationId);
  }

  try {
    const session = await createInternalSession(
      payload,
      correlationId,
      request.headers.get("user-agent"),
    );

    if (!isPortalRoleAllowed(area, session.user.role)) {
      await revokeInternalSession(session.sessionToken, correlationId);

      if (format === "form") {
        const target = resolvePortalRoleUrl(
          origin,
          session.user.role,
          "/login?error=PORTAL_ROLE_MISMATCH",
        );
        return target
          ? redirectToTarget(target, correlationId)
          : redirectToLogin(origin, area, "LOGIN_UNAVAILABLE", correlationId);
      }

      const response = NextResponse.json({
        authenticated: false,
      } satisfies AuthMeResponse);
      response.headers.set(CORRELATION_HEADER, correlationId);
      return response;
    }

    const response = format === "form"
      ? redirectToTarget(
          resolvePortalRoleUrl(origin, session.user.role)!,
          correlationId,
        )
      : NextResponse.json({
          authenticated: true,
          user: session.user,
          expiresAt: session.expiresAt,
        });

    response.cookies.set({
      name: getSessionCookieName(),
      value: session.sessionToken,
      ...getSessionCookieOptions(),
      expires: new Date(session.expiresAt),
    });
    ensureCsrfCookie(request, response);
    response.headers.set(CORRELATION_HEADER, correlationId);
    return response;
  } catch (error) {
    const failure = getInternalApiError(error);

    if (format === "form") {
      const presentationCode = failure.status === 401
        ? "INVALID_CREDENTIALS"
        : "LOGIN_UNAVAILABLE";
      return redirectToLogin(
        origin,
        area,
        presentationCode,
        failure.error.correlation_id,
      );
    }

    const response = NextResponse.json(failure.error, {
      status: failure.status,
    });
    response.headers.set(
      CORRELATION_HEADER,
      failure.error.correlation_id,
    );
    return response;
  }
}

function getLoginRequestFormat(
  contentType: string | null,
): LoginRequestFormat | null {
  if (!contentType) {
    return null;
  }

  const [mediaType, ...parameters] = contentType
    .split(";")
    .map((part) => part.trim());
  const normalizedMediaType = mediaType.toLowerCase();
  const validParameters = parameters.every((parameter) =>
    /^charset\s*=\s*(?:"utf-8"|utf-8)$/i.test(parameter)
  );

  if (!validParameters) {
    return null;
  }
  if (normalizedMediaType === "application/json") {
    return "json";
  }
  return normalizedMediaType === "application/x-www-form-urlencoded"
    ? "form"
    : null;
}

function isSameOriginFormPost(request: NextRequest, origin: string): boolean {
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

async function readBoundedLoginBody(request: NextRequest): Promise<string> {
  const declaredLength = request.headers.get("content-length");
  if (declaredLength) {
    if (!/^\d+$/.test(declaredLength)) {
      throw new LoginBodyError("invalid");
    }
    if (Number(declaredLength) > MAX_LOGIN_BODY_BYTES) {
      throw new LoginBodyError("too_large");
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
      if (totalBytes > MAX_LOGIN_BODY_BYTES) {
        throw new LoginBodyError("too_large");
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
    throw new LoginBodyError("invalid");
  }
}

function parseLoginPayload(
  body: string,
  format: LoginRequestFormat,
): unknown {
  if (format === "json") {
    try {
      return JSON.parse(body);
    } catch {
      throw new LoginBodyError("invalid");
    }
  }

  const form = new URLSearchParams(body);
  const emails = form.getAll("email");
  const passwords = form.getAll("password");
  if (emails.length !== 1 || passwords.length !== 1) {
    throw new LoginBodyError("invalid");
  }

  return {
    email: emails[0],
    password: passwords[0],
  } satisfies Partial<LoginPayload>;
}

function isLoginPayload(payload: unknown): payload is LoginPayload {
  if (!payload || typeof payload !== "object") {
    return false;
  }

  const candidate = payload as Partial<LoginPayload>;
  return (
    typeof candidate.email === "string"
    && candidate.email.trim().length > 0
    && candidate.email.length <= 254
    && typeof candidate.password === "string"
    && candidate.password.length > 0
    && candidate.password.length <= 1024
  );
}

function redirectToLogin(
  origin: string,
  area: LoginArea,
  code: LoginPresentationCode,
  correlationId: ApiError["correlation_id"],
) {
  const target = resolvePortalAreaUrl(
    origin,
    area,
    `/login?error=${code}`,
  );
  if (!target) {
    return portalLoginForbidden(correlationId);
  }
  return redirectToTarget(target, correlationId);
}

function redirectToTarget(
  target: string,
  correlationId: ApiError["correlation_id"],
) {
  const response = NextResponse.redirect(target, { status: 303 });
  response.headers.set(CORRELATION_HEADER, correlationId);
  return response;
}

function invalidCredentials(correlationId: ApiError["correlation_id"]) {
  const response = NextResponse.json(
    {
      code: "INVALID_CREDENTIALS",
      message: "Identifiants invalides.",
      correlation_id: correlationId,
    } satisfies ApiError,
    { status: 401 },
  );
  response.headers.set(CORRELATION_HEADER, correlationId);
  return response;
}

function payloadTooLarge(correlationId: ApiError["correlation_id"]) {
  const response = NextResponse.json(
    {
      code: "PAYLOAD_TOO_LARGE",
      message: "La demande de connexion est trop volumineuse.",
      correlation_id: correlationId,
    } satisfies ApiError,
    { status: 413 },
  );
  response.headers.set(CORRELATION_HEADER, correlationId);
  return response;
}

function unsupportedMediaType(correlationId: ApiError["correlation_id"]) {
  const response = NextResponse.json(
    {
      code: "UNSUPPORTED_MEDIA_TYPE",
      message: "Le format de la demande n'est pas pris en charge.",
      correlation_id: correlationId,
    } satisfies ApiError,
    { status: 415 },
  );
  response.headers.set(CORRELATION_HEADER, correlationId);
  return response;
}

function portalLoginForbidden(correlationId: ApiError["correlation_id"]) {
  const response = NextResponse.json(
    {
      code: "PORTAL_LOGIN_FORBIDDEN",
      message: "La connexion n'est pas autorisée depuis ce portail.",
      correlation_id: correlationId,
    } satisfies ApiError,
    { status: 403 },
  );
  response.headers.set(CORRELATION_HEADER, correlationId);
  return response;
}
