import { SERVICE_NAMES } from "@kermaria/shared";
import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { checkInternalApiReadiness } from "@/lib/internal-api";
import { validateServerRuntimeConfiguration } from "@/lib/runtime-config";

export const dynamic = "force-dynamic";

export async function GET(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  let configurationHealthy = true;
  let apiInternalHealthy = false;

  try {
    validateServerRuntimeConfiguration();
    apiInternalHealthy = await checkInternalApiReadiness(correlationId);
  } catch {
    configurationHealthy = false;
  }

  const ready = configurationHealthy && apiInternalHealthy;
  const response = NextResponse.json(
    {
      status: ready ? "healthy" : "unhealthy",
      service: SERVICE_NAMES.webportal,
      check: "ready",
      timestamp_utc: new Date().toISOString(),
      checks: {
        configuration: configurationHealthy ? "healthy" : "unhealthy",
        api_internal: apiInternalHealthy ? "healthy" : "unhealthy",
      },
    },
    { status: ready ? 200 : 503 },
  );
  response.headers.set(CORRELATION_HEADER, correlationId);

  return response;
}
