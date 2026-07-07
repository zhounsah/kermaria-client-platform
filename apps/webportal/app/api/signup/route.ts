import "server-only";

import { NextRequest, NextResponse } from "next/server";

import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  getPublicCommercialCatalog,
  getPublicPackCatalogContent,
} from "@/lib/internal-api";
import { isSignupEnabled } from "@/lib/public-routes";
import {
  checkRateLimit,
  getRequestIdentifier,
} from "@/lib/rate-limit";
import { logBffFailure } from "@/lib/bff-observability";
import {
  buildSignupPackSnapshot,
  resolvePackSelectionInput,
} from "@/lib/public-packs";
import { callInternalSignup, verifyHCaptcha } from "@/lib/signup-server";

type SignupRequestBody = {
  companyName?: unknown;
  contactName?: unknown;
  email?: unknown;
  phone?: unknown;
  message?: unknown;
  packKey?: unknown;
  commitmentMonths?: unknown;
  paymentMode?: unknown;
  hcaptchaToken?: unknown;
  // Honeypot (doit rester vide) + horodatage de rendu (anti-bot timing).
  website?: unknown;
  formRenderedAt?: unknown;
};

const MAX_COMPANY_LENGTH = 200;
const MAX_CONTACT_LENGTH = 200;
const MAX_EMAIL_LENGTH = 320;
const MAX_PHONE_LENGTH = 40;
const MAX_MESSAGE_LENGTH = 2000;
const RATE_LIMIT_MAX = 3;
const RATE_LIMIT_WINDOW_MS = 60 * 60 * 1000;
const MIN_FILL_MS = 2_000;

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

// Réponse identique quel que soit l'état réel (email déjà pris, bot
// détecté, etc.) : non-leak sur l'existence d'un compte.
const ACCEPTED_BODY = {
  code: "SIGNUP_ACCEPTED",
  message:
    "Demande enregistrée. Vérifiez votre boîte mail pour confirmer votre adresse.",
};

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );

  if (!isSignupEnabled()) {
    return NextResponse.json(
      {
        code: "SIGNUP_DISABLED",
        message: "Les inscriptions ne sont pas ouvertes.",
        correlation_id: correlationId,
      },
      { status: 403 },
    );
  }

  const identifier = getRequestIdentifier(request);
  const rateDecision = checkRateLimit(
    `signup:${identifier}`,
    RATE_LIMIT_MAX,
    RATE_LIMIT_WINDOW_MS,
  );
  if (rateDecision.limited) {
    const response = NextResponse.json(
      {
        code: "RATE_LIMITED",
        message:
          "Trop de demandes successives. Réessayez dans quelques minutes.",
        correlation_id: correlationId,
      },
      { status: 429 },
    );
    response.headers.set("Retry-After", String(rateDecision.retryAfterSeconds));
    return response;
  }

  let body: SignupRequestBody;
  try {
    body = (await request.json()) as SignupRequestBody;
  } catch {
    return NextResponse.json(
      {
        code: "INVALID_REQUEST",
        message: "Le corps de la requête est invalide.",
        correlation_id: correlationId,
      },
      { status: 400 },
    );
  }

  // Anti-bot défense en profondeur : honeypot rempli ou formulaire
  // soumis trop vite -> on renvoie la réponse d'acceptation sans rien
  // persister (le bot ne peut pas distinguer un succès d'un rejet).
  const honeypot =
    typeof body.website === "string" ? body.website.trim() : "";
  const renderedAt =
    typeof body.formRenderedAt === "number" ? body.formRenderedAt : 0;
  if (honeypot.length > 0 || (renderedAt > 0 && Date.now() - renderedAt < MIN_FILL_MS)) {
    return NextResponse.json(
      { ...ACCEPTED_BODY, correlation_id: correlationId },
      { status: 200 },
    );
  }

  const { errors, payload } = validateSignupPayload(body);
  if (Object.keys(errors).length > 0) {
    return NextResponse.json(
      {
        code: "INVALID_REQUEST",
        message: "Vérifiez les champs signalés.",
        field_errors: errors,
        correlation_id: correlationId,
      },
      { status: 400 },
    );
  }

  const captchaToken =
    typeof body.hcaptchaToken === "string" ? body.hcaptchaToken : null;
  const captcha = await verifyHCaptcha(captchaToken, identifier);
  if (!captcha.ok) {
    // Le message client reste générique (non-leak), mais on trace le code
    // interne réel + les error-codes hCaptcha pour pouvoir distinguer une
    // mauvaise config (CAPTCHA_MISCONFIGURED), un mismatch sitekey/secret,
    // une indisponibilité réseau (CAPTCHA_UNAVAILABLE) ou un vrai échec.
    logBffFailure({
      category: "captcha",
      code: captcha.code,
      correlation_id: correlationId,
      operation: "signup.verifyHCaptcha",
      status: 400,
      surface: "webportal-bff",
      detail: captcha.errorCodes?.length
        ? captcha.errorCodes.join(",")
        : undefined,
    });
    return NextResponse.json(
      {
        code: "CAPTCHA_FAILED",
        message:
          "La vérification anti-robot a échoué. Rechargez la page et réessayez.",
        correlation_id: correlationId,
      },
      { status: 400 },
    );
  }

  const hasPackSelection =
    body.packKey !== undefined
    || body.commitmentMonths !== undefined
    || body.paymentMode !== undefined;
  let packSelection = null;
  if (hasPackSelection) {
    const selection = resolvePackSelectionInput({
      packKey: body.packKey,
      commitmentMonths: body.commitmentMonths,
      paymentMode: body.paymentMode,
    });
    if (!selection) {
      return NextResponse.json(
        {
          code: "INVALID_PACK_SELECTION",
          message: "Le pack choisi n'est pas valide.",
          correlation_id: correlationId,
        },
        { status: 400 },
      );
    }

    const [catalogResult, packContentResult] = await Promise.all([
      getPublicCommercialCatalog(),
      getPublicPackCatalogContent(),
    ]);
    packSelection = buildSignupPackSnapshot(
      catalogResult.data,
      selection,
      packContentResult.data,
    );
    if (!packSelection) {
      return NextResponse.json(
        {
          code: "PACK_SELECTION_UNAVAILABLE",
          message: "Le pack choisi n'est plus disponible.",
          correlation_id: correlationId,
        },
        { status: 409 },
      );
    }
  }

  const result = await callInternalSignup(
    "/internal/signup",
    {
      ...payload,
      packSelection,
      sourceAddress: identifier === "unknown" ? null : identifier,
      userAgent: request.headers.get("user-agent")?.slice(0, 500) ?? null,
    },
    correlationId,
  );

  if (!result.ok) {
    return NextResponse.json(
      {
        code: result.code,
        message: result.message,
        correlation_id: result.correlationId ?? correlationId,
      },
      { status: result.status >= 500 ? 502 : result.status },
    );
  }

  return NextResponse.json(
    { ...ACCEPTED_BODY, correlation_id: result.correlationId ?? correlationId },
    { status: 200 },
  );
}

