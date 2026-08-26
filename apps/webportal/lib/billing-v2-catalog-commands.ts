import "server-only";

/**
 * Reconstruction stricte des commandes d'administration du catalogue Billing V2.
 *
 * Le BFF ne fabrique aucun chemin a partir de valeurs libres : le champ `kind`
 * choisit la route, et les identifiants sont valides comme UUID avant d'y etre
 * injectes. Aucune commande ne transporte de prix resolu : le montant saisi est
 * relaye tel quel, et c'est API-INTERNAL qui decide seul s'il peut devenir une
 * nouvelle version tarifaire, quelle fenetre fermer et si un recouvrement
 * l'interdit.
 */
export type BillingV2CatalogAdminCommand = {
  path: string;
  method: "PATCH" | "POST" | "DELETE";
  payload: Record<string, unknown> | undefined;
};

const CATALOG_STATUSES = new Set(["active", "inactive"]);
const CATALOG_CADENCES = new Set(["monthly", "one_time"]);
const CATALOG_TRIGGERS = new Set([
  "initial_subscription",
  "subscription_change",
]);
const CATALOG_SCOPES = new Set([
  "subscription",
  "primary_user",
  "additional_user",
]);
const CATALOG_PAYMENT_MODES = new Set(["monthly", "upfront"]);
const CATALOG_BILLING_TYPES = new Set(["recurring", "one_time", "included"]);
const CATALOG_DEFAULT_SCOPES = new Set(["subscription", "user"]);
const CATALOG_PRICING_MODELS = new Set(["fixed", "tiered"]);
/**
 * Environnements reellement existants chez chaque fournisseur.
 *
 * Valider fournisseur et environnement separement laisserait passer
 * `stripe/sandbox` ou `paypal/test`, deux couples qui n'existent pas. Le
 * rattachement serait accepte en back-office puis introuvable au paiement.
 * L'API interne applique la meme matrice : ceci est la premiere barriere,
 * pas la seule.
 */
export const CATALOG_PROVIDER_ENVIRONMENTS: Readonly<
  Record<string, readonly string[]>
> = {
  stripe: ["test", "live"],
  paypal: ["sandbox", "live"],
};

const CATALOG_PROVIDERS = new Set(
  Object.keys(CATALOG_PROVIDER_ENVIRONMENTS),
);
const UUID_PATTERN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const CATALOG_CODE_PATTERN = /^[A-Za-z0-9_-]{1,96}$/;

export function parseBillingV2CatalogAdminCommand(
  value: unknown,
): BillingV2CatalogAdminCommand | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const source = value as Record<string, unknown>;
  const kind = typeof source.kind === "string" ? source.kind : null;

  switch (kind) {
    case "service.create":
      return buildServiceCreate(source);
    case "service.update":
      return buildServiceUpdate(source);
    case "tier.create":
      return buildTierCreate(source);
    case "tier.update":
      return buildTierUpdate(source);
    case "price.publish":
      return buildPricePublish(source);
    case "price.close":
      return buildPriceClose(source);
    case "preset.create":
      return buildPresetCreate(source);
    case "preset.update":
      return buildPresetUpdate(source);
    case "preset.item.add":
      return buildPresetItem(source, "add");
    case "preset.item.update":
      return buildPresetItem(source, "update");
    case "preset.item.remove":
      return buildPresetItemRemove(source);
    case "commitment.create":
      return buildCommitmentCreate(source);
    case "commitment.update":
      return buildCommitmentUpdate(source);
    case "commitment.payment_option":
      return buildPaymentOption(source);
    case "provider.mapping":
      return buildProviderMapping(source);
    default:
      return null;
  }
}

function buildServiceCreate(source: Record<string, unknown>) {
  const code = readCatalogCode(source.code);
  const name = optionalString(source.name, 160);
  const billingType = optionalEnum(source.billingType, CATALOG_BILLING_TYPES);
  const defaultScopeType = optionalEnum(source.defaultScopeType, CATALOG_DEFAULT_SCOPES);
  const pricingModel = optionalEnum(source.pricingModel, CATALOG_PRICING_MODELS);
  if (!code || !name || !billingType || !defaultScopeType || !pricingModel) {
    return null;
  }
  return {
    path: "/services",
    method: "POST" as const,
    payload: {
      code,
      name,
      description: optionalString(source.description, 4000),
      category: optionalString(source.category, 80),
      billingType,
      defaultScopeType,
      pricingModel,
      mandatoryForSubscription: source.mandatoryForSubscription === true,
      discountEligible: source.discountEligible !== false,
      displayOrder: optionalInteger(source.displayOrder, 0, 100000) ?? 0,
    },
  };
}

