import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
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
  /Les documents affichés dans cet espace sont informatifs tant que la facturation réelle n’est pas activée\./,
);
assert.match(
  invoicesPage,
  /Document informatif [-—] ne constitue pas une facture officielle\./,
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
assert.match(
  documentsPage,
  /Aucun paiement n(?:'|&apos;)est possible depuis le portail\./,
);
assert.match(documentDetailPage, /Aucun paiement n&apos;est possible depuis le portail\./);
assert.match(servicesPage, /Catalogue informatif/);
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

console.log("Vérification du contrat socle commercial V0.15 réussie.");
