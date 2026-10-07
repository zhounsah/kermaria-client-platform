import { NextRequest, NextResponse } from "next/server";
import { getInternalSession } from "@/lib/internal-api";
import { getInternalApiUrl, getInternalServiceHeaders } from "@/lib/runtime-config";
import { getSessionCookieName } from "@/lib/session-config";
import { resolveCorrelationId } from "@/lib/correlation";

export async function GET(request: NextRequest, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!/^[0-9a-f-]{36}$/i.test(id)) return new NextResponse(null, { status: 404 });
  const token = request.cookies.get(getSessionCookieName())?.value;
  if (!token) return new NextResponse(null, { status: 401 });
  const correlationId = resolveCorrelationId(null);
  try {
    const session = await getInternalSession(token, correlationId);
    if (session.user.role !== "client_user") return new NextResponse(null, { status: 403 });
    const base = getInternalApiUrl();
    if (!base) return new NextResponse(null, { status: 503 });
    const upstream = await fetch(`${base}/internal/portal/data-requests/${encodeURIComponent(id)}/file`, {
      cache: "no-store", signal: AbortSignal.timeout(20000),
      headers: { ...getInternalServiceHeaders(), "X-Portal-Session": token, "X-Correlation-Id": correlationId },
    });
    if (!upstream.ok) return new NextResponse(null, { status: upstream.status === 404 ? 404 : 503 });
    return new NextResponse(await upstream.arrayBuffer(), {
      headers: { "Content-Type": upstream.headers.get("Content-Type") ?? "application/octet-stream",
        "Content-Disposition": upstream.headers.get("Content-Disposition") ?? "attachment",
        "Cache-Control": "no-store", "X-Content-Type-Options": "nosniff" },
    });
  } catch { return new NextResponse(null, { status: 503 }); }
}
