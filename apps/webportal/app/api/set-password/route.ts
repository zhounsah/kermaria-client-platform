import "server-only";

import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  checkRateLimit,
  getRequestIdentifier,
} from "@/lib/rate-limit";
import { getPortalPublicUrlFromHeaders } from "@/lib/public-routes";
import { callInternalSignup } from "@/lib/signup-server";

type SetPasswordRequestBody = {
  token?: unknown;
  password?: unknown;
};

const MIN_PASSWORD_LENGTH = 12;
const MAX_PASSWORD_LENGTH = 200;
const RATE_LIMIT_MAX = 5;
const RATE_LIMIT_WINDOW_MS = 15 * 60 * 1000;

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );

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
    };
    const response = respondSetPassword(
      request,
      isBrowserFormPost(request),
      payload,
      429,
    );
    response.headers.set("Retry-After", String(rateDecision.retryAfterSeconds));
    return response;
  }

  const browserFormPost = isBrowserFormPost(request);

  let body: SetPasswordRequestBody;
  try {
    body = browserFormPost
      ? await readBrowserFormBody(request)
      : (await request.json()) as SetPasswordRequestBody;
  } catch {
    return respondSetPassword(
      request,
      browserFormPost,
      {
        code: "INVALID_REQUEST",
        message: "Le corps de la requête est invalide.",
        correlation_id: correlationId,
      },
      400,
    );
  }

  const token = typeof body.token === "string" ? body.token.trim() : "";
  const password = typeof body.password === "string" ? body.password : "";

  if (!token) {
    return respondSetPassword(
      request,
      browserFormPost,
      {
        code: "TOKEN_INVALID",
        message: "Lien invalide ou expiré.",
        correlation_id: correlationId,
      },
      400,
      token,
    );
  }

  if (
    password.length < MIN_PASSWORD_LENGTH
    || password.length > MAX_PASSWORD_LENGTH
  ) {
    return respondSetPassword(
      request,
      browserFormPost,
      {
        code: "INVALID_PASSWORD",
        message: `Le mot de passe doit comporter entre ${MIN_PASSWORD_LENGTH} et ${MAX_PASSWORD_LENGTH} caractères.`,
        correlation_id: correlationId,
      },
      400,
      token,
    );
  }

  const result = await callInternalSignup(
    "/internal/signup/set-password",
    { token, password },
    correlationId,
  );

  return respondSetPassword(
    request,
    browserFormPost,
    {
      code: result.code,
      message: result.message,
      correlation_id: result.correlationId ?? correlationId,
    },
    result.ok ? 200 : result.status >= 500 ? 502 : result.status,
    token,
  );
}

function isBrowserFormPost(request: NextRequest) {
  const contentType = request.headers.get("content-type") ?? "";

  return (
    contentType.includes("application/x-www-form-urlencoded")
    || contentType.includes("multipart/form-data")
  );
}

async function readBrowserFormBody(
  request: NextRequest,
): Promise<SetPasswordRequestBody> {
  const formData = await request.formData();

  return {
    password: formData.get("password"),
    token: formData.get("token"),
  };
}

function respondSetPassword(
  request: NextRequest,
  browserFormPost: boolean,
  payload: { code: string; message: string; correlation_id: string },
  status: number,
  token?: string,
) {
  if (!browserFormPost) {
    return NextResponse.json(payload, { status });
  }

  const redirectUrl = new URL(
    "/set-password",
    getPortalPublicUrlFromHeaders(request.headers),
  );
  if (payload.code === "PASSWORD_SET") {
    redirectUrl.searchParams.set("status", "success");
  } else {
    if (token) {
      redirectUrl.searchParams.set("token", token);
    }
    redirectUrl.searchParams.set("error", payload.code);
  }

  return NextResponse.redirect(redirectUrl, { status: 303 });
}
