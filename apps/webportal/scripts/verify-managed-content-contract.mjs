import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const sharedTypes = await read("../../packages/shared/src/index.ts");
const internalApi = await read("lib/internal-api.ts");
const payloads = await read("lib/bff-payloads.ts");
const publicRouteConfig = await read("lib/public-route-config.ts");
const managedMarkdown = await read("components/ManagedMarkdown.tsx");
const publicManagedContentArticle = await read("components/PublicManagedContentArticle.tsx");
const publicPackCard = await read("components/PublicPackCard.tsx");
const comparisonTable = await read("components/PublicPackComparisonTable.tsx");
const adminNavigation = await read("components/AdminNavigation.tsx");
const adminContentPage = await read("app/admin/content/page.tsx");
const adminContentDetailPage = await read("app/admin/content/[key]/page.tsx");
const adminStorefrontContentForm = await read("components/AdminStorefrontContentForm.tsx");
const publicStorefrontPage = await read("components/PublicStorefrontPage.tsx");
const storefrontContent = await read("lib/storefront-content.ts");
const servicesPage = await read("app/services/page.tsx");
const serviceDetailPage = await read("app/services/[category]/page.tsx");
const tarifsPage = await read("app/tarifs/page.tsx");
const adminPackCatalogPage = await read("app/admin/public-pack-catalog/page.tsx");
const cgvPage = await read("app/cgv/page.tsx");
const privacyPage = await read("app/politique-confidentialite/page.tsx");
const mentionsPage = await read("app/mentions-legales/page.tsx");
const aProposPage = await read("app/a-propos/page.tsx");
const infrastructurePage = await read("app/infrastructure/page.tsx");
const packSheetPage = await read("app/offres/[slug]/page.tsx");
const adminContentRoute = await read("app/api/admin/content/route.ts");
const adminContentDetailRoute = await read("app/api/admin/content/[key]/route.ts");
const {
  resolveStorefrontCommercialActions,
  resolveStorefrontServicesLandingActions,
  resolveStorefrontTariffAction,
  resolveStorefrontPublicCta,
  resolveStorefrontPublicRelatedLinks,
  storefrontServiceSelfServiceOrderable,
} = await import(new URL("../lib/storefront-content.ts", import.meta.url));

assert.match(sharedTypes, /type ManagedContentKey =/);
assert.match(sharedTypes, /type ManagedContentType =/);
assert.match(sharedTypes, /page:infrastructure/);
assert.match(sharedTypes, /interface ManagedContentSummary/);
assert.match(sharedTypes, /interface ManagedContentDetail/);
assert.match(sharedTypes, /interface ManagedContentPayload/);
assert.match(sharedTypes, /buildPackSheetContentKey/);
assert.match(sharedTypes, /getManagedContentRegistry/);
assert.match(sharedTypes, /StorefrontContentKey/);
assert.match(sharedTypes, /storefront:vpn-entreprise/);
assert.match(sharedTypes, /storefront:messagerie-professionnelle/);
assert.match(sharedTypes, /publicVisible: boolean/);
assert.match(sharedTypes, /selfServiceOrderable: boolean/);

