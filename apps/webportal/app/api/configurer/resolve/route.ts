import "server-only";

import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { resolveCatalogConfiguration } from "@/lib/catalog-configuration-server";
import { normalizeCatalogConfigurationInput } from "@/lib/public-configurator";

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );

  let body: Record<string, unknown>;
  try {
    body = (await request.json()) as Record<string, unknown>;
  } catch {
    return NextResponse.json(
      {
        code: "INVALID_REQUEST",
        message: "Le corps de la requete est invalide.",
        correlation_id: correlationId,
      },
      { status: 400 },
    );
  }

  const configuration = normalizeCatalogConfigurationInput(body);
  if (!configuration) {
    return NextResponse.json(
      {
        code: "INVALID_CONFIGURATION",
        message: "La configuration demandee n'est pas valide.",
        correlation_id: correlationId,
      },
      { status: 400 },
    );
  }

  const result = await resolveCatalogConfiguration(configuration, correlationId);
  if (!result.ok) {
    return NextResponse.json(
      {
        code: result.error.code,
        message: result.error.message,
        correlation_id: result.error.correlation_id,
      },
      { status: result.status >= 500 ? 502 : result.status },
    );
  }

  return NextResponse.json(result.data, { status: 200 });
}
