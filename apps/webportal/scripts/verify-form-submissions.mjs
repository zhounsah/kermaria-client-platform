import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

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

console.log("Vérification des formulaires BFF réussie.");
