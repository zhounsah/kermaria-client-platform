import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

for (const component of [
  "LoadingState",
  "ErrorState",
  "FormMessage",
  "SubmitButton",
  "SectionCard",
]) {
  await read(`components/${component}.tsx`);
}

const loadingPage = await read("app/loading.tsx");
const errorPage = await read("app/error.tsx");
const clientApi = await read("lib/client-api.ts");
const formValidation = await read("lib/form-validation.ts");
const internalApi = await read("lib/internal-api.ts");
const styles = await read("app/globals.css");
const passwordPage = await read("app/password/page.tsx");
const invoiceTable = await read("components/InvoiceTable.tsx");

assert.match(loadingPage, /LoadingState/);
assert.match(errorPage, /ErrorState/);
assert.match(errorPage, /reset/);
assert.doesNotMatch(errorPage, /error\.message|error\.stack|console\./);

assert.match(clientApi, /DEFAULT_TIMEOUT_MS/);
assert.match(clientApi, /parseJsonSafely/);
assert.match(clientApi, /userMessageFor/);
assert.match(clientApi, /AbortController/);
assert.doesNotMatch(
  clientApi,
  /INTERNAL_API_URL|SERVICE_AUTH_TOKEN|localStorage|sessionStorage|console\./,
);

assert.match(formValidation, /validateLoginPayload/);
assert.match(formValidation, /validateSupportRequest/);
assert.match(formValidation, /validateServiceRequest/);

assert.match(internalApi, /INTERNAL_API_TIMEOUT_MS/);
assert.match(internalApi, /readInternalJson/);
assert.match(internalApi, /AbortSignal\.timeout/);

assert.match(styles, /prefers-reduced-motion/);
assert.match(styles, /\.invoice-table td::before/);
assert.match(styles, /\.field-error/);
assert.match(styles, /\.loading-state/);
assert.match(styles, /\.error-state/);

assert.doesNotMatch(passwordPage, /type="password"|getAdHealth/);
assert.match(passwordPage, /Aucune communication Active Directory réelle/);

assert.match(invoiceTable, /className="invoice-table"/);
assert.match(invoiceTable, /data-label=/);
assert.match(invoiceTable, /Informations indicatives/);

for (const page of [
  "dashboard",
  "services",
  "invoices",
  "support",
  "request-service",
  "profile",
]) {
  const source = await read(`app/${page}/page.tsx`);
  assert.match(
    source,
    /ErrorState/,
    `La page /${page} doit distinguer une erreur d'un état vide.`,
  );
}

for (const component of [
  "LoginForm",
  "SupportRequestForm",
  "ServiceRequestForm",
]) {
  const source = await read(`components/${component}.tsx`);
  assert.match(source, /isSubmittingRef\.current/);
  assert.match(source, /requestBffJson/);
  assert.doesNotMatch(
    source,
    /INTERNAL_API_URL|SERVICE_AUTH_TOKEN|localStorage|sessionStorage/,
  );
}

console.log("Vérification du contrat UX client V0.10 réussie.");
