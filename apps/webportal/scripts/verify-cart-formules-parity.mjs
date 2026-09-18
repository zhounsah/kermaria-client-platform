import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const read = (path) => readFileSync(resolve(root, path), "utf8");
const assert = (condition, message) => { if (!condition) throw new Error(message); };

const legacy = read("components/BillingV2FormuleConfigurator.tsx");
const seed = read("../api-internal/Services/BillingV2PublicCatalogSeed.cs");
const cart = read("../api-internal/Services/BillingV2CartService.cs");
const legacyQuote = read("../api-internal/Services/BillingV2PublicQuoteBuilder.cs");
const migration = read("../api-internal/Migrations/MariaDb/091_billing_v2_preset_cart_metadata.sql");
const generic = read("components/BillingV2CartFormuleConfigurator.tsx");

for (const code of [
  "pack-dossier-securise", "pack-acces-distance",
  "pack-bureau-windows-distance", "pack-pro-association",
]) {
  assert(seed.includes(`"${code}"`), `Le preset historique ${code} doit avoir une baseline de référence.`);
  assert(migration.includes(code), `091 doit décrire les options de ${code}.`);
}

for (const control of [
  "storagePersonalTierCode", "backupPersonal", "storageSharedTierCode",
  "backupShared", "vpnTierCode", "remoteDesktop", "additionalUsers", "supportPlus",
]) {
  assert(legacy.includes(control), `Le contrôle legacy ${control} doit rester tracé.`);
}
assert(legacy.includes("MAX_ADDITIONAL_USERS"), "La limite historique d'utilisateurs doit rester traçable.");
assert(migration.includes("selected_by_default") && migration.includes("required_item = 0 OR selected_by_default = 1"),
  "091 doit séparer défaut et obligation avec l'invariant associé.");
assert(migration.includes("maximum_quantity") && migration.includes("'USER-ADDITIONAL' THEN 10"),
  "091 doit conserver la borne historique de dix utilisateurs supplémentaires dans la définition serveur.");
assert(cart.includes("Where(item => item.SelectedByDefault)") && cart.includes("AddPresetItemAsync"),
  "Le Cart doit initialiser seulement les défauts et ajouter une option par son ID de définition.");
assert(cart.includes("CART_PRESET_ITEM_REQUIRED") && cart.includes("command.Quantity < definition.MinimumQuantity"),
  "Un Cart de preset doit refuser un ajout forgeable et appliquer les bornes de quantité côté serveur.");
const removeStart = cart.indexOf("public Task<BillingV2CartMutationResult> RemoveItemAsync");
const removeEnd = cart.indexOf("public Task<BillingV2CartMutationResult> SetCommitmentAsync", removeStart);
assert(cart.slice(removeStart, removeEnd).includes("ReadDependencyIssuesAsync")
  && cart.slice(removeStart, removeEnd).includes("CART_DEPENDENCY_REQUIRED"),
  "La suppression d'une option requise par une dépendance doit être refusée côté serveur.");
assert(generic.includes("presetDefinition") && generic.includes("add_preset_item"),
  "Le nouveau configurateur doit recevoir et utiliser la définition autoritaire du preset.");
assert(!generic.includes("STORAGE-PERSONAL") && !generic.includes("VPN-ACCESS"),
  "Le nouveau configurateur ne doit pas choisir commercialement des services par code.");
assert(cart.includes("_pricing.Calculate(new BillingV2PricingRequest")
  && legacyQuote.includes("pricing.Calculate(new BillingV2PricingRequest"),
  "Les devis Cart et legacy doivent déléguer les sous-totaux, remises et total dû au même moteur de pricing Billing V2.");

console.log("Parité structurelle legacy / Cart des formules vérifiée.");
