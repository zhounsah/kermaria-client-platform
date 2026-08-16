import type { BillingV2PublicSelection } from "@kermaria/shared";

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
  if (!presetCode || !storagePersonalTierCode) {
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
    storagePersonalTierCode,
    backupPersonal: source.backupPersonal === true,
    storageSharedTierCode: readString(source.storageSharedTierCode),
    backupShared: source.backupShared === true,
    vpnTierCode: readString(source.vpnTierCode),
    remoteDesktop: source.remoteDesktop === true,
    additionalUsers: typeof additionalUsers === "number" ? additionalUsers : 0,
    supportPlus: source.supportPlus === true,
  };
}

function readString(value: unknown) {
  if (typeof value !== "string") {
    return null;
  }

  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}
