import "server-only";

import { NextRequest, NextResponse } from "next/server";

import { logBffFailure } from "@/lib/bff-observability";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";
import {
  getPublicCommercialCatalog,
  getPublicPackCatalogContent,
} from "@/lib/internal-api";
import {
  buildSignupPackSnapshot,
  resolvePackSelectionInput,
} from "@/lib/public-packs";
import { isSignupEnabled } from "@/lib/public-routes";
import { checkRateLimit, getRequestIdentifier } from "@/lib/rate-limit";
import { callInternalSignup, verifyHCaptcha } from "@/lib/signup-server";

type SignupRequestBody = {
  customerType?: unknown;
  companyName?: unknown;
  addressLine1?: unknown;
  addressLine2?: unknown;
  postalCode?: unknown;
  city?: unknown;
  country?: unknown;
  personalTitle?: unknown;
  givenName?: unknown;
  surname?: unknown;
  initials?: unknown;
  email?: unknown;
  phone?: unknown;
  message?: unknown;
  packKey?: unknown;
  commitmentMonths?: unknown;
  paymentMode?: unknown;
  hcaptchaToken?: unknown;
  website?: unknown;
  formRenderedAt?: unknown;
};

const MAX_COMPANY_LENGTH = 200;
const MAX_CONTACT_LENGTH = 200;
const MAX_EMAIL_LENGTH = 320;
const MAX_PHONE_LENGTH = 40;
const MAX_MESSAGE_LENGTH = 2000;
const MAX_ADDRESS_LENGTH = 255;
const MAX_POSTAL_CODE_LENGTH = 32;
const MAX_CITY_LENGTH = 160;
const MAX_COUNTRY_LENGTH = 100;
const MAX_TITLE_LENGTH = 32;
const MAX_SHORT_NAME_LENGTH = 120;
const MAX_INITIALS_LENGTH = 16;
const RATE_LIMIT_MAX = 3;
const RATE_LIMIT_WINDOW_MS = 60 * 60 * 1000;
const MIN_FILL_MS = 2_000;

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const allowedCustomerTypes = new Set([
  "professional",
  "association",
  "individual",
]);

