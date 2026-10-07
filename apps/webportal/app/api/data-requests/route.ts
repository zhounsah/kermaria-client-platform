import type { DataSubjectRequestCreatePayload, DataSubjectRequestDetail, DataSubjectRequestSummary } from "@kermaria/shared";
import { NextRequest } from "next/server";
import { controlledPortalError, handlePortalGet, handlePortalPayloadMutationTyped } from "@/lib/portal-bff";
import { resolveCorrelationId } from "@/lib/correlation";

const types = new Set(["access", "rectification", "erasure", "portability", "objection", "restriction"]);

export function GET(request: NextRequest) {
  return handlePortalGet<DataSubjectRequestSummary[]>(request, "/internal/portal/data-requests");
}

export async function POST(request: NextRequest) {
  const value: unknown = await request.json().catch(() => null);
  if (!value || typeof value !== "object") return invalid();
  const candidate = value as Record<string, unknown>;
  if (typeof candidate.requestType !== "string" || !types.has(candidate.requestType)
    || typeof candidate.details !== "string"
    || candidate.details.trim().length < 10 || candidate.details.trim().length > 5000) return invalid();
  const payload: DataSubjectRequestCreatePayload = {
    requestType: candidate.requestType as DataSubjectRequestCreatePayload["requestType"],
    details: candidate.details.trim(),
  };
  return handlePortalPayloadMutationTyped<DataSubjectRequestDetail, DataSubjectRequestCreatePayload>(
    request, "/internal/portal/data-requests", payload);
}

function invalid() {
  return controlledPortalError(400, "INVALID_REQUEST", "Vérifiez la demande saisie.", resolveCorrelationId(null));
}
