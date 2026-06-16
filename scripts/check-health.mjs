const apiBaseUrl = stripTrailingSlash(
  process.env.API_INTERNAL_BASE_URL?.trim() || "http://127.0.0.1:5000",
);
const webportalBaseUrl = stripTrailingSlash(
  process.env.WEBPORTAL_BASE_URL?.trim() || "http://127.0.0.1:3000",
);

const checks = [
  {
    label: "API-INTERNAL /health",
    url: `${apiBaseUrl}/health`,
    validate(payload) {
      return typeof payload?.status === "string" && typeof payload?.service === "string";
    },
  },
  {
    label: "API-INTERNAL /ready",
    url: `${apiBaseUrl}/ready`,
    validate(payload) {
      return payload?.check === "ready" && payload?.status === "healthy";
    },
  },
  {
    label: "API-INTERNAL /health/ready",
    url: `${apiBaseUrl}/health/ready`,
    validate(payload) {
      return payload?.check === "ready" && payload?.status === "healthy";
    },
  },
  {
    label: "WEBPORTAL /api/health/live",
    url: `${webportalBaseUrl}/api/health/live`,
    validate(payload) {
      return payload?.check === "live" && payload?.status === "healthy";
    },
  },
  {
    label: "WEBPORTAL /api/health/ready",
    url: `${webportalBaseUrl}/api/health/ready`,
    validate(payload) {
      return payload?.check === "ready" && payload?.status === "healthy";
    },
  },
];

try {
  for (const check of checks) {
    const response = await fetch(check.url, {
      headers: {
        Accept: "application/json",
        "X-Correlation-Id": `check-health-${crypto.randomUUID()}`,
      },
      signal: AbortSignal.timeout(8000),
    });

    if (!response.ok) {
      throw new Error(`${check.label} a repondu HTTP ${response.status}.`);
    }

    const correlationId = response.headers.get("X-Correlation-Id");
    if (!correlationId) {
      throw new Error(`${check.label} ne retourne pas X-Correlation-Id.`);
    }

    const contentType = response.headers.get("content-type") ?? "";
    if (!contentType.toLowerCase().includes("application/json")) {
      throw new Error(`${check.label} ne retourne pas du JSON.`);
    }

    const bodyText = await response.text();
    if (containsSensitiveHealthContent(bodyText)) {
      throw new Error(`${check.label} expose un contenu sensible.`);
    }

    let payload;
    try {
      payload = JSON.parse(bodyText);
    } catch {
      throw new Error(`${check.label} retourne un JSON invalide.`);
    }

    if (!check.validate(payload)) {
      throw new Error(`${check.label} retourne un payload incoherent.`);
    }

    process.stdout.write(`OK  ${check.label}\n`);
  }

  process.stdout.write(
    "Verification des health checks V0.17 reussie.\n",
  );
} catch (error) {
  const message = error instanceof Error ? error.message : String(error);
  process.stderr.write(`Verification health en echec: ${message}\n`);
  process.exit(1);
}

function stripTrailingSlash(value) {
  return value.replace(/\/+$/u, "");
}

function containsSensitiveHealthContent(bodyText) {
  return /SQL_PASSWORD|SERVICE_AUTH_TOKEN|DEMO_[A-Z_]+|BEGIN [A-Z ]*PRIVATE KEY/u.test(
    bodyText,
  );
}
