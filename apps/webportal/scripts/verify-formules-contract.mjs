import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import ts from "typescript";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

function transpileToDataUrl(source, label) {
  const transpiled = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.ES2022,
      target: ts.ScriptTarget.ES2022,
    },
    fileName: label,
    reportDiagnostics: true,
  });
  const errors = (transpiled.diagnostics ?? []).filter(
    (diagnostic) => diagnostic.category === ts.DiagnosticCategory.Error,
  );
  assert.deepEqual(errors, [], `${label} doit etre transpile sans erreur.`);
  return `data:text/javascript;base64,${Buffer.from(transpiled.outputText).toString("base64")}`;
}

// --- Artefacts ---
const sharedTypes = await read("../../packages/shared/src/index.ts");
const catalogSeed = await read(
  "../../apps/api-internal/Services/BillingV2PublicCatalogSeed.cs",
);
const vpsCloudAttributesMigration = await read(
  "../../apps/api-internal/Migrations/MariaDb/081_billing_v2_vps_cloud_tier_attributes.sql",
);
const catalogService = await read(
  "../../apps/api-internal/Services/BillingV2PublicCatalogService.cs",
);
const selectionPolicy = await read(
  "../../apps/api-internal/Services/BillingV2PublicSelectionPolicy.cs",
);
const quoteBuilder = await read(
  "../../apps/api-internal/Services/BillingV2PublicQuoteBuilder.cs",
);
const catalogModel = await read(
  "../../apps/api-internal/Services/BillingV2PublicCatalogModel.cs",
);
const programCs = await read("../../apps/api-internal/Program.cs");
const internalApi = await read("lib/internal-api.ts");
const helpers = await read("lib/billing-v2-formules.ts");
const quoteRoute = await read("app/api/formules/devis/route.ts");
const subscribeRoute = await read("app/api/formules/souscrire/route.ts");
const selectionReader = await read("lib/billing-v2-selection.ts");
const checkoutService = await read(
  "../../apps/api-internal/Services/BillingV2AuthoritativeCheckoutService.cs",
);
const nativeResolver = await read(
  "../../apps/api-internal/Services/BillingV2NativeSelectionResolver.cs",
);
const listPage = await read("app/formules/page.tsx");
const configuratorPage = await read("app/formules/[code]/page.tsx");
const configurator = await read(
  "components/BillingV2FormuleConfigurator.tsx",
);
const directSubscribe = await read("components/BillingV2DirectSubscribe.tsx");
const appShell = await read("components/AppShell.tsx");
const helpLabel = await read("components/FormuleHelpLabel.tsx");
const helpContent = await read("lib/formule-help.ts");
const globalStyles = await read("app/globals.css");
const loginPage = await read("app/login/page.tsx");
const loginForm = await read("components/LoginForm.tsx");
const publicRouteConfig = await read("lib/public-route-config.ts");
const signupPage = await read("app/signup/page.tsx");
const signupForm = await read("components/SignupForm.tsx");
const signupRoute = await read("app/api/signup/route.ts");
const resumePage = await read("app/formules/reprendre/page.tsx");
const setPasswordForm = await read("components/SetPasswordForm.tsx");
const signupRepoMaria = await read(
  "../../apps/api-internal/Data/Repositories/MariaDbSignupRepository.cs",
);
const signupService = await read(
  "../../apps/api-internal/Services/SignupService.cs",
);
const offersPage = await read("app/offres/page.tsx");
const sitemap = await read("app/sitemap.ts");
const helpersRuntime = await import(transpileToDataUrl(
  helpers,
  "billing-v2-formules.ts",
));

