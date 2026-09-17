import "server-only";

import type {
  BillingV2PublicCatalog,
  BillingV2PublicPriceComponent,
  BillingV2PublicService,
  BillingV2PublicTier,
  PublicCommercialCatalog,
  PublicCommercialCta,
  PublicCommercialMoney,
  PublicCommercialOrderingMode,
  PublicCommercialPriceType,
  PublicCommercialService,
  PublicCommercialTier,
} from "@kermaria/shared";

import { describeTierAttributes, resolveServicePublicLabel } from "@/lib/billing-v2-formules";
import {
  resolveStorefrontDirectTariffAction,
  resolveStorefrontTariffAction,
  storefrontServiceUrlForBillingCode,
} from "@/lib/storefront-content";
import { resolvePublicCommercialOrdering } from "@/lib/public-commercial-ordering";

/**
 * Projection de vitrine de Billing V2.
 *
 * Sources, par champ :
 * - montants, devise, paliers, frais initiaux, visibilite et mode de
 *   commercialisation :
 *   `BillingV2PublicCatalog` fourni par API-INTERNAL ;
 * - URL et CTA : mappings de parcours storefront existants ;
 * - categorie et unite : adaptation de presentation des metadonnees Billing.
 *
 * Aucun montant n'est declare, additionne, remisé ou transforme ici. Pour
 * l'etiquette « a partir de », la fonction selectionne simplement le plus
 * petit montant deja resolu parmi les paliers publies ; elle ne devient pas
 * une autorite de calcul contractuel.
 *
 * Limite fiscale connue : le catalogue public ne fournit pas aujourd'hui une
 * autorite fiscale suffisante pour choisir entre HT et TTC ou calculer une
 * TVA. `TAX_NOTICE` est donc la formulation provisoire centralisee de cette
 * projection ; elle devra etre remplacee par une donnee metier fiable.
 *
 * `publicOrderingMode` est l'autorité de présentation quand il est fourni par
 * Billing V2. `selfServiceOrderable` reste un drapeau technique historique :
 * il ne signifie pas qu'un service est achetable seul. Les catalogues qui ne
 * portent pas encore le nouveau champ utilisent temporairement un repli fermé
 * qui ne peut jamais publier `direct`.
 */
export function buildPublicCommercialCatalog(
  catalog: BillingV2PublicCatalog,
): PublicCommercialCatalog {
  if (catalog.source === "unavailable") {
    return {
      source: catalog.source,
      taxNotice: TAX_NOTICE,
      services: [],
    };
  }

  return {
    source: catalog.source,
    taxNotice: TAX_NOTICE,
    services: catalog.services
      .filter((service) => service.publicVisible)
      .map((service, index) => projectService(service, catalog, index + 1))
      .sort((left, right) => left.displayOrder - right.displayOrder || left.name.localeCompare(right.name, "fr")),
  };
}

const TAX_NOTICE = "Montants affichés hors taxes applicables.";

const CATEGORY_PRESENTATION: Readonly<Record<string, string>> = {
  "Accès": "Postes et accès à distance",
  "Cloud": "Hébergement et services en ligne",
  "Domaines": "Messagerie et domaines",
  "Identité": "Réseau et sécurité",
  "Infogérance": "Assistance et maintenance",
  "Messagerie": "Messagerie et domaines",
  "Réseau": "Réseau et sécurité",
  "Sauvegarde": "Sauvegarde et stockage",
  "Sécurité": "Réseau et sécurité",
  "Stockage": "Sauvegarde et stockage",
  "Supervision": "Assistance et maintenance",
  "Support": "Assistance et maintenance",
  "Utilisateurs": "Postes et accès à distance",
  "Web": "Hébergement et services en ligne",
};

const CATEGORY_ORDER: Readonly<Record<string, number>> = {
  "Assistance et maintenance": 10,
  "Postes et accès à distance": 20,
  "Sauvegarde et stockage": 30,
  "Réseau et sécurité": 40,
  "Hébergement et services en ligne": 50,
  "Messagerie et domaines": 60,
};

