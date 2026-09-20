import type {
  AdminCustomerCreatePayload,
  AdminCustomerCreateResponse,
} from "@kermaria/shared";
import { NextRequest } from "next/server";

import { controlledAdminError, handleAdminGet, handleAdminMutation } from "@/lib/admin-bff";
import { CORRELATION_HEADER, resolveCorrelationId } from "@/lib/correlation";

export function GET(request: NextRequest) {
  return handleAdminGet(request, "/internal/admin/customers");
}

export async function POST(request: NextRequest) {
  const correlationId = resolveCorrelationId(
    request.headers.get(CORRELATION_HEADER),
  );
  const payload = parsePayload(await readJson(request));
  if (!payload) {
    return controlledAdminError(
      400,
      "INVALID_REQUEST",
      "Les informations client sont invalides.",
      correlationId,
    );
  }

  return handleAdminMutation<AdminCustomerCreatePayload, AdminCustomerCreateResponse>(
    request,
    "/internal/admin/customers",
    "POST",
    payload,
  );
}

function parsePayload(value: unknown): AdminCustomerCreatePayload | null {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    return null;
  }

  const source = value as Record<string, unknown>;
  const customerType = source.customerType;
  if (
    customerType !== "individual"
    && customerType !== "professional"
    && customerType !== "association"
  ) {
    return null;
  }

  const required = [
    "displayName",
    "billingEmail",
    "addressLine1",
    "postalCode",
    "city",
    "country",
  ] as const;
  if (required.some((key) => !isText(source[key], 1, key === "billingEmail" ? 320 : 255))) {
    return null;
  }

  return {
    customerType,
    displayName: text(source.displayName),
    billingEmail: text(source.billingEmail),
    phone: optionalText(source.phone, 40),
    addressLine1: text(source.addressLine1),
    addressLine2: optionalText(source.addressLine2, 255),
    postalCode: text(source.postalCode),
    city: text(source.city),
    country: text(source.country),
  };
}

function isText(value: unknown, min: number, max: number) {
  return typeof value === "string" && value.trim().length >= min && value.trim().length <= max;
}

function text(value: unknown) {
  return (value as string).trim();
}

function optionalText(value: unknown, max: number) {
  if (value === null || value === undefined || value === "") {
    return null;
  }
  return isText(value, 1, max) ? text(value) : null;
}

async function readJson(request: NextRequest) {
  try {
    return await request.json();
  } catch {
    return null;
  }
}