function buildServiceUpdate(source: Record<string, unknown>) {
  const id = readUuid(source.id);
  if (!id) {
    return null;
  }

  return {
    path: "/services/" + id,
    method: "PATCH" as const,
    payload: {
      name: optionalString(source.name, 160),
      description: optionalString(source.description, 4000),
      category: optionalString(source.category, 80),
      status: optionalEnum(source.status, CATALOG_STATUSES),
      displayOrder: optionalInteger(source.displayOrder, 0, 100000),
      publicVisible: optionalBoolean(source.publicVisible),
      selfServiceOrderable: optionalBoolean(source.selfServiceOrderable),
      discountEligible: optionalBoolean(source.discountEligible),
      mandatoryForSubscription: optionalBoolean(
        source.mandatoryForSubscription,
      ),
    },
  };
}

function buildTierUpdate(source: Record<string, unknown>) {
  const id = readUuid(source.id);
  if (!id) {
    return null;
  }

  const attributes = readTierAttributes(source.attributes);
  if (attributes === false) {
    return null;
  }

  return {
    path: "/tiers/" + id,
    method: "PATCH" as const,
    payload: {
      label: optionalString(source.label, 160),
      publicLabel: optionalString(source.publicLabel, 160),
      description: optionalString(source.description, 4000),
      status: optionalEnum(source.status, CATALOG_STATUSES),
      displayOrder: optionalInteger(source.displayOrder, 0, 100000),
      publicSelectable: optionalBoolean(source.publicSelectable),
      numericValue: optionalInteger(source.numericValue, 0, 1e12),
      unit: optionalString(source.unit, 32),
      attributes,
    },
  };
}

function buildTierCreate(source: Record<string, unknown>) {
  const serviceId = readUuid(source.serviceId);
  const code = readCatalogCode(source.code);
  const label = optionalString(source.label, 160);
  const attributes = readTierAttributes(source.attributes);
  if (!serviceId || !code || !label || attributes === false) {
    return null;
  }
  return {
    path: "/services/" + serviceId + "/tiers",
    method: "POST" as const,
    payload: {
      code,
      label,
      publicLabel: optionalString(source.publicLabel, 160),
      description: optionalString(source.description, 4000),
      displayOrder: optionalInteger(source.displayOrder, 0, 100000) ?? 0,
      numericValue: optionalInteger(source.numericValue, 0, 1e12),
      unit: optionalString(source.unit, 32),
      attributes,
    },
  };
}

function buildPricePublish(source: Record<string, unknown>) {
  const serviceId = readUuid(source.serviceId);
  const amountCents = optionalInteger(source.amountCents, 0, 100000000);
  const cadence = optionalEnum(source.billingCadence, CATALOG_CADENCES);
  if (!serviceId || amountCents === undefined || !cadence) {
    return null;
  }

  const tierId = readOptionalUuid(source.tierId);
  if (tierId === false) {
    return null;
  }

  return {
    path: "/prices",
    method: "POST" as const,
    payload: {
      serviceId,
      tierId,
      amountCents,
      currency: optionalString(source.currency, 3) ?? "EUR",
      billingCadence: cadence,
      chargeTrigger:
        optionalEnum(source.chargeTrigger, CATALOG_TRIGGERS)
        ?? "initial_subscription",
      taxRateBasisPoints: optionalInteger(source.taxRateBasisPoints, 0, 10000),
      effectiveAt: optionalIsoDate(source.effectiveAt),
    },
  };
}

function buildPriceClose(source: Record<string, unknown>) {
  const id = readUuid(source.id);
  if (!id) {
    return null;
  }

  return {
    path: "/prices/" + id + "/close",
    method: "POST" as const,
    payload: { effectiveAt: optionalIsoDate(source.effectiveAt) },
  };
}

