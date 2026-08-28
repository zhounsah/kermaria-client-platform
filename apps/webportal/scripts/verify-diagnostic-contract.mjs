import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import ts from "typescript";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

function transpileToDataUrl(source, label) {
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
  return `data:text/javascript;base64,${encoded}`;
}

async function importPureTypeScript(source, label) {
  return import(transpileToDataUrl(source, label));
}

const billingV2FormulesStub = String.raw`
export const SERVICE_CODES={storagePersonal:"STORAGE-PERSONAL",storageShared:"STORAGE-SHARED",backupPersonal:"BACKUP-PERSONAL",backupShared:"BACKUP-SHARED",vpn:"VPN-ACCESS",remoteDesktop:"RDS-ACCESS",additionalUser:"USER-ADDITIONAL",supportPlus:"SUPPORT-PLUS"};
export function selectableTiers(c,s){return c.services.find(x=>x.code===s)?.tiers.filter(x=>x.publicSelectable)??[]}
export function buildBaselineSelection(p,c){const i=s=>p.items.find(x=>x.serviceCode===s);return{presetCode:p.code,commitmentCode:c,paymentMode:"monthly",storagePersonalTierCode:i(SERVICE_CODES.storagePersonal)?.tierCode??"32",backupPersonal:i(SERVICE_CODES.backupPersonal)!==undefined,storageSharedTierCode:i(SERVICE_CODES.storageShared)?.tierCode??null,backupShared:i(SERVICE_CODES.backupShared)!==undefined,vpnTierCode:i(SERVICE_CODES.vpn)?.tierCode??null,remoteDesktop:i(SERVICE_CODES.remoteDesktop)!==undefined,additionalUsers:i(SERVICE_CODES.additionalUser)?.quantity??0,supportPlus:i(SERVICE_CODES.supportPlus)!==undefined}}
export function findService(c,s){return c.services.find(x=>x.code===s)}
export function resolveTierLabel(s,c){return !s||!c?null:(s.tiers.find(x=>x.code===c)?.label??c)}
`;
const billingV2FormulesStubUrl =
  `data:text/javascript;base64,${Buffer.from(billingV2FormulesStub).toString("base64")}`;
const billingV2SelectionStubUrl =
  `data:text/javascript;base64,${Buffer.from("export const MAX_ADDITIONAL_USERS=10;").toString("base64")}`;
const diagnosticRecommendationConfigStub = String.raw`
export const DEFAULT_DIAGNOSTIC_RECOMMENDATION_CONFIG={schemaVersion:1,rules:[
{profileId:"simple_backup",presetCode:"pack-dossier-securise"},
{profileId:"vpn_access",presetCode:"pack-acces-distance"},
{profileId:"windows_desktop",presetCode:"pack-bureau-windows-distance"},
{profileId:"team_or_structure",presetCode:"pack-pro-association"},
{profileId:"team_windows_desktop",presetCode:"pack-pro-association"}
]};
export function resolveDiagnosticPresetCode(profileId,config=DEFAULT_DIAGNOSTIC_RECOMMENDATION_CONFIG){return config.rules.find(rule=>rule.profileId===profileId)?.presetCode??null}
`;
const diagnosticRecommendationConfigStubUrl =
  `data:text/javascript;base64,${Buffer.from(diagnosticRecommendationConfigStub).toString("base64")}`;


async function importBillingV2Runtime(source, label) {
  return importPureTypeScript(
    source
      .replaceAll('"@/lib/billing-v2-formules"', JSON.stringify(billingV2FormulesStubUrl))
      .replaceAll('"@/lib/billing-v2-selection"', JSON.stringify(billingV2SelectionStubUrl))
      .replaceAll('"@/lib/diagnostic-recommendation-config"', JSON.stringify(diagnosticRecommendationConfigStubUrl)),
    label,
  );
}

const recommendationStub = String.raw`
export function recommendOffer(answers){
  return {
    status:"standard",
    reasons:[],
    warnings:[],
    suggestedOptions:[],
    selection:{
      presetCode:answers.needsWindowsDesktop
        ?"pack-bureau-windows-distance"
        :answers.needsVpn
          ?"pack-acces-distance"
          :"pack-dossier-securise",
      commitmentCode:"FLEX",
      paymentMode:"monthly",
      storagePersonalTierCode:typeof answers.estimatedStorageGb==="number"?String(answers.estimatedStorageGb):"32",
      backupPersonal:true,
      storageSharedTierCode:null,
      backupShared:false,
      vpnTierCode:answers.needsVpn?"ESSENTIAL":null,
      remoteDesktop:answers.needsWindowsDesktop,
      additionalUsers:Math.max(0,(answers.users??1)-1),
      supportPlus:false
    }
  };
}
`;
const recommendationStubUrl =
  `data:text/javascript;base64,${Buffer.from(recommendationStub).toString("base64")}`;

