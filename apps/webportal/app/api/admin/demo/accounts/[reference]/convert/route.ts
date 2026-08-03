import type { DemoConversionResult } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminMutation } from "@/lib/admin-bff";

type ConvertPayload = { offerExternalReference: string | null };

export async function POST(
  request: NextRequest,
  context: { params: Promise<{ reference: string }> },
) {
  const { reference } = await context.params;
  const body = await readJson(request);
  const offer =
    typeof body?.offerExternalReference === "string"
      && body.offerExternalReference.trim().length > 0
      ? body.offerExternalReference.trim()
      : null;

  return handleAdminMutation<ConvertPayload, DemoConversionResult>(
    request,
    `/internal/admin/demo/accounts/${encodeURIComponent(reference)}/convert`,
    "POST",
    { offerExternalReference: offer },
  );
}

async function readJson(
  request: NextRequest,
): Promise<{ offerExternalReference?: unknown } | null> {
  try {
    return await request.json();
  } catch {
    return null;
  }
}
