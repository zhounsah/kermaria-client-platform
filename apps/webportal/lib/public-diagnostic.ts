import type {
  BillingV2PublicCatalog,
  BillingV2PublicSelection,
  DiagnosticAnswers,
  DiagnosticRecommendation,
  DiagnosticRecommendationReasonCode,
  DiagnosticRecommendationWarningCode,
} from "@kermaria/shared";

import {
  SERVICE_CODES,
  buildBaselineSelection,
  selectableTiers,
} from "@/lib/billing-v2-formules";
import { MAX_ADDITIONAL_USERS } from "@/lib/billing-v2-selection";

const PRESET_DOSSIER = "pack-dossier-securise";
const PRESET_ACCESS = "pack-acces-distance";
const PRESET_WINDOWS = "pack-bureau-windows-distance";
const PRESET_PRO = "pack-pro-association";
const MAX_TOTAL_USERS = MAX_ADDITIONAL_USERS + 1;

/**
 * Traduit les réponses du diagnostic en intention commerciale Billing V2.
 *
 * Cette fonction ne calcule aucun prix. Elle ne manipule que des codes et des
 * paliers publics exposés par le catalogue. Le devis reste exclusivement du
 * ressort de `/api/formules/devis` / BillingV2PricingEngine.
 */
export function recommendOffer(
  answers: DiagnosticAnswers,
  catalog: BillingV2PublicCatalog,
): DiagnosticRecommendation {
  const reasons = new Set<DiagnosticRecommendationReasonCode>();
  const warnings = new Set<DiagnosticRecommendationWarningCode>();
  const users = answers.users ?? 1;
  const storage = answers.estimatedStorageGb;

  if (storage === null) {
    warnings.add("storage_unknown");
  } else if (storage === "above_public_max") {
    warnings.add("storage_requires_quote");
  }

  if (answers.backupFrequency === "unknown") {
    warnings.add("backup_frequency_unknown");
  }

  if (users < 1 || users > MAX_TOTAL_USERS) {
    warnings.add("users_require_quote");
  }
  if (answers.customerType === "other") {
    warnings.add("other_structure_requires_review");
  }
  if (
    answers.restoreTestRecency === "never"
    || answers.restoreTestRecency === "more_than_12_months"
    || answers.restoreTestRecency === "unknown"
  ) {
    warnings.add("no_recent_restore_test");
  }
  if (answers.continuityPlan === "no" || answers.continuityPlan === "unknown") {
    warnings.add("no_continuity_plan");
  }

  const personalStorageTiers = selectableTiers(catalog, SERVICE_CODES.storagePersonal)
    .filter((tier) => tier.numericValue !== null)
    .sort((left, right) => (left.numericValue ?? 0) - (right.numericValue ?? 0));
  const selectedStorageTier = typeof storage === "number"
    ? personalStorageTiers.find((tier) => (tier.numericValue ?? 0) >= storage) ?? null
    : null;

  if (typeof storage === "number" && !selectedStorageTier) {
    warnings.add("storage_requires_quote");
  }

  const needsWindows = answers.needsWindowsDesktop === true;
  const needsVpn = answers.needsVpn === true;
  const teamOrStructure =
    answers.customerType === "association"
    || answers.customerType === "business"
    || users > 1;

  if (needsWindows) {
    reasons.add("needs_windows_desktop");
  }
  if (needsVpn) {
    reasons.add("needs_vpn");
  }
  if (answers.needsRemoteFiles) {
    reasons.add("needs_remote_files");
  }
  if (teamOrStructure) {
    reasons.add(
      answers.customerType === "association"
        ? "association_context"
        : "team_or_structure",
    );
  }
  if (answers.recoveryImportance === "high") {
    reasons.add("strong_recovery_need");
  }

  let presetCode = PRESET_DOSSIER;
  if (needsWindows && teamOrStructure) {
    // Une structure reste sur la base Pro afin de conserver l'espace partagé
    // et le support, puis le bureau Windows est ajouté comme composant V2.
    presetCode = PRESET_PRO;
  } else if (needsWindows) {
    presetCode = PRESET_WINDOWS;
  } else if (teamOrStructure) {
    presetCode = PRESET_PRO;
  } else if (needsVpn) {
    presetCode = PRESET_ACCESS;
  } else {
    reasons.add("simple_backup");
  }

  const preset = catalog.presets.find((item) => item.code === presetCode);
  const commitment = catalog.commitments.find(
    (item) => item.code === "FLEX"
      && item.paymentOptions.some((option) => option.paymentMode === "monthly"),
  ) ?? catalog.commitments.find(
    (item) => item.months <= 1
      && item.paymentOptions.some((option) => option.paymentMode === "monthly"),
  );

  if (!preset || !commitment || hasBlockingWarning(warnings)) {
    return requiresQuote(reasons, warnings);
  }

  const baseline = buildBaselineSelection(preset, commitment.code);
  const vpnTierCode = needsVpn && !baseline.vpnTierCode
    ? selectableTiers(catalog, SERVICE_CODES.vpn)[0]?.code ?? null
    : baseline.vpnTierCode;

  const selection: BillingV2PublicSelection = {
    ...baseline,
    paymentMode: "monthly",
    storagePersonalTierCode:
      selectedStorageTier?.code ?? baseline.storagePersonalTierCode,
    vpnTierCode,
    remoteDesktop: baseline.remoteDesktop || needsWindows,
    additionalUsers: Math.max(0, users - 1),
  };

  if (typeof storage === "number") {
    reasons.add("storage_within_pack");
  }

  return {
    status: "standard",
    reasons: [...reasons],
    warnings: [...warnings],
    suggestedOptions: [],
    selection,
  };
}

function hasBlockingWarning(
  warnings: Set<DiagnosticRecommendationWarningCode>,
) {
  return (
    warnings.has("storage_requires_quote")
    || warnings.has("users_require_quote")
    || warnings.has("other_structure_requires_review")
  );
}

function requiresQuote(
  reasons: Set<DiagnosticRecommendationReasonCode>,
  warnings: Set<DiagnosticRecommendationWarningCode>,
): DiagnosticRecommendation {
  return {
    status: "requires_quote",
    reasons: [...reasons],
    warnings: [...warnings],
    suggestedOptions: [],
    selection: null,
  };
}
