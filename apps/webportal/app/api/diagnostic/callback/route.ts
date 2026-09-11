import "server-only";

import { NextRequest, NextResponse } from "next/server";

import { logBffFailure } from "@/lib/bff-observability";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { validateDiagnosticCallbackPayload } from "@/lib/diagnostic-callback";
import { validateDiagnosticCallbackRequest } from "@/lib/diagnostic-callback-security";
import { checkRateLimit, getRequestIdentifier } from "@/lib/rate-limit";
import { getInternalApiUrl, getInternalServiceHeaders } from "@/lib/runtime-config";

const RATE_LIMIT_MAX = 3;
const RATE_LIMIT_WINDOW_MS = 60 * 60 * 1000;
const MAX_BODY_BYTES = 8_192;

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(request.headers.get(CORRELATION_HEADER));
  const requestRejection = validateDiagnosticCallbackRequest(request);
  if (requestRejection) {
    return NextResponse.json(
      { ...requestRejection, correlation_id: correlationId },
      { status: requestRejection.status },
    );
  }
  const decision = checkRateLimit(`diagnostic-callback:${getRequestIdentifier(request)}`, RATE_LIMIT_MAX, RATE_LIMIT_WINDOW_MS);
  if (decision.limited) {
    const response = NextResponse.json({ code: "RATE_LIMITED", message: "Trop de demandes successives. Réessayez dans quelques minutes.", correlation_id: correlationId }, { status: 429 });
    response.headers.set("Retry-After", String(decision.retryAfterSeconds));
    return response;
  }

  const contentLength = Number(request.headers.get("content-length"));
  if (Number.isFinite(contentLength) && contentLength > MAX_BODY_BYTES) return payloadTooLarge(correlationId);

  let body: unknown;
  try {
    const rawBody = await request.text();
    if (new TextEncoder().encode(rawBody).byteLength > MAX_BODY_BYTES) return payloadTooLarge(correlationId);
    body = JSON.parse(rawBody);
  } catch {
    return NextResponse.json({ code: "INVALID_REQUEST", message: "Le corps de la requête est invalide.", correlation_id: correlationId }, { status: 400 });
  }
  const { payload, errors } = validateDiagnosticCallbackPayload(body);
  if (!payload) return NextResponse.json({ code: "INVALID_REQUEST", message: "Vérifiez les champs signalés.", field_errors: errors, correlation_id: correlationId }, { status: 400 });

  // Un robot reçoit une réponse neutre sans déclencher d'e-mail.
  if (payload.website) {
    return NextResponse.json({ code: "CALLBACK_SENT", message: "Votre demande a bien été envoyée.", correlation_id: correlationId });
  }

  let internalApiUrl: string | undefined;
  try { internalApiUrl = getInternalApiUrl(); } catch { internalApiUrl = undefined; }
  if (!internalApiUrl) return unavailable(correlationId);

  try {
    const upstream = await fetch(`${internalApiUrl}/internal/public/diagnostic-callback`, {
      method: "POST", cache: "no-store", signal: AbortSignal.timeout(10_000),
      headers: { Accept: "application/json", "Content-Type": "application/json", ...getInternalServiceHeaders(), [CORRELATION_HEADER]: correlationId },
      body: JSON.stringify({ ...payload, sourcePath: "/diagnostic" }),
    });
    if (!upstream.ok) {
      logBffFailure({ category: "diagnostic_callback", code: "CALLBACK_DISPATCH_FAILED", correlation_id: correlationId, operation: "POST /internal/public/diagnostic-callback", status: upstream.status, surface: "public" });
      return NextResponse.json({ code: "CALLBACK_DISPATCH_FAILED", message: "Votre demande n'a pas pu être transmise. Réessayez dans quelques instants.", correlation_id: correlationId }, { status: upstream.status >= 500 ? 502 : upstream.status });
    }
    return NextResponse.json({ code: "CALLBACK_SENT", message: "Votre demande a bien été envoyée. Je vous recontacterai dès que possible.", correlation_id: correlationId });
  } catch {
    return unavailable(correlationId);
  }
}

function unavailable(correlationId: string) {
  return NextResponse.json({ code: "INTERNAL_API_UNAVAILABLE", message: "Le service est temporairement indisponible. Réessayez dans quelques instants.", correlation_id: correlationId }, { status: 503 });
}

function payloadTooLarge(correlationId: string) {
  return NextResponse.json({ code: "PAYLOAD_TOO_LARGE", message: "La demande est trop volumineuse.", correlation_id: correlationId }, { status: 413 });
}
