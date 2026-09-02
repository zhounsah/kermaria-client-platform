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

const servicesPage = await read("app/services/page.tsx");
assert.match(servicesPage, /getClientVps\(\)/);
assert.match(servicesPage, /Voir mon VPS/);
assert.match(servicesPage, /\/services\/vps\/\$\{encodeURIComponent\(item\.id\)\}/);
assert.doesNotMatch(servicesPage, /mapping technique caché/i);

const detailPage = await read("app/services/vps/[id]/page.tsx");
assert.match(detailPage, /getClientVpsDetail\(id\)/);
assert.match(detailPage, /Adresse IP publique/);
assert.match(detailPage, /Votre VPS est en service/);
assert.doesNotMatch(detailPage, /infrastructureTarget|operationalNotes|provider/i);

const subscriptionsPage = await read("app/profile/subscriptions/page.tsx");
assert.doesNotMatch(subscriptionsPage, /Souscription Billing V2/i);

console.log("Contrat de projection VPS client vérifié.");
