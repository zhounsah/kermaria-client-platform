import type {
  BillingV2PublicCatalog,
  BillingV2PublicPreset,
  BillingV2PublicSelection,
  BillingV2PublicService,
} from "@kermaria/shared";

/**
 * Aides d'affichage pour la conception commerciale Billing V2.
 *
 * Regle stricte : ce module ne calcule AUCUN prix. Il met en forme des
 * montants deja calcules par API-INTERNAL et traduit des codes catalogue en
 * libelles lisibles. Toute addition tarifaire faite ici ferait du navigateur
 * une seconde autorite financiere.
 */

export const SERVICE_CODES = {
  base: "BASE-SERVICE",
  storagePersonal: "STORAGE-PERSONAL",
  storageShared: "STORAGE-SHARED",
  backupPersonal: "BACKUP-PERSONAL",
  backupShared: "BACKUP-SHARED",
  vpn: "VPN-ACCESS",
  remoteDesktop: "RDS-ACCESS",
  additionalUser: "USER-ADDITIONAL",
  supportPlus: "SUPPORT-PLUS",
} as const;

const PRESET_TAGLINES: Record<string, string> = {
  "pack-dossier-securise":
    "Mettre à l'abri les documents qu'on ne pourrait pas reconstituer.",
  "pack-acces-distance":
    "Retrouver ses fichiers depuis l'extérieur, par une liaison chiffrée.",
  "pack-bureau-windows-distance":
    "Un poste de travail Windows complet, accessible à distance.",
  "pack-pro-association":
    "Travailler à plusieurs sur un espace partagé, avec un support renforcé.",
};

export function resolvePresetTagline(presetCode: string) {
  return (
    PRESET_TAGLINES[presetCode]
    ?? "Configuration recommandée, ajustable à vos besoins."
  );
}

/** Montant calcule par le serveur, simplement relaye. */
export function resolvePresetBaselineMonthlyCents(
  preset: BillingV2PublicPreset,
) {
  return preset.baselineMonthlyAmountCents;
}

export type CompositionEntry = {
  key: string;
  label: string;
};

export function describePresetComposition(
  preset: BillingV2PublicPreset,
  catalog: BillingV2PublicCatalog,
): CompositionEntry[] {
  return preset.items.map((item) => {
    const service = findService(catalog, item.serviceCode);
    const tierLabel = resolveTierLabel(service, item.tierCode);
    const name = service?.name ?? item.serviceCode;
    const quantitySuffix = item.quantity > 1 ? ` × ${item.quantity}` : "";

    return {
      key: `${item.serviceCode}-${item.tierCode ?? "flat"}-${item.scopeTemplate}`,
      label: tierLabel
        ? `${name} — ${tierLabel}${quantitySuffix}`
        : `${name}${quantitySuffix}`,
    };
  });
}

export function findService(
  catalog: BillingV2PublicCatalog,
  serviceCode: string,
): BillingV2PublicService | undefined {
  return catalog.services.find((service) => service.code === serviceCode);
}

export function resolveTierLabel(
  service: BillingV2PublicService | undefined,
  tierCode: string | null,
) {
  if (!service || !tierCode) {
    return null;
  }

  return (
    service.tiers.find((tier) => tier.code === tierCode)?.label ?? tierCode
  );
}

export function selectableTiers(
  catalog: BillingV2PublicCatalog,
  serviceCode: string,
) {
  return (
    findService(catalog, serviceCode)?.tiers.filter(
      (tier) => tier.publicSelectable,
    ) ?? []
  );
}

/**
 * Configuration recommandee de la formule, exprimee en codes catalogue.
 * C'est le point de depart du configurateur, jamais un prix.
 */
export function buildBaselineSelection(
  preset: BillingV2PublicPreset,
  commitmentCode: string,
): BillingV2PublicSelection {
  const item = (serviceCode: string) =>
    preset.items.find((entry) => entry.serviceCode === serviceCode);

  const storagePersonal = item(SERVICE_CODES.storagePersonal);
  const storageShared = item(SERVICE_CODES.storageShared);
  const vpn = item(SERVICE_CODES.vpn);
  const additionalUser = item(SERVICE_CODES.additionalUser);

  return {
    presetCode: preset.code,
    commitmentCode,
    storagePersonalTierCode: storagePersonal?.tierCode ?? "32",
    backupPersonal: item(SERVICE_CODES.backupPersonal) !== undefined,
    storageSharedTierCode: storageShared?.tierCode ?? null,
    backupShared: item(SERVICE_CODES.backupShared) !== undefined,
    vpnTierCode: vpn?.tierCode ?? null,
    remoteDesktop: item(SERVICE_CODES.remoteDesktop) !== undefined,
    additionalUsers: additionalUser?.quantity ?? 0,
    supportPlus: item(SERVICE_CODES.supportPlus) !== undefined,
  };
}

const CHECKOUT_REASON_MESSAGES: Record<string, string> = {
  BILLING_V2_PUBLIC_CUSTOM_CONFIGURATION_NOT_CHECKOUTABLE:
    "Cette configuration personnalisée n'est pas encore souscriptible en "
    + "ligne. Revenez à la configuration recommandée pour souscrire "
    + "immédiatement, ou contactez-nous pour la mettre en place.",
  BILLING_V2_PUBLIC_CHECKOUT_ROUTE_MISSING:
    "Cette combinaison formule / engagement n'est pas ouverte à la "
    + "souscription en ligne.",
  BILLING_V2_FIRST_REAL_SUBSCRIPTION_NOT_APPROVED:
    "La souscription en ligne n'est pas encore ouverte. Contactez-nous pour "
    + "mettre en place cette formule.",
  BILLING_V2_NEW_SUBSCRIPTIONS_FLAG_OFF:
    "La souscription en ligne n'est pas encore ouverte. Contactez-nous pour "
    + "mettre en place cette formule.",
  BILLING_V2_AUTHORITATIVE_CHECKOUT_FLAG_OFF:
    "La souscription en ligne n'est pas encore ouverte. Contactez-nous pour "
    + "mettre en place cette formule.",
  BILLING_V2_AUTHORITATIVE_CHECKOUT_NO_SQL:
    "La souscription en ligne est momentanément indisponible.",
};

export function describeCheckoutReason(reasonCode: string) {
  return (
    CHECKOUT_REASON_MESSAGES[reasonCode]
    ?? "La souscription en ligne n'est pas disponible pour cette configuration."
  );
}
