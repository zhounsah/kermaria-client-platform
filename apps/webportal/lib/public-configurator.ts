import type {
  CatalogConfigurationInput,
  CommercialOfferPaymentMode,
  PublicPackCode,
  PublicPackCommitmentMonths,
} from "@kermaria/shared";

import {
  normalizeCommitmentMonths,
  normalizePaymentMode,
  normalizePublicPackKey,
  type PublicPackSelectionInput,
} from "@/lib/public-packs";

const USER_VALUES = new Set([1, 2, 3, 4, 5, 6]);
const STORAGE_VALUES = new Set([8, 32, 64]);

export const DEFAULT_CATALOG_CONFIGURATION: CatalogConfigurationInput = {
  packKey: "pack-dossier-securise",
  commitmentMonths: 1,
  paymentMode: "monthly",
  users: 1,
  storageGb: null,
  needsVpn: null,
  needsWindowsDesktop: null,
};

export function configurationFromSelection(
  selection: PublicPackSelectionInput,
): CatalogConfigurationInput {
  return {
    ...DEFAULT_CATALOG_CONFIGURATION,
    packKey: selection.packKey,
    commitmentMonths: selection.commitmentMonths,
    paymentMode: selection.paymentMode,
  };
}

export function normalizeCatalogConfigurationInput(
  value: Record<string, unknown>,
): CatalogConfigurationInput | null {
  const packKey = normalizePublicPackKey(value.packKey);
  const commitmentMonths = normalizeCommitmentMonths(value.commitmentMonths);
  const paymentMode = normalizePaymentMode(value.paymentMode, commitmentMonths);
  if (!packKey || !commitmentMonths || !paymentMode) {
    return null;
  }

  const users = normalizeUsers(value.users);
  if (users === undefined) {
    return null;
  }

  const storageGb = normalizeStorageGb(value.storageGb);
  if (storageGb === undefined) {
    return null;
  }

  const needsVpn = normalizeNullableBoolean(value.needsVpn);
  const needsWindowsDesktop = normalizeNullableBoolean(value.needsWindowsDesktop);
  if (needsVpn === undefined || needsWindowsDesktop === undefined) {
    return null;
  }

  return {
    packKey,
    commitmentMonths,
    paymentMode,
    users,
    storageGb,
    needsVpn,
    needsWindowsDesktop,
  };
}

export function configurationFromSearchParams(
  params: URLSearchParams | Record<string, string | string[] | undefined>,
): CatalogConfigurationInput | null {
  const read = (key: string) => {
    if (params instanceof URLSearchParams) {
      const values = params.getAll(key);
      return values.length === 0 ? null : values.length === 1 ? values[0] : undefined;
    }

    const value = params[key];
    return Array.isArray(value) ? undefined : value ?? null;
  };

  return normalizeCatalogConfigurationInput({
    packKey: read("pack"),
    commitmentMonths: read("commitment") ?? "1",
    paymentMode: read("payment") ?? "monthly",
    users: read("users") ?? "1",
    storageGb: read("storage"),
    needsVpn: read("vpn"),
    needsWindowsDesktop: read("windows"),
  });
}

export function configurationToQueryString(
  configuration: CatalogConfigurationInput,
) {
  const params = new URLSearchParams({
    pack: configuration.packKey,
    commitment: String(configuration.commitmentMonths),
    payment: configuration.paymentMode,
  });
  if (configuration.users !== null) {
    params.set("users", String(configuration.users));
  }
  if (configuration.storageGb !== null) {
    params.set("storage", String(configuration.storageGb));
  }
  if (configuration.needsVpn !== null) {
    params.set("vpn", configuration.needsVpn ? "yes" : "no");
  }
  if (configuration.needsWindowsDesktop !== null) {
    params.set("windows", configuration.needsWindowsDesktop ? "yes" : "no");
  }
  return params.toString();
}

export function updateConfigurationPaymentMode(
  commitmentMonths: PublicPackCommitmentMonths,
  currentPaymentMode: CommercialOfferPaymentMode,
): CommercialOfferPaymentMode {
  return commitmentMonths === 1 ? "monthly" : currentPaymentMode;
}

function normalizeUsers(value: unknown): number | null | undefined {
  if (value === null || value === undefined || value === "") {
    return null;
  }
  const numeric = typeof value === "number" ? value : Number(String(value).trim());
  if (!Number.isInteger(numeric) || !USER_VALUES.has(numeric)) {
    return undefined;
  }
  return numeric;
}

function normalizeStorageGb(value: unknown): number | null | undefined {
  if (value === null || value === undefined || value === "") {
    return null;
  }
  const numeric = typeof value === "number" ? value : Number(String(value).trim());
  if (!Number.isInteger(numeric) || !STORAGE_VALUES.has(numeric)) {
    return undefined;
  }
  return numeric;
}

function normalizeNullableBoolean(value: unknown): boolean | null | undefined {
  if (value === null || value === undefined || value === "") {
    return null;
  }
  if (typeof value === "boolean") {
    return value;
  }
  const normalized = String(value).trim().toLowerCase();
  if (["yes", "true", "1", "oui"].includes(normalized)) {
    return true;
  }
  if (["no", "false", "0", "non"].includes(normalized)) {
    return false;
  }
  if (["unknown", "unsure", "je-ne-sais-pas"].includes(normalized)) {
    return null;
  }
  return undefined;
}

export function packKeyFromMaybeString(value: string): PublicPackCode | null {
  return normalizePublicPackKey(value);
}
