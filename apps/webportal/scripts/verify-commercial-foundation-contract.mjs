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

async function importPublicPacksRuntime(source) {
  const sharedStub = String.raw`
const TEST_PACK = {
  key: "pack-dossier-securise",
  slug: "dossier-securise",
  label: "Pack test",
  shortLabel: "Test",
  headline: "Headline",
  audience: "Audience",
  description: "Description",
  highlights: ["Highlight"],
  included: ["Included"],
  order: 10,
};

function externalReference(commitmentMonths, paymentMode) {
  return "PACK-TEST-" + commitmentMonths + "-" + paymentMode;
}

export function createDefaultPublicPackCatalogContent() {
  return {
    packs: [{
      packCode: TEST_PACK.key,
      label: TEST_PACK.label,
      shortLabel: TEST_PACK.shortLabel,
      headline: TEST_PACK.headline,
      audience: TEST_PACK.audience,
      description: TEST_PACK.description,
      highlights: TEST_PACK.highlights,
      included: TEST_PACK.included,
      displayOrder: TEST_PACK.order,
    }],
  };
}

export function getPublicPackManifest(packKey) {
  return packKey === TEST_PACK.key ? TEST_PACK : null;
}

export function resolvePublicPackVariantFromCatalog(
  catalog,
  packKey,
  commitmentMonths,
  paymentMode,
) {
  if (packKey !== TEST_PACK.key) {
    return null;
  }
  const reference = externalReference(commitmentMonths, paymentMode);
  const offer = catalog.find(
    (candidate) => candidate.externalReference === reference,
  );
  if (!offer) {
    return null;
  }
  return {
    offer,
    externalReference: reference,
    commitmentMonths,
    paymentMode,
    billingIntervalMonths: paymentMode === "upfront" ? commitmentMonths : 1,
    discountPercent: commitmentMonths === 12 ? 20 : commitmentMonths === 6 ? 10 : 0,
    monthlyPriceAmountCents: 1000,
    billingPriceAmountCents: paymentMode === "upfront" ? 1000 * commitmentMonths : 1000,
    setupFeeAmountCents: 500,
    firstChargeAmountCents: paymentMode === "upfront" ? 1000 * commitmentMonths + 500 : 1500,
    currency: "EUR",
  };
}

export function resolvePublicPackCatalog(catalog) {
  const monthly1 = resolvePublicPackVariantFromCatalog(
    catalog,
    TEST_PACK.key,
    1,
    "monthly",
  );
  const monthly6 = resolvePublicPackVariantFromCatalog(
    catalog,
    TEST_PACK.key,
    6,
    "monthly",
  );
  const monthly12 = resolvePublicPackVariantFromCatalog(
    catalog,
    TEST_PACK.key,
    12,
    "monthly",
  );
  if (!monthly1 || !monthly6 || !monthly12) {
    return [];
  }
  return [{
    ...TEST_PACK,
    variantsByCommitment: {
      1: { monthly: monthly1, upfront: null },
      6: {
        monthly: monthly6,
        upfront: resolvePublicPackVariantFromCatalog(
          catalog,
          TEST_PACK.key,
          6,
          "upfront",
        ),
      },
      12: {
        monthly: monthly12,
        upfront: resolvePublicPackVariantFromCatalog(
          catalog,
          TEST_PACK.key,
          12,
          "upfront",
        ),
      },
    },
  }];
}
`;
  const sharedStubUrl = `data:text/javascript;base64,${Buffer.from(sharedStub).toString("base64")}`;
  const executableSource = source.replaceAll(
    '"@kermaria/shared"',
    JSON.stringify(sharedStubUrl),
  );
  assert.notEqual(
    executableSource,
    source,
    "public-packs.ts doit importer le contrat partage attendu.",
  );
  return importPureTypeScript(executableSource, "public-packs.ts");
}

