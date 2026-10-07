import { NextRequest, NextResponse } from "next/server";
import { getInternalApiUrl, getInternalServiceHeaders } from "@/lib/runtime-config";

export async function GET(_request: NextRequest, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!/^[0-9a-f-]{36}$/i.test(id)) return new NextResponse(null, { status: 404 });
  const base = getInternalApiUrl();
  if (!base) return new NextResponse(null, { status: 503 });
  try {
    const response = await fetch(`${base}/internal/public/site-media/${encodeURIComponent(id)}`, {
      cache: "force-cache", headers: getInternalServiceHeaders(), signal: AbortSignal.timeout(10000),
    });
    if (!response.ok) return new NextResponse(null, { status: response.status === 404 ? 404 : 503 });
    return new NextResponse(await response.arrayBuffer(), {
      headers: { "Content-Type": response.headers.get("Content-Type") ?? "application/octet-stream",
        "X-Content-Type-Options": "nosniff", "Cache-Control": "public, max-age=31536000, immutable" },
    });
  } catch { return new NextResponse(null, { status: 503 }); }
}
