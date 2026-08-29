import type {
  DemoContentTemplateAdminView,
  DemoContentTemplateMutationResponse,
  DemoContentTemplateSavePayload,
  DemoContentTemplateServicePayload,
} from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminGet, handleAdminMutation } from "@/lib/admin-bff";
import {
  invalidCommunicationRequest,
  isExpectedVersion,
  readJsonBody,
} from "@/lib/admin-communications-bff";

export function GET(request: NextRequest) {
  return handleAdminGet<DemoContentTemplateAdminView>(
    request,
    "/internal/admin/settings/demo-templates",
  );
}

// Le BFF ne valide que la forme. Le registre ferme des types de service, les
// bornes de texte et l'unicite des noms sont revalides par API-INTERNAL, seule
// autorite : un type inconnu du code y est refuse, pas ici.
export async function PUT(request: NextRequest) {
  const body = await readJsonBody(request);
  if (!isPayload(body)) {
    return invalidCommunicationRequest(request);
  }
  return handleAdminMutation<
    DemoContentTemplateSavePayload,
    DemoContentTemplateMutationResponse
  >(request, "/internal/admin/settings/demo-templates", "PUT", body);
}

function isPayload(value: unknown): value is DemoContentTemplateSavePayload {
  if (!value || typeof value !== "object") return false;
  const candidate = value as Partial<DemoContentTemplateSavePayload>;
  return typeof candidate.templateKey === "string"
    && candidate.templateKey.trim().length > 0
    && typeof candidate.label === "string"
    && candidate.label.trim().length > 0
    && typeof candidate.description === "string"
    && typeof candidate.enabled === "boolean"
    && typeof candidate.displayOrder === "number"
    && Number.isInteger(candidate.displayOrder)
    && isExpectedVersion(candidate.expectedVersion)
    && Array.isArray(candidate.services)
    && candidate.services.every(isServicePayload);
}

function isServicePayload(
  value: unknown,
): value is DemoContentTemplateServicePayload {
  if (!value || typeof value !== "object") return false;
  const candidate = value as Partial<DemoContentTemplateServicePayload>;
  return typeof candidate.serviceType === "string"
    && candidate.serviceType.trim().length > 0
    && typeof candidate.name === "string"
    && candidate.name.trim().length > 0
    && typeof candidate.description === "string"
    && typeof candidate.scope === "string";
}
