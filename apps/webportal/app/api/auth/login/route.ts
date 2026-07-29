import type { ApiError, LoginPayload } from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { ensureCsrfCookie } from "@/lib/csrf-server";
import {
  createInternalSession,
  getInternalApiError,
} from "@/lib/internal-api";
import { getPortalPublicUrlFromHeaders } from "@/lib/public-routes";
import {
  getSessionCookieName,
  getSessionCookieOptions,
} from "@/lib/session-config";
import { resolvePortalRoleUrl } from "@/lib/public-route-config";

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const browserFormPost = isBrowserFormPost(request);

  let payload: unknown;

  try {
    payload = browserFormPost
      ? await readBrowserFormPayload(request)
      : await request.json();
  } catch {
    return invalidCredentials(request, correlationId, browserFormPost);
  }

  if (!isLoginPayload(payload)) {
    return invalidCredentials(request, correlationId, browserFormPost);
  }

  try {
    const session = await createInternalSession(
      payload,
      correlationId,
      request.headers.get("user-agent"),
    );

    if (browserFormPost) {
      const response = NextResponse.redirect(
        resolvePortalRoleUrl(
          getPortalPublicUrlFromHeaders(request.headers),
          session.user.role,
        ),
        { status: 303 },
      );
      response.cookies.set({
        name: getSessionCookieName(),
        value: session.sessionToken,
        ...getSessionCookieOptions(),
        expires: new Date(session.expiresAt),
      });
      ensureCsrfCookie(request, response);
      response.headers.set(CORRELATION_HEADER, correlationId);
      return response;
    }

    const response = NextResponse.json({
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

    if (browserFormPost) {
      return redirectToLogin(
        request,
        failure.error.code,
        getLoginEmail(payload),
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

function isBrowserFormPost(request: NextRequest) {
  const contentType = request.headers.get("content-type") ?? "";

  return (
    contentType.includes("application/x-www-form-urlencoded")
    || contentType.includes("multipart/form-data")
  );
}

async function readBrowserFormPayload(
  request: NextRequest,
): Promise<Partial<LoginPayload>> {
  const formData = await request.formData();

  return {
    email: formData.get("email")?.toString() ?? "",
    password: formData.get("password")?.toString() ?? "",
  };
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

function getLoginEmail(payload: unknown) {
  if (!payload || typeof payload !== "object") {
    return "";
  }

  const email = (payload as Partial<LoginPayload>).email;
  return typeof email === "string" ? email.trim() : "";
}

function invalidCredentials(
  request: NextRequest,
  correlationId: ApiError["correlation_id"],
  browserFormPost: boolean,
) {
  if (browserFormPost) {
    return redirectToLogin(request, "INVALID_CREDENTIALS", "", correlationId);
  }

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

function redirectToLogin(
  request: NextRequest,
  errorCode: string,
  email: string,
  correlationId: string,
) {
  const redirectUrl = new URL(
    "/login",
    getPortalPublicUrlFromHeaders(request.headers),
  );
  redirectUrl.searchParams.set("error", errorCode);
  if (email) {
    redirectUrl.searchParams.set("email", email);
  }

  const response = NextResponse.redirect(redirectUrl, { status: 303 });
  response.headers.set(CORRELATION_HEADER, correlationId);
  return response;
}
