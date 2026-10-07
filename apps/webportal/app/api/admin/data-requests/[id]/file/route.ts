import type { DataSubjectRequestDetail } from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";
import { getInternalSession } from "@/lib/internal-api";
import { getInternalApiUrl, getInternalServiceHeaders } from "@/lib/runtime-config";
import { getSessionCookieName } from "@/lib/session-config";
import { hasValidCsrfToken } from "@/lib/csrf-server";
import { resolveCorrelationId } from "@/lib/correlation";

export async function POST(request: NextRequest, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!/^[0-9a-f-]{36}$/i.test(id)) return NextResponse.json({ code: "INVALID_REQUEST", message: "Demande invalide." }, { status: 400 });
  const token = request.cookies.get(getSessionCookieName())?.value;
  if (!token) return NextResponse.json({ code: "UNAUTHORIZED", message: "Connectez-vous." }, { status: 401 });
  if (!hasValidCsrfToken(request)) return NextResponse.json({ code: "CSRF_FORBIDDEN", message: "Requête non autorisée." }, { status: 403 });
  const correlationId = resolveCorrelationId(null);
  try {
    const session = await getInternalSession(token, correlationId);
    if (session.user.role !== "internal_admin") return NextResponse.json({ code: "ACCESS_DENIED", message: "Accès refusé." }, { status: 403 });
    const form = await request.formData();
    const file = form.get("file");
    if (!(file instanceof File) || file.size < 4 || file.size > 10 * 1024 * 1024)
      return NextResponse.json({ code: "INVALID_FILE", message: "Fichier invalide ou trop volumineux." }, { status: 400 });
    const base = getInternalApiUrl();
    if (!base) return NextResponse.json({ code: "UNAVAILABLE", message: "Service indisponible." }, { status: 503 });
    const upstream = await fetch(`${base}/internal/admin/data-requests/${encodeURIComponent(id)}/file`, {
      method: "POST", body: form, cache: "no-store", signal: AbortSignal.timeout(30000),
      headers: { ...getInternalServiceHeaders(), "X-Portal-Session": token, "X-Correlation-Id": correlationId },
    });
    const payload = await upstream.json();
    return NextResponse.json(payload as DataSubjectRequestDetail, { status: upstream.status });
  } catch { return NextResponse.json({ code: "UNAVAILABLE", message: "Envoi impossible pour le moment." }, { status: 503 }); }
}