const sharedTypes = await read("../../packages/shared/src/index.ts");
const diagnosticEngine = await read("lib/public-diagnostic.ts");
const billingV2Formules = await read("lib/billing-v2-formules.ts");
const diagnosticContext = await read("lib/diagnostic-context.ts");
const adaptiveDiagnostic = await read("lib/adaptive-diagnostic.ts");
const diagnosticPage = await read("app/diagnostic/page.tsx");
const diagnosticWizard = await read("components/PublicDiagnosticWizard.tsx");
const contactForm = await read("components/ContactForm.tsx");
const globalsCss = await read("app/globals.css");
const publicStorefrontPage = await read("components/PublicStorefrontPage.tsx");
const priorityServicePage = await read("components/PublicPriorityServicePage.tsx");
const messagingCategoryPage = await read("components/PublicMessagingCategoryPage.tsx");
const serviceRoute = await read("app/services/[category]/page.tsx");
const publicShell = await read("components/PublicShell.tsx");
const publicRoutes = await read("lib/public-route-config.ts");
const sitemap = await read("app/sitemap.ts");
const signupPage = await read("app/signup/page.tsx");
const signupRoute = await read("app/api/signup/route.ts");
const diagnosticValidation = await read("lib/diagnostic-configuration-validation.ts");
const diagnosticConfigurationLib = await read("lib/diagnostic-configuration.ts");
const diagnosticAdminPage = await read("app/admin/settings/diagnostic/page.tsx");
const diagnosticAdminCenter = await read("components/AdminDiagnosticCenter.tsx");
const diagnosticAdminEditor = await read("components/AdminDiagnosticEditor.tsx");
const diagnosticAdminSimulator = await read("components/AdminDiagnosticSimulator.tsx");
const diagnosticDraftRoute = await read("app/api/admin/diagnostic/draft/route.ts");
const diagnosticPublishRoute = await read("app/api/admin/diagnostic/publish/route.ts");
const diagnosticRegistryCs = await read(
  "../../apps/api-internal/Data/Configuration/DiagnosticConfigurationRegistry.cs",
);
const diagnosticServiceCs = await read(
  "../../apps/api-internal/Services/DiagnosticConfigurationService.cs",
);
const diagnosticRepositoryCs = await read(
  "../../apps/api-internal/Data/Repositories/MariaDbDiagnosticConfigurationRepository.cs",
);
const diagnosticMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/075_diagnostic_configuration.sql",
);
const programCs = await read("../../apps/api-internal/Program.cs");
const fiscalPolicy = await read("../../apps/api-internal/Services/FiscalPolicy.cs");

const diagnosticRuntime = await importBillingV2Runtime(
  diagnosticEngine,
  "public-diagnostic.ts",
);
const billingV2FormulesRuntime = await importPureTypeScript(
  billingV2Formules,
  "billing-v2-formules.ts",
);
// `diagnostic-context.ts` n'a plus que des imports de types : il se transpile
// en module autonome, reutilisable tel quel comme dependance reelle de
// l'interpreteur adaptatif. Le moteur de recommandation, lui, reste stube
// pour que ce contrat teste la DSL et non la tarification.
const diagnosticContextUrl = transpileToDataUrl(
  diagnosticContext,
  "diagnostic-context.ts",
);
const contextRuntime = await import(diagnosticContextUrl);
const adaptiveRuntime = await importPureTypeScript(
  adaptiveDiagnostic
    .replaceAll('"@/lib/public-diagnostic"', JSON.stringify(recommendationStubUrl))
    .replaceAll('"@/lib/diagnostic-context"', JSON.stringify(diagnosticContextUrl)),
  "adaptive-diagnostic.ts",
);

// Seule la constante d'operateurs est importee a l'execution depuis le contrat
// partage : on la stube, et on verifie juste apres qu'elle correspond bien a la
// liste declaree dans `packages/shared`.
const DSL_OPERATORS = ["equals", "not_equals", "one_of", "includes", "only", "answered"];
const sharedDiagnosticStubUrl = `data:text/javascript;base64,${Buffer.from(
  `export const DIAGNOSTIC_CONDITION_OPERATORS=${JSON.stringify(DSL_OPERATORS)};`,
).toString("base64")}`;
const sharedOperatorBlock =
  /DIAGNOSTIC_CONDITION_OPERATORS = \[([\s\S]*?)\] as const;/.exec(sharedTypes)?.[1] ?? "";