const ACCEPTED_BODY = {
  code: "SIGNUP_ACCEPTED",
  message:
    "Demande enregistree. Verifiez votre boite mail pour confirmer votre adresse.",
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
          "Trop de demandes successives. Reessayez dans quelques minutes.",
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
        message: "Le corps de la requete est invalide.",
        correlation_id: correlationId,
      },
      { status: 400 },
    );
  }

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
        message: "Verifiez les champs signales.",
        field_errors: errors,
        correlation_id: correlationId,
      },
      { status: 400 },
    );
  }

  const captchaToken =
    typeof body.hcaptchaToken === "string" ? body.hcaptchaToken : null;
  const captcha = await verifyHCaptcha(captchaToken, identifier, correlationId);
  if (!captcha.ok) {
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
          "La verification anti-robot a échoué. Rechargez la page et reessayez.",
        correlation_id: correlationId,
      },
      { status: 400 },
    );
  }

  const hasPackSelection =
    hasProvidedPackValue(body.packKey)
    || hasProvidedPackValue(body.commitmentMonths)
    || hasProvidedPackValue(body.paymentMode);
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
  const customerType =
    typeof body.customerType === "string"
      ? body.customerType.trim().toLowerCase()
      : "";
  const companyName =
    typeof body.companyName === "string" ? body.companyName.trim() : "";
  const addressLine1 =
    typeof body.addressLine1 === "string" ? body.addressLine1.trim() : "";
  const addressLine2 =
    typeof body.addressLine2 === "string" ? body.addressLine2.trim() : "";
  const postalCode =
    typeof body.postalCode === "string" ? body.postalCode.trim() : "";
  const city = typeof body.city === "string" ? body.city.trim() : "";
  const country =
    typeof body.country === "string" ? body.country.trim() : "";
  const personalTitle =
    typeof body.personalTitle === "string" ? body.personalTitle.trim() : "";
  const givenName =
    typeof body.givenName === "string" ? body.givenName.trim() : "";
  const surname =
    typeof body.surname === "string" ? body.surname.trim() : "";
  const initials =
    typeof body.initials === "string" ? body.initials.trim() : "";
  const email = typeof body.email === "string" ? body.email.trim() : "";
  const phone = typeof body.phone === "string" ? body.phone.trim() : "";
  const message =
    typeof body.message === "string" ? body.message.trim() : "";
  const contactName = [givenName, surname].filter(Boolean).join(" ").trim();

  if (!allowedCustomerTypes.has(customerType)) {
    errors.customerType = "Selectionnez un type de structure valide.";
  }

  if (!companyName) {
    errors.companyName = "Le nom ou la raison sociale est requis.";
  } else if (companyName.length > MAX_COMPANY_LENGTH) {
    errors.companyName = `Champ limité à ${MAX_COMPANY_LENGTH} caractères.`;
  }

  if (!addressLine1) {
    errors.addressLine1 = "L'adresse postale est requise.";
  } else if (addressLine1.length > MAX_ADDRESS_LENGTH) {
    errors.addressLine1 = `Champ limité à ${MAX_ADDRESS_LENGTH} caractères.`;
  }

  if (addressLine2.length > MAX_ADDRESS_LENGTH) {
    errors.addressLine2 = `Champ limité à ${MAX_ADDRESS_LENGTH} caractères.`;
  }

  if (!postalCode) {
    errors.postalCode = "Le code postal est requis.";
  } else if (postalCode.length > MAX_POSTAL_CODE_LENGTH) {
    errors.postalCode =
      `Champ limité à ${MAX_POSTAL_CODE_LENGTH} caractères.`;
  }

  if (!city) {
    errors.city = "La ville est requise.";
  } else if (city.length > MAX_CITY_LENGTH) {
    errors.city = `Champ limité à ${MAX_CITY_LENGTH} caractères.`;
  }

  if (!country) {
    errors.country = "Le pays est requis.";
  } else if (country.length > MAX_COUNTRY_LENGTH) {
    errors.country = `Champ limité à ${MAX_COUNTRY_LENGTH} caractères.`;
  }

  if (personalTitle.length > MAX_TITLE_LENGTH) {
    errors.personalTitle = `Champ limité à ${MAX_TITLE_LENGTH} caractères.`;
  }

  if (!givenName) {
    errors.givenName = "Le prenom est requis.";
  } else if (givenName.length > MAX_SHORT_NAME_LENGTH) {
    errors.givenName =
      `Champ limité à ${MAX_SHORT_NAME_LENGTH} caractères.`;
  }

  if (!surname) {
    errors.surname = "Le nom est requis.";
  } else if (surname.length > MAX_SHORT_NAME_LENGTH) {
    errors.surname = `Champ limité à ${MAX_SHORT_NAME_LENGTH} caractères.`;
  }

  if (!contactName) {
    errors.contactName = "Le nom complet du contact est requis.";
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

  if (initials.length > MAX_INITIALS_LENGTH) {
    errors.initials = `Champ limité à ${MAX_INITIALS_LENGTH} caractères.`;
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
      customer: {
        customerType,
        displayName: companyName,
        billingEmail: email,
        phone: phone || null,
        addressLine1,
        addressLine2: addressLine2 || null,
        postalCode,
        city,
        country,
      },
      primaryUser: {
        personalTitle: personalTitle || null,
        givenName,
        surname,
        initials: initials || null,
        displayName: contactName,
        email,
        phone: phone || null,
        isPrimaryContact: true,
      },
    },
  };
}

function hasProvidedPackValue(value: unknown) {
  if (value === null || value === undefined) {
    return false;
  }

  return typeof value !== "string" || value.trim().length > 0;
}
