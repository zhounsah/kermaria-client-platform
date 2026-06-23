import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const sharedTypes = await read("../../packages/shared/src/index.ts");
const internalApi = await read("lib/internal-api.ts");
const formatters = await read("lib/formatters.ts");
const adminDocumentPage = await read("app/admin/commercial-documents/[id]/page.tsx");
const issuingSection = await read("components/AdminInvoiceIssuingSection.tsx");
const issueRoute = await read("app/api/admin/commercial-documents/[id]/issue/route.ts");
const invoiceRoute = await read("app/api/admin/commercial-documents/[id]/invoice/route.ts");
const invoicePdfRoute = await read("app/api/admin/commercial-documents/[id]/invoice/pdf/route.ts");
const envExample = await read("../../.env.example");

// Shared types : 'issued' est un statut valide
assert.match(sharedTypes, /["|']issued["|']/, "CommercialDocumentStatus doit inclure 'issued'.");

// internal-api : helpers BPCE exposés
assert.match(internalApi, /getAdminCommercialDocumentInvoice/, "getAdminCommercialDocumentInvoice doit être exporté.");
assert.match(internalApi, /BpceIssuedInvoiceInfo/, "Le type BpceIssuedInvoiceInfo doit être exporté.");

// Formatters : statut 'issued' localisé
assert.match(formatters, /issued/, "Le statut 'issued' doit être dans commercialDocumentStatus.");
assert.match(formatters, /Facture émise/, "Le label 'Facture émise' doit être présent.");

// Page admin : charge l'invoice existante et la passe au composant
assert.match(adminDocumentPage, /getAdminCommercialDocumentInvoice/, "La page admin doit charger l'invoice BPCE existante.");
assert.match(adminDocumentPage, /existingInvoice/, "La page admin doit passer existingInvoice au composant.");
assert.match(adminDocumentPage, /AdminInvoiceIssuingSection/, "La page admin doit inclure AdminInvoiceIssuingSection.");

// Composant émission
assert.match(issuingSection, /Émettre la facture BPCE/, "Le bouton d'émission doit être présent.");
assert.match(issuingSection, /fiscalNumber/, "Le numéro fiscal doit être affiché après émission.");
assert.match(issuingSection, /invoice\/pdf/, "Le lien de téléchargement PDF doit être présent.");
assert.doesNotMatch(issuingSection, /Payer|paiement en ligne|PayPal|Stripe/, "Aucun paiement en ligne ne doit être exposé.");

// Routes BFF admin
assert.match(issueRoute, /\/internal\/admin\/commercial-documents/, "La route issue doit pointer vers l'API interne.");
assert.match(invoiceRoute, /\/internal\/admin\/commercial-documents/, "La route invoice GET doit pointer vers l'API interne.");
assert.match(invoicePdfRoute, /\/internal\/admin\/commercial-documents/, "La route PDF doit pointer vers l'API interne.");
assert.match(invoicePdfRoute, /application\/pdf/, "La route PDF doit retourner application/pdf.");

// Variables d'environnement BPCE documentées
assert.match(envExample, /BPCE_INTEGRATION_MODE/, "BPCE_INTEGRATION_MODE doit être dans .env.example.");
assert.match(envExample, /BPCE_REFRESH_TOKEN/, "BPCE_REFRESH_TOKEN doit être dans .env.example.");
assert.match(envExample, /BPCE_SENDER_ID/, "BPCE_SENDER_ID doit être dans .env.example.");
assert.match(envExample, /REPLACE_WITH_SECURE_VALUE/, "Les secrets BPCE doivent rester des placeholders dans .env.example.");
assert.doesNotMatch(envExample, /eyJhbGci/, "Aucun JWT réel ne doit apparaître dans .env.example.");

console.log("Vérification du contrat facturation BPCE V0.20 réussie.");
