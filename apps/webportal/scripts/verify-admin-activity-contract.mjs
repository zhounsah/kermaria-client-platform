import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const activityRoute = await read("app/api/admin/activity/route.ts");
const adminBff = await read("lib/admin-bff.ts");
const internalApi = await read("lib/internal-api.ts");
const dashboard = await read("app/admin/page.tsx");
const activityPage = await read("app/admin/activity/page.tsx");
const filters = await read("components/AdminRequestFilters.tsx");
const attentionBadge = await read("components/RequestAttentionBadge.tsx");
const followUp = await read("components/AdminRequestFollowUp.tsx");
const sharedTypes = await read("../../packages/shared/src/index.ts");

assert.match(activityRoute, /handleAdminGet<AdminActivityOverview>/);
assert.match(activityRoute, /"\/internal\/admin\/activity"/);
assert.match(adminBff, /attentionFilters/);
assert.match(adminBff, /"to_handle"/);
assert.match(adminBff, /"client_reply"/);
assert.match(adminBff, /INVALID_REQUEST/);
assert.doesNotMatch(
  adminBff,
  /NEXT_PUBLIC_INTERNAL_API_URL|NEXT_PUBLIC_SERVICE_AUTH_TOKEN/,
);

assert.match(internalApi, /import "server-only"/);
assert.match(internalApi, /getAdminActivity/);
assert.match(internalApi, /"\/internal\/admin\/activity"/);
assert.doesNotMatch(internalApi, /NEXT_PUBLIC_INTERNAL_API_URL/);

assert.match(dashboard, /Support à traiter/);
assert.match(dashboard, /Réponses client/);
assert.match(dashboard, /href="\/admin\/activity"/);
assert.match(dashboard, /await requireAdminSession\(\)/);
assert.match(activityPage, /recentActivities/);
assert.match(activityPage, /Flux d'activit/);
assert.match(filters, /name="attention"/);
assert.match(filters, /value="to_handle"/);
assert.match(filters, /value="client_reply"/);
assert.match(attentionBadge, /hasRecentClientReply/);
assert.match(followUp, /Les notes internes restent privées/);
assert.doesNotMatch(
  dashboard + followUp,
  /INTERNAL_API_URL|SERVICE_AUTH_TOKEN|dangerouslySetInnerHTML/,
);

assert.match(sharedTypes, /interface AdminActivityOverview/);
assert.match(sharedTypes, /interface AdminActivityItem/);
assert.match(sharedTypes, /hasRecentClientReply: boolean/);
assert.match(sharedTypes, /requiresAttention: boolean/);

for (const page of [
  "app/admin/support-requests/page.tsx",
  "app/admin/service-requests/page.tsx",
]) {
  const source = await read(page);
  assert.match(source, /RequestAttentionBadge/);
  assert.match(source, /filters\.attention/);
}

for (const page of [
  "app/admin/support-requests/[id]/page.tsx",
  "app/admin/service-requests/[id]/page.tsx",
]) {
  const source = await read(page);
  assert.match(source, /AdminRequestFollowUp/);
  assert.match(source, /PublicConversation/);
  assert.match(source, /InternalNoteList/);
}

console.log("Vérification du contrat centre d'activité V0.14 réussie.");
