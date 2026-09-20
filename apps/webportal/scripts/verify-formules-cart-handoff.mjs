import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const read = (path) => readFileSync(resolve(root, path), "utf8");

const page = read("app/formules/[code]/page.tsx");
const resumePage = read("app/formules/reprendre/page.tsx");
const legacy = read("components/BillingV2FormuleConfigurator.tsx");
const cartConfigurator = read("components/BillingV2CartFormuleConfigurator.tsx");
const cartRoute = read("app/api/billing-v2/cart/route.ts");
const cartService = read("../api-internal/Services/BillingV2CartService.cs");
const cartPolicy = read("../api-internal/Services/BillingV2CartPolicy.cs");
const cartModel = read("../api-internal/Services/BillingV2CartModel.cs");
const apiProgram = read("../api-internal/Program.cs");
const shared = read("../../packages/shared/src/index.ts");

assert.match(page, /BillingV2FormuleConfigurator/,
  "La page formule doit monter le configurateur commercial historique.");
assert.doesNotMatch(page, /BillingV2CartFormuleConfigurator|isBillingV2CartFormulesEnabled/,
  "La consultation d'une formule ne doit plus basculer vers un bootstrap Cart.");
assert.doesNotMatch(resumePage, /BillingV2CartResume|isBillingV2CartFormulesEnabled/,
  "La reprise historique de formule ne doit ni claim ni current un Cart.");

const quoteEffect = legacy.slice(legacy.indexOf("useEffect("), legacy.indexOf("const update"));
assert.match(quoteEffect, /\/api\/formules\/devis/,
  "La configuration locale doit continuer a demander le devis historique authoritative.");
assert.doesNotMatch(quoteEffect, /\/api\/billing-v2\/cart|command:\s*"current"|initialize_preset/,
  "Un montage ou changement local de formule ne doit pas creer/toucher un Cart.");
assert.match(legacy, /Ajouter au panier/);
assert.match(legacy, /command:\s*"import_formula_selection"/);
assert.doesNotMatch(legacy, /\/api\/formules\/souscrire|approveUrl|Souscrire/,
  "Le CTA public formule ne doit plus engager le checkout legacy direct.");
assert.match(legacy, /formulaSelection:\s*selection/,
  "Le handoff ne transmet que la selection locale, jamais le devis affiche.");

assert.match(cartRoute, /"import_formula_selection"/);
assert.match(cartRoute, /readBillingV2SelectionPayload\(payload\.formulaSelection\)/,
  "Le BFF doit filtrer la selection formule avant API-INTERNAL.");
assert.match(cartRoute, /cartCreatingCommands[\s\S]*import_formula_selection/,
  "Seul le CTA d'import peut proposer un token anonyme Cart.");
assert.match(cartRoute, /cartActivityCommands[\s\S]*import_formula_selection/,
  "Le cookie anonyme n'est renouvele qu'apres une reponse Cart reussie.");

assert.match(apiProgram, /"import_formula_selection" when payload\.FormulaSelection is not null/);
assert.match(cartService, /ImportFormulaSelectionAsync/);
assert.match(cartService, /BillingV2PublicSelectionPolicy\.Resolve/,
  "L'import formule doit revalider la meme politique de selection que le devis legacy.");
assert.match(cartService, /ResolveFormulaPresetItems/,
  "Chaque composant importe doit correspondre a une ligne autorisee du preset.");
assert.match(cartService, /BillingV2CartPolicy\.ResolvePresetComposition/,
  "La composition formule doit etre construite par la politique serveur, pas copiee du navigateur.");
assert.match(cartService, /service\.public_visible = 1 OR item\.required_item = 1/,
  "Un socle required non visible comme service autonome doit rester lisible par l'import Cart.");
