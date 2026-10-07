import type { DataSubjectRequestDetail, DataSubjectRequestMessagePayload } from "@kermaria/shared";
import { NextRequest } from "next/server";
import { handlePortalPayloadMutationTyped } from "@/lib/portal-bff";

export async function POST(request: NextRequest, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const candidate: unknown = await request.json().catch(() => null);
  const body = candidate && typeof candidate === "object" && "body" in candidate
    && typeof candidate.body === "string" ? candidate.body.trim() : "";
  const payload: DataSubjectRequestMessagePayload = { body };
  return handlePortalPayloadMutationTyped<DataSubjectRequestDetail, DataSubjectRequestMessagePayload>(
    request, `/internal/portal/data-requests/${encodeURIComponent(id)}/messages`, payload);
}
