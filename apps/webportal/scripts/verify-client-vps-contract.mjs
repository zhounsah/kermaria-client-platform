import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const shared = await read("../../packages/shared/src/index.ts");
assert.match(shared, /export interface ClientVpsSummary/);
assert.match(shared, /export interface ClientVpsDetail extends ClientVpsSummary/);
assert.match(shared, /export interface ClientVpsSpecifications/);

const api = await read("../../apps/api-internal/Program.cs");
assert.match(api, /"\/internal\/portal\/vps"/);
assert.match(api, /"\/internal\/portal\/vps\/\{id\}"/);
assert.match(api, /ResolveClientSessionAsync\([\s\S]*?IClientVpsService/);

const service = await read("../../apps/api-internal/Services/ClientVpsService.cs");
assert.match(service, /WHERE request_row\.customer_id = @customer_id/);
assert.match(service, /AND request_row\.id = @vps_id/);
assert.match(service, /event_row\.settlement_status = 'settled'/);
assert.match(service, /"active" => "active"/);
assert.doesNotMatch(service, /request_row\.infrastructure_target/);
assert.doesNotMatch(service, /request_row\.instance_reference/);
assert.doesNotMatch(service, /request_row\.operational_notes/);
assert.doesNotMatch(service, /provider_[a-z_]*id/i);

const clientServiceCatalog = await read("../../apps/api-internal/Services/ClientServiceCatalogService.cs");
const buildServiceSummaryStart = clientServiceCatalog.indexOf(
  "private static ServiceSummary BuildServiceSummary",
);
const buildServiceSummaryEnd = clientServiceCatalog.indexOf(
  "private static string ResolvePortalStatus",
  buildServiceSummaryStart,
);
assert.notEqual(buildServiceSummaryStart, -1);
assert.notEqual(buildServiceSummaryEnd, -1);
const buildServiceSummary = clientServiceCatalog.slice(
  buildServiceSummaryStart,
  buildServiceSummaryEnd,
);
assert.match(
  clientServiceCatalog,
  /private const string ClientSubscriptionScope = "Inclus dans votre souscription";/,
);
assert.match(
  buildServiceSummary,
  /startedAt,\s*ClientSubscriptionScope,\s*ClientSubscriptionScope,/,
);
assert.doesNotMatch(clientServiceCatalog, /Couvert via/);
assert.doesNotMatch(buildServiceSummary, /SourceLabel|sourceLabels/);

const servicesPage = await read("app/services/page.tsx");
assert.match(servicesPage, /getClientVps\(\)/);
assert.match(servicesPage, /Voir mon VPS/);
assert.match(servicesPage, /\/services\/vps\/\$\{encodeURIComponent\(item\.id\)\}/);
assert.doesNotMatch(servicesPage, /mapping technique caché/i);

const serviceCard = await read("components/ServiceCard.tsx");
assert.doesNotMatch(serviceCard, /commercialTerms/);
assert.doesNotMatch(serviceCard, /Billing V2|Subscription Billing V2|Souscription Billing V2|Couvert via/i);
assert.match(serviceCard, /<span>\{service\.scope\}<\/span>/);

const detailPage = await read("app/services/vps/[id]/page.tsx");
assert.match(detailPage, /getClientVpsDetail\(id\)/);
assert.match(detailPage, /Adresse IP publique/);
assert.match(detailPage, /Votre VPS est en service/);
assert.doesNotMatch(detailPage, /infrastructureTarget|operationalNotes|provider/i);

const subscriptionsPage = await read("app/profile/subscriptions/page.tsx");
assert.doesNotMatch(subscriptionsPage, /Billing V2|Subscription Billing V2|Souscription Billing V2/i);

const adminVpsPage = await read("app/admin/vps/page.tsx");
assert.match(adminVpsPage, /const isActive = item\.provisioningStatus === "active";/);
assert.match(
  adminVpsPage,
  /\{!isActive \? \(\s*<StatusBadge label=\{status\.label\} tone=\{status\.tone\} \/>\s*\) : null\}/,
  "Un VPS actif ne doit pas conserver le badge technique préparatoire.",
);
assert.match(
  adminVpsPage,
  /technicalStatus === "approved"\s*\?\s*\{ label: "Prêt à provisionner"/,
  "Une demande approuvée non active doit continuer à pouvoir être affichée prête à provisionner.",
);

console.log("Contrat de copy client et des états VPS vérifié.");