for (const operator of DSL_OPERATORS) {
  assert.ok(
    sharedOperatorBlock.includes(`"${operator}"`),
    `Operateur absent du contrat partage : ${operator}`,
  );
}
assert.equal(
  (sharedOperatorBlock.match(/"[a-z_]+",/g) ?? []).length,
  DSL_OPERATORS.length,
  "La DSL doit rester fermee : aucun operateur supplementaire.",
);

const validationRuntime = await importPureTypeScript(
  diagnosticValidation
    .replaceAll('"@/lib/diagnostic-context"', JSON.stringify(diagnosticContextUrl))
    .replaceAll('"@kermaria/shared"', JSON.stringify(sharedDiagnosticStubUrl)),
  "diagnostic-configuration-validation.ts",
);

const catalog = {
  source: "test",
  currency: "EUR",
  commitments: [{
    code: "FLEX",
    name: "Sans engagement",
    months: 1,
    paymentOptions: [{ paymentMode: "monthly", discountBasisPoints: 0 }],
  }],
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

// Le moteur commercial existant reste une simple construction de selection V2.
assert.equal(recommendation({ needsRemoteFiles: false }).selection?.presetCode, "pack-dossier-securise");
assert.equal(recommendation({ needsVpn: true }).selection?.presetCode, "pack-acces-distance");
assert.equal(recommendation({ needsWindowsDesktop: true }).selection?.presetCode, "pack-bureau-windows-distance");
const proRecommendation = recommendation({ customerType: "business", users: 2 });
assert.equal(proRecommendation.selection?.presetCode, "pack-pro-association");
assert.deepEqual(
  {
    sharedStorage: proRecommendation.selection?.storageSharedTierCode,
    sharedBackup: proRecommendation.selection?.backupShared,
    vpn: proRecommendation.selection?.vpnTierCode !== null,
    remoteDesktop: proRecommendation.selection?.remoteDesktop,
    additionalUsers: proRecommendation.selection?.additionalUsers,
    supportPlus: proRecommendation.selection?.supportPlus,
  },
  {
    sharedStorage: "128",
    sharedBackup: true,
    vpn: true,
    remoteDesktop: false,
    additionalUsers: 1,
    supportPlus: true,
  },
  "Une petite structure doit conserver le profil de base Pro / Association.",
);
assert.equal(recommendation({ estimatedStorageGb: 256 }).status, "standard");
assert.equal(recommendation({ estimatedStorageGb: "above_public_max" }).status, "requires_quote");
assert.equal(recommendation({ users: 12 }).status, "requires_quote");
assert.equal(recommendation({ customerType: "other" }).status, "requires_quote");

const remappedConfig = {
  schemaVersion: 1,
  rules: [
    { profileId: "simple_backup", presetCode: "pack-acces-distance" },
    { profileId: "vpn_access", presetCode: "pack-acces-distance" },
    { profileId: "windows_desktop", presetCode: "pack-bureau-windows-distance" },
    { profileId: "team_or_structure", presetCode: "pack-pro-association" },
    { profileId: "team_windows_desktop", presetCode: "pack-pro-association" },
  ],
};
assert.equal(
  diagnosticRuntime.recommendOffer(baseAnswers({ needsRemoteFiles: false }), catalog, remappedConfig).selection?.presetCode,
  "pack-acces-distance",
  "La configuration back-office doit pouvoir changer la formule sans modifier le moteur.",
);

const quoteOnlyConfig = {
  ...remappedConfig,
  rules: remappedConfig.rules.map((rule) =>
    rule.profileId === "simple_backup" ? { ...rule, presetCode: null } : rule
  ),
};
assert.equal(
  diagnosticRuntime.recommendOffer(baseAnswers({ needsRemoteFiles: false }), catalog, quoteOnlyConfig).status,
  "requires_quote",
  "Un profil admin sans formule doit basculer vers un cadrage/devis.",
);

const missingPresetConfig = {
  ...remappedConfig,
  rules: remappedConfig.rules.map((rule) =>
    rule.profileId === "simple_backup" ? { ...rule, presetCode: "future-formula" } : rule
  ),
};
assert.equal(
  diagnosticRuntime.recommendOffer(baseAnswers({ needsRemoteFiles: false }), catalog, missingPresetConfig).status,
  "requires_quote",
  "Un preset configure mais absent du catalogue Billing ne doit jamais etre propose.",
);

assert.doesNotMatch(
  diagnosticEngine,
  /AmountCents|monthlyAmountCents|setupFeeAmountCents|formatCurrencyFromCents/,
  "Le moteur de recommandation ne doit calculer ou lire aucun prix.",
);

// Contrat de contexte ferme et partageable.
assert.deepEqual(
  [...contextRuntime.DIAGNOSTIC_CONTEXT_IDS],
  ["backup", "remote-access", "network", "messaging", "domain-dns", "server", "web-hosting", "general"],
);
for (const id of contextRuntime.DIAGNOSTIC_CONTEXT_IDS) {
  assert.equal(contextRuntime.resolveDiagnosticContext(id), id);
  const definition = contextRuntime.getDiagnosticContextDefinition(id);
  assert.ok(definition.title.length > 10, `${id} doit avoir une introduction dediee.`);
  if (id !== "general") {
    assert.ok(definition.questions.length >= 4, `${id} doit avoir un parcours cible.`);
  }
}
for (const invalid of [
  undefined,
  "",
  "vpn",
  "SERVER&context=backup",
  "backup?serviceCode=VPN-ACCESS",
  "billing",
  "../backup",
]) {
  assert.equal(
    contextRuntime.resolveDiagnosticContext(invalid),
    "general",
    `Le contexte invalide ${String(invalid)} doit retomber sur general.`,
  );
}
assert.equal(contextRuntime.buildDiagnosticHref("general"), "/diagnostic");
assert.equal(contextRuntime.buildDiagnosticHref("backup"), "/diagnostic?context=backup");
assert.equal(
  contextRuntime.contextualizeDiagnosticHref("/diagnostic", "network"),
  "/diagnostic?context=network",
);
assert.equal(
  contextRuntime.contextualizeDiagnosticHref("/contact", "network"),
  "/contact",
  "La contextualisation ne doit jamais detourner un CTA commercial non diagnostic.",
);

const serviceContexts = {
  "sauvegarde-externalisee": "backup",
  "supervision-nas": "backup",
  "vpn-entreprise": "remote-access",
  "bureau-windows-distance": "remote-access",
  unifi: "network",
  firewall: "network",
  "messagerie-professionnelle": "messaging",
  "gestion-dns-domaines": "domain-dns",
  vps: "server",
  "infogerance-vps": "server",
  "maintenance-linux": "server",
  "supervision-informatique": "server",
  "hebergement-web": "web-hosting",
  "maintenance-wordpress": "web-hosting",
  "cloudflare-waf": "web-hosting",
};
for (const [slug, expected] of Object.entries(serviceContexts)) {
  assert.equal(
    contextRuntime.diagnosticContextForServiceSlug(slug),
    expected,
    `${slug} doit transmettre le contexte ${expected}.`,
  );
}
assert.equal(
  contextRuntime.diagnosticContextForServiceSlug("domaines-messagerie"),
  "general",
  "La categorie mixte Domaines & Messagerie doit rester un aiguillage general.",
);

// Embranchements : une reponse devenue cachee doit etre retiree.
const remoteExisting = {
  "remote-target": "files",
  structure: "business",
  users: "2",
  "remote-existing": "existing",
  sites: "several",
};
assert.ok(
  contextRuntime.getVisibleDiagnosticQuestions("remote-access", remoteExisting)
    .some((question) => question.id === "sites"),
);
const remoteNew = { ...remoteExisting, "remote-existing": "new" };
assert.ok(
  !contextRuntime.getVisibleDiagnosticQuestions("remote-access", remoteNew)
    .some((question) => question.id === "sites"),
);
assert.equal(
  contextRuntime.pruneHiddenDiagnosticAnswers("remote-access", remoteNew).sites,
  undefined,
);

// Les textes presentes au client ne contiennent aucun identifiant interne.
const forbiddenCustomerJargon = /\b(serviceCode|tierCode|presetCode|Billing|provider)\b/i;
for (const id of contextRuntime.DIAGNOSTIC_CONTEXT_IDS) {
  const definition = contextRuntime.getDiagnosticContextDefinition(id);
  const customerCopy = JSON.stringify({
    label: definition.label,
    eyebrow: definition.eyebrow,
    title: definition.title,
    intro: definition.intro,
    questions: definition.questions,
  });
  assert.doesNotMatch(customerCopy, forbiddenCustomerJargon, `${id} expose du jargon interne.`);
}
assert.doesNotMatch(
  contextRuntime.buildDiagnosticContactMessage("network", {
    "network-goal": ["coverage"],
    "network-existing": "unifi",
    "network-sites": "one",
    "network-scale": "small",
  }),
  forbiddenCustomerJargon,
);

// Frontiere commerciale adaptative.
const backupSimple = adaptiveRuntime.buildAdaptiveDiagnosticOutcome(
  "backup",
  {
    "backup-targets": ["files"],
    storage: "32",
    structure: "individual",
    users: "1",
    "backup-existing": "yes",
    "restore-test": "recent",
  },
  catalog,
);
assert.equal(backupSimple.recommendation?.selection?.presetCode, "pack-dossier-securise");

const backup128Answers = {
  "backup-targets": ["files"],
  storage: "128",
  structure: "individual",
  users: "1",
  "backup-existing": "partial",
  "restore-test": "never",
};
const backup128Adaptive = adaptiveRuntime.buildAdaptiveDiagnosticOutcome(
  "backup",
  backup128Answers,
  catalog,
);
assert.deepEqual(
  backup128Adaptive.recommendation?.selection,
  {
    presetCode: "pack-dossier-securise",
    commitmentCode: "FLEX",
    paymentMode: "monthly",
    storagePersonalTierCode: "128",
    backupPersonal: true,
    storageSharedTierCode: null,
    backupShared: false,
    vpnTierCode: null,
    remoteDesktop: false,
    additionalUsers: 0,
    supportPlus: false,
  },
  "Le diagnostic sauvegarde 128 Go individuel doit produire le profil Dossier securise attendu.",
);

const backup128Recommendation = diagnosticRuntime.recommendOffer(
  baseAnswers({
    estimatedStorageGb: 128,
    needsRemoteFiles: false,
    needsVpn: false,
    needsWindowsDesktop: false,
    backupFrequency: "unknown",
    restoreTestRecency: "never",
    continuityPlan: "unknown",
  }),
  catalog,
);
assert.equal(backup128Recommendation.status, "standard");
assert.deepEqual(
  backup128Recommendation.selection,
  backup128Adaptive.recommendation?.selection,
  "Le moteur commercial reel et le parcours adaptatif doivent converger vers la meme selection.",
);

const backup128Configuration = billingV2FormulesRuntime.describeSelectionConfiguration(
  backup128Recommendation.selection,
  catalog,
);
assert.deepEqual(
  backup128Configuration.map(({ label, value }) => [label, value]),
  [
    ["Stockage personnel", "128 Go"],
    ["Sauvegarde personnelle", "Incluse"],
    ["Espace partag\u00e9", "Non"],
    ["Sauvegarde partag\u00e9e", "Non"],
    ["Acc\u00e8s s\u00e9curis\u00e9 \u00e0 distance", "Non"],
    ["Bureau Windows \u00e0 distance", "Non"],
    ["Utilisateurs", "1"],
    ["Support renforc\u00e9", "Non"],
  ],
  "Le profil Billing doit etre traduit en configuration publique sans codes internes.",
);


for (const complexTargets of [
  ["workstations"],
  ["server"],
  ["nas"],
  ["files", "workstations"],
  ["unknown"],
]) {
  const result = adaptiveRuntime.buildAdaptiveDiagnosticOutcome(
    "backup",
    {
      "backup-targets": complexTargets,
      storage: "32",
      structure: "business",
      users: "2",
      "backup-existing": "yes",
      "restore-test": "recent",
    },
    catalog,
  );
  assert.equal(result.recommendation, null, `${complexTargets.join(",")} doit rester sur cadrage.`);
}

const remoteVpn = adaptiveRuntime.buildAdaptiveDiagnosticOutcome(
  "remote-access",
  {
    "remote-target": "files",
    structure: "individual",
    users: "1",
    "remote-existing": "existing",
    sites: "one",
    devices: ["windows"],
  },
  catalog,
);
assert.equal(remoteVpn.recommendation?.selection?.presetCode, "pack-acces-distance");

const remoteDesktop = adaptiveRuntime.buildAdaptiveDiagnosticOutcome(
  "remote-access",
  {
    "remote-target": "windows-desktop",
    structure: "individual",
    users: "1",
    "remote-existing": "new",
    devices: ["windows"],
  },
  catalog,
);
assert.equal(remoteDesktop.recommendation?.selection?.presetCode, "pack-bureau-windows-distance");

assert.equal(
  adaptiveRuntime.buildAdaptiveDiagnosticOutcome(
    "remote-access",
    {
      "remote-target": "internal-app",
      structure: "business",
      users: "2",
      "remote-existing": "existing",
      sites: "several",
      devices: ["windows"],
    },
    catalog,
  ).recommendation,
  null,
  "Plusieurs sites doivent imposer un cadrage.",
);

for (const id of ["network", "messaging", "domain-dns", "server", "web-hosting"]) {
  const injected = adaptiveRuntime.buildAdaptiveDiagnosticOutcome(
    id,
    {
      "backup-targets": ["files"],
      storage: "16",
      structure: "individual",
      users: "1",
      "remote-target": "files",
    },
    catalog,
  );
  assert.equal(
    injected.recommendation,
    null,
    `Le contexte ${id} ne doit jamais produire une formule, meme avec des reponses forgees.`,
  );
  assert.ok(injected.guidance.title.length > 10);
}
assert.equal(adaptiveRuntime.canContextProduceFormula("backup"), true);
assert.equal(adaptiveRuntime.canContextProduceFormula("remote-access"), true);
assert.equal(adaptiveRuntime.canContextProduceFormula("server"), false);
assert.doesNotMatch(adaptiveDiagnostic, /STORAGE-|VPN-ACCESS|RDS-ACCESS|monthlyAmountCents/);

// Route et UI.
assert.match(diagnosticPage, /resolveDiagnosticContext\(rawContext\)/);
assert.match(diagnosticPage, /searchParams:/);
assert.match(diagnosticPage, /initialContext=\{initialContext\}/);
assert.match(diagnosticPage, /getBillingV2FormulesCatalog/);
assert.doesNotMatch(diagnosticPage, /catalog\.presets\.length === 0/);
assert.match(diagnosticPage, /path:\s*"\/diagnostic"/);
assert.match(diagnosticPage, /Diagnostic informatique adapt/);

assert.match(diagnosticWizard, /GENERAL_CONTEXT_CHOICES/);
assert.match(diagnosticWizard, /getVisibleDiagnosticQuestions/);
assert.match(diagnosticWizard, /pruneHiddenDiagnosticAnswers/);
assert.match(diagnosticWizard, /buildAdaptiveDiagnosticOutcome/);
assert.match(diagnosticWizard, /fetch\("\/api\/formules\/devis"/);
assert.match(diagnosticWizard, /billingV2SelectionToSearchParams/);
assert.match(diagnosticWizard, /params\.set\("source", "diagnostic"\)/);
assert.match(diagnosticWizard, /<ContactForm/);
assert.match(diagnosticWizard, /defaultMessage=\{buildDiagnosticContactMessage/);
assert.match(diagnosticWizard, /submitLabel="Envoyer mon diagnostic"/);
assert.doesNotMatch(diagnosticWizard, /\/configurer\?|configurationToQueryString/);
assert.doesNotMatch(diagnosticWizard, /toIncVat|vatRate|0\.2|20\s*\/\s*100/);
assert.match(diagnosticWizard, /<fieldset className="diagnostic-step"/);
assert.match(diagnosticWizard, /<legend ref=\{legendRef\} tabIndex=\{-1\}>/);
assert.match(diagnosticWizard, /aria-live="polite"/);
assert.match(diagnosticWizard, /aria-describedby=\{hintId\}/);
assert.match(globalsCss, /@media \(max-width: 820px\)[\s\S]*\.diagnostic-options[\s\S]*grid-template-columns: 1fr/);
assert.match(globalsCss, /@media \(max-width: 560px\)[\s\S]*\.diagnostic-actions[\s\S]*flex-direction: column/);
assert.match(diagnosticWizard, /role="progressbar"/);
assert.match(diagnosticWizard, /<DiagnosticIcon context=/);
assert.match(diagnosticWizard, /data-selected=\{checked \? "true" : "false"\}/);
assert.match(diagnosticWizard, /describeSelectionConfiguration\(selection, catalog\)/);
assert.match(diagnosticWizard, /formulaConfiguration\.map/);
assert.match(diagnosticWizard, /Configuration issue de votre diagnostic/);
assert.match(globalsCss, /diagnostic-options label:has\(input:checked\)/);
assert.match(globalsCss, /@media \(prefers-reduced-motion:reduce\)/);
assert.doesNotMatch(globalsCss, /\.diagnostic-result-details\s*\{[^}]*position\s*:\s*sticky/);
// Garde-fous structurels : fieldset et legend, annonces de progression et repli mobile.
assert.match(contactForm, /defaultMessage\?: string/);
assert.match(contactForm, /submitLabel\?: string/);
assert.match(contactForm, /idleLabel=\{submitLabel\}/);

// Les pages Services transmettent le contexte au niveau du rendu, sans modifier
// la resolution commerciale ou la page de categorie mixte.
assert.match(publicStorefrontPage, /diagnosticContextForServiceSlug/);
assert.match(publicStorefrontPage, /contextualizeDiagnosticHref/);
assert.match(priorityServicePage, /diagnosticContextForServiceSlug/);
assert.match(priorityServicePage, /buildDiagnosticHref\(diagnosticContext\)/);
assert.match(serviceRoute, /serviceSlug=\{serviceSlug\}/);
assert.match(serviceRoute, /slug === "domaines-messagerie"/);
assert.doesNotMatch(
  messagingCategoryPage,
  /diagnosticContextForServiceSlug|context=messaging|context=domain-dns/,
  "La categorie mixte ne doit pas forcer un sous-contexte.",
);

// Contrats publics existants conserves.
assert.match(sharedTypes, /interface DiagnosticRecommendation[\s\S]*selection:\s*BillingV2PublicSelection \| null/);
assert.match(publicShell, /publicHref\("\/diagnostic"\)/);
assert.match(publicRoutes, /"\/diagnostic"/);
assert.match(sitemap, /path:\s*"\/diagnostic"/);
for (const [label, source] of [
  ["page diagnostic", diagnosticPage],
  ["assistant diagnostic", diagnosticWizard],
  ["routes publiques", publicRoutes],
  ["plan du site", sitemap],
  ["coquille publique", publicShell],
]) {
  assert.doesNotMatch(
    source,
    /\/configurer|PublicConfigurator|catalogConfiguration/,
    `${label} ne doit plus renvoyer vers le configurateur supprime.`,
  );
}
assert.doesNotMatch(
  programCs,
  /ICatalogConfigurationService|\/internal\/portal\/configuration\/resolve/,
);
assert.match(programCs, /IFiscalPolicy/);
assert.match(fiscalPolicy, /FiscalRegimes\.FranchiseBase/);
assert.match(fiscalPolicy, /FiscalRegimes\.Standard/);

assert.match(signupPage, /readBillingV2SelectionSearchParams\(rawSearchParams\)/);
assert.match(signupRoute, /billingV2Selection,/);
assert.doesNotMatch(
  signupRoute
    .split(/\r?\n/)
    .filter((line) => !/^\s*(\/\/|\*|\/\*)/.test(line))
    .join("\n"),
  /catalogConfiguration|packSelection/,
);

// --- Diagnostic administrable (Centre de configuration, section 9) ---------

// La configuration integree au code doit satisfaire le registre ferme : sinon
// le repli du parcours public serait lui-meme refuse a la publication.
const defaultValidation = validationRuntime.validateDiagnosticConfiguration(
  contextRuntime.DEFAULT_DIAGNOSTIC_CONFIGURATION,
);
assert.deepEqual(
  defaultValidation.errors,
  [],
  "La configuration integree au code doit passer la validation fermee.",
);
assert.notEqual(defaultValidation.configuration, null);

// Une configuration inconnue ou incomplete est refusee, jamais tronquee.
assert.notEqual(
  validationRuntime.validateDiagnosticConfiguration({ schemaVersion: 1, contexts: [] })
    .errors.length,
  0,
);
assert.notEqual(
  validationRuntime.validateDiagnosticConfiguration(null).errors.length,
  0,
);

// Un operateur hors DSL est refuse : la base ne stocke aucun script.
const forgedOperator = JSON.parse(
  JSON.stringify(contextRuntime.DEFAULT_DIAGNOSTIC_CONFIGURATION),
);
forgedOperator.contexts
  .find((context) => context.id === "backup")
  .guidance[0].when[0].operator = "eval";
assert.notEqual(
  validationRuntime.validateDiagnosticConfiguration(forgedOperator).errors.length,
  0,
  "Un operateur inconnu doit etre refuse.",
);

// La derniere regle de chaque contexte reste inconditionnelle : un resultat
// existe donc toujours, quelle que soit la combinaison de reponses.
for (const context of contextRuntime.DEFAULT_DIAGNOSTIC_CONFIGURATION.contexts) {
  const last = context.guidance[context.guidance.length - 1];
  assert.equal(
    last.when.length,
    0,
    `Le contexte ${context.id} doit finir par une regle inconditionnelle.`,
  );
}

// Les contextes fermes cote API-INTERNAL et cote WebPortal ne peuvent pas
// diverger : une divergence rendrait toute publication impossible.
for (const id of contextRuntime.DIAGNOSTIC_CONTEXT_IDS) {
  assert.ok(
    diagnosticRegistryCs.includes(`"${id}"`),
    `Le registre C# doit connaitre le contexte ${id}.`,
  );
}
for (const operator of ["equals", "not_equals", "one_of", "includes", "only", "answered"]) {
  assert.ok(diagnosticRegistryCs.includes(`"${operator}"`));
  assert.ok(adaptiveDiagnostic.includes(`case "${operator}"`));
}

// Aucun moteur de scripting : ni evaluation, ni construction de fonction.
assert.doesNotMatch(adaptiveDiagnostic, /eval\(|new Function|Function\(/);
assert.doesNotMatch(diagnosticValidation, /eval\(|new Function/);

// Versioning, publication atomique et repli.
assert.match(diagnosticMigration, /CREATE TABLE IF NOT EXISTS diagnostic_configurations/);
assert.match(
  diagnosticMigration,
  /CREATE TABLE IF NOT EXISTS diagnostic_configuration_revisions/,
);
assert.match(diagnosticMigration, /state IN \('draft', 'published'\)/);
assert.match(diagnosticRepositoryCs, /BeginTransactionAsync/);
assert.match(diagnosticRepositoryCs, /FOR UPDATE/);
assert.match(diagnosticRepositoryCs, /MariaDbIdentifierReader\.ReadNullable/);
assert.doesNotMatch(diagnosticRepositoryCs, /CREATE TABLE|ALTER TABLE/);
assert.match(diagnosticServiceCs, /DIAGNOSTIC_VERSION_CONFLICT/);
assert.match(diagnosticServiceCs, /DIAGNOSTIC_PUBLISHED/);
// La revalidation avant publication interdit qu'un brouillon accepte autrefois
// atteigne le public apres un durcissement du registre.
assert.match(diagnosticServiceCs, /Revalidation avant publication/);

// API : les routes existent et la mutation est bornee par la permission dediee.
for (const route of [
  "/internal/admin/diagnostic/configuration",
  "/internal/admin/diagnostic/revisions",
  "/internal/admin/diagnostic/validate",
  "/internal/admin/diagnostic/draft",
  "/internal/admin/diagnostic/publish",
  "/internal/public/diagnostic/configuration",
]) {
  assert.ok(programCs.includes(route), `Route manquante : ${route}`);
}
assert.match(programCs, /"settings\.diagnostic\.write"/);

// Le parcours public lit la version publiee, jamais le brouillon.
assert.match(diagnosticConfigurationLib, /getPublicDiagnosticConfiguration/);
assert.match(diagnosticConfigurationLib, /DEFAULT_DIAGNOSTIC_CONFIGURATION/);
assert.match(diagnosticPage, /resolvePublishedDiagnosticConfiguration/);
assert.match(diagnosticPage, /configuration=\{configuration\}/);

// BFF : enveloppe verifiee, autorite laissee a API-INTERNAL.
assert.match(diagnosticDraftRoute, /handleAdminMutation/);
assert.match(diagnosticDraftRoute, /"PUT"/);
assert.match(diagnosticPublishRoute, /expectedDraftVersion/);
assert.match(diagnosticPublishRoute, /expectedPublishedVersion/);

// Simulateur : il appelle le moteur reel, il ne le reimplemente pas.
assert.match(diagnosticAdminSimulator, /buildAdaptiveDiagnosticOutcome/);
assert.match(diagnosticAdminSimulator, /getVisibleDiagnosticQuestions/);
assert.match(diagnosticAdminSimulator, /pruneHiddenDiagnosticAnswers/);
assert.match(diagnosticAdminSimulator, /outcome\.appliedRuleIds/);
// Le diagnostic ne calcule jamais de prix, pas meme dans l'administration.
assert.doesNotMatch(diagnosticAdminSimulator, /monthlyAmountCents|toIncVat|vatRate/);

// Administration : publication explicite et confirmee, jamais implicite.
assert.match(diagnosticAdminCenter, /\/api\/admin\/diagnostic\/draft/);
assert.match(diagnosticAdminCenter, /\/api\/admin\/diagnostic\/publish/);
assert.match(diagnosticAdminCenter, /window\.confirm/);
assert.match(diagnosticAdminCenter, /validateDiagnosticConfiguration/);
assert.match(diagnosticAdminPage, /requireAdminSession/);
assert.match(diagnosticAdminEditor, /DIAGNOSTIC_CONDITION_OPERATORS/);

console.log("Verification diagnostic adaptatif WEBPORTAL reussie.");
