import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const adminBff = await read("lib/admin-bff.ts");
const internalApi = await read("lib/internal-api.ts");
const payloads = await read("lib/workflow-payloads.ts");

assert.match(adminBff, /handleAdminMutation/);
assert.match(adminBff, /session\.user\.role !== "internal_admin"/);
assert.match(adminBff, /mutateInternalAdminData/);
assert.doesNotMatch(
  adminBff,
  /localStorage|sessionStorage|NEXT_PUBLIC_INTERNAL_API_URL/,
);
assert.match(internalApi, /import "server-only"/);
assert.match(internalApi, /mutateInternalAdminData/);
assert.doesNotMatch(internalApi, /NEXT_PUBLIC_INTERNAL_API_URL/);
assert.match(payloads, /parseStatusPayload/);
assert.match(payloads, /parseRequestTextPayload/);
assert.match(payloads, /text\.length >= 3 && text\.length <= 2000/);

for (const requestType of ["support-requests", "service-requests"]) {
  const detailRoute = await read(`app/api/admin/${requestType}/[id]/route.ts`);
  const statusRoute = await read(
    `app/api/admin/${requestType}/[id]/status/route.ts`,
  );
  const notesRoute = await read(
    `app/api/admin/${requestType}/[id]/notes/route.ts`,
  );
  const messagesRoute = await read(
    `app/api/admin/${requestType}/[id]/messages/route.ts`,
  );

  assert.match(detailRoute, /handleAdminGet/);
  assert.match(statusRoute, /export async function PATCH/);
  assert.match(statusRoute, /parseStatusPayload/);
  assert.match(notesRoute, /export async function POST/);
  assert.match(notesRoute, /parseRequestTextPayload/);
  assert.match(messagesRoute, /export async function POST/);
  assert.match(messagesRoute, /parseRequestTextPayload/);
}

for (const component of [
  "StatusChangeForm",
  "InternalNoteForm",
  "PublicMessageForm",
]) {
  const source = await read(`components/${component}.tsx`);
  assert.match(source, /isSubmittingRef\.current/);
  assert.match(source, /requestBffJson/);
  assert.doesNotMatch(
    source,
    /INTERNAL_API_URL|SERVICE_AUTH_TOKEN|localStorage|sessionStorage/,
  );
}

for (const page of [
  "app/admin/support-requests/[id]/page.tsx",
  "app/admin/service-requests/[id]/page.tsx",
]) {
  const source = await read(page);
  assert.match(source, /await requireAdminSession\(\)/);
  assert.match(source, /InternalNoteForm/);
  assert.match(source, /PublicMessageForm/);
}

for (const page of [
  "app/support/[id]/page.tsx",
  "app/request-service/[id]/page.tsx",
]) {
  const source = await read(page);
  assert.match(source, /await requireClientSession\(\)/);
  assert.match(source, /RequestTimeline/);
  assert.doesNotMatch(
    source,
    /InternalNote|internalNotes|authorDisplayName/,
  );
}

console.log("Vérification du contrat workflow V0.11 réussie.");
