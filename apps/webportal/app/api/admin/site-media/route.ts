import type { SiteMediaAsset } from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";
import { controlledAdminError, handleAdminGet } from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { hasValidCsrfToken } from "@/lib/csrf-server";
import { getInternalSession } from "@/lib/internal-api";
import { getInternalApiUrl, getInternalServiceHeaders } from "@/lib/runtime-config";
import { getSessionCookieName } from "@/lib/session-config";

export function GET(request: NextRequest) {
  return handleAdminGet<SiteMediaAsset[]>(request, "/internal/admin/site-media");
}

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(request.headers.get(CORRELATION_HEADER));
  const token = request.cookies.get(getSessionCookieName())?.value;
  if (!token) return controlledAdminError(401, "UNAUTHORIZED", "Connectez-vous.", correlationId);
  if (!hasValidCsrfToken(request)) return controlledAdminError(403, "CSRF_FORBIDDEN", "Requête non autorisée.", correlationId);
  try {
    const session = await getInternalSession(token, correlationId);
    if (session.user.role !== "internal_admin") return controlledAdminError(403, "ACCESS_DENIED", "Accès refusé.", correlationId);
    const data = await request.formData();
    const file = data.get("file");
    if (!(file instanceof File) || file.size < 20 || file.size > 5 * 1024 * 1024)
      return controlledAdminError(400, "INVALID_IMAGE", "Choisissez une image de 5 Mo maximum.", correlationId);
    const base = getInternalApiUrl();
    if (!base) return controlledAdminError(503, "INTERNAL_API_UNAVAILABLE", "Service indisponible.", correlationId);
    const response = await fetch(`${base}/internal/admin/site-media`, {
      method: "POST", body: data, cache: "no-store", signal: AbortSignal.timeout(20000),
      headers: { ...getInternalServiceHeaders(), "X-Portal-Session": token, [CORRELATION_HEADER]: correlationId },
    });
    const body = await response.json();
    return NextResponse.json(body, { status: response.status });
  } catch {
    return controlledAdminError(503, "MEDIA_UNAVAILABLE", "Envoi impossible pour le moment.", correlationId);
  }
}
