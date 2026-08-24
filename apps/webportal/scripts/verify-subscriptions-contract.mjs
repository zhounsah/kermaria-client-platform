import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { readFile } from "node:fs/promises";

/**
 * Contrat des abonnements apres la bascule Billing V2.
 *
 * La souscription elle-meme est verifiee ailleurs : le calcul et le checkout
 * autoritaire par les suites C# `--billing-v2-*`, le parcours public par
 * `verify-formules-contract.mjs`. Ce script garde ce que ces suites ne voient
 * pas : les surfaces portail qui *lisent* et *resilient* un abonnement, et la
 * frontiere entre le navigateur, le BFF et l'API interne.
 */

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const sharedTypes = await read("../../packages/shared/src/index.ts");
const internalApi = await read("lib/internal-api.ts");
const paypalLib = await read("lib/paypal.ts");
const paypalWebhookLib = await read("lib/paypal-webhook.ts");
const billingV2ReturnRoute = await read(
  "app/api/subscriptions/billing-v2/return/route.ts",
);
const paypalWebhookRoute = await read("app/api/webhooks/paypal/route.ts");
const stripeWebhookRoute = await read("app/api/webhooks/stripe/route.ts");
const adminCancelRoute = await read(
  "app/api/admin/subscriptions/[id]/cancel/route.ts",
);
const clientCancelRoute = await read(
  "app/api/subscriptions/[id]/cancel/route.ts",
);
const adminListPage = await read("app/admin/subscriptions/page.tsx");
const adminDetailPage = await read("app/admin/subscriptions/[id]/page.tsx");
const clientListPage = await read("app/profile/subscriptions/page.tsx");
const adminCancelButton = await read(
  "components/AdminCancelSubscriptionButton.tsx",
);
const clientCancelButton = await read(
  "components/ClientCancelSubscriptionButton.tsx",
);
const adminNav = await read("components/AdminNavigation.tsx");
const billingV2AdministrationService = await read(
  "../../apps/api-internal/Services/BillingV2SubscriptionAdministrationService.cs",
);
const programCs = await read("../../apps/api-internal/Program.cs");
const subscriptionContracts = await read(
  "../../apps/api-internal/Contracts/SubscriptionContracts.cs",
);

const failures = [];
function check(label, run) {
  try {
    run();
  } catch (error) {
    failures.push([label, error.message]);
  }
}

// --- 1. L'abonnement est decrit par le catalogue V2, pas par une offre. ----

check("le resume d abonnement designe une formule V2, jamais une offre", () => {
  assert.match(
    subscriptionContracts,
    /\[property: JsonPropertyName\("presetId"\)\] string\? PresetId/,
    "L identite d origine est un preset V2, nullable pour une souscription directe.",
  );
  assert.match(
    subscriptionContracts,
    /\[property: JsonPropertyName\("presetCode"\)\] string\? PresetCode/,
  );
  assert.doesNotMatch(
    subscriptionContracts,
    /CommercialOfferId|OfferExternalReference|PublicPackCode/,
    "Aucune identite d offre legacy ne doit subsister dans le contrat.",
  );
  assert.match(
    subscriptionContracts,
    /string BillingSystem = "billing_v2"/,
    "Il n existe plus qu un systeme de facturation.",
  );
});

check("le contrat partage suit la meme identite", () => {
  const summary = sharedTypes.slice(
    sharedTypes.indexOf("interface SubscriptionSummary"),
  );
  assert.match(summary, /presetId: string \| null;/);
  assert.match(summary, /presetCode: string \| null;/);
  assert.doesNotMatch(
    summary.slice(0, summary.indexOf("\n}")),
    /commercialOfferId|offerExternalReference|publicPackCode/,
    "Le contrat partage ne doit plus exposer d identite d offre legacy.",
  );
});

// --- 2. Les surfaces de lecture passent par le BFF serveur. ---------------

check("les lectures d abonnement restent server-only", () => {
  assert.match(internalApi, /import "server-only"/);
  assert.match(internalApi, /"\/internal\/portal\/subscriptions"/);
  assert.match(internalApi, /"\/internal\/admin\/subscriptions"/);
});

