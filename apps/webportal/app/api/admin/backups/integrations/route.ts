import type { BackupIntegrationPayload } from "@kermaria/shared";
import { NextRequest } from "next/server";

import {
  handleAdminGet,
  handleAdminMutation,
} from "@/lib/admin-bff";

export const dynamic = "force-dynamic";

export function GET(request: NextRequest) {
  return handleAdminGet(
    request,
    "/internal/admin/backups/integrations",
  );
}

export async function POST(request: NextRequest) {
  let candidate: unknown;
  try {
    candidate = await request.json();
  } catch {
    candidate = null;
  }

  const payload = parsePayload(candidate);
  if (!payload) {
    return handleAdminMutation(
      request,
      "/internal/admin/backups/integrations",
      "POST",
      undefined,
    );
  }

  return handleAdminMutation<
    BackupIntegrationPayload,
    unknown
  >(
    request,
    "/internal/admin/backups/integrations",
    "POST",
    payload,
  );
}

function parsePayload(value: unknown): BackupIntegrationPayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<BackupIntegrationPayload>;
  if (
    candidate.provider !== "veeam"
    || typeof candidate.externalJobId !== "string"
    || typeof candidate.customerId !== "string"
    || typeof candidate.serviceId !== "string"
    || typeof candidate.enabled !== "boolean"
    || typeof candidate.expectedIntervalMinutes !== "number"
    || typeof candidate.criticalAfterMinutes !== "number"
    || typeof candidate.staleAfterMinutes !== "number"
  ) {
    return null;
  }

  return {
    id: typeof candidate.id === "string" ? candidate.id : undefined,
    provider: "veeam",
    externalJobId: candidate.externalJobId.trim(),
    customerId: candidate.customerId.trim(),
    serviceId: candidate.serviceId.trim(),
    enabled: candidate.enabled,
    expectedIntervalMinutes: candidate.expectedIntervalMinutes,
    criticalAfterMinutes: candidate.criticalAfterMinutes,
    staleAfterMinutes: candidate.staleAfterMinutes,
  };
}