/**
 * Replis de vocabulaire uniquement. Les tarifs, les paliers et les decisions
 * de commande ne figurent pas ici. Ils servent lorsque le nom administrable
 * Billing V2 reste trop technique pour un libelle public.
 *
 * Source ideale a terme : champs "nom public" et "resume public" du catalogue
 * administrable. Ils n'existent pas encore ; ce repli isole evite de diffuser
 * le jargon sur /tarifs sans creer de seconde autorite commerciale.
 */
const CLIENT_PRESENTATION_FALLBACKS: Readonly<Record<string, {
  name?: string;
  description?: string;
}>> = {
  "CLOUDFLARE-MANAGED": {
    name: "Protection de vos services en ligne",
    description: "Renforcez la sécurité et la disponibilité de votre présence en ligne.",
  },
  "DNS-MANAGED": {
    name: "Adresse Internet et DNS",
    description: "Reliez votre nom de domaine à vos sites, e-mails et services en ligne.",
  },
  "FIREWALL-MANAGED": {
    name: "Protection de votre réseau",
    description: "Faites suivre et maintenir le dispositif qui protège votre réseau professionnel.",
  },
  "IDENTITY-MANAGED": {
    name: "Comptes et accès de votre équipe",
    description: "Organisez les comptes et les droits d’accès de vos collaborateurs.",
  },
  "MAIL-DMARC-MANAGED": {
    name: "Protection de votre nom d’expéditeur",
    description: "Aidez les destinataires à reconnaître les e-mails réellement envoyés en votre nom.",
  },
  "SSL-MANAGED": {
    name: "Sécurité des échanges en ligne",
    description: "Protégez les échanges entre vos visiteurs, vos outils et vos services en ligne.",
  },
  "UNIFI-MANAGED": {
    name: "Réseau Wi-Fi supervisé",
    description: "Suivez et maintenez votre réseau Wi-Fi professionnel.",
  },
  "VPS-CLOUD": {
    name: "Serveur virtuel dans le cloud",
    description: "Choisissez les ressources adaptées à votre site, application ou service en ligne.",
  },
  "VPS-LOCAL": {
    name: "Serveur virtuel hébergé localement",
    description: "Choisissez les ressources adaptées à votre site, application ou service en ligne.",
  },
  "WAF-REVERSE-PROXY": {
    name: "Protection avancée de votre site",
    description: "Filtrez les requêtes malveillantes avant qu’elles n’atteignent vos services en ligne.",
  },
};

function projectService(
  service: BillingV2PublicService,
  catalog: BillingV2PublicCatalog,
  displayOrder: number,
): PublicCommercialService {
  const tierProjections = service.tiers.map((tier) => projectTier(tier, catalog.currency));
  const recurringAmounts = [
    service.flatMonthlyAmountCents,
    ...tierProjections.map((tier) => tier.recurringPrice?.amountCents ?? null),
  ].filter((amount): amount is number => amount !== null && amount > 0);
  const startingPrice = recurringAmounts.length > 0
    ? money(Math.min(...recurringAmounts), catalog.currency)
    : null;
  const publicUrl = storefrontServiceUrlForBillingCode(service.code);
  const preferredOfferAction = resolvePreferredOfferAction(service.code, catalog);
  const offerCount = countPublicOffersForService(service.code, catalog);
  const directAction = resolveStorefrontDirectTariffAction(
    service.code,
    service.selfServiceOrderable,
    service.tiers,
  );
  const ordering = resolvePublicCommercialOrdering({
    requestedMode: service.publicOrderingMode,
    legacyMode: resolveLegacyOrderingMode(service, preferredOfferAction),
    offerCount,
    preferredOfferCta: preferredOfferAction,
    directCta: directAction,
  });
  const orderingMode = ordering.orderingMode;
  const directlyOrderable = orderingMode === "direct";
  const requiresQuote = orderingMode === "quote";
  const priceType = resolvePriceType(startingPrice, tierProjections);
  const fallback = CLIENT_PRESENTATION_FALLBACKS[service.code];
  const name = fallback?.name ?? resolveServicePublicLabel(service.code, service.name);
  const description = service.description?.trim() || fallback?.description || null;
  const category = presentCategory(service.category);

  return {
    id: service.code,
    slug: publicUrl ? publicUrl.slice("/services/".length) : slugFromCode(service.code),
    name,
    category,
    description,
    priceType,
    startingPrice,
    billingPeriodLabel: startingPrice ? "par mois" : null,
    billingUnitLabel: startingPrice ? presentBillingUnit(service.scopeType) : null,
    initialFees: initialFeesOf(service.flatPriceComponents ?? []),
    tiers: tierProjections,
    // Billing V2 ne porte actuellement aucun add-on public generique distinct
    // des paliers : ne pas en inventer pour remplir la carte.
    options: [],
    included: [],
    notIncluded: [],
    orderingMode,
    directlyOrderable,
    requiresQuote,
    publicUrl,
    offerCount: ordering.offerCount,
    // Les destinations proviennent soit d'un preset réellement publié, soit
    // du configurateur VPS individuel existant, soit du contact. Aucun CTA
    // n'est fabriqué à partir d'un prix ou d'une page descriptive.
    primaryCta: ordering.primaryCta,
    secondaryCta: publicUrl === null || publicUrl === ordering.primaryCta.href
      ? null
      : { label: "Découvrir le service", href: publicUrl },
    displayOrder: categoryOrder(category, displayOrder),
    visible: service.publicVisible,
  };
}