assert.match(cartPolicy, /foreach \(var required in presetItems\.Where\(item => item\.RequiredItem\)/,
  "Tout item required doit etre injecte depuis la definition du preset.");
assert.doesNotMatch(cartPolicy, /presetItems\.Where\(item => item\.SelectedByDefault && !item\.RequiredItem\)[\s\S]*selected\.TryAdd/,
  "Une option facultative selected_by_default ne doit pas etre injectee comme un item required.");
assert.match(cartService, /ReadDependencyIssuesAsync/,
  "Les dependances sont revalidees apres materialisation des items Cart.");
assert.match(cartService, /ResolveImportDependenciesAsync/,
  "Les dependances deterministes doivent etre resolues dans la transaction d'import.");
assert.match(cartService, /HasAllRequiredPresetItems/,
  "La composition ecrite doit etre controlee avant le commit.");
assert.match(cartService, /HasDuplicateStructuralItems/,
  "Un Cart formule ne doit pas valider de doublon structurel.");
assert.match(cartService, /var quoted = await QuoteAsync/,
  "L'import doit produire un CartQuote recalculé par le moteur serveur.");
assert.match(cartService, /CART_MERGE_REQUIRES_REVIEW/,
  "Une fusion qui demanderait un choix client doit etre refusee explicitement, jamais remplacee.");
assert.match(cartService, /private static FormulaImportPlan PlanFormulaImport/,
  "Le handoff doit planifier une fusion formule/Cart avant toute ecriture.");
assert.match(cartService, /existingPresetItems\.Length != 1[\s\S]{0,220}SameFormulaComposition/,
  "Un retry de la meme ligne de preset doit rester idempotent et refuser toute divergence.");
assert.match(cartService, /var equivalent = cart\.Items\.FirstOrDefault\(item => SameFormulaComposition\(item, definition\)\)[\s\S]{0,260}links\.Add\(new\(equivalent\.Id, definition\.PresetItemId\)\)[\s\S]{0,100}continue/,
  "Une intention commerciale exactement identique ne doit pas etre dupliquee et peut être reliée à sa définition de preset sans réécrire origin.");
assert.match(cartService, /ServiceId, definition\.ServiceId[\s\S]{0,500}FormulaImportPlan\.Review/,
  "Un item existant dans le meme scope mais avec une autre configuration exige une revue explicite.");
assert.match(cartService, /HasAllRequiredPresetItems[\s\S]{0,360}SameFormulaComposition/,
  "La validation finale accepte un required structurel equivalent sans en creer un doublon.");
assert.match(cartModel, /"CART_FORMULA_SELECTION_IMPORTED"/);
assert.match(cartModel, /"CART_MERGE_REQUIRES_REVIEW"/);
const importStart = cartService.indexOf("public async Task<BillingV2CartMutationResult> ImportFormulaSelectionAsync");
const importEnd = cartService.indexOf("public async Task<BillingV2CartMutationResult> InitializeFromPresetAsync", importStart);
assert.doesNotMatch(cartService.slice(importStart, importEnd), /BillingV2AuthoritativeCheckoutService|PaymentAttempt|BillingEvent|outbox|provisioning/i,
  "L'import formule vers Cart ne doit appeler aucun effet financier ou provider.");

assert.match(shared, /\| "import_formula_selection"/);
assert.match(shared, /formulaSelection\?: BillingV2PublicSelection/);
const cartCommandSection = shared.slice(shared.indexOf("export interface BillingV2CartCommandRequest"));
for (const forbidden of ["monthlyPrice", "amountCents", "discount", "dueNow", "servicePriceId"]) {
  assert.doesNotMatch(cartCommandSection, new RegExp(forbidden),
    `La commande Cart formule ne doit pas accepter ${forbidden} depuis le navigateur.`);
}

assert.doesNotMatch(cartConfigurator, /\/api\/formules\/souscrire|approveUrl|Souscrire/,
  "Le composant Cart conserve son edition future sans raccourci checkout legacy.");

console.log("Handoff formule locale vers Cart verifie : aucun Cart au bootstrap, import explicite et sans prix client.");
