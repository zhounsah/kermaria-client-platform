import type { DataSubjectRequestDetail, DataSubjectRequestMessagePayload } from "@kermaria/shared";
import { NextRequest } from "next/server";
import { handleAdminMutation } from "@/lib/admin-bff";

const statuses = new Set(["in_progress", "waiting_for_customer", "response_ready", "closed", "refused"]);

export async function POST(request: NextRequest, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const candidate: unknown = await request.json().catch(() => null);
  const value = candidate && typeof candidate === "object" ? candidate as Record<string, unknown> : {};
  const payload: DataSubjectRequestMessagePayload = {
    body: typeof value.body === "string" ? value.body.trim() : "",
    status: typeof value.status === "string" && statuses.has(value.status)
      ? value.status as DataSubjectRequestMessagePayload["status"] : undefined,
    extendDeadline: value.extendDeadline === true,
  };
  return handleAdminMutation<DataSubjectRequestMessagePayload, DataSubjectRequestDetail>(
    request, `/internal/admin/data-requests/${encodeURIComponent(id)}/messages`, "POST", payload);
}
