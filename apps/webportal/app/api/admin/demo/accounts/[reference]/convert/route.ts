import type { DemoConversionResult } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminMutation } from "@/lib/admin-bff";

/**
 * La conversion porte des codes de services Billing V2, pas une reference
 * d'offre : c'est `billing_v2_provisioning_rules` qui decrit les groupes AD
 * reellement accordes.
 */
type ConvertPayload = { serviceCodes: string[] };

const SERVICE_CODE_PATTERN = /^[A-Z0-9][A-Z0-9-]{1,63}$/;

export async function POST(
  request: NextRequest,
  context: { params: Promise<{ reference: string }> },
) {
  const { reference } = await context.params;
  const body = await readJson(request);
  const serviceCodes = Array.isArray(body?.serviceCodes)
    ? body.serviceCodes
        .map((value) =>
          typeof value === "string" ? value.trim().toUpperCase() : "",
        )
        .filter((value) => SERVICE_CODE_PATTERN.test(value))
    : [];

  return handleAdminMutation<ConvertPayload, DemoConversionResult>(
    request,
    `/internal/admin/demo/accounts/${encodeURIComponent(reference)}/convert`,
    "POST",
    { serviceCodes },
  );
}

async function readJson(
  request: NextRequest,
): Promise<{ serviceCodes?: unknown } | null> {
  try {
    return await request.json();
  } catch {
    return null;
  }
}
