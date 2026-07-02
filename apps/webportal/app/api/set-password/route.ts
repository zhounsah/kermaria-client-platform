import "server-only";

import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  checkRateLimit,
  getRequestIdentifier,
} from "@/lib/rate-limit";
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
    const response = NextResponse.json(
      {
        code: "RATE_LIMITED",
        message: "Trop de tentatives. Réessayez dans quelques minutes.",
        correlation_id: correlationId,
      },
      { status: 429 },
    );
    response.headers.set("Retry-After", String(rateDecision.retryAfterSeconds));
    return response;
  }

  let body: SetPasswordRequestBody;
  try {
    body = (await request.json()) as SetPasswordRequestBody;
  } catch {
    return NextResponse.json(
      {
        code: "INVALID_REQUEST",
        message: "Le corps de la requête est invalide.",
        correlation_id: correlationId,
      },
      { status: 400 },
    );
  }

  const token = typeof body.token === "string" ? body.token.trim() : "";
  const password = typeof body.password === "string" ? body.password : "";

  if (!token) {
    return NextResponse.json(
      {
        code: "TOKEN_INVALID",
        message: "Lien invalide ou expiré.",
        correlation_id: correlationId,
      },
      { status: 400 },
    );
  }

  if (
    password.length < MIN_PASSWORD_LENGTH
    || password.length > MAX_PASSWORD_LENGTH
  ) {
    return NextResponse.json(
      {
        code: "INVALID_PASSWORD",
        message: `Le mot de passe doit comporter entre ${MIN_PASSWORD_LENGTH} et ${MAX_PASSWORD_LENGTH} caractères.`,
        correlation_id: correlationId,
      },
      { status: 400 },
    );
  }

  const result = await callInternalSignup(
    "/internal/signup/set-password",
    { token, password },
    correlationId,
  );

  return NextResponse.json(
    {
      code: result.code,
      message: result.message,
      correlation_id: result.correlationId ?? correlationId,
    },
    { status: result.ok ? 200 : result.status >= 500 ? 502 : result.status },
  );
}
