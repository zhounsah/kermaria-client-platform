import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import ts from "typescript";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

async function importTypeScript(source, label) {
  const transpiled = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.ES2022, target: ts.ScriptTarget.ES2022 },
    fileName: label,
    reportDiagnostics: true,
  });
  const errors = (transpiled.diagnostics ?? []).filter(
    (diagnostic) => diagnostic.category === ts.DiagnosticCategory.Error,
  );
  assert.deepEqual(errors, [], `${label} doit etre transpile sans erreur.`);
  return import(`data:text/javascript;base64,${Buffer.from(transpiled.outputText).toString("base64")}`);
}

function validAnswers(profile) {
  const common = {
    profile,
    equipmentCount: "1-2",
    equipmentAge: "under3",
    performance: "none",
    updates: "automatic",
    backup: "automatic_external_tested",
    network: "stable",
    wifiCoverage: "good",
    mfa: "all",
    sharedAccounts: "no",
    phishing: "aware",
  };
  return profile === "individual" ? common : {
    ...common,
    guestWifi: "separate",
    continuity: "tested",
    businessDependence: "low",
  };
}

function validPayload(profile = "individual") {
  return {
    answers: validAnswers(profile),
    name: "  Jean\n Dupont  ",
    phone: "06 12 34 56 78",
    email: "jean@example.test",
    organisation: profile === "individual" ? "" : " Association exemple ",
    preferredTime: " Le matin ",
    comment: "Ligne 1\r\nLigne 2",
    consent: true,
    website: "",
  };
}

const callbackSource = await read("lib/diagnostic-callback.ts");
const callback = await importTypeScript(callbackSource, "diagnostic-callback.ts");
const preDiagnostic = await importTypeScript(
  await read("lib/pre-diagnostic.ts"),
  "pre-diagnostic.ts",
);

for (const profile of ["individual", "professional", "association"]) {
  const validated = callback.validateDiagnosticCallbackPayload(validPayload(profile));
  assert.ok(validated.payload, `Le payload ${profile} valide doit passer.`);
}

const foreignProfileKey = validPayload("individual");
foreignProfileKey.answers.guestWifi = "same";
assert.equal(callback.validateDiagnosticCallbackPayload(foreignProfileKey).payload, null, "Une cle professionnelle ne doit pas etre acceptee pour un particulier.");

const unknownKey = validPayload("professional");
unknownKey.answers.unknown = "value";
assert.equal(callback.validateDiagnosticCallbackPayload(unknownKey).payload, null, "Une cle inconnue doit etre refusee par le BFF.");

const unknownValue = validPayload("association");
unknownValue.answers.backup = "not-a-real-answer";
assert.ok(callback.validateDiagnosticCallbackPayload(unknownValue).payload, "Le BFF peut transporter une valeur syntaxiquement valide : API-INTERNAL reste l'autorite de la liste fermee.");

const normalized = callback.validateDiagnosticCallbackPayload(validPayload());
assert.equal(normalized.payload?.name, "Jean Dupont", "Les champs monolignes doivent normaliser les espaces et retours a la ligne.");
assert.equal(normalized.payload?.comment, "Ligne 1\nLigne 2", "Le commentaire conserve ses retours a la ligne apres normalisation.");
const callbackAnswers = preDiagnostic.scoringAnswersForProfile({
  ...validAnswers("individual"),
  commercialIntent: "backup_simple",
  commercialStorage: "32",
}, "individual");
assert.equal(callbackAnswers.commercialIntent, undefined, "Les réponses commerciales ne doivent jamais être envoyées dans le rappel de pré-diagnostic.");
assert.deepEqual(Object.keys(callbackAnswers).sort(), Object.keys(validAnswers("individual")).sort(), "Le rappel conserve exactement le schéma santé fermé du profil.");

