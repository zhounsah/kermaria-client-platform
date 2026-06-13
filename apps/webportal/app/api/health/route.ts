import { SERVICE_NAMES } from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";

export const dynamic = "force-dynamic";

export function GET(request: NextRequest) {
  const correlationId = resolveCorrelationId(request.headers.get(CORRELATION_HEADER));
  const response = NextResponse.json({
    status: "ok",
    service: SERVICE_NAMES.webportal,
  });

  response.headers.set(CORRELATION_HEADER, correlationId);

  return response;
}
