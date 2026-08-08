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

const diagnosticRuntime = await importPureTypeScript(
  diagnosticEngine,
  "public-diagnostic.ts",
);
const diagnosticBeforeAfterRuntime = await importPureTypeScript(
  diagnosticBeforeAfter,
  "diagnostic-before-after.ts",
);
const configuratorRuntime = await importConfiguratorRuntime(configuratorEngine);

const catalog = [
  {
    key: "pack-dossier-securise",
    capabilities: {
      includedUsers: 1,
      includedStorageGb: 32,
      supportsRemoteFiles: true,
      supportsVpn: false,
      supportsWindowsDesktop: false,
      supportsBackup: true,
    },
  },
  {
    key: "pack-acces-distance",
    capabilities: {
      includedUsers: 1,
      includedStorageGb: 32,
      supportsRemoteFiles: true,
      supportsVpn: true,
      supportsWindowsDesktop: false,
      supportsBackup: true,
    },
  },
  {
    key: "pack-bureau-windows-distance",
    capabilities: {
      includedUsers: 1,
      includedStorageGb: 32,
      supportsRemoteFiles: true,
      supportsVpn: true,
      supportsWindowsDesktop: true,
      supportsBackup: true,
    },
  },
  {
    key: "pack-pro-association",
    capabilities: {
      includedUsers: 2,
      includedStorageGb: 64,
      supportsRemoteFiles: true,
      supportsVpn: true,
      supportsWindowsDesktop: false,
      supportsBackup: true,
    },
  },
];

function baseAnswers(overrides = {}) {
  return {
    customerType: "individual",
    users: 1,
    dataKinds: ["personal_documents"],
    estimatedStorageGb: 8,
    needsRemoteFiles: true,
    needsVpn: false,
    needsWindowsDesktop: false,
    recoveryImportance: "normal",
    backupFrequency: "daily",
    restoreTestRecency: "less_than_6_months",
    continuityPlan: "yes",
    ...overrides,
  };
}

function recommendation(overrides) {
  return diagnosticRuntime.recommendOffer(baseAnswers(overrides), catalog);
}

assert.equal(
  recommendation({ needsRemoteFiles: false }).offerId,
  "pack-dossier-securise",
  "Sauvegarde simple particulier -> Pack Dossier Securise.",
);
assert.equal(
  recommendation({ needsVpn: true }).offerId,
  "pack-acces-distance",
  "Besoin VPN -> Pack Acces a Distance.",
);
assert.equal(
  recommendation({ needsWindowsDesktop: true }).offerId,
  "pack-bureau-windows-distance",
  "Bureau Windows distant -> Pack Bureau Windows a Distance.",
);
assert.equal(
  recommendation({ needsWindowsDesktop: true }).configuration?.needsVpn,
  true,
  "Le diagnostic doit pre-remplir le VPN quand le pack Bureau Windows l'inclut.",
);
assert.equal(
  recommendation({
    customerType: "association",
    users: 2,
    dataKinds: ["association_data"],
  }).offerId,
  "pack-pro-association",
  "Association avec plusieurs utilisateurs -> Pack Pro / Association.",
);
assert.equal(
  recommendation({ estimatedStorageGb: 64 }).offerId,
  "pack-pro-association",
  "Stockage au-dessus du quota Dossier -> pack standard plus adapte.",
);
assert.deepEqual(
  recommendation({ estimatedStorageGb: null }).warnings,
  ["storage_unknown"],
  "Je ne sais pas sur le stockage doit avertir sans bloquer quand la frequence est connue.",
);
assert.ok(
  recommendation({ backupFrequency: "unknown" }).warnings.includes(
    "backup_frequency_unknown",
  ),
  "Je ne sais pas sur la frequence de sauvegarde doit ajouter un point a verifier.",
);
const windowsStorageQuote = recommendation({
  needsWindowsDesktop: true,
  estimatedStorageGb: 64,
});
assert.equal(windowsStorageQuote.status, "requires_quote");
assert.ok(
  windowsStorageQuote.warnings.includes("windows_storage_requires_quote"),
  "Bureau Windows 1 utilisateur avec volume superieur au standard doit cadrer le volume.",
);
assert.equal(
  windowsStorageQuote.warnings.includes("windows_team_requires_quote"),
  false,
  "Bureau Windows 1 utilisateur ne doit pas afficher un motif multi-utilisateurs.",
);
assert.equal(
  recommendation({ estimatedStorageGb: 128 }).status,
  "requires_quote",
  "Stockage hors standard -> cadrage.",
);
assert.equal(
  recommendation({ customerType: "other" }).status,
  "requires_quote",
  "Structure hors standard -> cadrage.",
);

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
    pack: {
      ...catalog[0],
      included: ["Sauvegardes quotidiennes"],
    },
  });