function buildPresetCreate(source: Record<string, unknown>) {
  const code = readCatalogCode(source.code);
  const name = optionalString(source.name, 160);
  if (!code || !name) {
    return null;
  }

  return {
    path: "/presets",
    method: "POST" as const,
    payload: {
      code,
      name,
      description: optionalString(source.description, 4000),
      status: optionalEnum(source.status, CATALOG_STATUSES) ?? "active",
      isPublic: source.isPublic === true,
      displayOrder: optionalInteger(source.displayOrder, 0, 100000) ?? 0,
    },
  };
}

function buildPresetUpdate(source: Record<string, unknown>) {
  const id = readUuid(source.id);
  if (!id) {
    return null;
  }

  return {
    path: "/presets/" + id,
    method: "PATCH" as const,
    payload: {
      name: optionalString(source.name, 160),
      description: optionalString(source.description, 4000),
      status: optionalEnum(source.status, CATALOG_STATUSES),
      isPublic: optionalBoolean(source.isPublic),
      displayOrder: optionalInteger(source.displayOrder, 0, 100000),
    },
  };
}

function buildPresetItem(
  source: Record<string, unknown>,
  mode: "add" | "update",
) {
  const presetId = readUuid(source.presetId);
  if (!presetId) {
    return null;
  }

  const serviceId = source.serviceId === undefined
    ? undefined
    : readUuid(source.serviceId);
  if (source.serviceId !== undefined && !serviceId) {
    return null;
  }

  const tierId = readOptionalUuid(source.tierId);
  if (tierId === false) {
    return null;
  }

  const scopeTemplate = optionalEnum(source.scopeTemplate, CATALOG_SCOPES);
  const payload = {
    serviceId,
    tierId,
    scopeTemplate,
    quantity: optionalInteger(source.quantity, 1, 1000),
    requiredItem: optionalBoolean(source.requiredItem),
    customerEditable: optionalBoolean(source.customerEditable),
    displayOrder: optionalInteger(source.displayOrder, 0, 100000),
  };

  if (mode === "add") {
    if (!serviceId || !scopeTemplate) {
      return null;
    }

    return {
      path: "/presets/" + presetId + "/items",
      method: "POST" as const,
      payload,
    };
  }

  const itemId = readUuid(source.itemId);
  if (!itemId) {
    return null;
  }

  return {
    path: "/presets/" + presetId + "/items/" + itemId,
    method: "PATCH" as const,
    payload,
  };
}

function buildPresetItemRemove(source: Record<string, unknown>) {
  const presetId = readUuid(source.presetId);
  const itemId = readUuid(source.itemId);
  if (!presetId || !itemId) {
    return null;
  }

  return {
    path: "/presets/" + presetId + "/items/" + itemId,
    method: "DELETE" as const,
    payload: undefined,
  };
}

function buildCommitmentCreate(source: Record<string, unknown>) {
  const code = readCatalogCode(source.code);
  const name = optionalString(source.name, 160);
  const months = optionalInteger(source.commitmentMonths, 1, 120);
  if (!code || !name || months === undefined) {
    return null;
  }

  return {
    path: "/commitments",
    method: "POST" as const,
    payload: {
      code,
      name,
      commitmentMonths: months,
      discountBasisPoints: optionalInteger(
        source.discountBasisPoints,
        0,
        10000,
      ),
      allowMonthlyPayment: source.allowMonthlyPayment !== false,
      allowUpfrontPayment: source.allowUpfrontPayment !== false,
      status: optionalEnum(source.status, CATALOG_STATUSES) ?? "active",
      displayOrder: optionalInteger(source.displayOrder, 0, 100000) ?? 0,
    },
  };
}

function buildCommitmentUpdate(source: Record<string, unknown>) {
  const id = readUuid(source.id);
  if (!id) {
    return null;
  }

  return {
    path: "/commitments/" + id,
    method: "PATCH" as const,
    payload: {
      name: optionalString(source.name, 160),
      commitmentMonths: optionalInteger(source.commitmentMonths, 1, 120),
      discountBasisPoints: optionalInteger(
        source.discountBasisPoints,
        0,
        10000,
      ),
      allowMonthlyPayment: optionalBoolean(source.allowMonthlyPayment),
      allowUpfrontPayment: optionalBoolean(source.allowUpfrontPayment),
      status: optionalEnum(source.status, CATALOG_STATUSES),
      displayOrder: optionalInteger(source.displayOrder, 0, 100000),
    },
  };
}

