import "server-only";

import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import { rejectInvalidPortalCsrf } from "@/lib/portal-bff";
import {
  getInternalApiError,
  getInternalSession,
  getPrivateVatTestEligibility,
  mutateInternalPortalPayloadTyped,
} from "@/lib/internal-api";
import { getPortalPublicUrl } from "@/lib/public-routes";
import { readBillingV2SelectionPayload } from "@/lib/billing-v2-selection";
import { getSessionCookieName } from "@/lib/session-config";
import { isBillingV2AuthoritativeCheckoutBffEnabled } from "@/lib/runtime-config";

/**
 * Souscription Billing V2 native.
 *
 * Ce que le navigateur envoie : des codes catalogue, un engagement, un mode de
 * reglement, un rail et une cle d'idempotence. Rien d'autre n'est relaye — la
 * reconstruction stricte de la selection garantit qu'un corps enrichi (montant,
 * remise, prix fournisseur) n'atteint jamais API-INTERNAL.
 *
 * Ce que le serveur fait : revalide la configuration contre le catalogue,
 * recalcule le prix avec BillingV2PricingEngine, cree l'intention et le
 * BillingEvent, puis publie la commande de checkout. Le BFF ne calcule ni
 * n'arbitre aucun montant.
 */
type BillingV2AuthoritativeCheckoutResponse = {
  created: boolean;
  subscriptionId: string;
  provider: string;
  environment: string;
  outboxEventId: string;
  idempotencyKeyHash: string;
  totalDueNowCents: number;
  reasonCode: string;
  approvalUrl?: string | null;
  correlationId: string;
};

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const fail = (code: string, message: string, status: number) =>
    NextResponse.json(
      { code, message, correlation_id: correlationId },
      { status, headers: { [CORRELATION_HEADER]: correlationId } },
    );

  // Refuser une requete non authentifiee ou cross-site avant de parser le
  // payload commercial. La validation metier ne doit pas preceder le garde CSRF.
  const sessionToken = request.cookies.get(getSessionCookieName())?.value;
  if (!sessionToken) {
    return fail("UNAUTHORIZED", "Session requise.", 401);
  }

  const csrfFailure = rejectInvalidPortalCsrf(request);
  if (csrfFailure) {
    return csrfFailure;
  }

  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return fail(
      "INVALID_REQUEST",
      "Le corps de la requete est invalide.",
      400,
    );
  }

  const selection = readBillingV2SelectionPayload(body);
  if (!selection) {
    return fail(
      "INVALID_REQUEST",
      "La configuration demandee est incomplete.",
      400,
    );
  }
  const acceptedCgvVersionLabel = (body as { acceptedCgvVersionLabel?: unknown })
    ?.acceptedCgvVersionLabel;
  if (typeof acceptedCgvVersionLabel !== "string"
    || !acceptedCgvVersionLabel.startsWith("Version du :")
    || acceptedCgvVersionLabel.length > 160) {
    return fail("BILLING_V2_CGV_ACCEPTANCE_REQUIRED", "Acceptez les CGV en vigueur avant de continuer.", 400);
  }

  // Seul le rail Stripe est ouvert a la souscription V2 native : le rail
  // PayPal n'a pas ete valide pour ce parcours.
  const rail = (body as { rail?: unknown }).rail;
  if (rail !== "stripe") {
    return fail(
      "BILLING_V2_RAIL_UNSUPPORTED",
      "Ce moyen de paiement n'est pas disponible pour cette offre.",
      400,
    );
  }

  const idempotencyKey = request.headers.get("Idempotency-Key")?.trim();
  if (!idempotencyKey || idempotencyKey.length > 128) {
    return fail(
      "BILLING_V2_IDEMPOTENCY_KEY_REQUIRED",
      "Une cle d'idempotence est requise pour initialiser un checkout.",
      400,
    );
  }

  try {
    const session = await getInternalSession(sessionToken, correlationId);
    if (session.user.role !== "client_user") {
      return fail("ACCESS_DENIED", "Acces refuse.", 403);
    }
  } catch {
    return fail("SESSION_INVALID", "Session invalide.", 401);
  }

  // Le BFF public reste fermé. Cette lecture liée à la session permet
  // uniquement au compte de recette de poursuivre vers le garde API final.
  if (!isBillingV2AuthoritativeCheckoutBffEnabled()) {
    try {
      const eligibility = await getPrivateVatTestEligibility(
        sessionToken, correlationId);
      if (!eligibility.eligible) {
        return fail("BILLING_V2_AUTHORITATIVE_CHECKOUT_FLAG_OFF",
          "La souscription en ligne n'est pas encore ouverte.", 503);
      }
    } catch {
      return fail("BILLING_V2_AUTHORITATIVE_CHECKOUT_FLAG_OFF",
        "La souscription en ligne n'est pas encore ouverte.", 503);
    }
  }

  const portalUrl = getPortalPublicUrl(request);
  const returnPath =
    "/api/subscriptions/billing-v2/return"
    + "?provider=stripe&session_id={CHECKOUT_SESSION_ID}";
  const cancelPath = selection.presetCode
    ? `/formules/${encodeURIComponent(selection.presetCode)}`
      + "?souscription=annulee"
    : "/souscrire?souscription=annulee";

  let result: BillingV2AuthoritativeCheckoutResponse;
  try {
    result = await mutateInternalPortalPayloadTyped<
      BillingV2AuthoritativeCheckoutResponse,
      {
        selection: typeof selection;
        provider: string;
        idempotencyKey: string;
        successUrl: string;
        cancelUrl: string;
        acceptedCgvVersionLabel: string;
      }
    >(
      "/internal/portal/billing-v2/subscriptions/checkout",
      {
        selection,
        provider: "stripe",
        idempotencyKey,
        successUrl: `${portalUrl}${returnPath}`,
        cancelUrl: `${portalUrl}${cancelPath}`,
        acceptedCgvVersionLabel,
      },
      sessionToken,
      correlationId,
    );
  } catch (error) {
    const failure = getInternalApiError(error);
    return NextResponse.json(failure.error, {
      status: failure.status,
      headers: { [CORRELATION_HEADER]: correlationId },
    });
  }

  if (result.approvalUrl) {
    return NextResponse.json(
      {
        approveUrl: result.approvalUrl,
        subscriptionId: result.subscriptionId,
        totalDueNowCents: result.totalDueNowCents,
        correlation_id: result.correlationId,
      },
      { headers: { [CORRELATION_HEADER]: correlationId } },
    );
  }

  // L'intention locale existe, la session fournisseur n'est pas encore
  // publiee : le client doit reessayer, pas etre redirige vers rien.
  return NextResponse.json(
    {
      code: "BILLING_V2_CHECKOUT_PENDING_PROVIDER_SESSION",
      message:
        "Votre configuration est enregistree, la page de paiement n'est pas "
        + "encore prete. Reessayez dans quelques instants.",
      subscriptionId: result.subscriptionId,
      outboxEventId: result.outboxEventId,
      correlation_id: result.correlationId,
    },
    { status: 409, headers: { [CORRELATION_HEADER]: correlationId } },
  );
}
