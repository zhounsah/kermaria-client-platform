import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import ts from "typescript";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

async function importPureTypeScript(source, label) {
  const transpiled = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.ES2022,
      target: ts.ScriptTarget.ES2022,
    },
    fileName: label,
    reportDiagnostics: true,
  });
  const errors = (transpiled.diagnostics ?? []).filter(
    (diagnostic) => diagnostic.category === ts.DiagnosticCategory.Error,
  );
  assert.deepEqual(errors, [], `${label} doit etre transpile sans erreur.`);

  const encoded = Buffer.from(transpiled.outputText).toString("base64");
  return import(`data:text/javascript;base64,${encoded}`);
}

async function importConfiguratorRuntime(source) {
  const publicPacksStub = String.raw`
const PACK_KEYS = new Set([
  "pack-dossier-securise",
  "pack-acces-distance",
  "pack-bureau-windows-distance",
  "pack-pro-association",
]);

export function normalizePublicPackKey(value) {
  const normalized = typeof value === "string" ? value.trim() : "";
  return PACK_KEYS.has(normalized) ? normalized : null;
}

export function normalizeCommitmentMonths(value) {
  const normalized = typeof value === "number" ? value : Number(String(value).trim());
  return [1, 6, 12].includes(normalized) && Number.isInteger(normalized)
    ? normalized
    : null;
}

export function normalizePaymentMode(value, commitmentMonths) {
  const normalized = typeof value === "string" ? value.trim() : "";
  if (commitmentMonths === 1) {
    return normalized === "monthly" ? "monthly" : null;
  }
  return normalized === "monthly" || normalized === "upfront"
    ? normalized
    : null;
}
`;
  const stubUrl = `data:text/javascript;base64,${Buffer.from(
    publicPacksStub,
  ).toString("base64")}`;
  return importPureTypeScript(
    source.replaceAll('"@/lib/public-packs"', JSON.stringify(stubUrl)),
    "public-configurator.ts",
  );
}

const billingV2FormulesStub = String.raw`
export const SERVICE_CODES={storagePersonal:"STORAGE-PERSONAL",storageShared:"STORAGE-SHARED",backupPersonal:"BACKUP-PERSONAL",backupShared:"BACKUP-SHARED",vpn:"VPN-ACCESS",remoteDesktop:"RDS-ACCESS",additionalUser:"USER-ADDITIONAL",supportPlus:"SUPPORT-PLUS"};
export function selectableTiers(c,s){return c.services.find(x=>x.code===s)?.tiers.filter(x=>x.publicSelectable)??[]}
export function buildBaselineSelection(p,c){const i=s=>p.items.find(x=>x.serviceCode===s);return{presetCode:p.code,commitmentCode:c,paymentMode:"monthly",storagePersonalTierCode:i(SERVICE_CODES.storagePersonal)?.tierCode??"32",backupPersonal:i(SERVICE_CODES.backupPersonal)!==undefined,storageSharedTierCode:i(SERVICE_CODES.storageShared)?.tierCode??null,backupShared:i(SERVICE_CODES.backupShared)!==undefined,vpnTierCode:i(SERVICE_CODES.vpn)?.tierCode??null,remoteDesktop:i(SERVICE_CODES.remoteDesktop)!==undefined,additionalUsers:i(SERVICE_CODES.additionalUser)?.quantity??0,supportPlus:i(SERVICE_CODES.supportPlus)!==undefined}}
export function findService(c,s){return c.services.find(x=>x.code===s)}
export function resolveTierLabel(s,c){return !s||!c?null:(s.tiers.find(x=>x.code===c)?.label??c)}
`;
const billingV2FormulesStubUrl=`data:text/javascript;base64,${Buffer.from(billingV2FormulesStub).toString("base64")}`;
const billingV2SelectionStubUrl=`data:text/javascript;base64,${Buffer.from("export const MAX_ADDITIONAL_USERS=10;").toString("base64")}`;
async function importBillingV2Runtime(source,label){return importPureTypeScript(source.replaceAll('"@/lib/billing-v2-formules"',JSON.stringify(billingV2FormulesStubUrl)).replaceAll('"@/lib/billing-v2-selection"',JSON.stringify(billingV2SelectionStubUrl)),label)}