function projectTier(
  tier: BillingV2PublicTier,
  fallbackCurrency: string,
): PublicCommercialTier {
  const recurringPrice = tier.monthlyAmountCents > 0
    ? money(tier.monthlyAmountCents, currencyOf(tier.priceComponents, fallbackCurrency))
    : null;
  return {
    id: tier.code,
    label: tier.label,
    description: tier.description,
    recurringPrice,
    initialFees: initialFeesOf(tier.priceComponents ?? []),
    details: describeTierAttributes(tier),
  };
}

function resolvePriceType(
  startingPrice: PublicCommercialMoney | null,
  tiers: readonly PublicCommercialTier[],
): PublicCommercialPriceType {
  if (!startingPrice) return "quote";
  return tiers.length > 0 ? "from" : "fixed";
}

function resolveLegacyOrderingMode(
  service: BillingV2PublicService,
  preferredOfferAction: PublicCommercialCta | null,
): Exclude<PublicCommercialOrderingMode, "direct"> {
  if (!service.selfServiceOrderable || preferredOfferAction === null) return "quote";

  // Les seuls parcours self-service actuellement prouves sont les presets
  // existants de /formules. Ils composent une offre : ce ne sont pas des
  // achats individuels du service affiche sur la carte.
  if (preferredOfferAction.href.startsWith("/formules/")) return "offer_component";

  // Echec ferme : le mode `direct` devra etre alimente par une source metier
  // explicite et une destination individuelle existante, pas par ce drapeau.
  return "quote";
}

function resolvePreferredOfferAction(
  serviceCode: string,
  catalog: BillingV2PublicCatalog,
): PublicCommercialCta | null {
  const action = resolveStorefrontTariffAction(serviceCode, catalog);
  return action.href.startsWith("/formules/") ? action : null;
}

function countPublicOffersForService(
  serviceCode: string,
  catalog: BillingV2PublicCatalog,
): number {
  return catalog.presets.filter((preset) => (
    preset.items.some((item) => item.serviceCode === serviceCode)
  )).length;
}

function initialFeesOf(
  components: readonly BillingV2PublicPriceComponent[],
): PublicCommercialMoney[] {
  return components
    .filter((component) => component.billingCadence === "one_time"
      && component.chargeTrigger === "initial_subscription")
    .map((component) => money(component.amountCents, component.currency));
}

function currencyOf(
  components: readonly BillingV2PublicPriceComponent[] | null | undefined,
  fallbackCurrency: string,
): string {
  return components?.find((component) => component.billingCadence === "monthly")?.currency ?? fallbackCurrency;
}

function money(amountCents: number, currency: string): PublicCommercialMoney {
  return { amountCents, currency };
}

function presentCategory(category: string): string {
  return CATEGORY_PRESENTATION[category] ?? (category || "Autres services");
}

function categoryOrder(category: string, fallback: number): number {
  const prefix = CATEGORY_ORDER[category] ?? 90;
  return prefix * 10_000 + fallback;
}

function presentBillingUnit(scopeType: string): string {
  return scopeType === "user" ? "par utilisateur et par mois" : "par mois";
}

function slugFromCode(code: string): string {
  return code.toLowerCase().replaceAll(/[^a-z0-9]+/g, "-").replaceAll(/^-|-$/g, "");
}