const sharedTypes = await read("../../packages/shared/src/index.ts");
const internalApi = await read("lib/internal-api.ts");
const adminBff = await read("lib/admin-bff.ts");
const payloads = await read("lib/bff-payloads.ts");
const invoicesPage = await read("app/invoices/page.tsx");
const catalogPage = await read("app/admin/catalog/page.tsx");
const documentsPage = await read("app/admin/commercial-documents/page.tsx");
const documentDetailPage = await read("app/commercial-documents/[id]/page.tsx");
const servicesPage = await read("app/services/page.tsx");
const invoiceTable = await read("components/InvoiceTable.tsx");
const publicPacks = await read("lib/public-packs.ts");
const publicPackCard = await read("components/PublicPackCard.tsx");
const publicPackComparison = await read(
  "components/PublicPackComparisonTable.tsx",
);
const publicPackOverview = await read("components/PublicPackOverviewGrid.tsx");
const offersPage = await read("app/offres/page.tsx");
const contactPage = await read("app/contact/page.tsx");
const publicPacksRuntime = await importPublicPacksRuntime(publicPacks);

const routeFiles = [
  "app/api/catalog/route.ts",
  "app/api/commercial-documents/route.ts",
  "app/api/commercial-documents/[id]/route.ts",
  "app/api/admin/catalog/route.ts",
  "app/api/admin/catalog/[id]/route.ts",
  "app/api/admin/commercial-documents/route.ts",
  "app/api/admin/commercial-documents/[id]/route.ts",
  "app/api/admin/commercial-documents/[id]/lines/route.ts",
  "app/api/admin/commercial-documents/[id]/lines/[lineId]/route.ts",
  "app/api/admin/commercial-documents/[id]/share/route.ts",
  "app/api/admin/commercial-documents/[id]/cancel/route.ts",
];

for (const file of routeFiles) {
  const source = await read(file);
  assert.doesNotMatch(
    source,
    /NEXT_PUBLIC_INTERNAL_API_URL|NEXT_PUBLIC_SERVICE_AUTH_TOKEN|localStorage|sessionStorage/,
  );
}

assert.match(sharedTypes, /interface CommercialOfferSummary/);
assert.match(sharedTypes, /interface CommercialDocumentDetail/);
assert.match(sharedTypes, /type CommercialDocumentStatus =/);
assert.match(sharedTypes, /type CommercialDocumentType =/);

assert.match(internalApi, /import "server-only"/);
assert.match(internalApi, /getCommercialCatalog/);
assert.match(internalApi, /getCommercialDocuments/);
assert.match(internalApi, /getCommercialDocument/);
assert.match(internalApi, /getAdminCatalog/);
assert.match(internalApi, /getAdminCommercialDocuments/);
assert.match(internalApi, /getAdminCommercialDocument/);
assert.match(internalApi, /"\/internal\/portal\/catalog"/);
assert.match(internalApi, /"\/internal\/portal\/commercial-documents"/);
assert.match(internalApi, /"\/internal\/admin\/catalog"/);
assert.match(internalApi, /"\/internal\/admin\/commercial-documents"/);
assert.doesNotMatch(
  internalApi,
  /NEXT_PUBLIC_INTERNAL_API_URL|NEXT_PUBLIC_SERVICE_AUTH_TOKEN/,
);

assert.match(adminBff, /handleAdminMutation</);
assert.match(adminBff, /INVALID_REQUEST/);
assert.doesNotMatch(
  adminBff,
  /NEXT_PUBLIC_INTERNAL_API_URL|NEXT_PUBLIC_SERVICE_AUTH_TOKEN/,
);

assert.match(payloads, /parseCommercialOfferPayload/);
assert.match(payloads, /parseCommercialDocumentPayload/);
assert.match(payloads, /parseCommercialDocumentLinePayload/);

assert.match(
  invoicesPage,
  /Vos documents commerciaux et factures émises\./,
);
assert.doesNotMatch(invoicesPage, /Payer|PayPal|Stripe/);

