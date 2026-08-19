import "server-only";

import type { BillingV2AdditionalUserAssignPayload } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { resolveCorrelationId, CORRELATION_HEADER } from "@/lib/correlation";
import {
  controlledPortalError,
  handlePortalPayloadMutationTyped,
  isValidPortalIdentifier,
} from "@/lib/portal-bff";

export const dynamic = "force-dynamic";

type AssignResponse = {
  code: string;
  message: string;
  correlation_id: string;
};

const MAX_FIELD_LENGTH = 160;

export async function POST(
  request: NextRequest,
  context: { params: Promise<{ id: string; userId: string }> },
) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const { id, userId } = await context.params;

  if (!isValidPortalIdentifier(id) || !isValidPortalIdentifier(userId)) {
    return controlledPortalError(
      400,
      "INVALID_REQUEST",
      "Identifiant invalide.",
      correlationId,
    );
  }

  let candidate: unknown;
  try {
    candidate = await request.json();
  } catch {
    return controlledPortalError(
      400,
      "INVALID_REQUEST",
      "La demande est invalide.",
      correlationId,
    );
  }

  // Recopie champ par champ, jamais l'objet reçu : un corps qui porterait
  // `customerId` ou `actorReference` serait relayé tel quel par un spread, et
  // le navigateur choisirait alors pour le compte de qui la place est
  // équipée. Ces valeurs viennent de la session, côté API.
  const payload = parseAssignPayload(candidate);
  if (!payload) {
    return controlledPortalError(
      400,
      "INVALID_REQUEST",
      "Les informations de l'utilisateur sont incomplètes ou invalides.",
      correlationId,
    );
  }

  return handlePortalPayloadMutationTyped<
    AssignResponse,
    BillingV2AdditionalUserAssignPayload
  >(
    request,
    `/internal/portal/billing-v2/subscriptions/${encodeURIComponent(id)}`
      + `/users/${encodeURIComponent(userId)}/assign`,
    payload,
  );
}

function parseAssignPayload(
  value: unknown,
): BillingV2AdditionalUserAssignPayload | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Record<string, unknown>;
  const email = text(candidate.email);
  const displayName = text(candidate.displayName);
  const personalTitle = text(candidate.personalTitle)?.toLowerCase() ?? null;
  const givenName = text(candidate.givenName);
  const surname = text(candidate.surname);
  const birthDate = text(candidate.birthDate);

  if (
    !email
    || email.length > 255
    || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)
    || !displayName
    || displayName.length > MAX_FIELD_LENGTH
    || (personalTitle !== "madame" && personalTitle !== "monsieur")
    || !givenName
    || !surname
    || !birthDate
    || !/^\d{4}-\d{2}-\d{2}$/.test(birthDate)
  ) {
    return null;
  }

  const optionals = {
    initials: text(candidate.initials),
    phone: text(candidate.phone),
  };

  if (
    givenName.length > MAX_FIELD_LENGTH
    || surname.length > MAX_FIELD_LENGTH
    || Object.values(optionals).some(
      (field) => field !== null && field.length > MAX_FIELD_LENGTH,
    )
  ) {
    return null;
  }

  return {
    email,
    displayName,
    personalTitle,
    givenName,
    surname,
    birthDate,
    ...optionals,
  };
}


function text(value: unknown): string | null {
  if (typeof value !== "string") {
    return null;
  }

  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}