const sharedTypes = await read("../../packages/shared/src/index.ts");
const diagnosticEngine = await read("lib/public-diagnostic.ts");
const diagnosticBeforeAfter = await read("lib/diagnostic-before-after.ts");
const configuratorEngine = await read("lib/public-configurator.ts");
const configuratorServer = await read("lib/catalog-configuration-server.ts");
const configuratorRoute = await read("app/api/configurer/resolve/route.ts");
const diagnosticPage = await read("app/diagnostic/page.tsx");
const configurerPage = await read("app/configurer/page.tsx");
const diagnosticWizard = await read("components/PublicDiagnosticWizard.tsx");
const configuratorComponent = await read("components/PublicConfigurator.tsx");
const publicShell = await read("components/PublicShell.tsx");
const publicRoutes = await read("lib/public-route-config.ts");
const sitemap = await read("app/sitemap.ts");
const signupPage = await read("app/signup/page.tsx");
const signupRoute = await read("app/api/signup/route.ts");
const programCs = await read("../../apps/api-internal/Program.cs");
const catalogConfigurationService = await read(
  "../../apps/api-internal/Services/CatalogConfigurationService.cs",
);
const fiscalPolicy = await read("../../apps/api-internal/Services/FiscalPolicy.cs");
const catalogConfigurationContracts = await read(
  "../../apps/api-internal/Contracts/CatalogConfigurationContracts.cs",
);
const signupContracts = await read(
  "../../apps/api-internal/Contracts/SignupContracts.cs",
);

const diagnosticRuntime = await importBillingV2Runtime(
  diagnosticEngine,
  "public-diagnostic.ts",
);
const diagnosticBeforeAfterRuntime = await importBillingV2Runtime(
  diagnosticBeforeAfter,
  "diagnostic-before-after.ts",
);
const configuratorRuntime = await importConfiguratorRuntime(configuratorEngine);

const catalog = {
  source: "test",
  currency: "EUR",
  commitments: [{
    code: "FLEX",
    name: "Sans engagement",
    months: 1,
    paymentOptions: [{ paymentMode: "monthly", discountBasisPoints: 0 }],
  }],
  checkoutRoutes: [],
  services: [
    {
      code: "STORAGE-PERSONAL",
      name: "Stockage personnel",
      category: "Stockage",
      scopeType: "user",
      flatMonthlyAmountCents: null,
      discountEligible: true,
      tiers: [16, 32, 64, 128, 256].map((value) => ({
        code: String(value),
        label: `${value} Go`,
        description: null,
        numericValue: value,
        monthlyAmountCents: 0,
        publicSelectable: true,
      })),
    },
    {
      code: "VPN-ACCESS",
      name: "Acces VPN",
      category: "Acces",
      scopeType: "user",
      flatMonthlyAmountCents: null,
      discountEligible: true,
      tiers: [
        { code: "ESSENTIAL", label: "VPN Essentiel", numericValue: 100 },
        { code: "PLUS", label: "VPN Plus", numericValue: 250 },
      ].map((tier) => ({
        ...tier,
        description: null,
        monthlyAmountCents: 0,
        publicSelectable: true,
      })),
    },
  ],
  presets: [
    {
      code: "pack-dossier-securise",
      name: "Dossier securise",
      description: "Dossier",
      displayOrder: 10,
      baselineMonthlyAmountCents: 0,
      items: [
        { serviceCode: "STORAGE-PERSONAL", tierCode: "32", quantity: 1 },
        { serviceCode: "BACKUP-PERSONAL", tierCode: "32", quantity: 1 },
      ],
    },
    {
      code: "pack-acces-distance",
      name: "Acces securise",
      description: "Acces",
      displayOrder: 20,
      baselineMonthlyAmountCents: 0,
      items: [
        { serviceCode: "STORAGE-PERSONAL", tierCode: "32", quantity: 1 },
        { serviceCode: "BACKUP-PERSONAL", tierCode: "32", quantity: 1 },
        { serviceCode: "VPN-ACCESS", tierCode: "ESSENTIAL", quantity: 1 },
      ],
    },
    {
      code: "pack-bureau-windows-distance",
      name: "Bureau a distance",
      description: "Windows",
      displayOrder: 30,
      baselineMonthlyAmountCents: 0,
      items: [
        { serviceCode: "STORAGE-PERSONAL", tierCode: "64", quantity: 1 },
        { serviceCode: "BACKUP-PERSONAL", tierCode: "64", quantity: 1 },
        { serviceCode: "VPN-ACCESS", tierCode: "PLUS", quantity: 1 },
        { serviceCode: "RDS-ACCESS", tierCode: null, quantity: 1 },
      ],
    },
    {
      code: "pack-pro-association",
      name: "Pro / Association",
      description: "Pro",
      displayOrder: 40,
      baselineMonthlyAmountCents: 0,
      items: [
        { serviceCode: "STORAGE-PERSONAL", tierCode: "64", quantity: 1 },
        { serviceCode: "BACKUP-PERSONAL", tierCode: "64", quantity: 1 },
        { serviceCode: "VPN-ACCESS", tierCode: "PLUS", quantity: 1 },
        { serviceCode: "STORAGE-SHARED", tierCode: "128", quantity: 1 },
        { serviceCode: "BACKUP-SHARED", tierCode: "128", quantity: 1 },
        { serviceCode: "USER-ADDITIONAL", tierCode: null, quantity: 1 },
        { serviceCode: "SUPPORT-PLUS", tierCode: null, quantity: 1 },
      ],
    },
  ],
};