assert.match(
  catalogPage,
  /Ces documents sont informatifs et ne constituent pas des factures\s+officielles\./,
);
assert.match(
  catalogPage,
  /Aucune numérotation fiscale définitive n(?:'|&apos;)est générée\s+dans cette version\./,
);
assert.doesNotMatch(documentsPage, /Payer|paiement en ligne|PayPal|Stripe/);
assert.match(documentDetailPage, /isIssued \? \(/);
assert.match(documentDetailPage, /PayButton/);
assert.match(documentDetailPage, /isPayPalConfigured/);
assert.match(documentDetailPage, /isStripeConfigured/);
assert.match(
  servicesPage,
  /getPendingPackSelection/,
  "La page services doit reprendre un pack choisi au signup quand il existe.",
);
assert.match(
  servicesPage,
  /PublicPackCard/,
  "La page services doit exposer le catalogue packs v0.32.",
);
assert.match(
  servicesPage,
  /Finaliser mon pack|Catalogue packs|Souscrire .* pack/,
  "La page services doit presenter clairement les packs grand public.",
);
assert.match(invoiceTable, /Informations indicatives/);
assert.doesNotMatch(
  [
    invoicesPage,
    catalogPage,
    documentsPage,
    documentDetailPage,
    invoiceTable,
  ].join("\n"),
  /href="\/pay"|>\s*Payer\s*</,
);

for (const [input, expected] of [
  [1, 1],
  [6, 6],
  [12, 12],
  ["1", 1],
  [" 6 ", 6],
  ["12", 12],
]) {
  assert.equal(
    publicPacksRuntime.normalizeCommitmentMonths(input),
    expected,
    `Engagement valide non reconnu: ${JSON.stringify(input)}`,
  );
}
for (const input of [
  0,
  1.1,
  6.5,
  13,
  Number.NaN,
  "",
  "01",
  "06",
  "6.0",
  "6months",
  "12x",
  "+6",
  "1e0",
  null,
  undefined,
  [],
  {},
]) {
  assert.equal(
    publicPacksRuntime.normalizeCommitmentMonths(input),
    null,
    `Engagement ambigu accepte: ${JSON.stringify(input)}`,
  );
}

assert.equal(publicPacksRuntime.normalizePaymentMode("monthly", 1), "monthly");
assert.equal(publicPacksRuntime.normalizePaymentMode("upfront", 1), null);
assert.equal(publicPacksRuntime.normalizePaymentMode(undefined, 1), null);
assert.equal(publicPacksRuntime.normalizePaymentMode("monthly", 6), "monthly");
assert.equal(publicPacksRuntime.normalizePaymentMode("upfront", 6), "upfront");
assert.equal(publicPacksRuntime.normalizePaymentMode("monthly", null), null);
assert.equal(
  publicPacksRuntime.resolvePackSelectionInput({
    packKey: "pack-dossier-securise",
    commitmentMonths: "1",
    paymentMode: "upfront",
  }),
  null,
  "Un engagement d'un mois ne doit jamais etre converti silencieusement en mensuel.",
);

const canonicalSelection = {
  packKey: "pack-dossier-securise",
  commitmentMonths: 6,
  paymentMode: "upfront",
};
assert.deepEqual(
  publicPacksRuntime.selectionFromSearchParams(
    new URLSearchParams(
      "pack=pack-dossier-securise&commitment=6&payment=upfront",
    ),
  ),
  canonicalSelection,
);
for (const duplicatedQuery of [
  "pack=pack-dossier-securise&pack=pack-dossier-securise&commitment=6&payment=upfront",
  "pack=pack-dossier-securise&commitment=6&commitment=6&payment=upfront",
  "pack=pack-dossier-securise&commitment=6&payment=upfront&payment=upfront",
]) {
  assert.equal(
    publicPacksRuntime.selectionFromSearchParams(
      new URLSearchParams(duplicatedQuery),
    ),
    null,
    `Le doublon doit etre refuse: ${duplicatedQuery}`,
  );
}
for (const duplicatedRecord of [
  {
    pack: ["pack-dossier-securise"],
    commitment: "6",
    payment: "upfront",
  },
  {
    pack: "pack-dossier-securise",
    commitment: ["6"],
    payment: "upfront",
  },
  {
    pack: "pack-dossier-securise",
    commitment: "6",
    payment: ["upfront"],
  },
]) {
  assert.equal(
    publicPacksRuntime.selectionFromSearchParams(duplicatedRecord),
    null,
    "Les tableaux issus de searchParams doivent etre refuses, meme avec une seule valeur.",
  );
}
assert.equal(
  publicPacksRuntime.selectionFromSearchParams(
    new URLSearchParams("pack=pack-dossier-securise&commitment=1"),
  ),
  null,
  "Le paiement mensuel d'un mois doit rester explicite dans l'URL.",
);

function buildTestOffer(commitmentMonths, paymentMode, status = "active") {
  return {
    id: `offer-${commitmentMonths}-${paymentMode}`,
    externalReference: `PACK-TEST-${commitmentMonths}-${paymentMode}`,
    name: `Offer ${commitmentMonths} ${paymentMode}`,
    status,
    priceAmountCents: 1000,
    setupFeeAmountCents: 500,
    billingIntervalMonths: paymentMode === "upfront" ? commitmentMonths : 1,
  };
}

const activeCatalog = [
  buildTestOffer(1, "monthly"),
  buildTestOffer(6, "monthly"),
  buildTestOffer(6, "upfront"),
  buildTestOffer(12, "monthly"),
  buildTestOffer(12, "upfront"),
];
const activeResolvedCatalog = publicPacksRuntime.resolvePackCatalog(activeCatalog);
assert.equal(activeResolvedCatalog.length, 1);
assert.equal(
  activeResolvedCatalog[0].variantsByCommitment[6].upfront.offer.status,
  "active",
);

const inactiveUpfrontCatalog = activeCatalog.map((offer) =>
  offer.externalReference === "PACK-TEST-6-upfront"
    ? { ...offer, status: "inactive" }
    : offer,
);
const catalogWithoutInactiveVariant = publicPacksRuntime.resolvePackCatalog(
  inactiveUpfrontCatalog,
);
assert.equal(
  catalogWithoutInactiveVariant.length,
  1,
  "Une option comptant inactive ne doit pas masquer les variantes mensuelles actives.",
);
assert.equal(
  catalogWithoutInactiveVariant[0].variantsByCommitment[6].upfront,
  null,
  "Une variante inactive ne doit jamais etre exposee dans le catalogue public resolu.",
);
assert.equal(
  publicPacksRuntime.isPackSelectionUnavailable(
    catalogWithoutInactiveVariant[0],
    {
      packKey: "pack-dossier-securise",
      commitmentMonths: 6,
      paymentMode: "upfront",
    },
  ),
  true,
  "Une selection historique comptant doit rester indisponible si sa variante active a disparu.",
);
assert.equal(
  publicPacksRuntime.isPackSelectionUnavailable(
    catalogWithoutInactiveVariant[0],
    {
      packKey: "pack-dossier-securise",
      commitmentMonths: 6,
      paymentMode: "monthly",
    },
  ),
  false,
  "La variante mensuelle active ne doit pas etre marquee indisponible.",
);
const historicalUpfrontSelection = {
  packKey: "pack-dossier-securise",
  commitmentMonths: 6,
  paymentMode: "upfront",
};
const unavailableInitialState =
  publicPacksRuntime.resolvePublicPackCardSelection(
    catalogWithoutInactiveVariant[0],
    historicalUpfrontSelection,
    null,
  );
assert.deepEqual(unavailableInitialState.selection, historicalUpfrontSelection);
assert.equal(unavailableInitialState.hasActiveOverride, false);
assert.equal(
  publicPacksRuntime.isPackSelectionUnavailable(
    catalogWithoutInactiveVariant[0],
    unavailableInitialState.selection,
  ),
  true,
  "Une selection initiale comptant inactive doit rester indisponible sans fallback.",
);

const refreshedMonthlySelection = {
  packKey: "pack-dossier-securise",
  commitmentMonths: 6,
  paymentMode: "monthly",
};
const refreshedMonthlyState =
  publicPacksRuntime.resolvePublicPackCardSelection(
    catalogWithoutInactiveVariant[0],
    refreshedMonthlySelection,
    null,
  );
assert.deepEqual(
  refreshedMonthlyState.selection,
  refreshedMonthlySelection,
  "Sans interaction, une nouvelle selection initiale mensuelle doit etre refletee.",
);
assert.equal(
  publicPacksRuntime.isPackSelectionUnavailable(
    catalogWithoutInactiveVariant[0],
    refreshedMonthlyState.selection,
  ),
  false,
);

const monthlyOverride = {
  baseFingerprint: refreshedMonthlyState.baseFingerprint,
  packKey: "pack-dossier-securise",
  commitmentMonths: 12,
  paymentMode: "monthly",
};
assert.equal(
  publicPacksRuntime.resolvePublicPackCardSelection(
    catalogWithoutInactiveVariant[0],
    refreshedMonthlySelection,
    monthlyOverride,
  ).hasActiveOverride,
  true,
  "L'interaction explicite doit dominer tant que la base serveur est stable.",
);
const newerInitialSelection = {
  packKey: "pack-dossier-securise",
  commitmentMonths: 1,
  paymentMode: "monthly",
};
const stateAfterInitialSelectionChange =
  publicPacksRuntime.resolvePublicPackCardSelection(
    catalogWithoutInactiveVariant[0],
    newerInitialSelection,
    monthlyOverride,
  );
assert.equal(stateAfterInitialSelectionChange.hasActiveOverride, false);
assert.deepEqual(
  stateAfterInitialSelectionChange.selection,
  newerInitialSelection,
  "Une nouvelle selection initiale doit invalider l'ancien override.",
);
const fingerprintA =
  publicPacksRuntime.buildPublicPackSelectionBaseFingerprint(
    catalogWithoutInactiveVariant[0],
    refreshedMonthlySelection,
  );
const fingerprintB =
  publicPacksRuntime.buildPublicPackSelectionBaseFingerprint(
    catalogWithoutInactiveVariant[0],
    newerInitialSelection,
  );
const fingerprintAAfterReturn =
  publicPacksRuntime.buildPublicPackSelectionBaseFingerprint(
    catalogWithoutInactiveVariant[0],
    refreshedMonthlySelection,
  );
assert.equal(
  fingerprintA,
  fingerprintAAfterReturn,
  "Une meme base doit produire une empreinte stable apres un cycle A-B-A.",
);
assert.notEqual(
  fingerprintA,
  fingerprintB,
  "Deux selections initiales distinctes doivent produire des cles distinctes.",
);

const activePackState = publicPacksRuntime.resolvePublicPackCardSelection(
  activeResolvedCatalog[0],
  refreshedMonthlySelection,
  null,
);
const activePackOverride = {
  baseFingerprint: activePackState.baseFingerprint,
  packKey: "pack-dossier-securise",
  commitmentMonths: 12,
  paymentMode: "upfront",
};
assert.equal(
  publicPacksRuntime.resolvePublicPackCardSelection(
    catalogWithoutInactiveVariant[0],
    refreshedMonthlySelection,
    activePackOverride,
  ).hasActiveOverride,
  false,
  "Un changement des variantes actives doit invalider l'ancien override.",
);

const inactiveMonthlyCatalog = activeCatalog.map((offer) =>
  offer.externalReference === "PACK-TEST-6-monthly"
    ? { ...offer, status: "inactive" }
    : offer,
);
assert.deepEqual(
  publicPacksRuntime.resolvePackCatalog(inactiveMonthlyCatalog),
  [],
  "Un pack incomplet dont une variante mensuelle est inactive doit etre masque.",
);
assert.equal(
  publicPacksRuntime.resolvePackSelection(
    inactiveMonthlyCatalog,
    {
      packKey: "pack-dossier-securise",
      commitmentMonths: 6,
      paymentMode: "monthly",
    },
  ),
  null,
  "Une selection inactive ne doit pas etre resolue.",
);
assert.equal(
  publicPacksRuntime.buildSignupPackSnapshot(
    inactiveMonthlyCatalog,
    {
      packKey: "pack-dossier-securise",
      commitmentMonths: 6,
      paymentMode: "monthly",
    },
  ),
  null,
  "Aucun snapshot ne doit etre construit depuis une offre inactive.",
);

assert.equal(
  publicPacksRuntime.selectionToContactQueryString(
    canonicalSelection,
    "offer-6-upfront",
  ),
  "pack=pack-dossier-securise&commitment=6&payment=upfront&offer=offer-6-upfront",
  "Le CTA contact doit transporter une selection canonique complete.",
);

for (const [source, componentName] of [
  [publicPackCard, "PublicPackCard"],
  [publicPackComparison, "PublicPackComparisonTable"],
]) {
  assert.match(
    source,
    /selectionToContactQueryString/,
    `${componentName} doit utiliser le helper canonique du CTA contact.`,
  );
  assert.match(
    source,
    /href=\{`\/contact\?\$\{selectionToContactQueryString\([\s\S]*?variant\.offer\.id[\s\S]*?\)\}`\}/,
    `${componentName} doit lier le CTA contact a l'offre de la variante affichee.`,
  );
  assert.match(
    source,
    /href=\{`\/signup\?\$\{selectionToQueryString\([\s\S]*?\)\}`\}/,
    `${componentName} doit conserver le CTA signup canonique.`,
  );
}

assert.match(
  publicPackCard,
  /commitmentMonths\s*>\s*1\s*&&\s*variantGroup\.upfront/,
  "Une carte ne doit proposer le paiement comptant que si la variante existe.",
);
assert.match(
  publicPackCard,
  /comptant indisponible/,
  "Une carte sans variante comptant doit annoncer son indisponibilite.",
);
assert.match(
  publicPackCard,
  /const \[selectionOverride, setSelectionOverride\]\s*=\s*useState<PublicPackSelectionOverride \| null>\(null\)/,
  "La carte ne doit memoriser qu'un override utilisateur optionnel.",
);
const publicPackCardWrapperStart = publicPackCard.indexOf(
  "export function PublicPackCard",
);
const statefulPublicPackCardStart = publicPackCard.indexOf(
  "function StatefulPublicPackCard",
  publicPackCardWrapperStart,
);
assert.ok(
  publicPackCardWrapperStart >= 0
    && statefulPublicPackCardStart > publicPackCardWrapperStart,
  "La carte doit separer son wrapper stateless de son composant stateful.",
);
const publicPackCardWrapper = publicPackCard.slice(
  publicPackCardWrapperStart,
  statefulPublicPackCardStart,
);
assert.match(
  publicPackCardWrapper,
  /buildPublicPackSelectionBaseFingerprint\(\s*props\.pack,\s*props\.initialSelection \?\? null,\s*\)/,
);
assert.match(
  publicPackCardWrapper,
  /<StatefulPublicPackCard \{\.\.\.props\} key=\{baseFingerprint\} \/>/,
  "L'empreinte de base doit etre la cle de remount de l'etat interne.",
);
assert.doesNotMatch(
  publicPackCardWrapper,
  /useState/,
  "Le wrapper public doit rester stateless.",
);
assert.match(
  publicPackCard.slice(statefulPublicPackCardStart),
  /useState<PublicPackSelectionOverride \| null>\(null\)/,
  "L'override doit appartenir uniquement au composant interne remonte.",
);
assert.match(
  publicPackCard,
  /const cardSelection\s*=\s*resolvePublicPackCardSelection\(\s*pack,\s*initialSelection,\s*selectionOverride,\s*\)/,
  "La selection effective doit etre derivee des props courantes a chaque rendu.",
);
assert.match(
  publicPackCard,
  /const variant\s*=\s*isPackSelectionUnavailable\(pack, cardSelection\.selection\)\s*\?\s*null\s*:\s*paymentMode\s*===\s*["']upfront["']\s*\?\s*variantGroup\.upfront\s*:\s*variantGroup\.monthly/,
  "Une selection comptant indisponible ne doit jamais retomber sur la variante mensuelle.",
);
assert.doesNotMatch(
  publicPackCard,
  /useEffect|setSelectionOverride\(null\)/,
  "La synchronisation ne doit reposer sur aucun reset implicite ou effet fragile.",
);
assert.match(
  publicPackCard,
  /onClick=\{\(\)\s*=>\s*\{\s*setSelectionOverride\(\{\s*baseFingerprint:\s*cardSelection\.baseFingerprint,\s*packKey:\s*pack\.key,\s*commitmentMonths,\s*paymentMode:\s*["']monthly["'],\s*\}\);\s*\}\}[\s\S]*?>\s*Passer au mensuel\s*<\/button>/,
  "La carte doit offrir une transition mensuelle explicite sans changer l'engagement courant.",
);
const monthlyOverrideButtonStart = publicPackCard.indexOf(
  'className="button button-secondary"',
);
const monthlyOverrideButtonEnd = publicPackCard.indexOf(
  "</button>",
  monthlyOverrideButtonStart,
);
assert.ok(
  monthlyOverrideButtonStart >= 0
    && monthlyOverrideButtonEnd > monthlyOverrideButtonStart,
  "Le bouton de bascule mensuelle doit etre present.",
);
assert.doesNotMatch(
  publicPackCard.slice(monthlyOverrideButtonStart, monthlyOverrideButtonEnd),
  /initialSelection|setCommitmentMonths/,
  "Passer au mensuel doit conserver l'engagement actuellement affiche.",
);
assert.match(
  publicPackCard,
  /!variant\s*\?\s*\([\s\S]*?<strong>Indisponible<\/strong>[\s\S]*?Passer au mensuel[\s\S]*?\)\s*:\s*\([\s\S]*?formatCurrencyFromCents\(variant\.monthlyPriceAmountCents\)/,
  "Le prix mensuel ne doit etre affiche que hors de l'etat historique indisponible.",
);
assert.match(
  publicPackCard,
  /!variant\s*\?\s*null\s*:\s*\(\s*<dl className=["']public-pack-facts["']/,
  "Les faits tarifaires doivent etre masques dans l'etat historique indisponible.",
);
assert.match(
  publicPackCard,
  /!variant\s*\?\s*null\s*:\s*mode\s*===\s*["']signup["']/,
  "Une selection initiale indisponible ne doit exposer aucun CTA transactionnel.",
);
assert.match(
  publicPackComparison,
  /const variant\s*=\s*isUpfront\s*\?\s*selectedGroup\.upfront\s*:\s*selectedGroup\.monthly/,
  "Le comparatif comptant ne doit jamais substituer une variante mensuelle.",
);
const unavailableComparisonStart = publicPackComparison.indexOf("if (!variant)");
const availableComparisonStart = publicPackComparison.indexOf(
  "const displayedPriceAmountCents",
  unavailableComparisonStart,
);
assert.ok(
  unavailableComparisonStart >= 0
    && availableComparisonStart > unavailableComparisonStart,
  "Le comparatif doit traiter explicitement une variante indisponible.",
);
const unavailableComparisonBranch = publicPackComparison.slice(
  unavailableComparisonStart,
  availableComparisonStart,
);
assert.match(unavailableComparisonBranch, /Indisponible/);
assert.match(unavailableComparisonBranch, /Comptant - indisponible/);
assert.doesNotMatch(
  unavailableComparisonBranch,
  /selectionToQueryString|selectionToContactQueryString|AddRecurringCheckoutButton/,
  "Une colonne comptant indisponible ne doit exposer aucun CTA transactionnel.",
);

assert.match(offersPage, /PublicPackOverviewGrid/);
assert.match(offersPage, /PublicPackComparisonTable/);
assert.ok(
  offersPage.indexOf("<PublicPackOverviewGrid")
    < offersPage.indexOf("<PublicPackComparisonTable"),
  "La vue d'ensemble doit preceder le comparatif detaille.",
);
assert.match(publicPackOverview, /packs\.map\(\(pack\)\s*=>/);
assert.match(publicPackOverview, /<PublicPackCard/);
assert.match(publicPackOverview, /signupEnabled=\{signupEnabled\}/);
assert.match(publicPackOverview, /highlightLabel=/);

for (const role of ["table", "row", "columnheader", "rowheader", "cell"]) {
  assert.match(
    publicPackComparison,
    new RegExp(`role=["']${role}["']`),
    `Le comparatif doit exposer le role ARIA ${role}.`,
  );
}
assert.match(
  publicPackComparison,
  /aria-label=["']Comparatif[^"']*["']/,
  "Le comparatif doit fournir un nom accessible explicite.",
);
assert.match(publicPackComparison, /aria-label=\{`\$\{row\.label\} inclus`\}/);
assert.match(
  publicPackComparison,
  /aria-label=\{`\$\{row\.label\} non inclus`\}/,
);

assert.match(contactPage, /selectionFromSearchParams/);
assert.match(contactPage, /buildSignupPackSnapshot/);
assert.match(contactPage, /\.status\s*===\s*["']active["']/);
assert.match(
  contactPage,
  /\.offerId\s*===\s*[A-Za-z_$][\w$]*\??\.id|[A-Za-z_$][\w$]*\??\.id\s*===\s*[A-Za-z_$][\w$]*\.offerId/,
  "Le snapshot contact doit correspondre exactement a l'offre active transmise.",
);
assert.match(contactPage, /PublicPackSelectionSummary/);
assert.match(contactPage, /offerReference=\{offerReference\}/);
assert.doesNotMatch(
  contactPage,
  /method:\s*["'](?:POST|PUT|PATCH|DELETE)["']|requestBffJson|callInternalSignup/,
  "Le rendu GET de contact ne doit effectuer aucune mutation.",
);
assert.doesNotMatch(
  offersPage,
  /method:\s*["'](?:POST|PUT|PATCH|DELETE)["']|requestBffJson/,
  "Le rendu GET des offres ne doit effectuer aucune mutation.",
);

console.log("Vérification du contrat socle commercial V0.15 réussie.");