function buildPaymentOption(source: Record<string, unknown>) {
  const id = readUuid(source.id);
  const mode = optionalEnum(source.paymentMode, CATALOG_PAYMENT_MODES);
  if (!id || !mode) {
    return null;
  }

  return {
    path: "/commitments/" + id + "/payment-options",
    method: "POST" as const,
    payload: {
      paymentMode: mode,
      discountBasisPoints:
        optionalInteger(source.discountBasisPoints, 0, 10000) ?? 0,
      status: optionalEnum(source.status, CATALOG_STATUSES) ?? "active",
      displayOrder: optionalInteger(source.displayOrder, 0, 100000) ?? 0,
    },
  };
}

function buildProviderMapping(source: Record<string, unknown>) {
  const id = readUuid(source.priceId);
  const provider = optionalEnum(source.provider, CATALOG_PROVIDERS);
  if (!id || !provider) {
    return null;
  }

  const environment = optionalEnum(
    source.environment,
    new Set(CATALOG_PROVIDER_ENVIRONMENTS[provider]),
  );
  if (!environment) {
    return null;
  }

  return {
    path: "/prices/" + id + "/provider-mapping",
    method: "POST" as const,
    payload: {
      provider,
      environment,
      externalProductId: optionalString(source.externalProductId, 255),
      externalPriceId: optionalString(source.externalPriceId, 255),
      externalPlanId: optionalString(source.externalPlanId, 255),
      status: optionalEnum(source.status, CATALOG_STATUSES) ?? "active",
    },
  };
}

function readUuid(value: unknown): string | null {
  return typeof value === "string" && UUID_PATTERN.test(value.trim())
    ? value.trim()
    : null;
}

/**
 * `false` signale une valeur presente mais invalide, `null` une absence
 * deliberee. Les confondre laisserait passer un identifiant errone comme s'il
 * s'agissait d'un prix sans palier.
 */
function readOptionalUuid(value: unknown): string | null | false {
  if (value === null || value === undefined) {
    return null;
  }

  return readUuid(value) ?? false;
}

function readCatalogCode(value: unknown): string | null {
  if (typeof value !== "string") {
    return null;
  }

  const trimmed = value.trim().toUpperCase();
  return CATALOG_CODE_PATTERN.test(trimmed) ? trimmed : null;
}

function optionalString(value: unknown, maxLength: number) {
  if (typeof value !== "string") {
    return undefined;
  }

  const trimmed = value.trim();
  if (trimmed.length === 0 || trimmed.length > maxLength) {
    return undefined;
  }

  return trimmed;
}

function optionalEnum(value: unknown, allowed: Set<string>) {
  if (typeof value !== "string") {
    return undefined;
  }

  const normalized = value.trim().toLowerCase();
  return allowed.has(normalized) ? normalized : undefined;
}

function optionalInteger(value: unknown, min: number, max: number) {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    return undefined;
  }

  const truncated = Math.trunc(value);
  return truncated >= min && truncated <= max ? truncated : undefined;
}

function optionalBoolean(value: unknown) {
  return typeof value === "boolean" ? value : undefined;
}

function optionalIsoDate(value: unknown) {
  if (typeof value !== "string" || value.trim().length === 0) {
    return undefined;
  }

  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? undefined : parsed.toISOString();
}

/**
 * `false` signale un tableau invalide, `undefined` son absence. Les deux ne
 * peuvent pas etre confondus : une absence laisse les attributs en place,
 * une valeur invalide doit faire echouer la commande entiere.
 */
function readTierAttributes(value: unknown) {
  if (value === undefined) {
    return undefined;
  }

  if (!Array.isArray(value) || value.length > 32) {
    return false as const;
  }

  const attributes: Array<Record<string, unknown>> = [];
  for (const candidate of value) {
    if (!candidate || typeof candidate !== "object") {
      return false as const;
    }

    const entry = candidate as Record<string, unknown>;
    const code = optionalString(entry.attributeCode, 64);
    if (!code) {
      return false as const;
    }

    attributes.push({
      attributeCode: code,
      valueNumeric: optionalInteger(entry.valueNumeric, -1e12, 1e12) ?? null,
      valueText: optionalString(entry.valueText, 255) ?? null,
      unit: optionalString(entry.unit, 32) ?? null,
    });
  }

  return attributes;
}