function baseAnswers(overrides = {}) {
  return {
    customerType: "individual",
    users: 1,
    dataKinds: ["personal_documents"],
    estimatedStorageGb: 16,
    needsRemoteFiles: true,
    needsVpn: false,
    needsWindowsDesktop: false,
    recoveryImportance: "normal",
    backupFrequency: "daily",
    restoreTestRecency: "less_than_3_months",
    continuityPlan: "yes",
    ...overrides,
  };
}

function recommendation(overrides = {}) {
  return diagnosticRuntime.recommendOffer(baseAnswers(overrides), catalog);
}

assert.equal(
  recommendation({ needsRemoteFiles: false }).selection?.presetCode,
  "pack-dossier-securise",
  "Sauvegarde simple -> preset Dossier securise V2.",
);
assert.equal(
  recommendation({ needsVpn: true }).selection?.presetCode,
  "pack-acces-distance",
  "Besoin VPN -> preset Acces securise V2.",
);
assert.equal(
  recommendation({ needsWindowsDesktop: true }).selection?.presetCode,
  "pack-bureau-windows-distance",
  "Bureau Windows -> preset Bureau a distance V2.",
);
assert.equal(
  recommendation({ needsWindowsDesktop: true }).selection?.remoteDesktop,
  true,
  "Le diagnostic doit produire le composant RDS V2.",
);
assert.equal(
  recommendation({ customerType: "association", users: 2 }).selection?.presetCode,
  "pack-pro-association",
  "Association -> preset Pro / Association V2.",
);
assert.equal(
  recommendation({ estimatedStorageGb: 64 }).selection?.storagePersonalTierCode,
  "64",
  "64 Go reste souscriptible dans la selection V2.",
);
assert.equal(recommendation({ estimatedStorageGb: 128 }).status, "standard");
assert.equal(
  recommendation({ estimatedStorageGb: 128 }).selection?.storagePersonalTierCode,
  "128",
);
assert.equal(recommendation({ estimatedStorageGb: 256 }).status, "standard");
assert.equal(
  recommendation({ estimatedStorageGb: 256 }).selection?.storagePersonalTierCode,
  "256",
);
assert.equal(
  recommendation({ estimatedStorageGb: "above_public_max" }).status,
  "requires_quote",
);
assert.ok(
  recommendation({ estimatedStorageGb: "above_public_max" }).warnings.includes(
    "storage_requires_quote",
  ),
);
assert.equal(recommendation({ users: 5 }).selection?.additionalUsers, 4);
assert.equal(recommendation({ users: 11 }).selection?.additionalUsers, 10);
assert.equal(recommendation({ users: 12 }).status, "requires_quote");
assert.ok(recommendation({ users: 12 }).warnings.includes("users_require_quote"));
assert.equal(
  recommendation({ needsWindowsDesktop: true, estimatedStorageGb: 128 }).status,
  "standard",
  "RDS + 128 Go est representable par Billing V2.",
);
const proWindows = recommendation({
  customerType: "business",
  users: 5,
  needsWindowsDesktop: true,
});
assert.equal(proWindows.selection?.presetCode, "pack-pro-association");
assert.equal(proWindows.selection?.remoteDesktop, true);
assert.equal(proWindows.selection?.additionalUsers, 4);
assert.deepEqual(
  recommendation({ estimatedStorageGb: null }).warnings,
  ["storage_unknown"],
  "Stockage inconnu avertit sans bloquer.",
);
assert.ok(
  recommendation({ backupFrequency: "unknown" }).warnings.includes(
    "backup_frequency_unknown",
  ),
);
assert.equal(recommendation({ customerType: "other" }).status, "requires_quote");