assert.ok(
  beforeAfterUnknownBackup.items.some(
    (item) =>
      item.before === "Fréquence de sauvegarde inconnue"
      && item.after === "Sauvegardes quotidiennes",
  ),
  "Le bloc Avant / Apres doit reprendre la frequence inconnue et la sauvegarde du catalogue.",
);
assert.ok(
  beforeAfterUnknownBackup.items.some(
    (item) => item.before === "Volume à protéger à confirmer",
  ),
  "Le bloc Avant / Apres doit rester utile quand le volume est inconnu.",
);

const beforeAfterWindows =
  diagnosticBeforeAfterRuntime.buildDiagnosticBeforeAfterSummary({
    answers: baseAnswers({
      needsWindowsDesktop: true,
      needsVpn: false,
    }),
    recommendation: recommendation({
      needsWindowsDesktop: true,
      needsVpn: false,
    }),
    pack: {
      ...catalog[2],
      included: ["VPN personnel inclus", "32 Go de stockage et sauvegardes"],
    },
  });
assert.ok(
  beforeAfterWindows.items.some(
    (item) =>
      item.before === "Bureau Windows distant non encore en place"
      && item.after === "Bureau Windows accessible à distance",
  ),
  "Le bloc Avant / Apres doit montrer le changement Bureau Windows.",
);

const beforeAfterQuote =
  diagnosticBeforeAfterRuntime.buildDiagnosticBeforeAfterSummary({
    answers: baseAnswers({
      needsWindowsDesktop: true,
      estimatedStorageGb: 64,
    }),
    recommendation: windowsStorageQuote,
    pack: null,
  });
assert.equal(beforeAfterQuote.title, "Avant cadrage");
assert.ok(
  beforeAfterQuote.items.some((item) =>
    item.after.includes("validés avant activation"),
  ),
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

assert.match(diagnosticEngine, /export function recommendOffer/);
assert.doesNotMatch(diagnosticEngine, /"Pack Dossier|Pack Acces|Pack Bureau/);
assert.match(diagnosticWizard, /REASON_LABELS/);
assert.match(diagnosticWizard, /WARNING_MESSAGES/);
assert.match(diagnosticWizard, /buildDiagnosticBeforeAfterSummary/);
assert.match(diagnosticWizard, /diagnostic-before-after/);
assert.match(diagnosticWizard, /backup_frequency_unknown/);
assert.match(diagnosticBeforeAfter, /export function buildDiagnosticBeforeAfterSummary/);
assert.match(diagnosticBeforeAfter, /items\.slice\(0, 5\)/);
assert.match(diagnosticBeforeAfter, /supportsVpn/);
assert.match(diagnosticBeforeAfter, /supportsWindowsDesktop/);
assert.match(diagnosticBeforeAfter, /findPackText/);
assert.match(diagnosticWizard, /Volume à protéger/);
assert.match(diagnosticWizard, /Souhaitez-vous disposer d'un bureau Windows accessible à distance/);
assert.match(diagnosticWizard, /Élevée - j&apos;ai besoin de retrouver mes fichiers très rapidement/);
assert.match(diagnosticWizard, /fichiers \?/);
assert.match(diagnosticWizard, /configurationToQueryString/);
assert.match(diagnosticWizard, /Personnaliser cette configuration/);
assert.match(diagnosticWizard, /source=diagnostic/);
assert.doesNotMatch(diagnosticWizard, /toIncVat|vatRate|0\.2|20\s*\/\s*100/);
assert.match(diagnosticPage, /alternates:\s*\{\s*canonical:\s*"\/diagnostic"/);
assert.match(diagnosticPage, /Diagnostic sauvegarde et accès distant/);
assert.match(diagnosticPage, /Vos données importantes pourraient-elles disparaître demain/);
assert.match(diagnosticPage, /Sans inscription/);
assert.match(diagnosticPage, /Aucun compte ni achat nécessaire/);
assert.doesNotMatch(diagnosticPage, /Sans engagement|Vos coordonnées servent/);
assert.match(publicShell, /href="\/diagnostic"/);
assert.match(publicRoutes, /"\/diagnostic"/);
assert.match(sitemap, /path:\s*"\/diagnostic"/);

assert.match(configurerPage, /robots:\s*\{\s*index:\s*false,\s*follow:\s*true\s*\}/);
assert.match(configurerPage, /alternates:\s*\{\s*canonical:\s*"\/configurer"/);
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
