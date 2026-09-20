import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = resolve(fileURLToPath(new URL(".", import.meta.url)));
const read = (path) => readFile(resolve(here, path), "utf8");
const migration = await read("../../api-internal/Migrations/MariaDb/091_billing_v2_preset_cart_metadata.sql");
const publicCatalog = await read("../../api-internal/Services/BillingV2PublicCatalogService.cs");
const presetReader = await read("../../api-internal/Services/BillingV2PresetItemReader.cs");
const nativeResolver = await read("../../api-internal/Services/BillingV2NativeSelectionResolver.cs");
const cartService = await read("../../api-internal/Services/BillingV2CartService.cs");
const adminService = await read("../../api-internal/Services/BillingV2CatalogAdministrationService.cs");
const adminContract = await read("../../api-internal/Contracts/BillingV2CatalogAdminContracts.cs");
const cartConfigurator = await read("../components/BillingV2CartFormuleConfigurator.tsx");

// La projection legacy est centralisee a l'API. Le configurateur historique ne
// reçoit donc pas selected_by_default et ne peut pas l'interpréter lui-même.
for (const [name, source] of [
  ["catalogue public", publicCatalog],
  ["lecteur checkout preset", presetReader],
  ["resolveur native legacy/direct", nativeResolver],
]) {
  assert.match(source, /selected_by_default\s*=\s*1/,
    `${name} doit exclure les options Cart non sélectionnées.`);
}

// Fixture declarative post-091 : les quatre defaults historiques, puis des
// options OFF. La projection legacy doit être strictement identique.
const defaults = {
  "pack-dossier-securise": ["BASE-SERVICE", "STORAGE-PERSONAL:32", "BACKUP-PERSONAL:32"],
  "pack-acces-distance": ["BASE-SERVICE", "STORAGE-PERSONAL:32", "BACKUP-PERSONAL:32", "VPN-ACCESS:ESSENTIAL"],
  "pack-bureau-windows-distance": ["BASE-SERVICE", "STORAGE-PERSONAL:64", "BACKUP-PERSONAL:64", "VPN-ACCESS:PLUS", "RDS-ACCESS"],
  "pack-pro-association": ["BASE-SERVICE", "STORAGE-PERSONAL:64", "BACKUP-PERSONAL:64", "VPN-ACCESS:PLUS", "STORAGE-SHARED:128", "BACKUP-SHARED:128", "USER-ADDITIONAL", "SUPPORT-PLUS"],
};
for (const [preset, before] of Object.entries(defaults)) {
  const post091 = [
    ...before.map((component) => ({ component, selectedByDefault: true })),
    { component: "VPN-ACCESS:PERFORMANCE", selectedByDefault: false },
    { component: "USER-ADDITIONAL", selectedByDefault: false },
  ];
  assert.deepEqual(post091.filter((item) => item.selectedByDefault).map((item) => item.component), before,
    `${preset}: les options OFF ne changent pas la composition legacy initiale.`);
}

// required -> selected est le seul invariant : selected reste compatible avec
// une option retirables, indispensable pour les toggles historiques.
for (const [required, selected, valid] of [[false, false, true], [false, true, true], [true, true, true], [true, false, false]]) {
  assert.equal(!required || selected, valid,
    `Invariant required=${required}/selected=${selected}.`);
}
assert.match(migration, /required_item = 0 OR selected_by_default = 1/);
const requiredMatrix = {
  "pack-dossier-securise": { "BASE-SERVICE": [1, 1], "STORAGE-PERSONAL": [1, 1], "BACKUP-PERSONAL": [1, 0] },
  "pack-acces-distance": { "BASE-SERVICE": [1, 1], "STORAGE-PERSONAL": [1, 1], "BACKUP-PERSONAL": [1, 0], "VPN-ACCESS": [1, 0] },
  "pack-bureau-windows-distance": { "BASE-SERVICE": [1, 1], "STORAGE-PERSONAL": [1, 1], "BACKUP-PERSONAL": [1, 0], "VPN-ACCESS": [1, 0], "RDS-ACCESS": [1, 0] },
  "pack-pro-association": { "BASE-SERVICE": [1, 1], "STORAGE-PERSONAL": [1, 1], "BACKUP-PERSONAL": [1, 0], "VPN-ACCESS": [1, 0], "STORAGE-SHARED": [1, 0], "BACKUP-SHARED": [1, 0], "USER-ADDITIONAL": [1, 0], "SUPPORT-PLUS": [1, 0] },
};
for (const services of Object.values(requiredMatrix)) {
  for (const [service, [before, after]] of Object.entries(services)) {
    assert.equal(before, 1, "Les lignes seed 048 étaient des composants initiaux required.");
    assert.equal(after, ["BASE-SERVICE", "STORAGE-PERSONAL"].includes(service) ? 1 : 0,
      "091 ne garde fixes que le socle et le stockage personnel.");
  }
}
assert.match(migration, /WHEN service\.code IN \('BASE-SERVICE', 'STORAGE-PERSONAL'\) THEN 1/);

// La migration est recuperable : les colonnes/contraintes et les INSERT sont
// idempotents et les lignes seedées sont recherchées avant insertion.
assert.match(migration, /ADD COLUMN IF NOT EXISTS selected_by_default/);
assert.match(migration, /ADD CONSTRAINT IF NOT EXISTS chk_billing_v2_preset_item_required_selected/);
assert.equal((migration.match(/AND NOT EXISTS \(/g) ?? []).length, 4,
  "Chaque seed de 091 doit être idempotent.");
const alter = migration.indexOf("ADD COLUMN IF NOT EXISTS");
const backfill = migration.indexOf("SET item.selected_by_default = 1");
const seed = migration.indexOf("Options plates explicites");
const check = migration.indexOf("ADD CONSTRAINT IF NOT EXISTS chk_billing_v2_preset_item_required_selected");
assert.ok(alter >= 0 && alter < backfill && backfill < seed && seed < check,
  "091 doit ajouter, normaliser, seed puis poser les contraintes dans cet ordre.");

// Un service à paliers multiples est une seule option commerciale : le rendu
// groupe service+scope et l'API n'accepte un nouveau palier que si une ligne
// soeur du preset l'autorise. source_preset_item_id reste l'ancre d'option.
assert.match(cartConfigurator, /reduce<Record<string, NonNullable<typeof cart\.presetDefinition>>>/);
assert.match(cartConfigurator, /tierSelectorLabel/);
assert.match(cartConfigurator, /Choisir une option/);
assert.match(cartConfigurator, /item\.requiredItem !== true && !quantityEditable/);
assert.match(cartService, /ligne soeur du meme preset autorise explicitement ce palier/);
assert.match(cartService, /allowed\.tier_id <=> @tier_id/);

// L'admin lit les métadonnées, et son UPDATE ne remplace jamais les bornes
// lorsqu'aucune mutation de quantité n'est demandée. Un round-trip sans
// modification conserve donc 0, 1 et 10 de USER-ADDITIONAL.
assert.match(adminContract, /bool SelectedByDefault,\s*int MinimumQuantity,\s*int MaximumQuantity/s);
assert.match(adminService, /item\.minimum_quantity, item\.maximum_quantity/);
assert.doesNotMatch(adminService, /SET[\s\S]*minimum_quantity\s*=/,
  "L'admin ne doit pas écraser les bornes de quantité au round-trip.");

console.log("Préflight 091 legacy : projection, contraintes, tiers et round-trip admin vérifiés.");