const beforeAfterUnknownBackup =
  diagnosticBeforeAfterRuntime.buildDiagnosticBeforeAfterSummary({
    answers: baseAnswers({
      backupFrequency: "unknown",
      restoreTestRecency: "never",
      continuityPlan: "unknown",
      estimatedStorageGb: null,
    }),
    recommendation: recommendation({
      backupFrequency: "unknown",
      restoreTestRecency: "never",
      continuityPlan: "unknown",
      estimatedStorageGb: null,
    }),
    catalog,
  });
assert.ok(
  beforeAfterUnknownBackup.items.some((item) =>
    item.after.includes("stockage personnel")
  ),
  "Le bloc Avant / Apres doit exploiter la selection V2.",
);
assert.ok(
  beforeAfterUnknownBackup.items.some((item) => item.before.includes("Volume")),
  "Le bloc Avant / Apres reste utile quand le volume est inconnu.",
);

const beforeAfterWindows =
  diagnosticBeforeAfterRuntime.buildDiagnosticBeforeAfterSummary({
    answers: baseAnswers({ needsWindowsDesktop: true, needsVpn: false }),
    recommendation: recommendation({ needsWindowsDesktop: true, needsVpn: false }),
    catalog,
  });
assert.ok(
  beforeAfterWindows.items.some(
    (item) =>
      item.before.includes("Bureau Windows")
      && item.after.includes("Bureau Windows accessible"),
  ),
  "Le bloc Avant / Apres doit montrer le changement Bureau Windows.",
);

const storageQuote = recommendation({ estimatedStorageGb: "above_public_max" });
const beforeAfterQuote =
  diagnosticBeforeAfterRuntime.buildDiagnosticBeforeAfterSummary({
    answers: baseAnswers({ estimatedStorageGb: "above_public_max" }),
    recommendation: storageQuote,
    catalog,
  });
assert.equal(beforeAfterQuote.title, "Avant cadrage");
assert.ok(
  beforeAfterQuote.items.some((item) => item.after.includes("valid")),
  "Le bloc Avant / Apres ne doit pas promettre une activation standard quand un cadrage est requis.",
);

assert.deepEqual(
  configuratorRuntime.configurationFromSearchParams(
    new URLSearchParams(
      "pack=pack-acces-distance&users=1&storage=32&vpn=yes&windows=no",
    ),
  ),
  {
    packKey: "pack-acces-distance",
    commitmentMonths: 1,
    paymentMode: "monthly",
    users: 1,
    storageGb: 32,
    needsVpn: true,
    needsWindowsDesktop: false,
  },
  "Le configurateur doit restaurer une URL partageable sans prix.",
);
for (const query of [
  "pack=pack-acces-distance&pack=pack-dossier-securise",
  "pack=pack-acces-distance&users=99",
  "pack=pack-acces-distance&storage=12",
  "pack=pack-inconnu",
  "pack=pack-acces-distance&vpn=maybe",
]) {
  assert.equal(
    configuratorRuntime.configurationFromSearchParams(new URLSearchParams(query)),
    null,
    `La configuration invalide doit etre rejetee: ${query}`,
  );
}
const restoredWithUnknownOption =
  configuratorRuntime.configurationFromSearchParams(
    new URLSearchParams("pack=pack-dossier-securise&options=FAKE_PRICE"),
  );