assert.match(internalApi, /getPublicManagedContent/);
assert.match(internalApi, /getAdminManagedContentList/);
assert.match(internalApi, /getAdminManagedContent\(/);
assert.match(internalApi, /\/internal\/portal\/content\//);
assert.match(internalApi, /\/internal\/admin\/content/);

assert.match(payloads, /parseManagedContentPayload/);
assert.match(publicRouteConfig, /PUBLIC_ROUTES/);
assert.match(publicRouteConfig, /PORTFOLIO_URL/);
assert.match(publicRouteConfig, /isPublicRoute/);

assert.match(adminContentRoute, /handleAdminGet<ManagedContentSummary\[]>/);
assert.match(adminContentDetailRoute, /handleAdminGet<ManagedContentDetail>/);
assert.match(adminContentDetailRoute, /handleAdminMutation/);
assert.match(adminContentDetailRoute, /isManagedContentKey/);
assert.match(adminContentDetailRoute, /decodeURIComponent/);

assert.match(adminContentPage, /await requireAdminSession\(\)/);
assert.match(adminContentPage, /target="_blank"/);
assert.match(adminContentDetailPage, /await requireAdminSession\(\)/);
assert.match(adminContentDetailPage, /decodeURIComponent/);
assert.match(adminContentDetailPage, /target="_blank"/);
assert.match(adminContentDetailPage, /AdminStorefrontContentForm/);
assert.match(adminStorefrontContentForm, /Titre commercial \/ H1/);
assert.match(adminStorefrontContentForm, /Title SEO/);
assert.match(adminStorefrontContentForm, /Meta description/);
assert.match(adminStorefrontContentForm, /selfServiceOrderable === false/);
assert.match(adminStorefrontContentForm, /isStorefrontSelfServiceCta/);
assert.match(adminStorefrontContentForm, /href !== "\/formules"/);
assert.doesNotMatch(adminStorefrontContentForm, /Contenu JSON/);
assert.match(publicStorefrontPage, /Questions fréquentes/);
assert.match(publicStorefrontPage, /ManagedMarkdown/);
assert.match(publicStorefrontPage, /resolveStorefrontPublicCta/);
assert.match(publicStorefrontPage, /resolveStorefrontPublicRelatedLinks/);
assert.match(publicStorefrontPage, /StorefrontCommercialActions/);
assert.match(publicStorefrontPage, /secondaryAction/);
assert.match(storefrontContent, /STOREFRONT_COMMERCIAL_ROUTES/);
assert.match(storefrontContent, /pack-acces-distance/);
assert.match(storefrontContent, /STOREFRONT_TARIFF_PRESET_BY_SERVICE_CODE/);
assert.match(storefrontContent, /STOREFRONT_SERVICE_SLUGS/);
assert.match(storefrontContent, /"vpn-entreprise": \["VPN-ACCESS"\]/);
assert.match(servicesPage, /storefront:services/);
assert.match(serviceDetailPage, /storefrontContentKeyForServiceSlug/);
assert.match(serviceDetailPage, /getBillingV2FormulesCatalog/);
assert.match(serviceDetailPage, /storefrontServiceSelfServiceOrderable/);
assert.match(serviceDetailPage, /selfServiceOrderable=/);
assert.match(serviceDetailPage, /resolveStorefrontCommercialActions/);
assert.match(serviceDetailPage, /commercialActions=/);
assert.match(servicesPage, /resolveStorefrontServicesLandingActions/);
assert.match(tarifsPage, /resolveStorefrontTariffAction/);
assert.match(tarifsPage, /serviceCode/);
assert.match(tarifsPage, /storefront:tarifs/);
assert.match(adminNavigation, /\/admin\/content/);
assert.match(
  adminPackCatalogPage,
  /Modifier la fiche technique/,
  "La page admin de vitrine packs doit proposer un lien rapide vers les fiches techniques.",
);

assert.match(cgvPage, /getPublicManagedContent\("legal:cgv"\)/);
assert.match(
  privacyPage,
  /getPublicManagedContent\("legal:politique-confidentialite"\)/,
);
assert.match(mentionsPage, /getPublicManagedContent\("legal:mentions-legales"\)/);
assert.match(aProposPage, /getPublicManagedContent\("page:a-propos"\)/);
assert.match(infrastructurePage, /getPublicManagedContent\("page:infrastructure"\)/);
assert.doesNotMatch(cgvPage, /placeholder/i);
assert.doesNotMatch(privacyPage, /placeholder/i);
assert.doesNotMatch(mentionsPage, /placeholder/i);
assert.doesNotMatch(aProposPage, /placeholder/i);
assert.doesNotMatch(infrastructurePage, /placeholder/i);

assert.match(packSheetPage, /buildPackSheetContentKey/);
assert.match(packSheetPage, /getPublicManagedContent/);
assert.match(packSheetPage, /ManagedMarkdown/);
assert.match(packSheetPage, /Composants techniques liés/);

assert.match(publicPackCard, /Voir la fiche technique/);
assert.match(comparisonTable, /Voir la fiche technique/);

assert.match(managedMarkdown, /ReactMarkdown/);
assert.doesNotMatch(managedMarkdown, /dangerouslySetInnerHTML/);
assert.doesNotMatch(managedMarkdown, /rehypeRaw|rehype-raw/);
assert.match(
  publicManagedContentArticle,
  /source === "api-internal-persistent" \? null/,
  "Les pages CMS publiques ne doivent pas afficher le bandeau technique lorsque la source persistante fonctionne.",
);

const nonSelfServiceCatalog = {
  source: "database",
  currency: "EUR",
  presets: [],
  services: [{
    code: "LINUX-PATCH-MANAGED",
    name: "Maintenance Linux",
    category: "Infogérance",
    scopeType: "subscription",
    flatMonthlyAmountCents: 1490,
    tiers: [],
    discountEligible: true,
    publicVisible: true,
    selfServiceOrderable: false,
  }],
  commitments: [],
};

const commercialCatalog = {
  source: "database",
  currency: "EUR",
  services: [],
  commitments: [],
  presets: [
    { code: "pack-dossier-securise", items: [{ serviceCode: "BACKUP-PERSONAL" }] },
    { code: "pack-acces-distance", items: [{ serviceCode: "VPN-ACCESS" }] },
    { code: "pack-bureau-windows-distance", items: [{ serviceCode: "RDS-ACCESS" }] },
  ],
};

assert.equal(
  storefrontServiceSelfServiceOrderable("maintenance-linux", nonSelfServiceCatalog),
  false,
  "public_visible=true + self_service_orderable=false doit rester non self-service.",
);
assert.deepEqual(
  resolveStorefrontPublicCta({ ctaLabel: "Commander", ctaHref: "/formules" }, false),
  { label: "Demander un devis", href: "/contact" },
  "Un CTA CMS /formules doit être remplacé pour un service non self-service.",
);
assert.deepEqual(
  resolveStorefrontPublicCta({ ctaLabel: "Configurer", ctaHref: "/diagnostic" }, false),
  { label: "Demander un audit", href: "/diagnostic" },
  "Un libellé Configurer doit être neutralisé même avec une destination non self-service.",
);
assert.deepEqual(
  resolveStorefrontPublicRelatedLinks([
    { label: "Configurer", href: "/formules" },
    { label: "Contact", href: "/contact" },
  ], false),
  [{ label: "Contact", href: "/contact" }],
  "A non-self-service service must remove self-service related links.",
);

assert.deepEqual(
  resolveStorefrontPublicCta({ ctaLabel: "Demander un devis", ctaHref: "/contact" }, false),
  { label: "Demander un devis", href: "/contact" },
  "Un CTA commercial sûr doit rester intact.",
);


const auditCta = { ctaLabel: "Demander un audit", ctaHref: "/diagnostic" };
const vpnActions = resolveStorefrontCommercialActions("vpn-entreprise", commercialCatalog, auditCta);
assert.equal(vpnActions.mode, "FORMULA");
assert.equal(vpnActions.primaryAction.href, "/formules/pack-acces-distance");
assert.equal(vpnActions.secondaryAction?.href, "/diagnostic");
assert.equal(vpnActions.presetCode, "pack-acces-distance");

const rdsActions = resolveStorefrontCommercialActions("bureau-windows-distance", commercialCatalog, auditCta);
assert.equal(rdsActions.mode, "FORMULA");
assert.equal(rdsActions.primaryAction.href, "/formules/pack-bureau-windows-distance");

const backupActions = resolveStorefrontCommercialActions("sauvegarde-externalisee", commercialCatalog, auditCta);
assert.equal(backupActions.mode, "HYBRID");
assert.equal(backupActions.primaryAction.href, "/formules/pack-dossier-securise");
assert.equal(backupActions.secondaryAction?.href, "/diagnostic");

const quoteActions = resolveStorefrontCommercialActions("vps", commercialCatalog, auditCta);
assert.deepEqual(quoteActions, {
  mode: "QUOTE",
  primaryAction: { label: "Demander un audit", href: "/diagnostic" },
  secondaryAction: null,
  presetCode: null,
});

const missingPresetActions = resolveStorefrontCommercialActions(
  "vpn-entreprise",
  { ...commercialCatalog, presets: [] },
  auditCta,
);
assert.equal(missingPresetActions.mode, "QUOTE");
assert.equal(missingPresetActions.primaryAction.href, "/diagnostic");

const inconsistentPresetActions = resolveStorefrontCommercialActions(
  "vpn-entreprise",
  { ...commercialCatalog, presets: [{ code: "pack-acces-distance", items: [] }] },
  auditCta,
);
assert.equal(inconsistentPresetActions.mode, "QUOTE");

assert.deepEqual(
  resolveStorefrontTariffAction("VPN-ACCESS", commercialCatalog),
  { label: "Voir la formule", href: "/formules/pack-acces-distance" },
);
assert.deepEqual(
  resolveStorefrontTariffAction(
    "VPN-ACCESS",
    { ...commercialCatalog, presets: [{ code: "pack-acces-distance", items: [] }] },
  ),
  { label: "Demander un devis", href: "/contact" },
);
assert.deepEqual(
  resolveStorefrontTariffAction("FIREWALL-MANAGED", commercialCatalog),
  { label: "Demander un devis", href: "/contact" },
);

const landingActions = resolveStorefrontServicesLandingActions(commercialCatalog, auditCta);
assert.equal(landingActions.mode, "HYBRID");
assert.equal(landingActions.primaryAction.href, "/diagnostic");
assert.equal(landingActions.secondaryAction?.href, "/formules");
assert.equal(
  resolveStorefrontServicesLandingActions({ ...commercialCatalog, presets: [] }, auditCta).mode,
  "QUOTE",
);

console.log("Vérification du contrat managed content V0.33 réussie.");
