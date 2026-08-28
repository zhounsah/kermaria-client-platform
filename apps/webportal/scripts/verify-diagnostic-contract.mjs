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

async function importBillingV2Runtime(source, label) {
  return importPureTypeScript(
    source
      .replaceAll('"@/lib/billing-v2-formules"', JSON.stringify(billingV2FormulesStubUrl))
      .replaceAll('"@/lib/billing-v2-selection"', JSON.stringify(billingV2SelectionStubUrl)),
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
      storagePersonalTierCode:"32",
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
const programCs = await read("../../apps/api-internal/Program.cs");
const fiscalPolicy = await read("../../apps/api-internal/Services/FiscalPolicy.cs");

const diagnosticRuntime = await importBillingV2Runtime(
  diagnosticEngine,
  "public-diagnostic.ts",
);
const contextRuntime = await importPureTypeScript(
  diagnosticContext,
  "diagnostic-context.ts",
);
const adaptiveRuntime = await importPureTypeScript(
  adaptiveDiagnostic.replaceAll(
    '"@/lib/public-diagnostic"',
    JSON.stringify(recommendationStubUrl),
  ),
  "adaptive-diagnostic.ts",
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
assert.equal(recommendation({ estimatedStorageGb: 256 }).status, "standard");
assert.equal(recommendation({ estimatedStorageGb: "above_public_max" }).status, "requires_quote");
assert.equal(recommendation({ users: 12 }).status, "requires_quote");
assert.equal(recommendation({ customerType: "other" }).status, "requires_quote");
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

console.log("Verification diagnostic adaptatif WEBPORTAL reussie.");
