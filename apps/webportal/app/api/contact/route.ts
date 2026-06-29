import "server-only";

import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  checkRateLimit,
  getRequestIdentifier,
} from "@/lib/rate-limit";
import {
  getInternalApiUrl,
  getInternalServiceHeaders,
} from "@/lib/runtime-config";

type ContactRequestBody = {
  name?: unknown;
  email?: unknown;
  subject?: unknown;
  message?: unknown;
  offerReference?: unknown;
};

type FieldErrors = Partial<
  Record<"name" | "email" | "subject" | "message", string>
>;

const MAX_NAME_LENGTH = 120;
const MAX_EMAIL_LENGTH = 254;
const MAX_SUBJECT_LENGTH = 150;
const MAX_MESSAGE_LENGTH = 5000;
const RATE_LIMIT_MAX = 5;
const RATE_LIMIT_WINDOW_MS = 5 * 60 * 1000;

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(null);
  const identifier = getRequestIdentifier(request);
  const rateDecision = checkRateLimit(
    `contact:${identifier}`,
    RATE_LIMIT_MAX,
    RATE_LIMIT_WINDOW_MS,
  );

  if (rateDecision.limited) {
    const response = NextResponse.json(
      {
        code: "RATE_LIMITED",
        message:
          "Trop d'envois successifs. Réessayez dans quelques minutes.",
        correlation_id: correlationId,
      },
      { status: 429 },
    );
    response.headers.set(
      "Retry-After",
      String(rateDecision.retryAfterSeconds),
    );
    return response;
  }

  let body: ContactRequestBody;
  try {
    body = (await request.json()) as ContactRequestBody;
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

  const { errors, payload } = validateContactPayload(body);
  if (Object.keys(errors).length > 0) {
    return NextResponse.json(
      {
        code: "INVALID_REQUEST",
        message: "Vérifiez les champs signalés.",
        field_errors: errors,
        correlation_id: correlationId,
      },
      { status: 400 },
    );
  }

  let internalApiUrl: string | undefined;
  try {
    internalApiUrl = getInternalApiUrl();
  } catch {
    return serviceUnavailable(correlationId);
  }

  if (!internalApiUrl) {
    if (process.env.NODE_ENV !== "production") {
      return NextResponse.json(
        {
          code: "CONTACT_MOCK_ACCEPTED",
          message:
            "Message reçu (mode développement local sans API interne).",
          correlation_id: correlationId,
        },
        { status: 202 },
      );
    }
    return serviceUnavailable(correlationId);
  }

  try {
    const upstream = await fetch(
      `${internalApiUrl}/internal/public/contact-message`,
      {
        method: "POST",
        cache: "no-store",
        signal: AbortSignal.timeout(10_000),
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json",
          ...getInternalServiceHeaders(),
          [CORRELATION_HEADER]: correlationId,
        },
        body: JSON.stringify(payload),
      },
    );

    const responseBody = await safeReadJson(upstream);

    if (!upstream.ok) {
      return NextResponse.json(
        {
          code:
            typeof responseBody?.code === "string"
              ? responseBody.code
              : "CONTACT_DISPATCH_FAILED",
          message:
            typeof responseBody?.message === "string"
              ? responseBody.message
              : "L'envoi du message a échoué.",
          correlation_id: correlationId,
        },
        { status: upstream.status >= 500 ? 502 : upstream.status },
      );
    }

    return NextResponse.json(
      {
        code:
          typeof responseBody?.code === "string"
            ? responseBody.code
            : "EMAIL_SENT",
        message:
          typeof responseBody?.message === "string"
            ? responseBody.message
            : "Message envoyé.",
        correlation_id: correlationId,
      },
      { status: 200 },
    );
  } catch {
    return serviceUnavailable(correlationId);
  }
}

function serviceUnavailable(correlationId: string) {
  return NextResponse.json(
    {
      code: "INTERNAL_API_UNAVAILABLE",
      message:
        "Le service est temporairement indisponible. Réessayez dans quelques instants.",
      correlation_id: correlationId,
    },
    { status: 503 },
  );
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

function validateContactPayload(body: ContactRequestBody) {
  const errors: FieldErrors = {};
  const name = typeof body.name === "string" ? body.name.trim() : "";
  const email = typeof body.email === "string" ? body.email.trim() : "";
  const subject =
    typeof body.subject === "string" ? body.subject.trim() : "";
  const message =
    typeof body.message === "string" ? body.message.trim() : "";
  const offerReferenceRaw =
    typeof body.offerReference === "string"
      ? body.offerReference.trim()
      : "";

  if (!name) {
    errors.name = "Le nom est requis.";
  } else if (name.length > MAX_NAME_LENGTH) {
    errors.name = `Le nom ne peut dépasser ${MAX_NAME_LENGTH} caractères.`;
  }

  if (!email) {
    errors.email = "L'adresse e-mail est requise.";
  } else if (email.length > MAX_EMAIL_LENGTH || !emailPattern.test(email)) {
    errors.email = "L'adresse e-mail est invalide.";
  }

  if (subject.length > MAX_SUBJECT_LENGTH) {
    errors.subject = `Le sujet ne peut dépasser ${MAX_SUBJECT_LENGTH} caractères.`;
  }

  if (!message) {
    errors.message = "Le message est requis.";
  } else if (message.length > MAX_MESSAGE_LENGTH) {
    errors.message = `Le message ne peut dépasser ${MAX_MESSAGE_LENGTH} caractères.`;
  }

  const offerReference =
    offerReferenceRaw.length > 64 ? null : offerReferenceRaw || null;

  return {
    errors,
    payload: {
      name,
      email,
      subject,
      message,
      offerReference,
    },
  };
}
