import type { BillingV2PublicCatalog, ManagedContentKey } from "@kermaria/shared";
export type StorefrontLink = { label: string; href: string };
export const STOREFRONT_SERVICES_PROBLEM_DESTINATIONS = [
  "/services/messagerie-professionnelle",
  "/services/sauvegarde-externalisee",
  "/vpn-ou-bureau-a-distance-que-choisir",
  "/services/unifi",
  "/services/cloud-hebergement",
  "/services/support-it",
] as const;
export const STOREFRONT_SERVICES_CATEGORY_DESTINATIONS = [
  "/services/cloud-hebergement",
  "/services/domaines-messagerie",
  "/services/reseau-securite",
  "/services/support-it",
] as const;
export type StorefrontServicesProblemDestination =
  (typeof STOREFRONT_SERVICES_PROBLEM_DESTINATIONS)[number];
export type StorefrontProblemEntry = {
  title: string;
  description: string;
  href: StorefrontServicesProblemDestination;
};
export type StorefrontSection = { heading: string; bodyMarkdown: string };
export type StorefrontFaq = { question: string; answer: string };
export type StorefrontCta = { label: string; href: string };
export type StorefrontCommercialMode = "FORMULA" | "QUOTE" | "HYBRID";
export type StorefrontCommercialActions = {
  mode: StorefrontCommercialMode;
  primaryAction: StorefrontCta;
  secondaryAction: StorefrontCta | null;
  presetCode: string | null;
};
export type StorefrontPageContent = {
  seoTitle: string;
  seoDescription: string;
  title: string;
  lead: string;
  ctaLabel: string;
  ctaHref: string;
  sections: StorefrontSection[];
  faq: StorefrontFaq[];
  relatedLinks: StorefrontLink[];
};
export type StorefrontServicesLandingContent = StorefrontPageContent & {
  problemEntries: StorefrontProblemEntry[];
};

export const DEFAULT_STOREFRONT_SERVICES_PROBLEM_ENTRIES: readonly StorefrontProblemEntry[] = [
  {
    title: "Mes emails posent problème",
    description: "Spam, migration, domaine, comptes ou configuration : identifiez le bon point de départ pour remettre la messagerie au propre.",
    href: "/services/messagerie-professionnelle",
  },
  {
    title: "Je veux protéger mes données",
    description: "Sauvegarde, restauration et conservation : protégez les fichiers importants avec une stratégie adaptée à leur usage.",
    href: "/services/sauvegarde-externalisee",
  },
  {
    title: "Je dois travailler à distance",
    description: "VPN et bureau Windows distant ne répondent pas au même besoin. Comparez les deux approches avant de choisir.",
    href: "/vpn-ou-bureau-a-distance-que-choisir",
  },
  {
    title: "Mon réseau ou mon Wi-Fi fonctionne mal",
    description: "Coupures, couverture, lenteurs ou segmentation : partez des usages réels pour fiabiliser le réseau et le Wi-Fi.",
    href: "/services/unifi",
  },
  {
    title: "J'ai un serveur, un site ou une application à maintenir",
    description: "Hébergement, mises à jour, supervision et sauvegarde : identifiez les briques à suivre pour garder le service maintenable.",
    href: "/services/cloud-hebergement",
  },
  {
    title: "Je veux déléguer mon informatique",
    description: "Support, maintenance, supervision et coordination : confiez le quotidien IT avec un périmètre clair et adapté à votre structure.",
    href: "/services/support-it",
  },
];

export const DEFAULT_STOREFRONT_SERVICES_CATEGORY_LINKS: readonly StorefrontLink[] = [
  { label: "Cloud & Hébergement", href: "/services/cloud-hebergement" },
  { label: "Domaines & Messagerie", href: "/services/domaines-messagerie" },
  { label: "Réseau & Sécurité", href: "/services/reseau-securite" },
  { label: "Support & IT", href: "/services/support-it" },
];
export const DEFAULT_STOREFRONT_SERVICES_LEAD =
  "Un problème de messagerie, des données à protéger, un accès distant à organiser, un Wi-Fi instable ou un serveur à maintenir ? Partez de votre besoin : Zachary IT vous oriente vers la solution adaptée.";

export const DEFAULT_STOREFRONT_SERVICES_SEO_DESCRIPTION =
  "Messagerie, sauvegarde, accès distant, réseau, hébergement et support : partez de votre problème pour identifier le service IT adapté avec Zachary IT.";

