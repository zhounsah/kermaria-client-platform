import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import ts from "typescript";

/**
 * Contrat des documents commerciaux apres la bascule Billing V2.
 *
 * Le catalogue commercial legacy a disparu, mais les devis et factures, eux,
 * restent. Ce script garde ce qui doit rester vrai apres cette separation :
 * un document est un instantane autonome, il ne pointe plus vers une entree de
 * catalogue, et une revision tarifaire ne doit donc pas pouvoir reecrire un
 * document deja emis.
 */

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

const sharedTypes = await read("../../packages/shared/src/index.ts");
const internalApi = await read("lib/internal-api.ts");
const adminBff = await read("lib/admin-bff.ts");
const payloads = await read("lib/bff-payloads.ts");
const invoicesPage = await read("app/invoices/page.tsx");
const documentsPage = await read("app/admin/commercial-documents/page.tsx");
const documentAdminPage = await read(
  "app/admin/commercial-documents/[id]/page.tsx",
);
const documentDetailPage = await read("app/commercial-documents/[id]/page.tsx");
const invoiceTable = await read("components/InvoiceTable.tsx");
const lineForm = await read("components/AdminCommercialDocumentLineForm.tsx");
const templates = await read("lib/commercial-document-templates.ts");

// --- 1. Le contrat partage garde les documents, pas le catalogue legacy. ----

assert.match(sharedTypes, /interface CommercialDocumentDetail/);
assert.match(sharedTypes, /interface CommercialDocumentLine/);
assert.match(sharedTypes, /type CommercialDocumentStatus =/);
assert.match(sharedTypes, /type CommercialDocumentType =/);
assert.doesNotMatch(
  sharedTypes,
  /CommercialOfferSummary|commercialOfferId|offerExternalReference/,
  "Le contrat partage ne doit plus exposer le catalogue commercial legacy.",
);

// --- 2. Une ligne de document est un instantane, pas un lien. --------------

assert.doesNotMatch(
  sharedTypes,
  /interface CommercialDocumentLine[\s\S]{0,600}?offerId/,
  "Une ligne de document ne doit plus porter d'identifiant d'offre : "
    + "un devis emis doit survivre a une revision tarifaire.",
);
for (const field of [
  /label: string;/,
  /description: string(?: \| null)?;/,
  /quantity: number;/,
  /unitLabel: string;/,
  /unitPriceCents: number;/,
]) {
  assert.match(
    sharedTypes.slice(sharedTypes.indexOf("interface CommercialDocumentLine")),
    field,
    `Une ligne de document doit porter son propre ${field}.`,
  );
}

assert.match(payloads, /parseCommercialDocumentPayload/);
assert.match(payloads, /parseCommercialDocumentLinePayload/);
assert.doesNotMatch(
  payloads,
  /parseCommercialOfferPayload/,
  "Le BFF ne doit plus valider de payload d'offre commerciale legacy.",
);
assert.doesNotMatch(
  payloads.slice(payloads.indexOf("parseCommercialDocumentLinePayload")),
  /offerId/,
  "Le payload de ligne ne doit plus accepter d'identifiant d'offre.",
);

// --- 3. Le catalogue ne fait que pre-remplir la saisie. --------------------

assert.match(
  templates,
  /export function buildCatalogLineTemplates/,
  "Le pre-remplissage catalogue doit passer par un constructeur de gabarits.",
);
assert.match(
  templates,
  /BillingV2AdminCatalogSnapshot/,
  "Les gabarits de ligne doivent venir du catalogue Billing V2.",
);
assert.match(
  lineForm,
  /templates: readonly CatalogLineTemplate\[\]/,
  "Le formulaire de ligne doit recevoir des gabarits, pas des offres.",
);
assert.doesNotMatch(
  lineForm,
  /offerId/,
  "Le formulaire de ligne ne doit plus transporter d'identifiant d'offre.",
);
assert.match(
  documentAdminPage,
  /buildCatalogLineTemplates/,
  "L'ecran document doit construire les gabarits depuis le catalogue V2.",
);
assert.match(
  documentAdminPage,
  /catalogResult\.error\s*\?\s*\[\]/,
  "Un catalogue indisponible ne doit pas empecher la saisie manuelle d'une ligne.",
);

