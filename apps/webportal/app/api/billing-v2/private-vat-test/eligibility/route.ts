import { NextRequest, NextResponse } from "next/server";

import { resolveCorrelationId } from "@/lib/correlation";
import { getPrivateVatTestEligibility } from "@/lib/internal-api";
import { readPortalSessionToken } from "@/lib/session-cookie";

/** Lecture sans effet de bord. API-INTERNAL fait foi pour l'allowlist. */
export async function GET(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get("X-Correlation-Id"),
  );
  const sessionToken = await readPortalSessionToken();
  if (!sessionToken) {
    return NextResponse.json({ eligible: false },
      { status: 401, headers: { "Cache-Control": "no-store" } });
  }
  try {
    const eligibility = await getPrivateVatTestEligibility(
      sessionToken, correlationId);
    return NextResponse.json(eligibility,
      { headers: { "Cache-Control": "no-store" } });
  } catch {
    return NextResponse.json({ eligible: false },
      { status: 503, headers: { "Cache-Control": "no-store" } });
  }
}
