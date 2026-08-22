import type { BillingV2PublicCatalog, ManagedContentKey } from "@kermaria/shared";
export type StorefrontLink = { label: string; href: string };
export type StorefrontSection = { heading: string; bodyMarkdown: string };
export type StorefrontFaq = { question: string; answer: string };
export type StorefrontCta = { label: string; href: string };
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
