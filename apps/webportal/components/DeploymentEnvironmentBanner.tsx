import { isDevelopmentDeployment } from "@/lib/deployment-environment";

/**
 * Bandeau permanent de l'instance DEV : aucun rendu en production.
 */
export function DeploymentEnvironmentBanner() {
  if (!isDevelopmentDeployment()) {
    return null;
  }

  const stripeMode =
    process.env.STRIPE_MODE?.trim().toLowerCase() === "test"
      ? "STRIPE TEST MODE"
      : "STRIPE DISABLED";

  return (
    <div
      role="status"
      style={{
        position: "sticky",
        top: 0,
        zIndex: 2147483647,
        background: "#b45309",
        color: "#ffffff",
        font: "700 13px/1.4 system-ui, sans-serif",
        letterSpacing: "0.04em",
        padding: "6px 12px",
        textAlign: "center",
      }}
    >
      DEVELOPMENT ENVIRONMENT — {stripeMode} — aucune donnée ni paiement réel
    </div>
  );
}