export const DEFAULT_STOREFRONT_SERVICES_SECTIONS: readonly StorefrontSection[] = [
  {
    heading: "Des services modulaires, pas un catalogue figé",
    bodyMarkdown: "Vous pouvez confier une brique précise ou un ensemble cohérent. Le périmètre est défini selon votre besoin, l'existant et les dépendances utiles ; les prestations nécessitant une étude restent traitées sur devis.",
  },
];

export const DEFAULT_STOREFRONT_SERVICES_FAQ: readonly StorefrontFaq[] = [
  {
    question: "Puis-je choisir un seul service ?",
    answer: "Oui. Les services sont modulaires ; les dépendances éventuelles sont expliquées avant la mise en place.",
  },
  {
    question: "Tout est-il commandable en ligne ?",
    answer: "Non. Les services nécessitant une étude, une migration ou une mise en service restent traités par devis.",
  },
  {
    question: "À qui s'adressent ces services ?",
    answer: "Aux indépendants, associations, TPE et petites structures qui veulent déléguer une informatique utile et maintenable.",
  },
];

export const STOREFRONT_SERVICE_SLUGS = [
  "vps",
  "infogerance-vps",
  "hebergement-web",
  "maintenance-linux",
  "maintenance-wordpress",
  "sauvegarde-externalisee",
  "supervision-informatique",
  "supervision-nas",
  "vpn-entreprise",
  "bureau-windows-distance",
  "unifi",
  "firewall",
  "cloudflare-waf",
  "gestion-dns-domaines",
  "messagerie-professionnelle",
] as const;
export type StorefrontServiceSlug = (typeof STOREFRONT_SERVICE_SLUGS)[number];
export const STOREFRONT_PRIORITY_SERVICE_SLUGS = [
  "messagerie-professionnelle",
  "vpn-entreprise",
  "sauvegarde-externalisee",
  "unifi",
  "infogerance-vps",
  "hebergement-web",
] as const satisfies readonly StorefrontServiceSlug[];
export type StorefrontPriorityServiceSlug =
  (typeof STOREFRONT_PRIORITY_SERVICE_SLUGS)[number];
export function isStorefrontPriorityServiceSlug(
  slug: StorefrontServiceSlug,
): slug is StorefrontPriorityServiceSlug {
  return STOREFRONT_PRIORITY_SERVICE_SLUGS.includes(
    slug as StorefrontPriorityServiceSlug,
  );
}
export type StorefrontBreadcrumbItem = { name: string; path: string };

const STOREFRONT_CATEGORY_BREADCRUMB_LABELS = {
  "cloud-hebergement": "Cloud & H\u00e9bergement",
  "domaines-messagerie": "Domaines & Messagerie",
  "reseau-securite": "R\u00e9seau & S\u00e9curit\u00e9",
  "support-it": "Support & IT",
} as const;

const STOREFRONT_SERVICE_BREADCRUMB_LABELS: Record<StorefrontServiceSlug, string> = {
  "vps": "VPS",
  "infogerance-vps": "Infog\u00e9rance VPS",
  "hebergement-web": "H\u00e9bergement web",
  "maintenance-linux": "Maintenance Linux",
  "maintenance-wordpress": "Maintenance WordPress",
  "sauvegarde-externalisee": "Sauvegarde externalis\u00e9e",
  "supervision-informatique": "Supervision informatique",
  "supervision-nas": "Supervision NAS",
  "vpn-entreprise": "VPN entreprise",
  "bureau-windows-distance": "Bureau Windows \u00e0 distance",
  "unifi": "UniFi",
  "firewall": "Firewall",
  "cloudflare-waf": "Cloudflare WAF",
  "gestion-dns-domaines": "Gestion DNS & domaines",
  "messagerie-professionnelle": "Messagerie professionnelle",
};

export function resolveStorefrontBreadcrumb(
  pathname: string,
): StorefrontBreadcrumbItem[] | null {
  if (pathname === "/services") return [{ name: "Services", path: "/services" }];
  if (pathname === "/tarifs") return [{ name: "Tarifs", path: "/tarifs" }];
  if (!pathname.startsWith("/services/")) return null;

  const slug = pathname.slice("/services/".length);
  const categoryLabel = STOREFRONT_CATEGORY_BREADCRUMB_LABELS[
    slug as keyof typeof STOREFRONT_CATEGORY_BREADCRUMB_LABELS
  ];
  if (categoryLabel) {
    return [
      { name: "Services", path: "/services" },
      { name: categoryLabel, path: `/services/${slug}` },
    ];
  }

  if (!STOREFRONT_SERVICE_SLUGS.includes(slug as StorefrontServiceSlug)) return null;
  return [
    { name: "Services", path: "/services" },
    {
      name: STOREFRONT_SERVICE_BREADCRUMB_LABELS[slug as StorefrontServiceSlug],
      path: `/services/${slug}`,
    },
  ];
}