// Le gabarit doit copier des valeurs, jamais poser un lien durable.
// `import type` est efface a la transpilation : le module est executable tel
// quel, sans avoir a resoudre `@/lib/internal-api`.
const templateModule = await importPureTypeScript(
  templates,
  "commercial-document-templates.ts",
);
const now = new Date("2026-08-23T12:00:00.000Z");
const price = (id, amountCents, status, validFrom, validUntil, version) => ({
  id,
  priceCode: `STORAGE-PERSONAL-8-MONTHLY-EUR-V${version}`,
  amountCents,
  currency: "EUR",
  billingCadence: "monthly",
  chargeTrigger: "subscription",
  taxRateBasisPoints: null,
  status,
  validFrom,
  validUntil,
  version,
});

const snapshot = {
  source: "mariadb",
  editable: true,
  currency: "EUR",
  services: [
    {
      id: "svc-1",
      code: "STORAGE-PERSONAL",
      name: "Stockage personnel",
      description: "Espace personnel",
      category: "Stockage",
      status: "active",
      flatPrices: [],
      tiers: [
        {
          id: "tier-1",
          code: "STORAGE-PERSONAL-8",
          name: "8 Go",
          publicLabel: "8 Go",
          status: "active",
          prices: [
            // En vigueur : le seul gabarit attendu.
            price("price-active", 900, "active", "2026-01-01T00:00:00.000Z", null, 2),
            // Version close : elle appartient aux documents qu'elle a produits.
            price(
              "price-superseded",
              700,
              "superseded",
              "2025-01-01T00:00:00.000Z",
              "2026-01-01T00:00:00.000Z",
              1,
            ),
            // Version programmee : un document emis aujourd'hui ne peut pas
            // s'appuyer sur un tarif qui n'a pas encore pris effet.
            price("price-future", 1100, "active", "2027-01-01T00:00:00.000Z", null, 3),
          ],
        },
      ],
    },
  ],
  presets: [],
  commitments: [],
};

const built = templateModule.buildCatalogLineTemplates(snapshot, now);
assert.equal(
  built.length,
  1,
  "Seul un tarif effectivement en vigueur doit devenir un gabarit.",
);
assert.equal(built[0].unitPriceCents, 900);
assert.equal(built[0].priceCode, "STORAGE-PERSONAL-8-MONTHLY-EUR-V2");
assert.equal(built[0].label, "Stockage personnel — 8 Go");
assert.ok(
  !Object.prototype.hasOwnProperty.call(built[0], "priceId")
    && !Object.prototype.hasOwnProperty.call(built[0], "offerId"),
  "Un gabarit copie des valeurs : il ne doit pas exposer d'identifiant a stocker.",
);

// --- 4. Frontieres serveur inchangees. ------------------------------------

assert.match(internalApi, /import "server-only"/);
assert.match(internalApi, /getCommercialDocuments/);
assert.match(internalApi, /getAdminCommercialDocuments/);
assert.match(internalApi, /getAdminCommercialDocument/);
assert.match(internalApi, /"\/internal\/portal\/commercial-documents"/);
assert.match(internalApi, /"\/internal\/admin\/commercial-documents"/);
assert.doesNotMatch(
  internalApi,
  /getCommercialCatalog|getPublicCommercialCatalog|getAdminCatalog\b/,
  "Le BFF ne doit plus lire de catalogue commercial legacy.",
);
assert.doesNotMatch(
  internalApi,
  /"\/internal\/portal\/catalog"|"\/internal\/admin\/catalog"/,
  "Les anciennes routes catalogue ne doivent plus etre appelees.",
);

assert.match(adminBff, /handleAdminMutation</);
assert.match(adminBff, /INVALID_REQUEST/);

const routeFiles = [
  "app/api/commercial-documents/route.ts",
  "app/api/commercial-documents/[id]/route.ts",
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
    `${file} ne doit pas exposer la frontiere serveur au navigateur.`,
  );
  assert.doesNotMatch(
    source,
    /offerId/,
    `${file} ne doit plus relayer d'identifiant d'offre legacy.`,
  );
}

// --- 5. Nature informative des documents cote client. ---------------------

assert.match(
  invoicesPage,
  /Vos documents commerciaux et factures émises\./,
);
assert.doesNotMatch(invoicesPage, /Payer|PayPal|Stripe/);
assert.doesNotMatch(documentsPage, /Payer|paiement en ligne|PayPal|Stripe/);
assert.match(invoiceTable, /Informations indicatives/);
assert.match(documentDetailPage, /isIssued \? \(/);
assert.doesNotMatch(
  [invoicesPage, documentsPage, invoiceTable].join("\n"),
  /href="\/pay"|>\s*Payer\s*</,
);

console.log("Vérification du contrat des documents commerciaux réussie.");
