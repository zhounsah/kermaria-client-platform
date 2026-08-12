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
      data: [{ id: "offer-active", status: "active" }],
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
const getPublicCommercialCatalog = async () => {
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
    /import \{ getPublicCommercialCatalog \} from "@\/lib\/internal-api";/,
    "",
  );

assert.notEqual(
  executableContactRoute,
  contactRoute,
  "La route contact doit etre preparee pour son execution isolee.",
);
assert.doesNotMatch(executableContactRoute, /^import /m);
assert.match(contactRoute, /getPublicCommercialCatalog\(\)/);
assert.match(contactRoute, /offer\.id === offerReference\.value/);
assert.match(contactRoute, /offer\.status === "active"/);
assert.match(contactRoute, /code: "INVALID_OFFER_REFERENCE"/);
assert.doesNotMatch(contactRoute, /offer\.externalReference\s*===/);

const contactRuntime = await importPureTypeScript(
  `${contactRouteHarness}\n${executableContactRoute}`,
  "contact-route.ts",
);

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

for (const [label, offerReference] of [
  ["absent", undefined],
  ["null", null],
  ["blank", "   \t  "],
]) {
  const input = { ...validContactBody };
  if (offerReference !== undefined) {
    input.offerReference = offerReference;
  }
  const result = await invokeContact(input);
  assert.equal(result.response.status, 200, label);
  assert.equal(result.state.fetchCalls.length, 1, label);
  assert.equal(result.state.fetchCalls[0].body.offerReference, null, label);
  assert.equal(result.state.calls.includes("catalog"), false, label);
}

let stableInvalidOfferBody;
for (const offerReference of [42, false, {}, [], ["offer-active"]]) {
  const result = await invokeContact({ ...validContactBody, offerReference });
  assert.equal(result.response.status, 400, JSON.stringify(offerReference));
  assert.equal(result.body.code, "INVALID_OFFER_REFERENCE");
  assert.equal(result.body.correlation_id, "contact-correlation");
  assert.equal(result.state.calls.includes("catalog"), false);
  assert.equal(result.state.fetchCalls.length, 0);
  stableInvalidOfferBody ??= result.body;
  assert.deepEqual(result.body, stableInvalidOfferBody);
}

const overlongOffer = await invokeContact({
  ...validContactBody,
  offerReference: "x".repeat(65),
});
assert.equal(overlongOffer.response.status, 400);
assert.deepEqual(overlongOffer.body, stableInvalidOfferBody);
assert.equal(overlongOffer.state.calls.includes("catalog"), false);
assert.equal(overlongOffer.state.fetchCalls.length, 0);

const activeOffer = await invokeContact({
  ...validContactBody,
  offerReference: "  offer-active  ",
});
assert.equal(activeOffer.response.status, 200);
assert.equal(activeOffer.state.fetchCalls.length, 1);
assert.equal(activeOffer.state.fetchCalls[0].body.offerReference, "offer-active");
assert.equal(
  activeOffer.state.fetchCalls[0].headers["X-Correlation-Id"],
  "contact-correlation",
);
assert.equal(
  activeOffer.state.fetchCalls[0].headers["X-Service-Auth"],
  "test-service-token",
);

const mixedCatalog = {
  data: [
    {
      id: "offer-active",
      externalReference: "PUBLIC-ACTIVE",
      status: "active",
    },
    { id: "offer-inactive", status: "inactive" },
  ],
  source: "api-internal-persistent",
  correlationId: "catalog-correlation",
};
for (const offerReference of [
  "offer-unknown",
  "offer-inactive",
  "PUBLIC-ACTIVE",
  "OFFER-ACTIVE",
]) {
  const result = await invokeContact(
    { ...validContactBody, offerReference },
    { catalogResult: mixedCatalog },
  );
  assert.equal(result.response.status, 400, offerReference);
  assert.deepEqual(result.body, stableInvalidOfferBody, offerReference);
  assert.equal(result.state.fetchCalls.length, 0, offerReference);
}

for (const [label, configuration] of [
  [
    "unavailable source",
    {
      catalogResult: {
        data: [{ id: "offer-active", status: "active" }],
        source: "unavailable",
        correlationId: "catalog-correlation",
      },
    },
  ],
  [
    "catalog error",
    {
      catalogResult: {
        data: [{ id: "offer-active", status: "active" }],
        source: "api-internal-persistent",
        correlationId: "catalog-correlation",
        error: { code: "INTERNAL_API_UNAVAILABLE" },
      },
    },
  ],
  [
    "malformed data",
    {
      catalogResult: {
        data: { id: "offer-active", status: "active" },
        source: "api-internal-persistent",
        correlationId: "catalog-correlation",
      },
    },
  ],
  [
    "null catalog entry",
    {
      catalogResult: {
        data: [null],
        source: "api-internal-persistent",
        correlationId: "catalog-correlation",
      },
    },
  ],
  [
    "malformed entry after active offer",
    {
      catalogResult: {
        data: [{ id: "offer-active", status: "active" }, null],
        source: "api-internal-persistent",
        correlationId: "catalog-correlation",
      },
    },
  ],
  [
    "incomplete catalog entry",
    {
      catalogResult: {
        data: [{}],
        source: "api-internal-persistent",
        correlationId: "catalog-correlation",
      },
    },
  ],
  [
    "catalog entry without id",
    {
      catalogResult: {
        data: [{ status: "active" }],
        source: "api-internal-persistent",
        correlationId: "catalog-correlation",
      },
    },
  ],
  [
    "catalog entry without status",
    {
      catalogResult: {
        data: [{ id: "offer-active" }],
        source: "api-internal-persistent",
        correlationId: "catalog-correlation",
      },
    },
  ],
  [
    "catalog entry with invalid field types",
    {
      catalogResult: {
        data: [{ id: 42, status: false }],
        source: "api-internal-persistent",
        correlationId: "catalog-correlation",
      },
    },
  ],
  ["catalog exception", { catalogFailure: true }],
]) {
  const result = await invokeContact(
    { ...validContactBody, offerReference: "offer-active" },
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
  offerReference: "offer-active",
});
assert.equal(invalidOrdinaryField.response.status, 400);
assert.equal(invalidOrdinaryField.body.code, "INVALID_REQUEST");
assert.equal(invalidOrdinaryField.state.calls.includes("catalog"), false);

const activeCallOrder = activeOffer.state.calls;
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
    { ...validContactBody, offerReference: "offer-active" },
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