assert.equal(
  restoredWithUnknownOption?.packKey,
  "pack-dossier-securise",
  "Les options inconnues dans l'URL doivent etre ignorees.",
);
const queryString = configuratorRuntime.configurationToQueryString({
  packKey: "pack-acces-distance",
  commitmentMonths: 6,
  paymentMode: "upfront",
  users: 1,
  storageGb: 32,
  needsVpn: true,
  needsWindowsDesktop: false,
});
assert.doesNotMatch(queryString, /price|email|name|phone|amount/i);

assert.match(sharedTypes, /interface PublicPackCapabilities/);
assert.match(sharedTypes, /capabilities:\s*PublicPackCapabilities/);
assert.doesNotMatch(
  sharedTypes.match(/export const PUBLIC_PACKS[\s\S]*?] as const/)?.[0] ?? "",
  /priceAmountCents|setupFeeAmountCents|monthlyPrice|billingPrice/,
  "packages/shared ne doit pas dupliquer les prix commerciaux des packs.",
);
assert.match(
  sharedTypes,
  /interface DiagnosticRecommendation[\s\S]*selection:\s*BillingV2PublicSelection \| null/,
);

assert.match(diagnosticEngine, /export function recommendOffer/);
assert.match(diagnosticEngine, /BillingV2PublicSelection/);
assert.match(diagnosticEngine, /selectableTiers/);
assert.match(diagnosticEngine, /MAX_ADDITIONAL_USERS/);
assert.doesNotMatch(diagnosticEngine, /ResolvedPublicPackManifest|CatalogConfigurationInput/);
assert.doesNotMatch(
  diagnosticEngine,
  /AmountCents|monthlyAmountCents|setupFeeAmountCents|formatCurrencyFromCents/,
  "Le moteur du diagnostic ne doit calculer ou lire aucun prix.",
);