function validateSignupPayload(body: SignupRequestBody) {
  const errors: Record<string, string> = {};
  const companyName =
    typeof body.companyName === "string" ? body.companyName.trim() : "";
  const contactName =
    typeof body.contactName === "string" ? body.contactName.trim() : "";
  const email = typeof body.email === "string" ? body.email.trim() : "";
  const phone = typeof body.phone === "string" ? body.phone.trim() : "";
  const message =
    typeof body.message === "string" ? body.message.trim() : "";

  if (!companyName) {
    errors.companyName = "Le nom ou la raison sociale est requis.";
  } else if (companyName.length > MAX_COMPANY_LENGTH) {
    errors.companyName = `Champ limité à ${MAX_COMPANY_LENGTH} caractères.`;
  }

  if (!contactName) {
    errors.contactName = "Le nom du contact est requis.";
  } else if (contactName.length > MAX_CONTACT_LENGTH) {
    errors.contactName = `Champ limité à ${MAX_CONTACT_LENGTH} caractères.`;
  }

  if (!email) {
    errors.email = "L'adresse e-mail est requise.";
  } else if (email.length > MAX_EMAIL_LENGTH || !emailPattern.test(email)) {
    errors.email = "L'adresse e-mail est invalide.";
  }

  if (phone.length > MAX_PHONE_LENGTH) {
    errors.phone = `Champ limité à ${MAX_PHONE_LENGTH} caractères.`;
  }

  if (message.length > MAX_MESSAGE_LENGTH) {
    errors.message = `Message limité à ${MAX_MESSAGE_LENGTH} caractères.`;
  }

  return {
    errors,
    payload: {
      companyName,
      contactName,
      email,
      phone: phone || null,
      message: message || null,
    },
  };
}
