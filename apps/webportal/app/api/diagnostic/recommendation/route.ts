import "server-only";

import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { getInternalApiUrl, getInternalServiceHeaders } from "@/lib/runtime-config";

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(request.headers.get(CORRELATION_HEADER));
  if (!request.headers.get("content-type")?.toLowerCase().startsWith("application/json")) {
    return NextResponse.json({ code: "UNSUPPORTED_MEDIA_TYPE", message: "Le format de la demande est invalide.", correlation_id: correlationId }, { status: 415 });
  }
  let payload: unknown;
  try { payload = await request.json(); } catch {
    return NextResponse.json({ code: "INVALID_REQUEST", message: "Le corps de la demande est invalide.", correlation_id: correlationId }, { status: 400 });
  }
  let internalApiUrl: string | undefined;
  try { internalApiUrl = getInternalApiUrl(); } catch { internalApiUrl = undefined; }
  if (!internalApiUrl) return NextResponse.json({ code: "INTERNAL_API_UNAVAILABLE", message: "Le service est temporairement indisponible.", correlation_id: correlationId }, { status: 503 });
  try {
    const upstream = await fetch(`${internalApiUrl}/internal/public/diagnostic/recommendation`, {
      method: "POST", cache: "no-store", signal: AbortSignal.timeout(10_000),
      headers: { Accept: "application/json", "Content-Type": "application/json", ...getInternalServiceHeaders(), [CORRELATION_HEADER]: correlationId },
      body: JSON.stringify(payload),
    });
    const body = await upstream.text();
    return new NextResponse(body, { status: upstream.status, headers: { "Content-Type": "application/json", [CORRELATION_HEADER]: correlationId } });
  } catch {
    return NextResponse.json({ code: "INTERNAL_API_UNAVAILABLE", message: "Le service est temporairement indisponible.", correlation_id: correlationId }, { status: 503 });
  }
}
