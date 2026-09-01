import type {
  BillingV2PublicCatalog,
  BillingV2PublicPreset,
  BillingV2PublicSelection,
  BillingV2PublicService,
  BillingV2PublicTier,
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

/**
 * Libellé public d'un service.
 *
 * Le catalogue porte des noms d'exploitation — « Socle de service »,
 * « Accès bureau distant RDS » — qui ne disent rien à un client et qui
 * exposent inutilement le découpage interne. La traduction se fait ici, et
 * pas dans le catalogue : la base reste l'autorité sur ce qui est facturé,
 * cette table ne change que ce qui est lu à l'écran.
 */
const SERVICE_PUBLIC_LABELS: Record<string, string> = {
  [SERVICE_CODES.base]: "Mise en service et suivi de votre espace",
  [SERVICE_CODES.remoteDesktop]: "Bureau Windows à distance",
  [SERVICE_CODES.vpn]: "Accès sécurisé à distance",
};

export function resolveServicePublicLabel(serviceCode: string, fallback: string) {
  return SERVICE_PUBLIC_LABELS[serviceCode] ?? fallback;
}

/**
 * Bénéfice affiché pour un service : la description commerciale du catalogue
 * si elle existe, la phrase du code sinon.
 *
 * L'ordre compte. Le catalogue est déjà l'autorité commerciale et se modifie
 * sans livraison ; laisser le code gagner rendrait la description
 * administrable invisible, ce qui est exactement le défaut que la
 * spécification demande de corriger.
 */
export function resolveServiceBenefit(
  serviceCode: string,
  service: BillingV2PublicService | undefined,
): string | undefined {
  const described = service?.description?.trim();
  return described && described.length > 0
    ? described
    : SERVICE_BENEFITS[serviceCode];
}

/**
 * Ce que le client obtient réellement, en une phrase. Une carte de formule
 * doit se lire en quelques secondes : la liste exhaustive des composants
 * facturés appartient au configurateur et au récapitulatif, pas à la vitrine.
 *
 * Ces phrases sont désormais un **repli** : quand le catalogue porte une
 * description commerciale pour le service, c'est elle qui s'affiche
 * (spécification, section 19). Le repli reste dans le code pour qu'un service
 * sans description ne disparaisse pas de la carte au lieu de s'y décrire
 * approximativement.
 */
const SERVICE_BENEFITS: Record<string, string> = {
  [SERVICE_CODES.storagePersonal]: "Un espace de stockage personnel",
  [SERVICE_CODES.storageShared]: "Un espace partagé pour toute la structure",
  [SERVICE_CODES.backupPersonal]: "Sauvegarde quotidienne de vos fichiers",
  [SERVICE_CODES.backupShared]: "Sauvegarde de l'espace partagé",
  [SERVICE_CODES.vpn]: "Accès sécurisé depuis l'extérieur",
  [SERVICE_CODES.remoteDesktop]: "Un bureau Windows accessible à distance",
  [SERVICE_CODES.additionalUser]: "Des comptes pour vos collaborateurs",
  [SERVICE_CODES.supportPlus]: "Support renforcé",
};

/**
 * Durée d'engagement telle qu'on la choisit : « Sans engagement », « 6 mois »,
 * « 12 mois ». Le catalogue la nomme « Engagement 12 mois », ce qui répète le
 * titre du champ et allonge inutilement le libellé du bouton.
 */
export function formatCommitmentDurationLabel(months: number, fallback: string) {
  if (months <= 1) {
    return "Sans engagement";
  }

  return months > 0 ? `${months} mois` : fallback;
}

export function formatDiscountPercent(basisPoints: number) {
  return `${basisPoints / 100}`.replace(".", ",");
}

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

/**
 * Accroche d'une formule : la description du catalogue si elle est renseignée,
 * l'accroche du code sinon, puis une phrase générique.
 *
 * La description de formule est déjà administrable dans `/admin/catalog` : la
 * lire ici ferme l'écart de la section 19 sans créer ni table ni écran
 * supplémentaire. Le repli de code n'est pas de la redondance — il évite
 * qu'une formule sans description s'affiche sans accroche du tout.
 */
export function resolvePresetTagline(
  preset: Pick<BillingV2PublicPreset, "code" | "description">,
) {
  const described = preset.description?.trim();
  if (described && described.length > 0) {
    return described;
  }

  return (
    PRESET_TAGLINES[preset.code]
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
    const name = resolveServicePublicLabel(
      item.serviceCode,
      service?.name ?? item.serviceCode,
    );
    const quantitySuffix = item.quantity > 1 ? ` × ${item.quantity}` : "";

    return {
      key: `${item.serviceCode}-${item.tierCode ?? "flat"}-${item.scopeTemplate}`,
      label: tierLabel
        ? `${name} — ${tierLabel}${quantitySuffix}`
        : `${name}${quantitySuffix}`,
    };
  });
}

