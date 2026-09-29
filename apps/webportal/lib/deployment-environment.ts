/**
 * Separation DEV/PROD du portail. APP_ENV est orthogonal a NODE_ENV : le
 * portail DEV tourne en build de production (NODE_ENV=production) mais
 * APP_ENV=Development lui interdit toute ressource transactionnelle reelle.
 *
 * Aucun message produit ici ne contient de valeur de cle : seule la famille
 * (prefixe) est nommee.
 */

export type DeploymentEnvironment = "Development" | "Production" | "Unspecified";

export const APP_ENV_RESPONSE_HEADER = "x-kermaria-app-env";
export const CONFIGURATION_EXIT_CODE = 78;

export function resolveDeploymentEnvironment(
  env: NodeJS.ProcessEnv = process.env,
): DeploymentEnvironment | null {
  const raw = env.APP_ENV?.trim().toLowerCase();
  if (!raw) {
    // Retrocompatibilite : la production en place ne pose pas APP_ENV.
    return env.NODE_ENV === "production" ? "Production" : "Unspecified";
  }
  if (raw === "development" || raw === "dev") {
    return "Development";
  }
  if (raw === "production" || raw === "prod") {
    return "Production";
  }
  return null;
}

export function isDevelopmentDeployment(
  env: NodeJS.ProcessEnv = process.env,
): boolean {
  return resolveDeploymentEnvironment(env) === "Development";
}

export function describeStripeKeyFamily(key: string | undefined): string {
  const normalized = key?.trim();
  if (!normalized) {
    return "none";
  }
  for (const family of ["sk_live", "sk_test", "rk_live", "rk_test"]) {
    if (normalized.startsWith(`${family}_`)) {
      return family;
    }
  }
  return "unknown";
}

export function findDeploymentEnvironmentViolations(
  env: NodeJS.ProcessEnv = process.env,
): string[] {
  const environment = resolveDeploymentEnvironment(env);
  if (environment === null) {
    return ["APP_ENV doit valoir Development ou Production."];
  }

  const violations: string[] = [];
  const stripeMode = env.STRIPE_MODE?.trim().toLowerCase();
  const keyFamily = describeStripeKeyFamily(env.STRIPE_SECRET_KEY);
  const publishableKey = env.STRIPE_PUBLISHABLE_KEY?.trim() ?? "";

  if (environment === "Development") {
    if (keyFamily === "sk_live" || keyFamily === "rk_live") {
      violations.push(
        `APP_ENV=Development refuse une cle Stripe live (STRIPE_SECRET_KEY de famille ${keyFamily}).`,
      );
    }
    if (publishableKey.startsWith("pk_live_")) {
      violations.push(
        "APP_ENV=Development refuse une cle publiable Stripe live (STRIPE_PUBLISHABLE_KEY).",
      );
    }
    if (stripeMode === "live") {
      violations.push("APP_ENV=Development refuse STRIPE_MODE=live.");
    }
    if (env.PAYPAL_MODE?.trim().toLowerCase() === "live") {
      violations.push("APP_ENV=Development refuse PAYPAL_MODE=live.");
    }
    const verify = env.STRIPE_WEBHOOK_VERIFY?.trim().toLowerCase();
    if (verify === "false" || verify === "0" || verify === "off") {
      violations.push(
        "APP_ENV=Development refuse STRIPE_WEBHOOK_VERIFY desactive.",
      );
    }
    if (!env.INTERNAL_API_URL?.trim()) {
      violations.push("APP_ENV=Development exige INTERNAL_API_URL.");
    }
  }

  if (environment === "Production") {
    if (keyFamily === "sk_test" || keyFamily === "rk_test") {
      violations.push(
        `APP_ENV=Production refuse une cle Stripe de test (STRIPE_SECRET_KEY de famille ${keyFamily}).`,
      );
    }
    if (publishableKey.startsWith("pk_test_")) {
      violations.push(
        "APP_ENV=Production refuse une cle publiable Stripe de test (STRIPE_PUBLISHABLE_KEY).",
      );
    }
    if (stripeMode === "test") {
      violations.push("APP_ENV=Production refuse STRIPE_MODE=test.");
    }
  }

  return violations;
}

/**
 * Le portail DEV ne parle qu'a une API qui s'annonce Development. Une API de
 * production (ou une version anterieure sans en-tete) est refusee.
 */
export async function findInternalApiIdentityViolation(
  env: NodeJS.ProcessEnv = process.env,
): Promise<string | null> {
  if (!isDevelopmentDeployment(env)) {
    return null;
  }

  const baseUrl = env.INTERNAL_API_URL?.trim().replace(/\/+$/, "");
  if (!baseUrl) {
    return "APP_ENV=Development exige INTERNAL_API_URL.";
  }

  try {
    const response = await fetch(`${baseUrl}/health/live`, {
      cache: "no-store",
      signal: AbortSignal.timeout(5000),
    });
    const announced = response.headers.get(APP_ENV_RESPONSE_HEADER);
    return announced === "Development"
      ? null
      : `INTERNAL_API_URL vise une API qui s'annonce ${announced ?? "sans identite"} au lieu de Development.`;
  } catch {
    return "INTERNAL_API_URL injoignable : identite de l'API DEV non verifiable.";
  }
}

export function describeDeploymentEnvironment(
  env: NodeJS.ProcessEnv = process.env,
): string[] {
  return [
    `Environment       : ${resolveDeploymentEnvironment(env) ?? "invalid"}`,
    `Internal API      : ${env.INTERNAL_API_URL?.trim() || "(none)"}`,
    `Stripe mode       : ${env.STRIPE_MODE?.trim().toLowerCase() || "disabled"}`,
    `Stripe key family : ${describeStripeKeyFamily(env.STRIPE_SECRET_KEY)}`,
    `PayPal mode       : ${env.PAYPAL_MODE?.trim().toLowerCase() || "disabled"}`,
  ];
}
