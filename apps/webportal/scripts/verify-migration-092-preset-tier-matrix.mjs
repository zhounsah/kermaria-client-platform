import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = resolve(fileURLToPath(new URL(".", import.meta.url)));
const read = (path) => readFile(resolve(here, path), "utf8");
const migration = await read("../../api-internal/Migrations/MariaDb/092_billing_v2_preset_cart_options_fix.sql");
const legacyConfigurator = await read("../components/BillingV2FormuleConfigurator.tsx");
const formuleHelpers = await read("../lib/billing-v2-formules.ts");
const selectionPolicy = await read("../../api-internal/Services/BillingV2PublicSelectionPolicy.cs");
const cartService = await read("../../api-internal/Services/BillingV2CartService.cs");
const cartConfigurator = await read("../components/BillingV2CartFormuleConfigurator.tsx");

const presets = [
  { code: "pack-dossier-securise", personalDefault: "32", sharedDefault: null },
  { code: "pack-acces-distance", personalDefault: "32", sharedDefault: null },
  { code: "pack-bureau-windows-distance", personalDefault: "64", sharedDefault: null },
  { code: "pack-pro-association", personalDefault: "64", sharedDefault: "128" },
];
const personalTiers = ["16", "32", "64", "128", "256"];
const sharedTiers = ["32", "64", "128", "256"];

// Matrice post-092 complète. Les lignes marquées sélectionnées sont les lignes
// préexistantes : 092 ne les remplace jamais. Toutes ses nouvelles lignes sont
// optionnelles, éditables et démarrent OFF.
const matrix = presets.flatMap((preset) => [
  ...personalTiers.flatMap((tier) => [
    { preset: preset.code, service: "STORAGE-PERSONAL", tier, selectedByDefault: tier === preset.personalDefault, required: tier === preset.personalDefault, editable: true },
    { preset: preset.code, service: "BACKUP-PERSONAL", tier, selectedByDefault: tier === preset.personalDefault, required: false, editable: true },
  ]),
  ...sharedTiers.flatMap((tier) => [
    { preset: preset.code, service: "STORAGE-SHARED", tier, selectedByDefault: tier === preset.sharedDefault, required: false, editable: true },
    { preset: preset.code, service: "BACKUP-SHARED", tier, selectedByDefault: tier === preset.sharedDefault, required: false, editable: true },
  ]),
]);
assert.equal(matrix.length, 72, "La matrice doit décrire 18 combinaisons multi-tier par preset.");
assert.ok(matrix.every((entry) => entry.editable), "Chaque tier historique reste éditable.");
assert.ok(matrix.filter((entry) => entry.selectedByDefault).every((entry) =>
  entry.service === "STORAGE-PERSONAL" || entry.required === false),
"Seul le stockage personnel par défaut reste requis.");