for (const [label, source] of [
  ["page admin liste", adminListPage],
  ["page admin detail", adminDetailPage],
  ["page client liste", clientListPage],
]) {
  check(`${label} ne fuite pas la frontiere serveur`, () => {
    assert.doesNotMatch(
      source,
      /NEXT_PUBLIC_INTERNAL_API_URL|NEXT_PUBLIC_SERVICE_AUTH_TOKEN|localStorage|sessionStorage/,
    );
  });
}

check("la page admin abonnements expose le rail de paiement", () => {
  assert.match(
    adminListPage,
    /formatSubscriptionRailLabel|Facture locale Kermaria/,
    "Un exploitant doit voir par quel rail un abonnement est encaisse.",
  );
});

check("lien admin vers les abonnements", () => {
  assert.match(adminNav, /\/admin\/subscriptions/);
});

// --- 3. La resiliation est bornee et revalidee cote serveur. --------------

// L'appartenance n'est plus verifiee par une pre-lecture du BFF : elle l'est
// la ou elle fait autorite. `ClientCancelAsync` resout l'abonnement DANS le
// perimetre du client de la session ; un identifiant devine dans l'URL y
// ressort en `PortalDataNotFoundException`, donc en 404. Verifier cela au
// niveau du BFF donnait une garantie plus faible, et contournable.
check("la resiliation client est bornee au perimetre du client", () => {
  assert.match(
    clientCancelRoute,
    /session\.user\.role !== "client_user"/,
    "Le BFF client doit refuser toute session qui n est pas un client.",
  );
  const scoped = billingV2AdministrationService.slice(
    billingV2AdministrationService.indexOf("public async Task<SubscriptionSummary> ClientCancelAsync"),
    billingV2AdministrationService.indexOf("public async Task<SubscriptionSummary> AdminCancelAsync"),
  );
  assert.notEqual(scoped.length, 0, "ClientCancelAsync doit exister.");
  assert.match(
    scoped,
    /GetClientSubscriptionsAsync\(\s*session\.CustomerId/,
    "La resiliation client doit resoudre l abonnement dans le perimetre de la session.",
  );
  assert.match(
    scoped,
    /PortalDataNotFoundException/,
    "Un abonnement hors perimetre doit etre introuvable, pas resiliable.",
  );
  assert.match(
    scoped,
    /forceImmediate: false/,
    "Le client ne peut jamais exiger une coupure immediate d une periode payee.",
  );
});

check("la resiliation borne l identifiant recu", () => {
  for (const [label, source] of [
    ["client", clientCancelRoute],
    ["admin", adminCancelRoute],
  ]) {
    assert.match(
      source,
      /\/\^\[A-Za-z0-9-\]\{1,100\}\$\//,
      `La route ${label} doit borner l identifiant d abonnement.`,
    );
  }
});

// Le comportement de la resiliation est verifie par la suite C#
// `--billing-v2-cancellation`. Ce qui se verifie ICI est structurel : le BFF
// ne doit PAS etre une seconde autorite fournisseur. Un BFF qui appellerait
// Stripe lui-meme pourrait conclure a une resiliation que l'API interne
// ignore — exactement la divergence que Billing V2 interdit.
check("le BFF de resiliation ne parle a aucun fournisseur", () => {
  for (const [label, source] of [
    ["client", clientCancelRoute],
    ["admin", adminCancelRoute],
  ]) {
    assert.doesNotMatch(
      source,
      /cancelPayPalSubscription|cancelStripeSubscription|scheduleStripeSubscriptionCancellationAtPeriodEnd/,
      `La route ${label} ne doit plus piloter le fournisseur depuis le portail.`,
    );
    assert.doesNotMatch(
      source,
      /api\.stripe\.com|api-m\.paypal\.com|api-m\.sandbox\.paypal\.com/,
      `La route ${label} ne doit contacter aucun fournisseur directement.`,
    );
    assert.match(
      source,
      /internal\/(portal|admin)\/subscriptions\/\$\{encodeURIComponent\(id\)\}\/cancel/,
      `La route ${label} doit deleguer la resiliation a API-INTERNAL.`,
    );
  }
});

// Le garde 409 rendait la resiliation morte pour TOUS les abonnements reels :
// apres la migration 071 il n'existe plus d'autre systeme d'abonnement.
check("aucun garde ne neutralise la resiliation Billing V2", () => {
  for (const [label, source] of [
    ["client", clientCancelRoute],
    ["admin", adminCancelRoute],
  ]) {
    assert.doesNotMatch(
      source,
      /BILLING_V2_CANCELLATION_NOT_AVAILABLE/,
      `La route ${label} ne doit plus refuser les abonnements Billing V2.`,
    );
  }
});

check("les boutons de resiliation passent par le BFF", () => {
  for (const [label, source] of [
    ["admin", adminCancelButton],
    ["client", clientCancelButton],
  ]) {
    assert.match(
      source,
      /\/api\/(admin\/)?subscriptions\//,
      `Le bouton ${label} doit appeler le BFF, jamais l API interne.`,
    );
    assert.doesNotMatch(
      source,
      /paypal\.com|stripe\.com|NEXT_PUBLIC_/,
      `Le bouton ${label} ne doit contacter aucun provider directement.`,
    );
  }
});

// --- 4. Les webhooks ne sont qu un signal relaye. -------------------------

check("les webhooks provider sont relayes vers l API interne", () => {
  assert.match(paypalWebhookRoute, /\/internal\/webhooks\/paypal/);
  assert.match(stripeWebhookRoute, /\/internal\/webhooks\/stripe/);
  assert.match(programCs, /"\/internal\/webhooks\/paypal"/);
  assert.match(programCs, /"\/internal\/webhooks\/stripe"/);
});

check("la signature webhook est verifiee avant traitement", () => {
  assert.match(paypalWebhookLib, /export async function verifyPayPalWebhookSignature/);
});

check("un evenement que Billing V2 ne rattache pas est acquitte sans effet", () => {
  // Il n'existe plus de second destinataire d'abonnement : ecrire ailleurs
  // reviendrait a ressusciter le rail supprime.
  assert.match(
    programCs,
    /TryCreatePayPalWebhook\([\s\S]{0,400}?status = "ignored"/,
    "Un evenement PayPal non rattache doit etre ignore, pas redirige.",
  );
});

// --- 5. Le retour navigateur ne fait pas autorite. ------------------------

// Il n'existe qu'UN parcours de retour : celui de Billing V2. Les deux routes
// heritees appelaient des endpoints internes supprimes ; elles ne
// retournaient plus qu'une erreur.
check("le retour de souscription revalide cote serveur", () => {
  assert.match(billingV2ReturnRoute, /import "server-only"/);
  assert.match(
    billingV2ReturnRoute,
    /internal\/portal\/billing-v2\/provider-return/,
    "Le retour doit etre revalide par API-INTERNAL, pas cru sur parole.",
  );
  assert.doesNotMatch(
    billingV2ReturnRoute,
    /amountCents|priceAmountCents/,
    "Aucun montant ne doit transiter par l URL de retour.",
  );
});

check("les routes de retour heritees ont disparu", () => {
  for (const dead of [
    "app/api/subscriptions/return/route.ts",
    "app/api/subscriptions/stripe/return/route.ts",
    "lib/subscription-return.ts",
  ]) {
    assert.equal(
      existsSync(new URL(`../${dead}`, import.meta.url)),
      false,
      `${dead} appelait un endpoint interne supprime : il ne doit pas revenir.`,
    );
  }
});

check("le helper PayPal reste server-only", () => {
  assert.match(paypalLib, /import "server-only"/);
  assert.doesNotMatch(paypalLib, /NEXT_PUBLIC_/);
});

if (failures.length > 0) {
  for (const [label, message] of failures) {
    console.error(`  FAIL ${label}`);
    console.error(`       ${message}`);
  }
  console.error(
    `\n${failures.length} verification(s) de contrat abonnements en echec.`,
  );
  process.exit(1);
}

console.log("Contrat abonnements Billing V2 verifie.");
