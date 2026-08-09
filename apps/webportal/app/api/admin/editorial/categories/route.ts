import type { EditorialCategory } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { parseEditorialCategoryPayload } from "@/lib/bff-payloads";
import {
  controlledAdminError,
  handleAdminMutation,
} from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const payload = parseEditorialCategoryPayload(await readJson(request));
  if (!payload) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "La categorie editoriale fournie est invalide.",
      correlationId,
    );
  }

  return handleAdminMutation<typeof payload, EditorialCategory>(
    request,
    "/internal/admin/editorial/categories",
    "POST",
    payload,
  );
}

async function readJson(request: NextRequest) {
  try {
    return await request.json();
  } catch {
    return null;
  }
}
