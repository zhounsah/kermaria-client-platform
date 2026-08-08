import type {
  CatalogConfigurationInput,
  DiagnosticAnswers,
  DiagnosticRecommendation,
  DiagnosticRecommendationReasonCode,
  DiagnosticRecommendationWarningCode,
  PublicPackCode,
  ResolvedPublicPackManifest,
} from "@kermaria/shared";

type RecommendationPack = Pick<
  ResolvedPublicPackManifest,
  "key" | "capabilities"
>;

export function recommendOffer(
  answers: DiagnosticAnswers,
  catalog: readonly RecommendationPack[],
): DiagnosticRecommendation {
  const available = new Set(catalog.map((pack) => pack.key));
  const reasons = new Set<DiagnosticRecommendationReasonCode>();
  const warnings = new Set<DiagnosticRecommendationWarningCode>();
  const users = answers.users ?? 1;
  const storage = answers.estimatedStorageGb;

  if (storage === null) {
    warnings.add("storage_unknown");
  } else if (storage > 64) {
    warnings.add("storage_requires_quote");
  }

  if (answers.backupFrequency === "unknown") {
    warnings.add("backup_frequency_unknown");
  }

  if (users > 2) {
    warnings.add("users_require_quote");
  }
  if (answers.customerType === "other") {
    warnings.add("other_structure_requires_review");
  }
  if (answers.restoreTestRecency === "never"
      || answers.restoreTestRecency === "more_than_12_months"
      || answers.restoreTestRecency === "unknown") {
    warnings.add("no_recent_restore_test");
  }
  if (answers.continuityPlan === "no" || answers.continuityPlan === "unknown") {
    warnings.add("no_continuity_plan");
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

  let targetPack: PublicPackCode = "pack-dossier-securise";
  if (needsWindows) {
    targetPack = "pack-bureau-windows-distance";
  } else if (teamOrStructure || (storage !== null && storage > 32)) {
    targetPack = "pack-pro-association";
  } else if (needsVpn) {
    targetPack = "pack-acces-distance";
  } else {
    reasons.add("simple_backup");
  }

  if (needsWindows && users > 1) {
    warnings.add("windows_team_requires_quote");
  }
  if (needsWindows && storage !== null && storage > 32) {
    warnings.add("windows_storage_requires_quote");
  }

  const target = catalog.find((pack) => pack.key === targetPack);
  if (!target || !available.has(targetPack) || hasBlockingWarning(warnings)) {
    return {
      status: "requires_quote",
      offerId: null,
      reasons: [...reasons],
      warnings: [...warnings],
      suggestedOptions: [],
      configuration: null,
    };
  }

  if (storage === null || storage <= target.capabilities.includedStorageGb) {
    reasons.add("storage_within_pack");
  }

  return {
    status: "standard",
    offerId: targetPack,
    reasons: [...reasons],
    warnings: [...warnings],
    suggestedOptions: [],
    configuration: {
      packKey: targetPack,
      commitmentMonths: 1,
      paymentMode: "monthly",
      users: answers.users,
      storageGb: answers.estimatedStorageGb,
      needsVpn: target.capabilities.supportsVpn ? true : answers.needsVpn,
      needsWindowsDesktop: target.capabilities.supportsWindowsDesktop
        ? true
        : answers.needsWindowsDesktop,
    } satisfies CatalogConfigurationInput,
  };
}

function hasBlockingWarning(
  warnings: Set<DiagnosticRecommendationWarningCode>,
) {
  return (
    warnings.has("storage_requires_quote")
    || warnings.has("users_require_quote")
    || warnings.has("windows_storage_requires_quote")
    || warnings.has("windows_team_requires_quote")
    || warnings.has("other_structure_requires_review")
  );
}
