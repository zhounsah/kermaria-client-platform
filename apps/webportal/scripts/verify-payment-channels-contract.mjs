import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

// Webportal artefacts
const clientDocumentPage = await read(
  "app/commercial-documents/[id]/page.tsx",
);
const clientPdfRoute = await read(
  "app/api/commercial-documents/[id]/invoice/pdf/route.ts",
);
const clientPaymentMethodRoute = await read(
  "app/api/commercial-documents/[id]/payment-method/route.ts",
);
const issuingSection = await read(
  "components/AdminInvoiceIssuingSection.tsx",
);
const markAsPaidRoute = await read(
  "app/api/admin/commercial-documents/[id]/mark-as-paid/route.ts",
);
const sendReminderRoute = await read(
  "app/api/admin/commercial-documents/[id]/send-reminder/route.ts",
);
const sendReminderButton = await read(
  "components/AdminSendReminderButton.tsx",
);
const paymentsPage = await read("app/admin/payments/page.tsx");
const emailLogPage = await read("app/admin/email-log/page.tsx");
const adminNav = await read("components/AdminNavigation.tsx");
const internalApi = await read("lib/internal-api.ts");

// Repo-level artefacts
const envExample = await read("../../.env.example");
const programCs = await read("../../apps/api-internal/Program.cs");
const invoiceIssuingCs = await read(
  "../../apps/api-internal/Services/InvoiceIssuingService.cs",
);
const billedSubscriptionTriggerCs = await read(
  "../../apps/api-internal/Services/Provisioning/BilledSubscriptionPaymentTrigger.cs",
);
const emailDispatchCs = await read(
  "../../apps/api-internal/Services/Email/EmailDispatchService.cs",
);
const emailConfigCs = await read(
  "../../apps/api-internal/Data/Configuration/EmailRuntimeConfiguration.cs",
);
const emailMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/010_email_log.sql",
);

// --- PDF client portail ---
assert.match(
  clientDocumentPage,
  /Télécharger la facture/,
  "Le bouton de téléchargement PDF client doit être présent.",
);
assert.match(
  clientDocumentPage,
  /\/api\/commercial-documents\/.+\/invoice\/pdf/,
  "Le bouton doit pointer vers la route BFF PDF client.",
);
assert.match(
  clientPdfRoute,
  /\/internal\/portal\/commercial-documents\/.+\/invoice\/pdf/,
  "La route BFF doit pointer vers l'endpoint portal interne (ownership-checked).",
);
assert.match(
  clientPdfRoute,
  /application\/pdf/,
  "La route BFF doit retourner application/pdf.",
);
assert.match(
  programCs,
  /\/internal\/portal\/commercial-documents\/\{id\}\/invoice\/pdf/,
  "L'endpoint portal interne PDF doit etre declare dans Program.cs.",
);
assert.match(
  programCs,
  /GetClientDocumentAsync/,
  "L'endpoint portal PDF doit utiliser GetClientDocumentAsync pour l'ownership.",
);
assert.match(
  programCs,
  /"\/internal\/portal\/commercial-documents\/\{id\}\/invoice"/,
  "L'endpoint portal metadata invoice doit exister (pour pdfAvailable).",
);
assert.match(
  clientDocumentPage,
  /getCommercialDocumentInvoice/,
  "La page client doit charger les metadonnees invoice pour decider du bouton PDF.",
);
assert.match(
  clientDocumentPage,
  /pdfAvailable/,
  "La page client doit conditionner le bouton PDF sur pdfAvailable.",
);
assert.match(
  clientDocumentPage,
  /Choisir votre mode de règlement/,
  "La page client doit proposer un choix explicite du mode de règlement.",
);
assert.match(
  clientDocumentPage,
  /document\.paymentMethod === "manual"/,
  "La page client doit rappeler qu'un virement sélectionné laisse la facture en attente.",
);
assert.match(
  clientDocumentPage,
  /bankTransferEnabled=/,
  "La page client doit transmettre la disponibilité du virement au composant de paiement.",
);
assert.match(
  clientPaymentMethodRoute,
  /\/internal\/portal\/commercial-documents\/.+\/payment-method/,
  "La route BFF de sélection du virement doit pointer vers l'endpoint portail interne.",
);
assert.match(
  clientPaymentMethodRoute,
  /paymentMethod: "manual"/,
  "La route BFF doit persister le choix de virement bancaire.",
);
assert.match(
  programCs,
  /EnsureInvoicePdfAsync/,
  "Les endpoints PDF doivent utiliser EnsureInvoicePdfAsync pour fetch on-demand depuis BPCE.",
);
assert.match(
  invoiceIssuingCs,
  /EnsureInvoicePdfAsync/,
  "InvoiceIssuingService doit exposer EnsureInvoicePdfAsync (cache + BPCE fallback).",
);

