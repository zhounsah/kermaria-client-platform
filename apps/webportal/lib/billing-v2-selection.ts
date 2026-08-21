import type {
  BillingV2PublicSelection,
  BillingV2PublicSelectionComponent,
} from "@kermaria/shared";

/**
 * Reconstruction stricte d'une selection Billing V2 recue du navigateur.
 *
 * Regle unique et non negociable : seuls les champs listes ici sont relayes a
 * API-INTERNAL. Un corps enrichi par le client — montant, remise, prix final,
 * identifiant de prix fournisseur, lignes deja tarifees — ne peut donc pas
 * traverser le BFF, meme si l'API interne les ignorerait de toute facon.
 *
 * Ce module est partage par le devis et la souscription : les deux voient
 * exactement la meme selection, ce qui rend impossible qu'un champ accepte
 * pour l'affichage devienne facturable sans passer par ici.
 */
const PAYMENT_MODES = new Set(["monthly", "upfront"]);

export const MAX_ADDITIONAL_USERS = 10;

export function readBillingV2SelectionPayload(
  payload: unknown,
): BillingV2PublicSelection | null {
  if (typeof payload !== "object" || payload === null) {
    return null;
  }

  const source = payload as Record<string, unknown>;
  const presetCode = readString(source.presetCode);
  const storagePersonalTierCode = readString(source.storagePersonalTierCode);
  const components = readComponents(source.components);
  if (!presetCode || (!storagePersonalTierCode && !components)) {
    return null;
  }

  const paymentMode = readString(source.paymentMode) ?? "monthly";
  if (!PAYMENT_MODES.has(paymentMode)) {
    return null;
  }

  const additionalUsers = source.additionalUsers;
  if (
    additionalUsers !== undefined
    && (typeof additionalUsers !== "number"
      || !Number.isInteger(additionalUsers)
      || additionalUsers < 0
      || additionalUsers > MAX_ADDITIONAL_USERS)
  ) {
    return null;
  }

  return {
    presetCode,
    commitmentCode: readString(source.commitmentCode) ?? "FLEX",
    paymentMode: paymentMode as BillingV2PublicSelection["paymentMode"],
    // Le champ historique reste obligatoire dans le contrat TypeScript ; la
    // forme V2.1 generique ne le consulte jamais apres validation serveur.
    storagePersonalTierCode: storagePersonalTierCode ?? "",
    backupPersonal: source.backupPersonal === true,
    storageSharedTierCode: readString(source.storageSharedTierCode),
    backupShared: source.backupShared === true,
    vpnTierCode: readString(source.vpnTierCode),
    remoteDesktop: source.remoteDesktop === true,
    additionalUsers: typeof additionalUsers === "number" ? additionalUsers : 0,
    supportPlus: source.supportPlus === true,
    components: components ?? undefined,
  };
}

function readComponents(value: unknown): BillingV2PublicSelectionComponent[] | null {
  if (!Array.isArray(value) || value.length === 0 || value.length > 64) {
    return null;
  }

  const components: BillingV2PublicSelectionComponent[] = [];
  for (const candidate of value) {
    if (typeof candidate !== "object" || candidate === null) {
      return null;
    }
    const source = candidate as Record<string, unknown>;
    const serviceCode = readString(source.serviceCode);
    const tierCode = source.tierCode === null ? null : readString(source.tierCode);
    const quantity = source.quantity;
    if (!serviceCode || (source.tierCode !== undefined && !tierCode)
      || typeof quantity !== "number" || !Number.isInteger(quantity)
      || quantity <= 0 || quantity > 1000) {
      return null;
    }
    components.push({ serviceCode, tierCode, quantity });
  }
  return components;
}

function readString(value: unknown) {
  if (typeof value !== "string") {
    return null;
  }

  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

type BillingV2SearchParams = Record<string, string | string[] | undefined>;

export function billingV2SelectionToSearchParams(
  selection: BillingV2PublicSelection,
): URLSearchParams {
  const params = new URLSearchParams();
  params.set("v2", "1");
  params.set("v2Preset", selection.presetCode);
  params.set("v2Commitment", selection.commitmentCode);
  params.set("v2Payment", selection.paymentMode);
  params.set("v2Personal", selection.storagePersonalTierCode);
  params.set("v2BackupPersonal", selection.backupPersonal ? "1" : "0");
  if (selection.storageSharedTierCode) {
    params.set("v2Shared", selection.storageSharedTierCode);
  }
  params.set("v2BackupShared", selection.backupShared ? "1" : "0");
  if (selection.vpnTierCode) {
    params.set("v2Vpn", selection.vpnTierCode);
  }
  params.set("v2Rds", selection.remoteDesktop ? "1" : "0");
  params.set("v2Users", String(selection.additionalUsers));
  params.set("v2Support", selection.supportPlus ? "1" : "0");
  return params;
}

export function readBillingV2SelectionSearchParams(
  searchParams: BillingV2SearchParams,
): BillingV2PublicSelection | null {
  if (singleParam(searchParams.v2) !== "1") {
    return null;
  }

  const usersRaw = singleParam(searchParams.v2Users) ?? "0";
  if (!/^\d+$/.test(usersRaw)) {
    return null;
  }

  return readBillingV2SelectionPayload({
    presetCode: singleParam(searchParams.v2Preset),
    commitmentCode: singleParam(searchParams.v2Commitment),
    paymentMode: singleParam(searchParams.v2Payment),
    storagePersonalTierCode: singleParam(searchParams.v2Personal),
    backupPersonal: singleParam(searchParams.v2BackupPersonal) === "1",
    storageSharedTierCode: singleParam(searchParams.v2Shared),
    backupShared: singleParam(searchParams.v2BackupShared) === "1",
    vpnTierCode: singleParam(searchParams.v2Vpn),
    remoteDesktop: singleParam(searchParams.v2Rds) === "1",
    additionalUsers: Number(usersRaw),
    supportPlus: singleParam(searchParams.v2Support) === "1",
  });
}

function singleParam(value: string | string[] | undefined): string | undefined {
  return typeof value === "string" ? value : undefined;
}
