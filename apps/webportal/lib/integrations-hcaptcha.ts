import "server-only";

import type { IntegrationView } from "@kermaria/shared";

// hCaptcha est la seule integration dont la configuration vit cote WEBPORTAL :
// c'est le BFF qui verifie le jeton a l'inscription, API-INTERNAL ne voit ni la
// cle publique ni le secret. Cette description est donc composee ici, puis
// ajoutee a la vue renvoyee par API-INTERNAL — sans jamais transporter le
// secret, dont seule la presence est publiee.
const PLACEHOLDER_MARKERS = [
  "changeme",
  "change-me",
  "placeholder",
  "replace_with",
  "replace-with",
  "example",
];

function isPlaceholder(value: string): boolean {
  const normalized = value.toLowerCase();
  return normalized.length === 0
    || normalized === "test"
    || normalized.startsWith("test")
    || PLACEHOLDER_MARKERS.some((marker) => normalized.includes(marker));
}

export function describeHCaptchaIntegration(): IntegrationView {
  const siteKey = process.env.HCAPTCHA_SITE_KEY?.trim() ?? "";
  const secret = process.env.HCAPTCHA_SECRET_KEY?.trim() ?? "";
  const production = process.env.NODE_ENV === "production";
  const secretUsable = secret.length > 0 && !isPlaceholder(secret);
  const siteKeyUsable = siteKey.length > 0 && !isPlaceholder(siteKey);

  // Fail-closed en production : sans secret exploitable, chaque soumission est
  // refusee. Hors production, la verification est sautee pour rester testable —
  // c'est un ecart assumé qu'il faut afficher, pas masquer.
  const state = secretUsable && siteKeyUsable
    ? "healthy"
    : production
      ? "critical"
      : "warning";

  return {
    key: "hcaptcha",
    label: "hCaptcha — inscription",
    mode: production ? "production" : "development",
    configured: secretUsable && siteKeyUsable,
    state,
    warning: secretUsable && siteKeyUsable
      ? null
      : production
        ? "Clé absente ou factice : toute demande d'inscription est refusée."
        : "Clé absente ou factice : hors production, la vérification est sautée.",
    riskNote:
      "Vérifié par le BFF au moment de l'inscription : API-INTERNAL ne voit ni la clé publique ni le secret.",
    facts: [
      { label: "Clé publique", value: siteKeyUsable ? "Configurée" : "Non configurée", kind: "state" },
      { label: "Secret", value: secretUsable ? "Configuré" : "Non configuré", kind: "secret" },
      {
        label: "Comportement sans clé",
        value: production ? "Inscription refusée" : "Vérification sautée",
        kind: "state",
      },
    ],
    operations: [
      {
        key: "hcaptcha_verify",
        label: "Vérifier la connectivité",
        description:
          "Non proposé : la vérification hCaptcha exige un jeton produit par un vrai widget, dans un vrai navigateur.",
        available: false,
        unavailableReason:
          "Aucun contrôle sans jeton de widget n'est possible côté serveur.",
      },
    ],
    lastSuccessAt: null,
    lastErrorAt: null,
    lastErrorSummary: null,
  };
}