// Les valeurs des caractéristiques restent dans le catalogue. Le portail ne
// connait que l'ordre et le format de trois codes publicement presentables.
const tierAttributesInDifferentOrder = {
  attributes: [
    { code: "disk_gib", valueNumeric: 80, valueText: null, unit: "GiB" },
    { code: "unknown_attribute", valueNumeric: 123, valueText: null, unit: null },
    { code: "ram_gib", valueNumeric: null, valueText: "8", unit: "GiB" },
    { code: "vcpu_count", valueNumeric: 4, valueText: null, unit: "count" },
  ],
};
assert.equal(
  helpersRuntime.describeTierAttributes(tierAttributesInDifferentOrder).join(" · "),
  "4 vCPU · 8 Go RAM · 80 Go stockage",
  "Les attributs du catalogue sont ordonnes et formates sans valeur de palier codee en dur.",
);
assert.equal(
  helpersRuntime.describeTierAttributes({
    attributes: tierAttributesInDifferentOrder.attributes.map((attribute) => (
      attribute.code === "ram_gib"
        ? { ...attribute, valueNumeric: 12, valueText: null }
        : attribute
    )),
  }).join(" · "),
  "4 vCPU · 12 Go RAM · 80 Go stockage",
  "Une mise a jour administrative de ram_gib se reflete sans modifier le prix ni le code frontend.",
);
assert.deepEqual(
  helpersRuntime.describeTierAttributes({ attributes: [] }),
  [],
  "Un palier sans attribut n'ajoute aucune caracteristique vide.",
);
assert.deepEqual(
  helpersRuntime.describeTierAttributes({}),
  [],
  "Un catalogue transitoirement sans propriete attributes ne doit pas faire echouer la vitrine.",
);
assert.deepEqual(
  helpersRuntime.describeTierAttributes({
    attributes: [{ code: "unknown_attribute", valueNumeric: 1, valueText: null, unit: null }],
  }),
  [],
  "Un attribut inconnu ne casse pas la presentation publique.",
);
assert.match(
  vpsCloudAttributesMigration,
  /INSERT IGNORE INTO billing_v2_service_tier_attributes/,
  "Les attributs VPS Cloud doivent etre ajoutables sans ecraser une valeur administree.",
);
for (const attribute of [
  ["S", "vcpu_count", 2, "count"], ["S", "ram_gib", 2, "GiB"], ["S", "disk_gib", 60, "GiB"],
  ["M", "vcpu_count", 4, "count"], ["M", "ram_gib", 8, "GiB"], ["M", "disk_gib", 160, "GiB"],
  ["L", "vcpu_count", 8, "count"], ["L", "ram_gib", 16, "GiB"], ["L", "disk_gib", 320, "GiB"],
  ["XL", "vcpu_count", 16, "count"], ["XL", "ram_gib", 32, "GiB"], ["XL", "disk_gib", 640, "GiB"],
]) {
  const [tierCode, attributeCode, value, unit] = attribute;
  const pattern = new RegExp(
    `(?:SELECT|UNION ALL SELECT) '${tierCode}'(?: AS tier_code)?, '${attributeCode}'(?: AS attribute_code)?, ${value}(?: AS value_numeric)?, '${unit}'(?: AS unit)?`,
  );
  assert.match(
    vpsCloudAttributesMigration,
    pattern,
    `La migration doit reproduire ${tierCode}/${attributeCode}=${value} ${unit}.`,
  );
}
assert.doesNotMatch(
  vpsCloudAttributesMigration,
  /billing_v2_service_prices|ON DUPLICATE KEY UPDATE|DELETE FROM|ALTER TABLE/,
  "La migration d'attributs ne doit modifier ni prix, ni attribut existant, ni schema.",
);
assert.match(
  helpers,
  /vcpu_count[\s\S]*ram_gib[\s\S]*disk_gib/,
  "Le portail ne mappe que les codes de presentation VPS connus.",
);
assert.doesNotMatch(
  helpers,
  /tier\.code\s*===\s*["'](?:NANO|MICRO|SMALL|MEDIUM)["']/,
  "Aucune capacite VPS ne depend du code d'un palier.",
);
assert.match(
  directSubscribe,
  /describeTierAttributes\(selectedTier\)\.join\(" · "\)/,
  "La souscription directe affiche les caracteristiques du palier selectionne.",
);

// --- 1. Le serveur reste la seule autorite financiere ---------------------

assert.match(
  quoteBuilder,
  /pricing\.Calculate\(new BillingV2PricingRequest\(/,
  "Le devis doit passer par BillingV2PricingEngine.",
);

assert.doesNotMatch(
  helpers,
  /amountCents\s*\+|\+\s*.*amountCents|reduce\(/,
  "Le webportal ne doit additionner aucun montant.",
);

assert.doesNotMatch(
  configurator,
  /monthlyAfterDiscountCents\s*=|amountCents\s*\*|\*\s*quantity/,
  "Le configurateur affiche les montants, il ne les calcule pas.",
);

assert.doesNotMatch(
  quoteRoute,
  /amountCents|monthlyAfterDiscountCents|totalDueNowCents/,
  "La route de devis ne doit relayer aucun montant venu du navigateur.",
);

assert.match(
  quoteRoute,
  /readBillingV2SelectionPayload/,
  "La route de devis doit reconstruire strictement la selection.",
);
assert.match(
  subscribeRoute,
  /readBillingV2SelectionPayload/,
  "La souscription doit reconstruire la selection avec le meme lecteur.",
);
assert.doesNotMatch(
  subscribeRoute,
  /amountCents|totalDueNow[^C]|monthlyAfterDiscountCents|discountBasisPoints/,
  "La souscription ne relaie aucun montant venu du navigateur.",
);
// Le lecteur partage est la seule porte d'entree : tout champ absent de cette
// liste ne peut pas atteindre API-INTERNAL.
assert.doesNotMatch(
  selectionReader,
  /amount|cents|price|discount/i,
  "Le lecteur de selection n'accepte aucun champ tarifaire.",
);

assert.ok(
  selectionReader.includes(String.raw`if (!/^\d+$/.test(usersRaw)) {`),
  "La reprise URL doit accepter un nombre entier d utilisateurs additionnels.",
);

// La charge utile acceptee par l'API ne porte aucun champ tarifaire.
const selectionInputBlock = catalogModel.slice(
  catalogModel.indexOf("class BillingV2PublicSelectionInput"),
  catalogModel.indexOf("public sealed record BillingV2PublicQuoteLine"),
);
assert.ok(
  selectionInputBlock.length > 0,
  "Le contrat d'entree public doit exister.",
);
assert.doesNotMatch(
  selectionInputBlock,
  /Amount|Cents|Price/i,
  "Aucun montant ne doit etre accepte depuis le navigateur.",
);

assert.match(
  configurator,
  /ne duplique pas automatiquement le stockage personnel/,
  "USER-ADDITIONAL doit etre presente comme une place nominative, sans duplication automatique des services user-scoped.",
);
assert.doesNotMatch(
  configurator,
  /son espace personnel et son/,
  "Le configurateur ne doit plus promettre implicitement stockage et acces avec la seule place USER-ADDITIONAL.",
);

// --- UX locale du configurateur -------------------------------------------
assert.match(
  configurator,
  /const selected = selection\.commitmentCode === item\.code;/,
  "Le configurateur doit distinguer la duree selectionnee des alternatives.",
);
assert.match(
  configurator,
  /\{!selected \? \([\s\S]*jusqu'à −\$\{formatDiscountPercent\(best\)\} %/,
  "La remise marketing maximale ne doit rester visible que sur une duree non selectionnee.",
);
assert.match(
  configurator,
  /quote\.discountBasisPoints > 0/,
  "La remise reellement appliquee doit continuer a venir du devis serveur dans le recapitulatif.",
);
for (const helpKey of [
  "personalStorage",
  "sharedStorage",
  "personalBackup",
  "sharedBackup",
  "vpn",
  "remoteDesktop",
  "additionalUser",
  "supportPlus",
]) {
  assert.match(
    configurator,
    new RegExp(`helpKey=["']${helpKey}["']`),
    `Le configurateur doit proposer l'aide ${helpKey}.`,
  );
  assert.match(
    helpContent,
    new RegExp(`\\b${helpKey}:\\s*\\{`),
    `Le texte d'aide ${helpKey} doit etre centralise.`,
  );
}
for (const accessibilityContract of [
  /role="button"/,
  /tabIndex=\{0\}/,
  /aria-label=\{`Afficher l\u2019aide : \$\{content\.title\}`\}/,
  /aria-expanded=\{open\}/,
  /aria-controls=\{popoverId\}/,
  /event\.key === "Escape"/,
  /event\.key === "Enter" \|\| event\.key === " "/,
  /event\.stopPropagation\(\)/,
]) {
  assert.match(
    helpLabel,
    accessibilityContract,
    "La bulle d'aide doit rester accessible au clavier et ne pas activer l'option parente.",
  );
}
assert.match(
  globalStyles,
  /\.formule-help-popover[\s\S]*width: min\(22rem, calc\(100vw - 32px\)\)/,
  "La bulle desktop doit rester bornee a la largeur du viewport.",
);
assert.match(
  globalStyles,
  /@media \(max-width: 48rem\)[\s\S]*\.formule-help-popover[\s\S]*position: fixed/,
  "La bulle mobile doit rester dans le viewport.",
);
assert.match(
  globalStyles,
  /@media \(min-width: 1101px\)[\s\S]*\.vps-service \.service-offer-grid[\s\S]*repeat\(4, minmax\(0, 1fr\)\)/,
  "Le comparatif VPS doit utiliser quatre colonnes quand la largeur desktop le permet.",
);

// --- 2. Les quatre formules publiques ------------------------------------

for (const presetCode of [
  "pack-dossier-securise",
  "pack-acces-distance",
  "pack-bureau-windows-distance",
  "pack-pro-association",
]) {
  assert.match(
    catalogSeed,
    new RegExp(presetCode),
    `Le repli catalogue doit porter la formule ${presetCode}.`,
  );
}

// Les prix du repli doivent rester ceux de la migration 048.
const migration = await read(
  "../../apps/api-internal/Migrations/MariaDb/048_billing_v2_catalog_seed.sql",
);
const seededPrices = [
  ["BASE-SERVICE-MONTHLY-EUR-V1", 690],
  ["STORAGE-PERSONAL-32-MONTHLY-EUR-V1", 300],
  ["STORAGE-PERSONAL-64-MONTHLY-EUR-V1", 500],
  ["BACKUP-PERSONAL-32-MONTHLY-EUR-V1", 200],
  ["BACKUP-PERSONAL-64-MONTHLY-EUR-V1", 300],
  ["STORAGE-SHARED-128-MONTHLY-EUR-V1", 890],
  ["BACKUP-SHARED-128-MONTHLY-EUR-V1", 500],
  ["VPN-ACCESS-ESSENTIAL-MONTHLY-EUR-V1", 390],
  ["VPN-ACCESS-PLUS-MONTHLY-EUR-V1", 590],
  ["RDS-ACCESS-MONTHLY-EUR-V1", 1590],
  ["USER-ADDITIONAL-MONTHLY-EUR-V1", 390],
  ["SUPPORT-PLUS-MONTHLY-EUR-V1", 990],
];

for (const [priceCode, amountCents] of seededPrices) {
  assert.match(
    migration,
    new RegExp(`'${priceCode}'[^\\r\\n]*?\\b${amountCents}\\b`),
    `La migration 048 doit porter ${priceCode} a ${amountCents} centimes.`,
  );
  assert.match(
    catalogSeed,
    new RegExp(`\\b${amountCents}\\b`),
    `Le repli catalogue doit reprendre ${amountCents} centimes.`,
  );
}

// --- 3. Engagements et modes de reglement --------------------------------

// La matrice remise x mode de reglement doit rester celle de la migration 048.
for (const [term, mode, basisPoints] of [
  ["FLEX", "monthly", 0],
  ["TERM-6", "monthly", 1000],
  ["TERM-6", "upfront", 1500],
  ["TERM-12", "monthly", 1500],
  ["TERM-12", "upfront", 2000],
]) {
  // La premiere ligne du seed est aliasee (`'FLEX' AS term_code`), les
  // suivantes non : on verifie donc la coexistence sur une meme ligne.
  const seeded = migration
    .split(String.fromCharCode(10))
    .some(
      (line) =>
        line.includes(`'${term}'`)
        && line.includes(`'${mode}'`)
        && new RegExp(String.raw`(^|[ ,])${basisPoints}([ ,]|$)`).test(line),
    );
  assert.ok(
    seeded,
    `La migration 048 doit porter ${term}/${mode} a ${basisPoints} points.`,
  );
  const seedMode = mode === "monthly" ? "Monthly" : "Upfront";
  assert.ok(
    catalogSeed.includes(`BillingV2PaymentModes.${seedMode}, ${basisPoints})`),
    `Le repli catalogue doit reprendre ${term}/${mode}.`,
  );
}

// Les drapeaux du catalogue restent l'autorite : une duree qui n'autorise pas
// un mode de reglement ne doit pas pouvoir l'exposer.
assert.match(
  catalogService,
  /term\.allow_monthly_payment = 1/,
  "Le mensuel reste conditionne au drapeau du catalogue.",
);
assert.match(
  catalogService,
  /term\.allow_upfront_payment = 1/,
  "Le comptant reste conditionne au drapeau du catalogue.",
);
assert.match(
  selectionPolicy,
  /BILLING_V2_PUBLIC_PAYMENT_MODE_UNAVAILABLE/,
  "Un mode de reglement non ouvert doit etre refuse en ferme.",
);


// --- 4. Dependances du catalogue respectees ------------------------------

assert.match(
  selectionPolicy,
  /ResolveTierByNumericValue\(\s*catalog,\s*BillingV2PublicCatalogCodes\.BackupPersonal/,
  "Le palier de sauvegarde personnelle suit la capacite couverte.",
);
assert.match(
  selectionPolicy,
  /BILLING_V2_PUBLIC_SHARED_BACKUP_WITHOUT_STORAGE/,
  "La sauvegarde partagee exige un espace partage.",
);
assert.match(
  selectionPolicy,
  /requirePublic\s*&&\s*!tier\.PublicSelectable/,
  "Un palier non public ne doit jamais etre selectionnable.",
);

// --- 5. Le checkout rejoint le parcours authoritative existant -----------

assert.match(
  configurator,
  /"\/api\/formules\/souscrire"/,
  "Le bouton final doit rejoindre le parcours de souscription V2 native.",
);
assert.match(
  subscribeRoute,
  /"\/internal\/portal\/billing-v2\/subscriptions\/checkout"/,
  "La souscription doit passer par le checkout authoritative existant.",
);
assert.match(
  configurator,
  /"Idempotency-Key": crypto\.randomUUID\(\)/,
  "Le checkout doit porter une cle d'idempotence.",
);
const publicSignupIndex = configurator.indexOf('if (currentArea === "public")');
const checkoutFetchIndex = configurator.indexOf('fetch("/api/formules/souscrire"');
assert.ok(
  publicSignupIndex >= 0 && checkoutFetchIndex > publicSignupIndex,
  "Sur la vitrine, l inscription doit etre choisie avant tout appel checkout.",
);
assert.match(
  configurator,
  /billingV2SelectionToSearchParams\(selection\)[\s\S]*"public"[\s\S]*signupPath/,
  "La vitrine doit transporter la selection complete vers l inscription publique.",
);
assert.match(
  configurator,
  /currentArea === "client"[\s\S]*\/login\?next=/,
  "Sur l hote client, une session expiree doit encore passer par le login borne.",
);
// Le nom du garde a change (`isClientCheckoutPortalPath`) : on verifie le
// comportement de redirection lui-meme, qui est l'invariant, plutot que la
// forme du code qui l'implemente.
const routeConfigRuntime = await import(transpileToDataUrl(
  publicRouteConfig,
  "public-route-config.ts",
));
const {
  resolvePortalPublicRedirectUrl,
  resolveClientCheckoutContinuationPath,
} = routeConfigRuntime;

// L'hote client garde /formules en local : le BFF de souscription a besoin du
// cookie de session host-only.
assert.equal(
  resolvePortalPublicRedirectUrl("dashboard.zachary-it.fr", "/formules"),
  null,
  "L'hote client doit pouvoir servir localement l'index /formules.",
);
assert.equal(
  resolvePortalPublicRedirectUrl("dashboard.zachary-it.fr", "/formules/pack-essentiel"),
  null,
  "L'hote client doit pouvoir servir localement la continuation /formules/<code>.",
);
// L'hote d'administration, lui, n'a aucune raison de servir le configurateur.
assert.equal(
  resolvePortalPublicRedirectUrl("administration.zachary-it.fr", "/formules"),
  "https://zachary-it.fr/formules",
  "L'hote d'administration ne doit jamais servir le configurateur en local.",
);
// Tout autre chemin vitrine bascule vers l'hote public, y compris depuis le
// tableau de bord : un seul hote doit repondre 200 pour l'indexation.
assert.equal(
  resolvePortalPublicRedirectUrl("dashboard.zachary-it.fr", "/tarifs"),
  "https://zachary-it.fr/tarifs",
  "Une page vitrine servie depuis l'hote client doit rediriger vers le public.",
);

// Le chemin de reprise reste borne a un unique code de formule.
assert.equal(
  resolveClientCheckoutContinuationPath("/formules/pack-essentiel"),
  "/formules/pack-essentiel",
);
assert.equal(
  resolveClientCheckoutContinuationPath("/formules/pack/essentiel"),
  null,
  "Un code de formule ne doit pas pouvoir porter de segment supplementaire.",
);
assert.equal(
  resolveClientCheckoutContinuationPath("/admin"),
  null,
  "Le chemin de reprise ne doit pas pouvoir designer une surface interne.",
);
assert.equal(
  resolveClientCheckoutContinuationPath("//evil.invalid/formules"),
  null,
  "Le chemin de reprise ne doit pas pouvoir devenir une redirection externe.",
);
assert.match(
  appShell,
  /keepAuthenticatedCheckoutShell[\s\S]*portalArea === "client"[\s\S]*client_user/,
  "Le configurateur servi sur dashboard doit reprendre le shell de la session client.",
);
assert.match(
  loginPage,
  /resolveClientCheckoutContinuationPath\(query\.next\)/,
  "La page de connexion doit valider strictement le chemin de reprise.",
);
assert.match(
  loginForm,
  /result\.user\.role === "client_user" && continuationPath[\s\S]*resolvePortalAreaUrl/,
  "Apres connexion client, le formulaire doit reprendre le configurateur demande.",
);
assert.match(
  signupPage,
  /readBillingV2SelectionSearchParams[\s\S]*quoteBillingV2Formule/,
  "L inscription doit relire puis revalider le devis V2 cote serveur.",
);
assert.match(
  signupForm,
  /billingV2Selection:\s*initialBillingV2Selection/,
  "Le formulaire doit transmettre la selection V2, jamais seulement le code du preset.",
);
assert.match(
  signupRoute,
  /readBillingV2SelectionPayload[\s\S]*INVALID_BILLING_V2_SELECTION[\s\S]*billingV2Selection,/,
  "Le BFF signup doit reconstruire strictement V2 et refuser une selection invalide"
    + " plutot que de la laisser tomber silencieusement.",
);
// Hors commentaires : le nom de ces champs ne doit plus apparaitre dans le
// code, ni pour les lire, ni pour les ignorer explicitement.
const signupRouteCode = signupRoute
  .split(/\r?\n/)
  .filter((line) => !/^\s*(\/\/|\*|\/\*)/.test(line))
  .join("\n");
assert.doesNotMatch(
  signupRouteCode,
  /packKey|commitmentMonths|offerExternalReference/,
  "Le BFF signup ne doit plus accepter les champs de l ancien second catalogue,"
    + " meme pour les ignorer.",
);
assert.match(
  signupRepoMaria,
  /SignupCatalogContextEnvelope\("billing_v2",\s*billingV2Selection\)/,
  "La selection V2 doit etre persistee dans le snapshot JSON existant.",
);
assert.match(
  signupService,
  /GetPendingBillingV2SelectionAsync/,
  "Le signup doit exposer la selection approuvee apres authentification.",
);
assert.match(
  resumePage,
  /requireClientSession\("\/formules\/reprendre"\)[\s\S]*getPendingBillingV2Selection\(\)[\s\S]*billingV2SelectionToSearchParams[\s\S]*redirect\(/,
  "La reprise doit exiger une session puis restaurer la selection persistante.",
);
assert.match(
  setPasswordForm,
  /\/login\?next=%2Fformules%2Freprendre/,
  "Apres activation du compte principal, le login doit reprendre la formule V2.",
);

assert.doesNotMatch(
  configurator,
  /stripe\.com|stripePriceId|price_/i,
  "Aucune logique Stripe ne doit etre recreee cote front.",
);
assert.doesNotMatch(
  quoteBuilder,
  /stripePriceId|price_id/i,
  "Aucun identifiant de prix provider ne doit reapparaitre.",
);
assert.match(
  quoteBuilder,
  /BillingV2AuthoritativeCheckoutReadiness/,
  "Le devis doit remonter le motif du gate authoritative.",
);
assert.doesNotMatch(
  subscribeRoute,
  /stripe\.com|stripePriceId|price_id|price_data/i,
  "Aucune logique Stripe ne doit etre recreee dans le BFF.",
);
assert.doesNotMatch(
  nativeResolver,
  /stripePriceId|stripe_price|provider_external_id|price_data/i,
  "La resolution native ne connait aucun identifiant de prix fournisseur.",
);

// --- 5 bis. Souscription V2 native ---------------------------------------

// Le checkout authoritative doit accepter une selection sans offre legacy,
// tout en conservant le chemin historique.
assert.match(
  checkoutService,
  /BillingV2PublicSelection\? Selection/,
  "Le checkout authoritative doit accepter une selection V2 native.",
);
assert.match(
  checkoutService,
  /BILLING_V2_CHECKOUT_SELECTION_REQUIRED/,
  "Un checkout sans selection doit etre refuse : il n existe plus d autre"
    + " identite commerciale sur laquelle retomber.",
);
assert.doesNotMatch(
  checkoutService,
  /legacy_offer_id|LegacyOfferId|commercial_offers/,
  "Le checkout authoritative ne doit plus connaitre le catalogue legacy.",
);
// Le Pricing Engine n'est pas duplique : la composition native retombe sur le
// meme calcul que le preset.
assert.match(
  checkoutService,
  /_pricing\.Calculate\(new BillingV2PricingRequest\(/,
  "Le checkout doit recalculer le prix avec le Pricing Engine.",
);
assert.ok(
  (checkoutService.match(/_pricing\.Calculate\(/g) ?? []).length === 1,
  "Un seul point de calcul tarifaire dans le checkout authoritative.",
);
// L'ancre d'idempotence devient la configuration elle-meme.
assert.match(
  checkoutService,
  /BillingV2CheckoutSelectionFingerprint/,
  "L'identite metier d'une demande doit etre l'empreinte de configuration.",
);
assert.match(
  nativeResolver,
  /BillingV2PublicSelectionPolicy\.Resolve/,
  "La selection doit etre revalidee cote serveur avant toute ecriture.",
);
assert.match(
  nativeResolver,
  /BillingV2ServicePriceResolutionPolicy\.Resolve/,
  "Les prix natifs doivent passer par la resolution d'ambiguite partagee.",
);
for (const forbiddenWrite of [
  "INSERT INTO",
  "UPDATE ",
  "DELETE FROM",
  "ALTER TABLE",
  "CREATE TABLE",
]) {
  assert.ok(
    !nativeResolver.includes(forbiddenWrite),
    `La resolution native doit rester en lecture seule (${forbiddenWrite}).`,
  );
}

// --- 6. Projection strictement en lecture --------------------------------

for (const forbiddenWrite of [
  "INSERT INTO",
  "UPDATE ",
  "DELETE FROM",
  "ALTER TABLE",
  "CREATE TABLE",
]) {
  assert.ok(
    !catalogService.includes(forbiddenWrite),
    `Le catalogue public ne doit contenir aucune ecriture (${forbiddenWrite}).`,
  );
}

assert.match(
  catalogService,
  /information_schema\.tables/,
  "La precondition de schema doit etre verifiee en lecture seule.",
);

// --- 7. Cablage des pages -------------------------------------------------

assert.match(
  programCs,
  /"\/internal\/portal\/billing-v2\/formules"/,
  "L'endpoint catalogue doit exister.",
);
assert.match(
  programCs,
  /"\/internal\/portal\/billing-v2\/formules\/devis"/,
  "L'endpoint de devis doit exister.",
);
assert.match(
  internalApi,
  /getBillingV2FormulesCatalog/,
  "Le webportal doit lire le catalogue via API-INTERNAL.",
);
assert.match(
  internalApi,
  /EMPTY_BILLING_V2_CATALOG/,
  "Le repli webportal doit etre vide, jamais tarifaire.",
);
assert.match(
  listPage,
  /Configurer/,
  "La page formules doit proposer l'action Configurer.",
);
assert.match(
  configuratorPage,
  /BillingV2FormuleConfigurator/,
  "La page de configuration doit monter le configurateur.",
);
assert.match(
  offersPage,
  /href="\/formules"/,
  "La page offres doit renvoyer vers les formules.",
);
assert.match(
  sitemap,
  /path: "\/formules"/,
  "Le hub des formules doit etre declare au sitemap.",
);

// --- 8. Contrats partages -------------------------------------------------

for (const contract of [
  "BillingV2PublicCatalog",
  "BillingV2PublicPreset",
  "BillingV2PublicQuote",
  "BillingV2PublicSelection",
  "BillingV2PublicPaymentOption",
  "BillingV2PublicTierAttribute",
  "baselineMonthlyAmountCents",
  "commitmentSavingsCents",
  "paymentMode",
]) {
  assert.match(
    sharedTypes,
    new RegExp(contract),
    `Le contrat partage ${contract} doit exister.`,
  );
}
assert.match(
  sharedTypes,
  /attributes:\s*BillingV2PublicTierAttribute\[\];/,
  "Un palier public expose toujours sa collection d'attributs.",
);

console.log("Contrat formules Billing V2 verifie.");