assert.match(diagnosticWizard, /REASON_LABELS/);
assert.match(diagnosticWizard, /WARNING_MESSAGES/);
assert.match(diagnosticWizard, /buildDiagnosticBeforeAfterSummary/);
assert.match(diagnosticWizard, /backup_frequency_unknown/);
assert.match(diagnosticWizard, /fetch\("\/api\/formules\/devis"/);
assert.match(diagnosticWizard, /billingV2SelectionToSearchParams/);
assert.match(diagnosticWizard, /`\/formules\/\$\{selection\.presetCode\}\?/);
assert.match(diagnosticWizard, /params\.set\("source", "diagnostic"\)/);
assert.doesNotMatch(diagnosticWizard, /configurationToQueryString|\/configurer\?/);
assert.match(diagnosticWizard, /<option value="256">Jusqu&apos;à 256 Go<\/option>/);
assert.match(diagnosticWizard, /<option value="above_public_max">Plus de 256 Go<\/option>/);
assert.match(diagnosticWizard, /Array\.from\(\{ length: 11 \}/);
assert.match(diagnosticWizard, /<option value="12">12 ou plus<\/option>/);
assert.match(diagnosticWizard, /Personnaliser cette configuration/);
assert.doesNotMatch(diagnosticWizard, /toIncVat|vatRate|0\.2|20\s*\/\s*100/);

assert.match(diagnosticBeforeAfter, /BillingV2PublicSelection/);
assert.match(diagnosticBeforeAfter, /items\.slice\(0, 5\)/);
assert.doesNotMatch(
  diagnosticBeforeAfter,
  /ResolvedPublicPackManifest|supportsVpn|supportsWindowsDesktop|findPackText/,
);

assert.match(diagnosticPage, /getBillingV2FormulesCatalog/);
assert.doesNotMatch(
  diagnosticPage,
  /getPublicCommercialCatalog|getPublicPackCatalogContent|resolvePackCatalog/,
);
assert.match(diagnosticPage, /<PublicDiagnosticWizard catalog=\{catalog\} \/>/);
assert.match(diagnosticPage, /buildPublicMetadata\(/);
assert.match(diagnosticPage, /path:\s*"\/diagnostic"/);
assert.match(diagnosticPage, /Diagnostic sauvegarde et accès distant/);
assert.match(diagnosticPage, /Vos données importantes pourraient-elles disparaître demain/);
assert.match(diagnosticPage, /Sans inscription/);
assert.match(diagnosticPage, /Aucun compte ni achat nécessaire/);
assert.doesNotMatch(diagnosticPage, /Sans engagement|Vos coordonnées servent/);
assert.match(publicShell, /publicHref\("\/diagnostic"\)/);
assert.match(publicRoutes, /"\/diagnostic"/);
assert.match(sitemap, /path:\s*"\/diagnostic"/);

assert.match(configurerPage, /robots:\s*\{\s*index:\s*false,\s*follow:\s*true\s*\}/);
assert.match(configurerPage, /buildPublicMetadata\(/);
assert.match(configurerPage, /path:\s*"\/configurer"/);
assert.match(configurerPage, /Personnalisez votre offre selon vos besoins/);
assert.match(configurerPage, /Retour au diagnostic/);
assert.match(configuratorComponent, /fetch\("\/api\/configurer\/resolve"/);
assert.match(configuratorComponent, /requires_different_offer/);
assert.match(configuratorComponent, /requires_quote/);
assert.match(configuratorComponent, /Besoin de stockage estimé/);
assert.match(configuratorComponent, /Inclus/);
assert.match(configuratorComponent, /windows_storage_not_standard/);
assert.match(configuratorComponent, /Continuer avec cette configuration/);
assert.match(configuratorComponent, /configurationToQueryString\(resolution\.resolvedConfiguration\)/);
assert.match(configuratorComponent, /Votre estimation/);
assert.match(configuratorComponent, /Total initial estimé/);
assert.match(configuratorComponent, /configurator-static-value/);
assert.doesNotMatch(configuratorComponent, /recalculé côté serveur|Tarif recalculé serveur|vendables dans le catalogue|catalogue commercial courant/);
assert.match(configuratorRoute, /normalizeCatalogConfigurationInput/);
assert.match(configuratorRoute, /resolveCatalogConfiguration/);
assert.match(configuratorServer, /import "server-only"/);
assert.match(configuratorServer, /\/internal\/portal\/configuration\/resolve/);
assert.match(configuratorServer, /getInternalServiceHeaders/);
assert.doesNotMatch(configuratorServer, /PriceAmountCents|monthlyPrice/i);

assert.match(programCs, /ICatalogConfigurationService/);
assert.match(programCs, /IFiscalPolicy/);
assert.match(programCs, /\/internal\/portal\/configuration\/resolve/);
assert.match(fiscalPolicy, /FiscalRegimes\.FranchiseBase/);
assert.match(fiscalPolicy, /FiscalRegimes\.Standard/);
assert.match(fiscalPolicy, /TVA non applicable/);
assert.match(catalogConfigurationContracts, /FiscalRegime/);
assert.match(catalogConfigurationContracts, /FiscalMention/);
assert.match(signupContracts, /FiscalRegime/);
assert.match(signupContracts, /FiscalMention/);
assert.match(catalogConfigurationService, /GetClientCatalogAsync/);
assert.match(catalogConfigurationService, /IFiscalPolicy/);
assert.match(catalogConfigurationService, /AmountIncludingTax/);
assert.doesNotMatch(catalogConfigurationService, /\/\s*10000\s*\+\s*1|ToIncVat/);
assert.match(catalogConfigurationService, /StatusRequiresDifferentOffer/);
assert.match(catalogConfigurationService, /StatusRequiresQuote/);
assert.match(catalogConfigurationService, /windows_storage_not_standard/);
assert.doesNotMatch(
  catalogConfigurationService,
  /pack-acces-distance[\s\S]{0,80}1900|pack-bureau-windows-distance[\s\S]{0,80}3500|pack-pro-association[\s\S]{0,80}4900/,
  "Le resolver API ne doit pas hardcoder de tarifs de packs.",
);

assert.match(signupPage, /resolveCatalogConfiguration\(catalogConfiguration\)/);
assert.match(signupPage, /initialCatalogConfiguration=\{resolvedConfiguration\}/);
assert.match(signupRoute, /packSelection:\s*catalogConfiguration \? null : packSelection/);
assert.match(signupRoute, /catalogConfiguration,/);

console.log("Verification diagnostic/configurateur WEBPORTAL reussie.");
