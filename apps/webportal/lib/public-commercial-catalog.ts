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

import { describeTierAttributes, resolveServicePublicDetail } from "@/lib/billing-v2-formules";
import {
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
  "Tests DEV": "Démonstration",
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
type LegacyServicePresentation = {
  legacyName: string;
  name: string;
  description: string;
  legacyDescription?: string;
};

// La projection n'agit que sur les libellés historiques exacts. Dès qu'un
// administrateur remplace un nom ou une description dans Billing V2, son texte
// prend le dessus. Les codes, prix, paliers et actions restent inchangés.
const LEGACY_SERVICE_PRESENTATION: Readonly<Record<string, LegacyServicePresentation>> = {
  "SERVICE-E2E-DEV": {
    legacyName: "Service E2E DEV", name: "Parcours de démonstration",
    legacyDescription: "Service de validation E2E DEV : appartenance au groupe AD de service dédié.",
    description: "Permet de vérifier une commande dans cet environnement de test.",
  },
  "STORAGE-PERSONAL": {
    legacyName: "Stockage personnel", name: "Espace personnel de fichiers",
    legacyDescription: "Quota de stockage personnel attribué à un utilisateur.",
    description: "Un espace pour conserver et retrouver vos fichiers.",
  },
  "STORAGE-SHARED": {
    legacyName: "Stockage partagé", name: "Espace de fichiers partagé",
    legacyDescription: "Quota de stockage partagé attribué à l'abonnement ou à l'organisation.",
    description: "Un espace commun pour les fichiers de votre équipe.",
  },
  "BACKUP-PERSONAL": {
    legacyName: "Sauvegarde du stockage personnel", name: "Copie de sécurité de vos fichiers",
    legacyDescription: "Sauvegarde du stockage personnel d'un utilisateur. Le tier doit suivre la capacité de stockage personnel couverte.",
    description: "Une copie de votre espace personnel, adaptée à sa capacité.",
  },
  "BACKUP-SHARED": {
    legacyName: "Sauvegarde du stockage partagé", name: "Copie de sécurité de l'espace partagé",
    legacyDescription: "Sauvegarde du stockage partagé. Le tier doit suivre la capacité de stockage partagé couverte.",
    description: "Une copie de l'espace commun, adaptée à sa capacité.",
  },
  "VPN-ACCESS": {
    legacyName: "Accès VPN", name: "Accès sécurisé à distance",
    legacyDescription: "Accès VPN sécurisé avec niveau de performance commercial.",
    description: "Retrouvez vos outils et vos fichiers depuis l'extérieur, par une connexion sécurisée.",
  },
  "RDS-ACCESS": {
    legacyName: "Accès bureau distant RDS", name: "Bureau Windows à distance",
    legacyDescription: "Accès utilisateur à l'environnement Windows distant.",
    description: "Retrouvez votre bureau de travail Windows depuis un autre lieu.",
  },
  "USER-ADDITIONAL": {
    legacyName: "Utilisateur supplémentaire", name: "Accès pour une personne supplémentaire",
    legacyDescription: "Compte utilisateur supplémentaire rattaché à l'abonnement.",
    description: "Ajoutez un accès personnel à l'offre de votre équipe.",
  },
  "SUPPORT-PLUS": {
    legacyName: "Support Plus", name: "Assistance renforcée",
    legacyDescription: "Option d'assistance renforcée pour les services souscrits.",
    description: "Une aide supplémentaire pour les services de votre offre.",
  },
  "CLOUDFLARE-MANAGED": {
    legacyName: "Cloudflare managé",
    name: "Protection de vos services en ligne",
    description: "Renforcez la sécurité et la disponibilité de votre présence en ligne.",
  },
  "DNS-MANAGED": {
    legacyName: "DNS managé",
    name: "Réglages de votre nom de domaine",
    description: "Reliez votre nom de domaine à vos sites, e-mails et services en ligne.",
  },
  "FIREWALL-MANAGED": {
    legacyName: "Firewall managé",
    name: "Protection de votre réseau",
    description: "Faites suivre et maintenir le dispositif qui protège votre réseau professionnel.",
  },
  "IDENTITY-MANAGED": {
    legacyName: "Gestion des identités",
    name: "Comptes et accès de votre équipe",
    description: "Organisez les comptes et les droits d’accès de vos collaborateurs.",
  },
  "MAIL-DMARC-MANAGED": {
    legacyName: "DMARC managé",
    name: "Protection de votre nom d’expéditeur",
    description: "Aidez les destinataires à reconnaître les e-mails réellement envoyés en votre nom.",
  },
  "SSL-MANAGED": {
    legacyName: "SSL managé",
    name: "Sécurité des échanges en ligne",
    description: "Protégez les échanges entre vos visiteurs, vos outils et vos services en ligne.",
  },
  "UNIFI-MANAGED": {
    legacyName: "UniFi managé",
    name: "Suivi de votre réseau Wi-Fi",
    description: "Suivez et maintenez votre réseau Wi-Fi professionnel.",
  },
  "VPS-CLOUD": {
    legacyName: "VPS cloud",
    name: "Serveur hébergé à distance",
    description: "Choisissez les ressources adaptées à votre site, application ou service en ligne.",
  },
  "VPS-LOCAL": {
    legacyName: "VPS local",
    name: "Serveur hébergé localement",
    description: "Choisissez les ressources adaptées à votre site, application ou service en ligne.",
  },
  "WAF-REVERSE-PROXY": {
    legacyName: "WAF et reverse proxy",
    name: "Protection avancée de votre site",
    description: "Filtrez les requêtes malveillantes avant qu’elles n’atteignent vos services en ligne.",
  },
  "MAIL-MANAGED": {
    legacyName: "Messagerie managée", name: "Suivi de vos adresses e-mail",
    description: "Mise en place et suivi des adresses e-mail de votre équipe.",
  },
  "M365-MANAGED": {
    legacyName: "Microsoft 365 managé", name: "Suivi de vos outils Microsoft 365",
    description: "Aide à la mise en place et au suivi de vos outils Microsoft 365.",
  },
  "WEB-EXTERNAL-MANAGED": {
    legacyName: "Hébergement Web géré", name: "Hébergement de votre site",
    description: "Mise en ligne et suivi de votre site web.",
  },
  "CMS-MAINT": {
    legacyName: "Maintenance CMS", name: "Entretien de votre site web",
    description: "Mises à jour et suivi de votre site web.",
  },
  "MONITORING-EXTERNAL": {
    legacyName: "Supervision externe", name: "Suivi de vos services hébergés ailleurs",
    description: "Contrôles du fonctionnement de services confiés à un autre fournisseur.",
  },
  "NAS-MONITORING": {
    legacyName: "Supervision NAS", name: "Suivi de votre espace de stockage",
    description: "Contrôles du fonctionnement de votre équipement de stockage.",
  },
  "BACKUP-EXTERNAL-MANAGED": {
    legacyName: "Sauvegarde externe managée", name: "Copie de sécurité hors site",
    description: "Une copie de sécurité conservée dans un autre lieu.",
  },
  "LINUX-PATCH-MANAGED": {
    legacyName: "Maintenance Linux", name: "Mises à jour de votre serveur",
    description: "Suivi des mises à jour du serveur qui héberge vos outils.",
  },
  "NEXTCLOUD-EXTERNAL-MAINT": {
    legacyName: "Maintenance Nextcloud externe", name: "Entretien de votre espace de partage",
    description: "Mises à jour et suivi de votre espace de fichiers partagé.",
  },
  "VPS-EXTERNAL-MANAGED": {
    legacyName: "Infogérance VPS externe", name: "Suivi d'un serveur hébergé ailleurs",
    description: "Entretien d'un serveur confié à un autre hébergeur.",
  },
  "VPS-MANAGED-ADDON": {
    legacyName: "Infogérance VPS Zachary IT", name: "Suivi de votre serveur Zachary IT",
    description: "Entretien de votre serveur hébergé par Zachary IT.",
  },
};

function projectService(
  service: BillingV2PublicService,
  catalog: BillingV2PublicCatalog,
  displayOrder: number,
): PublicCommercialService {
  const tierProjections = service.tiers.map((tier) => projectTier(service.code, tier, catalog.currency));
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
  // `direct` reste nécessaire, mais le catalogue API doit également déclarer
  // l'item réellement compatible avec le Cart (prix actif, scope et policy de
  // configuration). Un code de service ou le prix affiché ne suffisent pas.
  const cartDirectEligible = service.cartDirectEligible === true;
  const directAction = cartDirectEligible
    ? { label: "Ajouter au panier", href: `#ajouter-${slugFromCode(service.code)}` }
    : null;
  const ordering = resolvePublicCommercialOrdering({
    requestedMode: service.publicOrderingMode,
    legacyMode: resolveLegacyOrderingMode(service, preferredOfferAction),
    offerCount,
    preferredOfferCta: preferredOfferAction,
    directCta: directAction,
  });
  const orderingMode = ordering.orderingMode;
  const directlyOrderable = orderingMode === "direct" && cartDirectEligible;
  const requiresQuote = orderingMode === "quote";
  const priceType = resolvePriceType(startingPrice, tierProjections);
  const fallback = LEGACY_SERVICE_PRESENTATION[service.code];
  const name = fallback?.legacyName === service.name ? fallback.name : service.name;
  const originalDescription = service.description?.trim() ?? "";
  const description = originalDescription && originalDescription !== fallback?.legacyDescription
    ? originalDescription : (fallback?.description ?? originalDescription) || null;
  const category = presentCategory(service.category);

  return {
    id: service.code,
    slug: publicUrl ? publicUrl.slice("/services/".length) : slugFromCode(service.code),
    name,
    tierSelectorLabel: service.tierSelectorLabel,
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
  serviceCode: string,
  tier: BillingV2PublicTier,
  fallbackCurrency: string,
): PublicCommercialTier {
  const recurringPrice = tier.monthlyAmountCents > 0
    ? money(tier.monthlyAmountCents, currencyOf(tier.priceComponents, fallbackCurrency))
    : null;
  return {
    id: tier.code,
    label: resolveServicePublicDetail(serviceCode, tier.label) ?? tier.label,
    description: tier.description,
    selectable: tier.publicSelectable,
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