// Mapping fermé et non administrable : le CMS ne choisit jamais le serviceCode.
// Une page qui agrège plusieurs services n'est self-service que si tous les
// services Billing visibles qui la composent sont explicitement commandables.
const STOREFRONT_SERVICE_BILLING_CODES: Record<StorefrontServiceSlug, readonly string[]> = {
  "vps": ["VPS-LOCAL", "VPS-CLOUD", "VPS-EXTERNAL-MANAGED", "VPS-MANAGED-ADDON"],
  "infogerance-vps": ["VPS-EXTERNAL-MANAGED", "VPS-MANAGED-ADDON"],
  "hebergement-web": ["WEB-EXTERNAL-MANAGED"],
  "maintenance-linux": ["LINUX-PATCH-MANAGED"],
  "maintenance-wordpress": ["CMS-MAINT"],
  "sauvegarde-externalisee": ["BACKUP-EXTERNAL-MANAGED"],
  "supervision-informatique": ["MONITORING-EXTERNAL"],
  "supervision-nas": ["NAS-MONITORING"],
  "vpn-entreprise": ["VPN-ACCESS"],
  "bureau-windows-distance": ["RDS-ACCESS"],
  "unifi": ["UNIFI-MANAGED"],
  "firewall": ["FIREWALL-MANAGED"],
  "cloudflare-waf": ["WAF-REVERSE-PROXY"],
  "gestion-dns-domaines": ["DOMAIN-MANAGED", "DNS-MANAGED"],
  "messagerie-professionnelle": ["MAIL-MANAGED", "MAIL-DMARC-MANAGED", "M365-MANAGED"],
};

type StorefrontCommercialRouteDefinition = {
  mode: Exclude<StorefrontCommercialMode, "QUOTE">;
  presetCode: string;
  formulaLabel: string;
  secondaryLabel: string;
  requiredPresetServiceCodes: readonly string[];
};

// Mapping commercial ferme et distinct du mapping technique SEO -> Billing.
// Toute page non declaree ici reste sur devis par defaut.
const STOREFRONT_COMMERCIAL_ROUTES: Readonly<Partial<Record<
  StorefrontServiceSlug,
  StorefrontCommercialRouteDefinition
>>> = {
  "sauvegarde-externalisee": {
    mode: "HYBRID",
    presetCode: "pack-dossier-securise",
    formulaLabel: "Prot\u00e9ger mes fichiers avec une offre",
    secondaryLabel: "Sauvegarder un serveur ou un NAS",
    requiredPresetServiceCodes: ["BACKUP-PERSONAL"],
  },
  "vpn-entreprise": {
    mode: "FORMULA",
    presetCode: "pack-acces-distance",
    formulaLabel: "Configurer mon acc\u00e8s \u00e0 distance",
    secondaryLabel: "J'ai un besoin sp\u00e9cifique",
    requiredPresetServiceCodes: ["VPN-ACCESS"],
  },
  "bureau-windows-distance": {
    mode: "FORMULA",
    presetCode: "pack-bureau-windows-distance",
    formulaLabel: "Configurer mon bureau \u00e0 distance",
    secondaryLabel: "Demander un conseil",
    requiredPresetServiceCodes: ["RDS-ACCESS"],
  },
};