const security = await importTypeScript(
  await read("lib/diagnostic-callback-security.ts"),
  "diagnostic-callback-security.ts",
);
const request = (headers, url = "https://zachary-it.fr/api/diagnostic/callback") => ({ headers: new Headers(headers), url });
assert.equal(security.validateDiagnosticCallbackRequest(request({ "content-type": "application/json", origin: "https://zachary-it.fr", "sec-fetch-site": "same-origin" })), null, "Une soumission JSON de meme origine doit etre acceptee.");
assert.equal(security.validateDiagnosticCallbackRequest(request({ "content-type": "application/json", origin: "http://localhost:3000", "sec-fetch-site": "same-origin" }, "http://localhost:3000/api/diagnostic/callback")), null, "localhost doit rester accepte dans un environnement de developpement reel.");
assert.equal(security.validateDiagnosticCallbackRequest(request({ "content-type": "text/plain", origin: "https://zachary-it.fr" }))?.status, 415, "text/plain doit etre refuse avant lecture du payload.");
assert.equal(security.validateDiagnosticCallbackRequest(request({ "content-type": "application/json", origin: "https://attacker.example", "sec-fetch-site": "cross-site" }))?.status, 403, "Une origine web tierce doit etre refusee.");

const rateLimit = await importTypeScript(
  (await read("lib/rate-limit.ts")).replace('import "server-only";\n\n', "").replace('import type { NextRequest } from "next/server";\n\n', ""),
  "rate-limit.ts",
);
const first = { headers: new Headers({ "x-real-ip": "203.0.113.8", "x-forwarded-for": "198.51.100.1" }) };
const second = { headers: new Headers({ "x-real-ip": "203.0.113.8", "x-forwarded-for": "198.51.100.2" }) };
assert.equal(rateLimit.getRequestIdentifier(first), rateLimit.getRequestIdentifier(second), "X-Forwarded-For client ne doit pas changer l'identite limitee.");
const rateKey = `diagnostic-proxy-test:${rateLimit.getRequestIdentifier(first)}:${Date.now()}`;
assert.equal(rateLimit.checkRateLimit(rateKey, 3, 60_000).limited, false);
assert.equal(rateLimit.checkRateLimit(rateKey, 3, 60_000).limited, false);
assert.equal(rateLimit.checkRateLimit(rateKey, 3, 60_000).limited, false);
assert.equal(rateLimit.checkRateLimit(`diagnostic-proxy-test:${rateLimit.getRequestIdentifier(second)}:${rateKey.split(":").at(-1)}`, 3, 60_000).limited, true, "Changer X-Forwarded-For ne doit pas ouvrir un nouveau bucket.");

const bffRoute = await read("app/api/diagnostic/callback/route.ts");
assert.match(bffRoute, /validateDiagnosticCallbackRequest\(request\)/, "La route BFF doit appliquer la garde MIME/origine avant le traitement.");
assert.doesNotMatch(bffRoute, /formRenderedAt|MIN_FILL_MS/, "Aucun delai de saisie ne doit produire un faux succes.");
assert.match(bffRoute, /payload\.website\)\s*\{[\s\S]*CALLBACK_SENT/, "Le honeypot seul peut conserver une reponse neutre.");

function mapPostBlock(source, path) {
  const marker = `"${path}"`;
  const routeStart = source.indexOf(marker);
  assert.notEqual(routeStart, -1, `Route API absente : ${path}`);
  const nextRoute = source.indexOf("app.MapPost(", routeStart + marker.length);
  return source.slice(routeStart, nextRoute === -1 ? source.length : nextRoute);
}

const apiProgram = await read("../../apps/api-internal/Program.cs");
const contactRoute = mapPostBlock(apiProgram, "/internal/public/contact-message");
const diagnosticRoute = mapPostBlock(apiProgram, "/internal/public/diagnostic-callback");
assert.match(contactRoute, /emailDispatch\.SendContactFormAsync\(/, "La route contact doit employer le dispatch historique qui conserve le corps.");
assert.doesNotMatch(contactRoute, /emailDispatch\.SendDiagnosticCallbackAsync\(/, "La route contact ne doit jamais emprunter le dispatch du pre-diagnostic.");
assert.match(diagnosticRoute, /emailDispatch\.SendDiagnosticCallbackAsync\(/, "La route pre-diagnostic doit employer le dispatch non persistant.");
assert.doesNotMatch(diagnosticRoute, /emailDispatch\.SendContactFormAsync\(/, "La route pre-diagnostic ne doit jamais emprunter le dispatch contact historique.");

console.log("Contrat pré-diagnostic, BFF et dispatch e-mail vérifié.");
