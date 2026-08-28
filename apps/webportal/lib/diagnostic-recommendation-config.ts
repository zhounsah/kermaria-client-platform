import type {
  DiagnosticRecommendationConfig,
  DiagnosticRecommendationProfileId,
  DiagnosticRecommendationRuleConfig,
} from "@kermaria/shared";
import { DIAGNOSTIC_RECOMMENDATION_PROFILE_IDS } from "@kermaria/shared";

export const DIAGNOSTIC_RECOMMENDATION_CONTENT_KEY =
  "diagnostic:recommendations" as const;

export const DEFAULT_DIAGNOSTIC_RECOMMENDATION_CONFIG: DiagnosticRecommendationConfig = {
  schemaVersion: 1,
  rules: [
    { profileId: "simple_backup", presetCode: "pack-dossier-securise" },
    { profileId: "vpn_access", presetCode: "pack-acces-distance" },
    { profileId: "windows_desktop", presetCode: "pack-bureau-windows-distance" },
    { profileId: "team_or_structure", presetCode: "pack-pro-association" },
    { profileId: "team_windows_desktop", presetCode: "pack-pro-association" },
  ],
};

export const DIAGNOSTIC_RECOMMENDATION_PROFILE_LABELS:
  Readonly<Record<DiagnosticRecommendationProfileId, {
    label: string;
    description: string;
  }>> = {
  simple_backup: {
    label: "Sauvegarde simple",
    description: "Protection de fichiers sans besoin VPN ni bureau Windows.",
  },
  vpn_access: {
    label: "Accès distant",
    description: "Accès privé à des fichiers ou à une ressource interne.",
  },
  windows_desktop: {
    label: "Bureau Windows",
    description: "Un utilisateur a besoin d'un environnement Windows distant complet.",
  },
  team_or_structure: {
    label: "Équipe / structure",
    description: "Plusieurs utilisateurs, une entreprise ou une association.",
  },
  team_windows_desktop: {
    label: "Équipe + Bureau Windows",
    description: "Une structure ou une équipe a également besoin d'un bureau Windows distant.",
  },
};

const PRESET_CODE_PATTERN = /^[a-z0-9][a-z0-9-]{1,79}$/;

export function parseDiagnosticRecommendationConfig(
  raw: string | null | undefined,
): DiagnosticRecommendationConfig | null {
  if (!raw) {
    return null;
  }

  try {
    return validateDiagnosticRecommendationConfig(JSON.parse(raw));
  } catch {
    return null;
  }
}

export function validateDiagnosticRecommendationConfig(
  value: unknown,
): DiagnosticRecommendationConfig | null {
  if (!value || typeof value !== "object") {
    return null;
  }

  const candidate = value as Partial<DiagnosticRecommendationConfig>;
  if (candidate.schemaVersion !== 1 || !Array.isArray(candidate.rules)) {
    return null;
  }

  const expected = new Set<string>(DIAGNOSTIC_RECOMMENDATION_PROFILE_IDS);
  const seen = new Set<string>();
  const rules: DiagnosticRecommendationRuleConfig[] = [];

  for (const rule of candidate.rules) {
    if (!rule || typeof rule !== "object") {
      return null;
    }

    const profileId = (rule as { profileId?: unknown }).profileId;
    const presetCode = (rule as { presetCode?: unknown }).presetCode;
    if (
      typeof profileId !== "string"
      || !expected.has(profileId)
      || seen.has(profileId)
      || !(
        presetCode === null
        || typeof presetCode === "string" && PRESET_CODE_PATTERN.test(presetCode)
      )
    ) {
      return null;
    }

    seen.add(profileId);
    rules.push({
      profileId: profileId as DiagnosticRecommendationProfileId,
      presetCode,
    });
  }

  if (
    rules.length !== DIAGNOSTIC_RECOMMENDATION_PROFILE_IDS.length
    || seen.size !== DIAGNOSTIC_RECOMMENDATION_PROFILE_IDS.length
  ) {
    return null;
  }

  return {
    schemaVersion: 1,
    rules,
  };
}

export function resolveDiagnosticPresetCode(
  profileId: DiagnosticRecommendationProfileId,
  config: DiagnosticRecommendationConfig = DEFAULT_DIAGNOSTIC_RECOMMENDATION_CONFIG,
): string | null {
  return config.rules.find((rule) => rule.profileId === profileId)?.presetCode ?? null;
}
