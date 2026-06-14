import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const portalBff = await read("lib/portal-bff.ts");
const internalApi = await read("lib/internal-api.ts");
const replyForm = await read("components/ClientReplyForm.tsx");
const conversation = await read("components/PublicConversation.tsx");
const sharedTypes = await read("../../packages/shared/src/index.ts");

assert.match(portalBff, /handlePortalPayloadMutation/);
assert.match(portalBff, /session\.user\.role !== "client_user"/);
assert.match(portalBff, /mutateInternalPortalPayload/);
assert.doesNotMatch(
  portalBff,
  /NEXT_PUBLIC_INTERNAL_API_URL|NEXT_PUBLIC_SERVICE_AUTH_TOKEN/,
);

assert.match(internalApi, /import "server-only"/);
assert.match(internalApi, /mutateInternalPortalPayload/);
assert.match(internalApi, /\[PORTAL_SESSION_HEADER\]: sessionToken/);
assert.doesNotMatch(internalApi, /NEXT_PUBLIC_INTERNAL_API_URL/);

for (const requestType of ["support-requests", "service-requests"]) {
  const route = await read(`app/api/${requestType}/[id]/messages/route.ts`);
  assert.match(route, /export async function POST/);
  assert.match(route, /parseRequestTextPayload/);
  assert.match(route, /isValidPortalIdentifier/);
  assert.match(route, /handlePortalPayloadMutation/);
}

assert.match(replyForm, /isSubmittingRef\.current/);
assert.match(replyForm, /normalized\.length < 3 \|\| normalized\.length > 2000/);
assert.match(replyForm, /requestBffJson/);
assert.match(replyForm, /router\.refresh\(\)/);
assert.doesNotMatch(
  replyForm,
  /INTERNAL_API_URL|SERVICE_AUTH_TOKEN|localStorage|sessionStorage|dangerouslySetInnerHTML/,
);

assert.match(conversation, /message\.authorType/);
assert.match(conversation, /Support Kermaria/);
assert.match(conversation, /Réponse client/);
assert.doesNotMatch(conversation, /dangerouslySetInnerHTML/);

for (const page of [
  "app/support/[id]/page.tsx",
  "app/request-service/[id]/page.tsx",
]) {
  const source = await read(page);
  assert.match(source, /await requireClientSession\(\)/);
  assert.match(source, /PublicConversation/);
  assert.match(source, /ClientReplyForm/);
  assert.doesNotMatch(
    source,
    /InternalNote|internalNotes|authorDisplayName/,
  );
}

for (const page of [
  "app/admin/support-requests/[id]/page.tsx",
  "app/admin/service-requests/[id]/page.tsx",
]) {
  const source = await read(page);
  assert.match(source, /PublicConversation/);
  assert.match(source, /InternalNoteList/);
}

assert.match(
  sharedTypes,
  /authorType: "admin" \| "client"/,
);

console.log("Vérification du contrat réponses client V0.13 réussie.");