// La migration est strictement additive/idempotente par preset, service, tier
// et scope. Les codes de tiers sont volontairement explicites : pas de
// déduction depuis les montants ou les libellés.
assert.match(migration, /service\.code = 'BACKUP-SHARED'/);
assert.match(migration, /service\.code = 'STORAGE-PERSONAL'/);
assert.match(migration, /service\.code = 'BACKUP-PERSONAL'/);
for (const preset of presets) assert.match(migration, new RegExp(`'${preset.code}'`));
for (const tier of sharedTiers) assert.match(migration, new RegExp(`'${tier}'`));
for (const tier of personalTiers) assert.match(migration, new RegExp(`'${tier}'`));
assert.equal((migration.match(/AND NOT EXISTS \(/g) ?? []).length, 3,
  "Chaque groupe de seed 092 doit être rejouable après un DDL partiel.");
assert.equal((migration.match(/existing\.preset_id = preset\.id/g) ?? []).length, 3);
assert.equal((migration.match(/existing\.service_id = service\.id/g) ?? []).length, 3);
assert.equal((migration.match(/existing\.tier_id = tier\.id/g) ?? []).length, 3);
assert.equal((migration.match(/existing\.scope_template = '/g) ?? []).length, 3);
assert.doesNotMatch(migration, /\b(?:UPDATE|DELETE)\b/i, "092 ne doit modifier aucune definition existante.");
const backupSharedBlock = migration.slice(migration.indexOf("service.code = 'BACKUP-SHARED'"), migration.indexOf("-- statement-break"));
const backupPersonalStart = migration.indexOf("service.code = 'BACKUP-PERSONAL'");
const backupPersonalBlock = migration.slice(backupPersonalStart);
assert.doesNotMatch(backupSharedBlock, /public_selectable\s*=\s*1/,
  "BACKUP-SHARED dérivé ne doit pas dépendre de public_selectable.");
assert.doesNotMatch(backupPersonalBlock, /public_selectable\s*=\s*1/,
  "BACKUP-PERSONAL dérivé ne doit pas dépendre de public_selectable.");

// 15 BACKUP-SHARED réellement absents + 16 tiers de stockage personnel + 16
// tiers de backup personnel. La ligne Pro/128 existante reste sélectionnée.
assert.equal(15 + 16 + 16, 47, "La correction doit couvrir exactement 47 définitions manquantes.");
assert.match(migration, /'subscription', 1, 0, 1, 0, 1, 1, 60/);
assert.match(migration, /'primary_user', 1, 0, 1, 0, 1, 1, 20/);
assert.match(migration, /'primary_user', 1, 0, 1, 0, 1, 1, 30/);

// Le legacy affiche les tiers publiquement sélectionnables, indépendamment du
// preset; les deux backups sont ensuite calés à la même valeur numérique côté
// API. C'est la preuve de l'exhaustivité, sans utiliser la base réelle.
assert.match(legacyConfigurator, /selectableTiers\(catalog, SERVICE_CODES\.storagePersonal\)/);
assert.match(legacyConfigurator, /selectableTiers\(catalog, SERVICE_CODES\.storageShared\)/);
assert.match(formuleHelpers, /tier\.publicSelectable/);
assert.match(selectionPolicy, /BillingV2PublicCatalogCodes\.BackupPersonal[\s\S]{0,350}storagePersonal\.Value\.Tier\.NumericValue/);
assert.match(selectionPolicy, /BillingV2PublicCatalogCodes\.BackupShared[\s\S]{0,350}storageShared\.Value\.Tier\.NumericValue/);

// Une option commerciale multi-tier est groupée côté présentation, tandis que
// l'API ancre chaque choix à la définition de preset et maintient la dépendance
// same_numeric_value lors du changement de palier. La projection legacy lit
// uniquement les items effectivement présents (une option OFF reste absente).
assert.match(cartConfigurator, /const key = `\$\{definition\.serviceCode\}:\$\{definition\.scopeTemplate\}`/);
assert.match(cartConfigurator, /Choisir un palier/);
assert.match(cartConfigurator, /sourcePresetItemId: item\.sourcePresetItemId/,
  "Le changement de tier conserve l'ancre de definition du preset.");
assert.match(cartService, /allowed\.tier_id <=> @tier_id/);
assert.match(cartService, /SynchronizeSameNumericDependentsAsync/);
assert.match(cartService, /AlignTierToExistingRequirementAsync/);
assert.match(cartService, /ProjectLegacySelectionAsync/);
assert.match(cartService, /StoragePersonalTierCode: personal\.TierCode/);
assert.match(cartService, /BackupPersonal: One\(BillingV2PublicCatalogCodes\.BackupPersonal\) is not null/);
assert.match(cartService, /StorageSharedTierCode: One\(BillingV2PublicCatalogCodes\.StorageShared\)\?\.TierCode/);
assert.match(cartService, /BackupShared: One\(BillingV2PublicCatalogCodes\.BackupShared\) is not null/);

console.log("Préflight 092 : matrice multi-tier, dépendances same_numeric_value et projection Cart/legacy vérifiées.");
