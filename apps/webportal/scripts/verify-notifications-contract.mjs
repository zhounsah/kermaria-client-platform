import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const portalBff = await read("lib/portal-bff.ts");
const internalApi = await read("lib/internal-api.ts");
const notificationCenter = await read("components/NotificationCenter.tsx");
const notificationPage = await read("app/notifications/page.tsx");
const dashboard = await read("app/dashboard/page.tsx");
const navigation = await read("components/PortalNavigation.tsx");
const sharedTypes = await read("../../packages/shared/src/index.ts");

assert.match(portalBff, /import "server-only"/);
assert.match(portalBff, /session\.user\.role !== "client_user"/);
assert.match(portalBff, /getInternalPortalData/);
assert.match(portalBff, /mutateInternalPortalData/);
assert.match(portalBff, /\^\[A-Za-z0-9-\]\{1,100\}\$/);
assert.doesNotMatch(
  portalBff,
  /NEXT_PUBLIC_INTERNAL_API_URL|NEXT_PUBLIC_SERVICE_AUTH_TOKEN/,
);

assert.match(internalApi, /getNotifications/);
assert.match(internalApi, /\/internal\/portal\/notifications/);
assert.doesNotMatch(internalApi, /NEXT_PUBLIC_INTERNAL_API_URL/);

const listRoute = await read("app/api/notifications/route.ts");
const readRoute = await read("app/api/notifications/[id]/read/route.ts");
const readAllRoute = await read("app/api/notifications/read-all/route.ts");
assert.match(listRoute, /export function GET/);
assert.match(readRoute, /export async function POST/);
assert.match(readRoute, /isValidPortalIdentifier/);
assert.match(readAllRoute, /export function POST/);

assert.match(notificationPage, /await requireClientSession\(\)/);
assert.match(notificationPage, /NotificationCenter/);
assert.match(notificationCenter, /mutationInProgress\.current/);
assert.match(notificationCenter, /requestBffJson/);
assert.match(notificationCenter, /safeNotificationLink/);
assert.doesNotMatch(
  notificationCenter,
  /INTERNAL_API_URL|SERVICE_AUTH_TOKEN|localStorage|sessionStorage|dangerouslySetInnerHTML/,
);

assert.match(dashboard, /getNotifications/);
assert.match(dashboard, /Activité récente/);
assert.match(navigation, /href: "\/notifications"/);
assert.match(sharedTypes, /interface PortalNotificationSummary/);
assert.doesNotMatch(
  sharedTypes.match(
    /export interface PortalNotificationSummary \{[\s\S]*?\n\}/,
  )?.[0] ?? "",
  /InternalRequestNote|internalNotes|password|token/i,
);

console.log("Vérification du contrat notifications V0.12 réussie.");