/**
 * Bénéfices mis en avant sur une carte de formule.
 *
 * Volontairement court et non exhaustif : la carte vend un usage, pas un
 * inventaire. Le socle est retiré — il est présent partout, donc il ne
 * distingue rien — et la capacité de stockage est reprise du palier retenu
 * parce que c'est le seul chiffre que le visiteur compare vraiment.
 */
export function describePresetBenefits(
  preset: BillingV2PublicPreset,
  catalog: BillingV2PublicCatalog,
  limit = 4,
): CompositionEntry[] {
  const seen = new Set<string>();
  const entries: CompositionEntry[] = [];

  for (const item of preset.items) {
    if (item.serviceCode === SERVICE_CODES.base) {
      continue;
    }

    const service = findService(catalog, item.serviceCode);
    const benefit = resolveServiceBenefit(item.serviceCode, service);
    if (!benefit || seen.has(item.serviceCode)) {
      continue;
    }

    seen.add(item.serviceCode);
    const tierLabel = resolveTierLabel(service, item.tierCode);
    const capacity =
      item.serviceCode === SERVICE_CODES.storagePersonal
      || item.serviceCode === SERVICE_CODES.storageShared;

    entries.push({
      key: item.serviceCode,
      label: capacity && tierLabel ? `${benefit} de ${tierLabel}` : benefit,
    });
  }

  return entries.slice(0, limit);
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
 * Présentation courte des caractéristiques administrées d'un palier.
 *
 * Les codes connus portent seulement leur libellé et leur format public. Les
 * valeurs viennent toujours de `tier.attributes` : aucune capacité de palier
 * n'est recopiée dans le portail. Un attribut inconnu est volontairement
 * ignoré jusqu'à ce que le catalogue lui attribue une présentation adaptée.
 */
const TIER_ATTRIBUTE_PRESENTERS: ReadonlyArray<{
  code: string;
  present: (value: string) => string;
}> = [
  { code: "vcpu_count", present: (value) => `${value} vCPU` },
  { code: "ram_gib", present: (value) => `${value} Go RAM` },
  { code: "disk_gib", present: (value) => `${value} Go stockage` },
];

export function describeTierAttributes(
  tier: Pick<BillingV2PublicTier, "attributes">,
): string[] {
  const valuesByCode = new Map(
    tier.attributes?.map((attribute) => [
      attribute.code,
      attribute.valueNumeric === null
        ? attribute.valueText
        : String(attribute.valueNumeric),
    ]) ?? [],
  );

  return TIER_ATTRIBUTE_PRESENTERS.flatMap(({ code, present }) => {
    const value = valuesByCode.get(code)?.trim();
    return value ? [present(value)] : [];
  });
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
    paymentMode: "monthly",
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

export type PublicSelectionConfigurationEntry = {
  key: string;
  label: string;
  value: string;
  enabled: boolean;
};

/**
 * Traduit la selection Billing V2 canonique en configuration lisible.
 *
 * Cette fonction n'invente aucune capacite et ne choisit aucune formule. Elle
 * decrit seulement la selection deja produite par le moteur de recommandation.
 * Les codes catalogue restent internes et aucun prix n'est calcule ici.
 */
export function describeSelectionConfiguration(
  selection: BillingV2PublicSelection,
  catalog: BillingV2PublicCatalog,
): PublicSelectionConfigurationEntry[] {
  const personalStorage = resolveSelectedTierLabel(
    catalog,
    SERVICE_CODES.storagePersonal,
    selection.storagePersonalTierCode,
  );
  const sharedStorage = selection.storageSharedTierCode
    ? resolveSelectedTierLabel(
        catalog,
        SERVICE_CODES.storageShared,
        selection.storageSharedTierCode,
      )
    : null;

  return [
    { key: "storage-personal", label: "Stockage personnel", value: personalStorage, enabled: true },
    {
      key: "backup-personal",
      label: "Sauvegarde personnelle",
      value: selection.backupPersonal ? "Incluse" : "Non",
      enabled: selection.backupPersonal,
    },
    {
      key: "storage-shared",
      label: "Espace partagé",
      value: sharedStorage ?? "Non",
      enabled: sharedStorage !== null,
    },
    {
      key: "backup-shared",
      label: "Sauvegarde partagée",
      value: selection.backupShared ? "Incluse" : "Non",
      enabled: selection.backupShared,
    },
    {
      key: "vpn",
      label: "Accès sécurisé à distance",
      value: selection.vpnTierCode ? "Inclus" : "Non",
      enabled: selection.vpnTierCode !== null,
    },
    {
      key: "remote-desktop",
      label: "Bureau Windows à distance",
      value: selection.remoteDesktop ? "Inclus" : "Non",
      enabled: selection.remoteDesktop,
    },
    {
      key: "users",
      label: "Utilisateurs",
      value: String(selection.additionalUsers + 1),
      enabled: true,
    },
    {
      key: "support-plus",
      label: "Support renforcé",
      value: selection.supportPlus ? "Inclus" : "Non",
      enabled: selection.supportPlus,
    },
  ];
}

function resolveSelectedTierLabel(
  catalog: BillingV2PublicCatalog,
  serviceCode: string,
  tierCode: string,
) {
  const label = resolveTierLabel(findService(catalog, serviceCode), tierCode);
  if (label && label !== tierCode) {
    return label;
  }
  return /^\d+$/.test(tierCode) ? `${tierCode} Go` : tierCode;
}

/**
 * Deux situations de refus sont administrables par un fragment système
 * (spécification, section 19) : « pas encore ouverte » et « indisponible
 * momentanément ». Ce sont celles dont le texte change réellement pendant la
 * phase de lancement.
 *
 * Les autres restent codées à dessein : elles décrivent une situation produit
 * précise — cette combinaison-là, ce mode de règlement-là. Les rendre
 * modifiables inviterait à les remplacer par un texte générique, et le
 * visiteur perdrait la seule information qui lui dit quoi changer.
 */
const CHECKOUT_SNIPPET_REASONS: Record<string, CheckoutSnippetKey> = {
  BILLING_V2_FIRST_REAL_SUBSCRIPTION_NOT_APPROVED: "checkout_not_open_yet",
  BILLING_V2_NEW_SUBSCRIPTIONS_FLAG_OFF: "checkout_not_open_yet",
  BILLING_V2_AUTHORITATIVE_CHECKOUT_FLAG_OFF: "checkout_not_open_yet",
  BILLING_V2_AUTHORITATIVE_CHECKOUT_NO_SQL: "checkout_temporarily_unavailable",
};

const CHECKOUT_REASON_MESSAGES: Record<string, string> = {
  BILLING_V2_PUBLIC_CHECKOUT_ROUTE_MISSING:
    "Cette combinaison formule / engagement n'est pas ouverte à la "
    + "souscription en ligne.",
  BILLING_V2_PUBLIC_PAYMENT_MODE_UNAVAILABLE:
    "Ce mode de règlement n'est pas proposé pour cette durée.",
  BILLING_V2_SCOPE_UPFRONT_OUT_OF_LAUNCH_SCOPE:
    "Le règlement en une fois n'est pas encore ouvert en ligne. Choisissez le "
    + "règlement au mois, ou contactez-nous.",
  BILLING_V2_IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_SELECTION:
    "Cette demande a déjà été enregistrée avec une autre configuration. "
    + "Rechargez la page pour repartir d'une demande propre.",
};

export type CheckoutSnippetKey =
  | "checkout_not_open_yet"
  | "checkout_temporarily_unavailable";

/** Textes administrables passés par la page serveur. */
export type CheckoutSnippets = Record<CheckoutSnippetKey, string>;

export function describeCheckoutReason(
  reasonCode: string,
  snippets?: CheckoutSnippets,
) {
  const snippetKey = CHECKOUT_SNIPPET_REASONS[reasonCode];
  if (snippetKey) {
    // Sans texte administré transmis, on retombe sur le repli de code : une
    // page ne doit jamais afficher un refus sans explication.
    return snippets?.[snippetKey] ?? CHECKOUT_SNIPPET_FALLBACKS[snippetKey];
  }

  return (
    CHECKOUT_REASON_MESSAGES[reasonCode]
    ?? "La souscription en ligne n'est pas disponible pour cette configuration."
  );
}

const CHECKOUT_SNIPPET_FALLBACKS: CheckoutSnippets = {
  checkout_not_open_yet:
    "La souscription en ligne n'est pas encore ouverte. Contactez-nous pour "
    + "mettre en place cette formule.",
  checkout_temporarily_unavailable:
    "La souscription en ligne est momentanément indisponible.",
};
