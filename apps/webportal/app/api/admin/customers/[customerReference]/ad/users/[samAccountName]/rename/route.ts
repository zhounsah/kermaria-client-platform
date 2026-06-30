import type { AdMutationResponse, AdUserRenamePayload } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { controlledAdminError, handleAdminMutation } from "@/lib/admin-bff";
import { parseAdUserRenamePayload } from "@/lib/bff-payloads";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { isValidPortalIdentifier } from "@/lib/portal-bff";

type RouteContext = {
  params: Promise<{ customerReference: string; samAccountName: string }>;
};

export async function POST(request: NextRequest, context: RouteContext) {
  const { customerReference, samAccountName } = await context.params;
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  if (
    !isValidPortalIdentifier(customerReference)
    || !/^[A-Za-z0-9._-]{1,64}$/.test(samAccountName)
  ) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "La ressource demandee est invalide.",
      correlationId,
    );
  }

  const rawJson = await readJson(request);
  const payload = parseAdUserRenamePayload(rawJson);
  if (!payload) {
    console.warn(
      "[ad-rename-bff] payload rejected by parser",
      summarizeRenameInput(rawJson),
    );
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Renommage AD rejete : SAM doit matcher /^[A-Za-z0-9._-]{1,64}$/, displayName 3-200 chars, UPN optionnel doit utiliser un domaine autorise (defaut home.bzh).",
      correlationId,
    );
  }

  return handleAdminMutation<AdUserRenamePayload, AdMutationResponse>(
    request,
    `/internal/admin/customers/${encodeURIComponent(customerReference)}/ad/users/${encodeURIComponent(samAccountName)}/rename`,
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

// Logs a non-sensitive shape summary (no values) to help diagnose
// rejected payloads without leaking content.
function summarizeRenameInput(value: unknown) {
  if (!value || typeof value !== "object") {
    return { kind: typeof value };
  }

  const candidate = value as Record<string, unknown>;
  const samRaw = typeof candidate.newSamAccountName === "string"
    ? candidate.newSamAccountName
    : null;
  const dnRaw = typeof candidate.newDisplayName === "string"
    ? candidate.newDisplayName
    : null;
  const upnRaw = typeof candidate.newUserPrincipalName === "string"
    ? candidate.newUserPrincipalName
    : null;

  return {
    newSamAccountName: samRaw === null
      ? "missing"
      : {
          length: samRaw.length,
          matchesRegex: /^[A-Za-z0-9._-]{1,64}$/.test(samRaw.trim()),
        },
    newDisplayName: dnRaw === null
      ? "missing"
      : {
          length: dnRaw.trim().length,
        },
    newUserPrincipalName: upnRaw === null
      ? null
      : {
          length: upnRaw.trim().length,
          hasAtSign: upnRaw.includes("@"),
          domain: upnRaw.split("@")[1]?.toLowerCase() ?? null,
        },
  };
}