const STOREFRONT_TARIFF_PRESET_BY_SERVICE_CODE: Readonly<Record<string, string>> = {
  "VPN-ACCESS": "pack-acces-distance",
  "RDS-ACCESS": "pack-bureau-windows-distance",
};
const SELF_SERVICE_LABEL_PATTERN = /\b(command(?:er|ez|e|es)?|achet(?:er|ez|e|es)?|achat|configur(?:er|ez|e|es|ation)?)\b/i;
export function storefrontContentKeyForServiceSlug(
  slug: StorefrontServiceSlug,
): ManagedContentKey {
  return `storefront:${slug}` as ManagedContentKey;
}
export function storefrontServiceSlugForContentKey(
  key: ManagedContentKey,
): StorefrontServiceSlug | null {
  if (!key.startsWith("storefront:")) return null;
  const slug = key.slice("storefront:".length);
  return STOREFRONT_SERVICE_SLUGS.includes(slug as StorefrontServiceSlug)
    ? slug as StorefrontServiceSlug
    : null;
}
export function storefrontServiceSelfServiceOrderable(
  slug: StorefrontServiceSlug,
  catalog: BillingV2PublicCatalog,
): boolean {
  const requiredCodes = STOREFRONT_SERVICE_BILLING_CODES[slug];
  const linkedServices = requiredCodes.map((code) =>
    catalog.services.find((service) => service.code === code) ?? null,
  );
  // Fail closed si le catalogue est indisponible, incomplet ou si un service
  // lié n'est pas public. Le CMS ne peut donc jamais réactiver le self-service.
  return linkedServices.length > 0
    && linkedServices.every((service) =>
      service !== null
      && service.publicVisible === true
      && service.selfServiceOrderable === true,
    );
}
export function resolveStorefrontCommercialActions(
  slug: StorefrontServiceSlug,
  catalog: BillingV2PublicCatalog,
  content: Pick<StorefrontPageContent, "ctaLabel" | "ctaHref">,
): StorefrontCommercialActions {
  const route = STOREFRONT_COMMERCIAL_ROUTES[slug];
  const quoteAction = resolveStorefrontPublicCta(content, false);

  if (!route) {
    return { mode: "QUOTE", primaryAction: quoteAction, secondaryAction: null, presetCode: null };
  }

  const preset = catalog.presets.find((item) => item.code === route.presetCode) ?? null;
  const presetContainsRequiredServices = preset !== null
    && route.requiredPresetServiceCodes.every((serviceCode) =>
      preset.items.some((item) => item.serviceCode === serviceCode),
    );

  if (!presetContainsRequiredServices) {
    return { mode: "QUOTE", primaryAction: quoteAction, secondaryAction: null, presetCode: null };
  }

  return {
    mode: route.mode,
    primaryAction: {
      label: route.formulaLabel,
      href: `/formules/${encodeURIComponent(route.presetCode)}`,
    },
    secondaryAction: { label: route.secondaryLabel, href: quoteAction.href },
    presetCode: route.presetCode,
  };
}

export function resolveStorefrontTariffAction(
  serviceCode: string,
  catalog: BillingV2PublicCatalog,
): StorefrontCta {
  const presetCode = STOREFRONT_TARIFF_PRESET_BY_SERVICE_CODE[serviceCode];
  const preset = presetCode
    ? catalog.presets.find((item) => item.code === presetCode) ?? null
    : null;

  if (preset?.items.some((item) => item.serviceCode === serviceCode)) {
    return {
      label: "Voir l'offre",
      href: `/formules/${encodeURIComponent(preset.code)}`,
    };
  }

  return { label: "Demander un devis", href: "/contact" };
}


