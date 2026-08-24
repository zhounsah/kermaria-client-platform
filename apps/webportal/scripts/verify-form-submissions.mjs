import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import ts from "typescript";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

async function importPureTypeScript(source, label) {
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

  const encoded = Buffer.from(transpiled.outputText).toString("base64");
  return import(`data:text/javascript;base64,${encoded}`);
}

const cases = [
  {
    component: "components/SupportRequestForm.tsx",
    route: "/api/support-requests",
    routeFile: "app/api/support-requests/route.ts",
  },
  {
    component: "components/ServiceRequestForm.tsx",
    route: "/api/service-requests",
    routeFile: "app/api/service-requests/route.ts",
  },
];

for (const testCase of cases) {
  const component = await readFile(
    new URL(`../${testCase.component}`, import.meta.url),
    "utf8",
  );
  const route = await readFile(
    new URL(`../${testCase.routeFile}`, import.meta.url),
    "utf8",
  );

  assert.match(component, /event\.preventDefault\(\)/);
  assert.match(component, new RegExp(`action="${testCase.route}"`));
  assert.match(component, /method="post"/);
  assert.match(
    component,
    new RegExp(`"${testCase.route.replaceAll("/", "\\/")}"`),
  );
  assert.match(component, /requestBffJson/);
  assert.match(component, /method:\s*"POST"/);
  assert.match(component, /submission\.result\.reference/);
  assert.match(component, /isSubmittingRef\.current/);
  assert.match(component, /aria-invalid/);
  assert.match(component, /SubmitButton/);
  assert.match(component, /FormMessage/);
  assert.doesNotMatch(component, /URLSearchParams|FormData|method="get"/i);
  assert.match(route, /export async function POST\(/);
  assert.match(route, /parse(?:Support|Service)RequestPayload/);
}

const serviceRequestForm = await readFile(
  new URL("../components/ServiceRequestForm.tsx", import.meta.url),
  "utf8",
);
const serviceRequestRoute = await readFile(
  new URL("../app/api/service-requests/route.ts", import.meta.url),
  "utf8",
);

for (const field of ["catalogItemId", "subject", "description"]) {
  assert.match(serviceRequestForm, new RegExp(`${field}:`));
}

for (const legacyField of ["serviceId", "timeline", "context"]) {
  assert.doesNotMatch(
    serviceRequestForm,
    new RegExp(`payload\\.${legacyField}|${legacyField}:`),
  );
  assert.doesNotMatch(
    serviceRequestRoute,
    new RegExp(`payload\\.${legacyField}`),
  );
}

const contactRoute = await read("app/api/contact/route.ts");
const contactForm = await read("components/ContactForm.tsx");

assert.match(contactForm, /fieldErrorId/);
assert.match(contactForm, /aria-describedby=\{fieldErrors\.name/);
assert.match(contactForm, /id=\{fieldErrorId\("email"\)\}/);
assert.match(contactForm, /id=\{fieldErrorId\("message"\)\}/);

const contactRouteHarness = String.raw`
type NextRequest = any;

const CORRELATION_HEADER = "X-Correlation-Id";
const NextResponse = {
  json(body: unknown, init: { status?: number } = {}) {
    return new Response(JSON.stringify(body), {
      status: init.status ?? 200,
      headers: { "Content-Type": "application/json" },
    });
  },
};

let testState: any;

export function resetContactRouteTestState() {
  testState = {
    calls: [],
    rateDecision: { limited: false, retryAfterSeconds: 0 },
    catalogFailure: false,
    catalogResult: {
      data: { presets: [{ code: "pack-acces-distance" }] },
      source: "api-internal-persistent",
      correlationId: "catalog-correlation",
    },
    internalApiUrl: "http://api-internal.test",
    internalConfigFailure: false,
    upstreamStatus: 200,
    upstreamBody: { code: "EMAIL_SENT", message: "Message sent." },
    upstreamContentType: "application/json",
    fetchCalls: [],
  };
}

export function configureContactRouteTestState(next: any) {
  for (const key of Object.keys(next)) {
    testState[key] = next[key];
  }
}

export function recordContactRouteTestCall(label: string) {
  testState.calls.push(label);
}

export function inspectContactRouteTestState() {
  return {
    calls: [...testState.calls],
    fetchCalls: [...testState.fetchCalls],
  };
}

const resolveCorrelationId = (value: string | null) => {
  testState.calls.push(["correlation", value]);
  return value ?? "contact-correlation";
};
const getRequestIdentifier = () => {
  testState.calls.push("identifier");
  return "203.0.113.10";
};
const checkRateLimit = () => {
  testState.calls.push("rate-limit");
  return testState.rateDecision;
};
const getBillingV2FormulesCatalog = async () => {
  testState.calls.push("catalog");
  if (testState.catalogFailure) {
    throw new Error("catalog unavailable");
  }
  return testState.catalogResult;
};
const getInternalApiUrl = () => {
  testState.calls.push("internal-api-url");
  if (testState.internalConfigFailure) {
    throw new Error("invalid runtime config");
  }
  return testState.internalApiUrl;
};
const getInternalServiceHeaders = () => {
  testState.calls.push("service-headers");
  return { "X-Service-Auth": "test-service-token" };
};
const fetch = async (url: string, init: any) => {
  testState.calls.push("fetch");
  testState.fetchCalls.push({
    url,
    method: init.method,
    headers: init.headers,
    body: JSON.parse(init.body),
  });
  return new Response(
    testState.upstreamContentType === "application/json"
      ? JSON.stringify(testState.upstreamBody)
      : String(testState.upstreamBody),
    {
      status: testState.upstreamStatus,
      headers: { "Content-Type": testState.upstreamContentType },
    },
  );
};

resetContactRouteTestState();
`;

const executableContactRoute = contactRoute
  .replace('import "server-only";', "")
  .replace(
    /import \{ NextRequest, NextResponse \} from "next\/server";/,
    "",
  )
  .replace(
    /import \{ CORRELATION_HEADER, resolveCorrelationId \} from "@\/lib\/correlation";/,
    "",
  )
  .replace(
    /import \{[\s\S]*?\} from "@\/lib\/rate-limit";/,
    "",
  )
  .replace(
    /import \{[\s\S]*?\} from "@\/lib\/runtime-config";/,
    "",
  )
  .replace(
    /import \{ getBillingV2FormulesCatalog \} from "@\/lib\/internal-api";/,
    "",
  );

assert.notEqual(
  executableContactRoute,
  contactRoute,
  "La route contact doit etre preparee pour son execution isolee.",
);
assert.doesNotMatch(executableContactRoute, /^import /m);
// Le contexte joint a un message de contact est un code de formule V2, jamais
// un identifiant d'offre : il n'existe plus de second catalogue a interroger.
assert.match(contactRoute, /getBillingV2FormulesCatalog\(\)/);
assert.match(contactRoute, /preset\?\.code === formuleCode\.value/);
assert.match(contactRoute, /code: "INVALID_FORMULE_CODE"/);
assert.doesNotMatch(
  contactRoute,
  /offerReference|getPublicCommercialCatalog|commercial_offer/,
  "La route contact ne doit plus connaitre le catalogue commercial legacy.",
);

const contactRuntime = await importPureTypeScript(
  `${contactRouteHarness}\n${executableContactRoute}`,
  "contact-route.ts",
);

const publishedCatalog = {
  data: {
    presets: [
      { code: "pack-acces-distance", name: "Acces a distance" },
      { code: "pack-dossier-securise", name: "Dossier securise" },
    ],
  },
  source: "api-internal-persistent",
  correlationId: "catalog-correlation",
};

const validContactBody = {
  name: "Alice Exemple",
  email: "alice@example.test",
  subject: "Question commerciale",
  message: "Bonjour",
};

function makeContactRequest(body, { jsonFailure = false } = {}) {
  return {
    headers: {
      get(name) {
        return name.toLowerCase() === "x-correlation-id"
          ? "incoming-correlation"
          : null;
      },
    },
    async json() {
      contactRuntime.recordContactRouteTestCall("json");
      if (jsonFailure) {
        throw new SyntaxError("invalid json");
      }
      return body;
    },
  };
}

async function invokeContact(body, configuration = {}, requestOptions = {}) {
  contactRuntime.resetContactRouteTestState();
  contactRuntime.configureContactRouteTestState(configuration);
  const response = await contactRuntime.POST(
    makeContactRequest(body, requestOptions),
  );
  return {
    response,
    body: await response.json(),
    state: contactRuntime.inspectContactRouteTestState(),
  };
}

// Sans code de formule, aucune lecture de catalogue : un message de contact
// ordinaire ne doit pas dependre de la disponibilite de la facturation.
for (const [label, formuleCode] of [
  ["absent", undefined],
  ["null", null],
  ["blank", "   \t  "],
]) {
  const input = { ...validContactBody };
  if (formuleCode !== undefined) {
    input.formuleCode = formuleCode;
  }
  const result = await invokeContact(input);
  assert.equal(result.response.status, 200, label);
  assert.equal(result.state.fetchCalls.length, 1, label);
  assert.equal(result.state.fetchCalls[0].body.formuleCode, null, label);
  assert.equal(result.state.calls.includes("catalog"), false, label);
}

let stableInvalidFormuleBody;
for (const formuleCode of [
  42,
  false,
  {},
  [],
  ["pack-acces-distance"],
  "pack acces distance",
  "-pack",
  "p",
]) {
  const result = await invokeContact({ ...validContactBody, formuleCode });
  assert.equal(result.response.status, 400, JSON.stringify(formuleCode));
  assert.equal(result.body.code, "INVALID_FORMULE_CODE");
  assert.equal(result.body.correlation_id, "contact-correlation");
  assert.equal(result.state.calls.includes("catalog"), false);
  assert.equal(result.state.fetchCalls.length, 0);
  stableInvalidFormuleBody ??= result.body;
  assert.deepEqual(result.body, stableInvalidFormuleBody);
}

const overlongFormule = await invokeContact({
  ...validContactBody,
  formuleCode: "x".repeat(65),
});
assert.equal(overlongFormule.response.status, 400);
assert.deepEqual(overlongFormule.body, stableInvalidFormuleBody);
assert.equal(overlongFormule.state.calls.includes("catalog"), false);
assert.equal(overlongFormule.state.fetchCalls.length, 0);

const activeFormule = await invokeContact(
  { ...validContactBody, formuleCode: "  Pack-Acces-Distance  " },
  { catalogResult: publishedCatalog },
);
assert.equal(activeFormule.response.status, 200);
assert.equal(activeFormule.state.fetchCalls.length, 1);
assert.equal(
  activeFormule.state.fetchCalls[0].body.formuleCode,
  "pack-acces-distance",
  "Le code transmis a l API interne doit etre celui du catalogue, normalise.",
);
assert.equal(
  activeFormule.state.fetchCalls[0].headers["X-Correlation-Id"],
  "contact-correlation",
);
assert.equal(
  activeFormule.state.fetchCalls[0].headers["X-Service-Auth"],
  "test-service-token",
);

for (const formuleCode of ["pack-inconnu", "pack-pro-association"]) {
  const result = await invokeContact(
    { ...validContactBody, formuleCode },
    { catalogResult: publishedCatalog },
  );
  assert.equal(result.response.status, 400, formuleCode);
  assert.deepEqual(result.body, stableInvalidFormuleBody, formuleCode);
  assert.equal(result.state.fetchCalls.length, 0, formuleCode);
}

for (const [label, configuration] of [
  [
    "unavailable source",
    {
      catalogResult: {
        data: { presets: [{ code: "pack-acces-distance" }] },
        source: "unavailable",
        correlationId: "catalog-correlation",
      },
    },
  ],
  [
    "catalog error",
    {
      catalogResult: {
        data: { presets: [{ code: "pack-acces-distance" }] },
        source: "api-internal-persistent",
        correlationId: "catalog-correlation",
        error: { code: "INTERNAL_API_UNAVAILABLE" },
      },
    },
  ],
  [
    "donnees absentes",
    {
      catalogResult: {
        source: "api-internal-persistent",
        correlationId: "catalog-correlation",
      },
    },
  ],
  [
    "presets absents",
    {
      catalogResult: {
        data: {},
        source: "api-internal-persistent",
        correlationId: "catalog-correlation",
      },
    },
  ],
  [
    "presets non tabulaires",
    {
      catalogResult: {
        data: { presets: { code: "pack-acces-distance" } },
        source: "api-internal-persistent",
        correlationId: "catalog-correlation",
      },
    },
  ],
  ["catalog exception", { catalogFailure: true }],
]) {
  // Un catalogue illisible n est jamais un catalogue vide : refuser en 400
  // ferait passer une formule publiee pour une reference inventee.
  const result = await invokeContact(
    { ...validContactBody, formuleCode: "pack-acces-distance" },
    configuration,
  );
  assert.equal(result.response.status, 503, label);
  assert.equal(result.body.code, "INTERNAL_API_UNAVAILABLE", label);
  assert.equal(result.body.correlation_id, "contact-correlation", label);
  assert.equal(result.state.fetchCalls.length, 0, label);
}

const rateLimited = await invokeContact(
  validContactBody,
  { rateDecision: { limited: true, retryAfterSeconds: 17 } },
);
assert.equal(rateLimited.response.status, 429);
assert.equal(rateLimited.response.headers.get("Retry-After"), "17");
assert.deepEqual(rateLimited.state.calls, [
  ["correlation", null],
  "identifier",
  "rate-limit",
]);

const invalidJson = await invokeContact(validContactBody, {}, {
  jsonFailure: true,
});
assert.equal(invalidJson.response.status, 400);
assert.equal(invalidJson.body.code, "INVALID_REQUEST");
assert.equal(invalidJson.state.calls.includes("catalog"), false);

const invalidOrdinaryField = await invokeContact({
  ...validContactBody,
  name: "",
  formuleCode: "pack-acces-distance",
});
assert.equal(invalidOrdinaryField.response.status, 400);
assert.equal(invalidOrdinaryField.body.code, "INVALID_REQUEST");
assert.equal(invalidOrdinaryField.state.calls.includes("catalog"), false);

const activeCallOrder = activeFormule.state.calls;
for (const [earlier, later] of [
  ["rate-limit", "json"],
  ["json", "catalog"],
  ["catalog", "internal-api-url"],
  ["internal-api-url", "fetch"],
]) {
  assert.ok(
    activeCallOrder.indexOf(earlier) < activeCallOrder.indexOf(later),
    `${earlier} doit preceder ${later}.`,
  );
}

const previousNodeEnv = process.env.NODE_ENV;
process.env.NODE_ENV = "development";
try {
  const localFallback = await invokeContact(
    { ...validContactBody, formuleCode: "pack-acces-distance" },
    { internalApiUrl: undefined },
  );
  assert.equal(localFallback.response.status, 202);
  assert.equal(localFallback.body.code, "CONTACT_MOCK_ACCEPTED");
  assert.equal(localFallback.state.calls.includes("catalog"), true);
  assert.equal(localFallback.state.fetchCalls.length, 0);
} finally {
  if (previousNodeEnv === undefined) {
    delete process.env.NODE_ENV;
  } else {
    process.env.NODE_ENV = previousNodeEnv;
  }
}

for (const [upstreamStatus, expectedStatus] of [
  [422, 422],
  [503, 502],
]) {
  const result = await invokeContact(validContactBody, {
    upstreamStatus,
    upstreamBody: { code: "UPSTREAM_CODE", message: "Upstream message." },
  });
  assert.equal(result.response.status, expectedStatus);
  assert.equal(result.body.code, "UPSTREAM_CODE");
  assert.equal(result.body.message, "Upstream message.");
  assert.equal(result.body.correlation_id, "contact-correlation");
}

console.log("Vérification des formulaires BFF réussie.");
