import type { DataSubjectRequestDetail } from "@kermaria/shared";
import { NextRequest } from "next/server";
import { handlePortalGet } from "@/lib/portal-bff";

export function GET(request: NextRequest, { params }: { params: Promise<{ id: string }> }) {
  return params.then(({ id }) => handlePortalGet<DataSubjectRequestDetail>(
    request, `/internal/portal/data-requests/${encodeURIComponent(id)}`));
}