// --- Vue admin paiements + marquage manuel ---
assert.match(
  programCs,
  /\/internal\/admin\/commercial-documents\/\{id\}\/mark-as-paid/,
  "L'endpoint admin mark-as-paid doit exister.",
);
assert.match(
  programCs,
  /\/internal\/portal\/commercial-documents\/\{id\}\/payment-method/,
  "L'endpoint portail de sélection du virement doit exister.",
);
assert.match(
  programCs,
  /commercial_document\.payment_method_selected/,
  "Le choix client du virement doit être audité.",
);
assert.match(
  programCs,
  /admin\.commercial_documents\.mark_as_paid/,
  "L'audit admin.commercial_documents.mark_as_paid doit etre emis.",
);
assert.match(
  markAsPaidRoute,
  /mark-as-paid/,
  "La route BFF mark-as-paid doit exister.",
);
assert.match(
  issuingSection,
  /Marquer payé \(hors PayPal\)/,
  "Le bouton 'Marquer payé (hors PayPal)' doit être présent.",
);
assert.match(
  paymentsPage,
  /AdminPaymentsPage/,
  "La page admin payments doit exister.",
);
assert.match(
  paymentsPage,
  /À régler/,
  "La page payments doit afficher le total à régler.",
);
assert.match(
  paymentsPage,
  /getAdminCommercialDocuments/,
  "La page payments doit charger les documents admin.",
);
assert.match(
  adminNav,
  /\/admin\/payments/,
  "Le lien Paiements doit etre dans la navigation admin.",
);

// --- Canal e-mail transactionnel ---
assert.match(
  envExample,
  /EMAIL_INTEGRATION_MODE=disabled/,
  "EMAIL_INTEGRATION_MODE doit etre disabled par defaut dans .env.example.",
);
assert.match(
  envExample,
  /SMTP_HOST=/,
  "SMTP_HOST doit etre dans .env.example.",
);
assert.match(
  envExample,
  /SMTP_PASSWORD=\*\*REPLACE_WITH_SECURE_VALUE\*\*/,
  "SMTP_PASSWORD doit rester un placeholder dans .env.example.",
);
assert.match(
  emailConfigCs,
  /enum EmailIntegrationMode/,
  "L'enum EmailIntegrationMode doit etre defini.",
);
assert.match(
  emailConfigCs,
  /PUBLIC_PORTAL_URL/,
  "PUBLIC_PORTAL_URL doit etre lu pour les liens portail dans les emails.",
);
assert.match(
  emailDispatchCs,
  /CustomerBillingEmail/,
  "Le dispatch doit utiliser CustomerBillingEmail comme destinataire.",
);
assert.match(
  emailDispatchCs,
  /no_recipient/,
  "Le dispatch doit tracer no_recipient si billing_email est vide.",
);
assert.match(
  emailMigration,
  /CREATE TABLE.+email_messages/i,
  "La migration 010_email_log doit creer la table email_messages.",
);
assert.match(
  emailMigration,
  /related_document_id/,
  "La table email_messages doit avoir related_document_id.",
);
assert.match(
  invoiceIssuingCs,
  /SendInvoiceIssuedAsync/,
  "IssueInvoiceAsync doit declencher SendInvoiceIssuedAsync sur send_email.",
);
assert.match(
  invoiceIssuingCs,
  /SendPaymentConfirmedAsync/,
  "ConfirmPaymentAsync doit declencher SendPaymentConfirmedAsync.",
);
assert.match(
  invoiceIssuingCs,
  /_billedSubscriptions\.OnDocumentPaidAsync/,
  "ConfirmPaymentAsync doit aussi declencher les souscriptions facturees apres paiement ou mark-as-paid.",
);
assert.match(
  billedSubscriptionTriggerCs,
  /pending_payment[\s\S]*ActivateAsync[\s\S]*RecordPaymentAsync/,
  "Le trigger document paye doit activer les souscriptions facturees puis enregistrer le cycle.",
);
assert.match(
  invoiceIssuingCs,
  /TryDispatchEmailAsync/,
  "Les envois d'email doivent passer par TryDispatchEmailAsync (best-effort, swallow exceptions).",
);
assert.match(
  programCs,
  /\/internal\/admin\/commercial-documents\/\{id\}\/send-reminder/,
  "L'endpoint admin send-reminder doit exister.",
);
assert.match(
  programCs,
  /\/internal\/admin\/email-log/,
  "L'endpoint admin email-log doit exister.",
);
assert.match(
  programCs,
  /admin\.commercial_documents\.send_reminder/,
  "L'audit admin.commercial_documents.send_reminder doit etre emis.",
);
assert.match(
  sendReminderRoute,
  /send-reminder/,
  "La route BFF send-reminder doit exister.",
);
assert.match(
  sendReminderButton,
  /Envoyer une relance/,
  "Le bouton de relance doit etre present.",
);
assert.match(
  emailLogPage,
  /Journal d'envoi/,
  "La page email-log doit avoir le titre Journal d'envoi.",
);
assert.match(
  internalApi,
  /getAdminEmailLog/,
  "getAdminEmailLog doit etre exporte.",
);
assert.match(
  adminNav,
  /\/admin\/email-log/,
  "Le lien E-mails doit etre dans la navigation admin.",
);

// --- Discipline secrets ---
assert.doesNotMatch(
  envExample,
  /eyJhbGci/,
  "Aucun JWT reel ne doit apparaitre dans .env.example.",
);

console.log("Vérification du contrat canaux de paiement V0.21 réussie.");