export function isStorefrontSelfServiceCta(label: string, href: string): boolean {
  const normalizedHref = href.trim().toLowerCase();
  return normalizedHref === "/formules"
    || normalizedHref.startsWith("/formules/")
    || SELF_SERVICE_LABEL_PATTERN.test(label.trim());
}
export function resolveStorefrontPublicRelatedLinks(
  links: readonly StorefrontLink[],
  selfServiceOrderable: boolean | null,
): StorefrontLink[] {
  if (selfServiceOrderable !== false) return [...links];
  return links.filter((link) => !isStorefrontSelfServiceCta(link.label, link.href));
}export function resolveStorefrontPublicCta(
  content: Pick<StorefrontPageContent, "ctaLabel" | "ctaHref">,
  selfServiceOrderable: boolean | null,
): StorefrontCta {
  const configured = { label: content.ctaLabel, href: content.ctaHref };
  if (selfServiceOrderable !== false || !isStorefrontSelfServiceCta(configured.label, configured.href)) {
    return configured;
  }
  // Le rendu public est l'autorité finale. Même un CTA CMS volontairement
  // incohérent est neutralisé pour un service Billing non self-service.
  if (configured.href === "/diagnostic") {
    return { label: "Demander un audit", href: "/diagnostic" };
  }
  return { label: "Demander un devis", href: "/contact" };
}
export function parseStorefrontPageContent(
  value: string,
): StorefrontPageContent | null {
  try {
    const candidate = JSON.parse(value) as Partial<StorefrontPageContent>;
    if (
      !isText(candidate.seoTitle, 10, 200)
      || !isText(candidate.seoDescription, 30, 400)
      || !isText(candidate.title, 3, 200)
      || !isText(candidate.lead, 10, 1200)
      || !isText(candidate.ctaLabel, 3, 80)
      || !isSafeInternalPath(candidate.ctaHref)
      || !Array.isArray(candidate.sections)
      || candidate.sections.length < 1
      || candidate.sections.length > 12
      || !candidate.sections.every(
        (section) => isText(section?.heading, 2, 4000)
          && isText(section?.bodyMarkdown, 3, 12000),
      )
      || !Array.isArray(candidate.faq)
      || candidate.faq.length < 2
      || candidate.faq.length > 12
      || !candidate.faq.every(
        (item) => isText(item?.question, 2, 4000)
          && isText(item?.answer, 3, 12000),
      )
      || !Array.isArray(candidate.relatedLinks)
      || candidate.relatedLinks.length < 1
      || candidate.relatedLinks.length > 12
      || !candidate.relatedLinks.every(
        (link) => isText(link?.label, 2, 4000) && isSafeInternalPath(link?.href),
      )
    ) {
      return null;
    }
    return {
      seoTitle: candidate.seoTitle.trim().replace(/\s*\|\s*Zachary IT$/i, ""),
      seoDescription: candidate.seoDescription.trim(),
      title: candidate.title.trim(),
      lead: candidate.lead.trim(),
      ctaLabel: candidate.ctaLabel.trim(),
      ctaHref: candidate.ctaHref.trim(),
      sections: candidate.sections.map((section) => ({
        heading: section.heading.trim(),
        bodyMarkdown: section.bodyMarkdown.trim(),
      })),
      faq: candidate.faq.map((item) => ({
        question: item.question.trim(),
        answer: item.answer.trim(),
      })),
      relatedLinks: candidate.relatedLinks.map((link) => ({
        label: link.label.trim(),
        href: link.href.trim(),
      })),
    };
  } catch {
    return null;
  }
}
export function parseStorefrontServicesLandingContent(
  value: string,
  allowLegacy = false,
): StorefrontServicesLandingContent | null {
  const base = parseStorefrontPageContent(value);
  if (!base) return null;

  try {
    const candidate = JSON.parse(value) as { problemEntries?: unknown };
    const hasValidProblemEntries = Array.isArray(candidate.problemEntries)
      && candidate.problemEntries.length === 6
      && candidate.problemEntries.every((entry) => {
        if (!entry || typeof entry !== "object") return false;
        const item = entry as Partial<StorefrontProblemEntry>;
        return isText(item.title, 3, 120)
          && isText(item.description, 10, 400)
          && typeof item.href === "string"
          && STOREFRONT_SERVICES_PROBLEM_DESTINATIONS.includes(
            item.href as StorefrontServicesProblemDestination,
          );
      });

    const hasValidCategoryLinks = base.relatedLinks.length === 4
      && base.relatedLinks.every((link) =>
        STOREFRONT_SERVICES_CATEGORY_DESTINATIONS.includes(
          link.href as (typeof STOREFRONT_SERVICES_CATEGORY_DESTINATIONS)[number],
        )
      )
      && new Set(base.relatedLinks.map((link) => link.href)).size === 4;

    if (!hasValidProblemEntries || !hasValidCategoryLinks) {
      if (!allowLegacy) return null;
      return {
        ...base,
        seoDescription: DEFAULT_STOREFRONT_SERVICES_SEO_DESCRIPTION,
        lead: DEFAULT_STOREFRONT_SERVICES_LEAD,
        sections: DEFAULT_STOREFRONT_SERVICES_SECTIONS.map((section) => ({ ...section })),
        faq: DEFAULT_STOREFRONT_SERVICES_FAQ.map((item) => ({ ...item })),
        relatedLinks: DEFAULT_STOREFRONT_SERVICES_CATEGORY_LINKS.map((link) => ({ ...link })),
        problemEntries: DEFAULT_STOREFRONT_SERVICES_PROBLEM_ENTRIES.map((entry) => ({ ...entry })),
      };
    }

    const problemEntries = (candidate.problemEntries as unknown[]).map((entry: unknown) => {
      const item = entry as StorefrontProblemEntry;
      return {
        title: item.title.trim(),
        description: item.description.trim(),
        href: item.href,
      };
    });

    if (new Set(problemEntries.map((entry) => entry.href)).size !== problemEntries.length) {
      return null;
    }

    return { ...base, problemEntries };
  } catch {
    return null;
  }
}
export function isStorefrontContentKey(key: ManagedContentKey): boolean {
  return key.startsWith("storefront:");
}
function isText(value: unknown, min: number, max: number): value is string {
  return typeof value === "string" && value.trim().length >= min && value.trim().length <= max;
}
function isSafeInternalPath(value: unknown): value is string {
  return typeof value === "string"
    && value.startsWith("/")
    && !value.startsWith("//")
    && !value.includes("\\")
    && value.length <= 160;
}
