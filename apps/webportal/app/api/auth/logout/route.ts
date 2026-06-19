import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { clearCsrfCookie } from "@/lib/csrf-server";
import { revokeInternalSession } from "@/lib/internal-api";
import {
  getSessionCookieName,
  getSessionCookieOptions,
} from "@/lib/session-config";

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const cookieName = getSessionCookieName();
  const sessionToken = request.cookies.get(cookieName)?.value;

  if (sessionToken) {
    try {
      await revokeInternalSession(sessionToken, correlationId);
    } catch {
      // Le cookie local est supprimé même si l'API interne est indisponible.
    }
  }

  const response = NextResponse.json({ authenticated: false });
  response.cookies.set({
    name: cookieName,
    value: "",
    ...getSessionCookieOptions(),
    expires: new Date(0),
  });
  clearCsrfCookie(response);
  response.headers.set(CORRELATION_HEADER, correlationId);
  return response;
}
