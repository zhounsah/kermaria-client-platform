import type { DataSubjectRequestDetail } from "@kermaria/shared";
import { NextRequest } from "next/server";
import { handleAdminGet } from "@/lib/admin-bff";

export function GET(request: NextRequest, { params }: { params: Promise<{ id: string }> }) {
  return params.then(({ id }) => handleAdminGet<DataSubjectRequestDetail>(
    request, `/internal/admin/data-requests/${encodeURIComponent(id)}`));
}
