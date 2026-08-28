"use client";

import type {
  DiagnosticRecommendationConfig,
  DiagnosticRecommendationProfileId,
  ManagedContentMutationResponse,
  ManagedContentPayload,
} from "@kermaria/shared";
import { DIAGNOSTIC_RECOMMENDATION_PROFILE_IDS } from "@kermaria/shared";
import { FormEvent, useRef, useState } from "react";
import { useRouter } from "next/navigation";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";
import {
  DIAGNOSTIC_RECOMMENDATION_CONTENT_KEY,
  DIAGNOSTIC_RECOMMENDATION_PROFILE_LABELS,
} from "@/lib/diagnostic-recommendation-config";

type DiagnosticPresetOption = {
  code: string;
  label: string;
  available: boolean;
};

type AdminDiagnosticRecommendationFormProps = {
  initialConfig: DiagnosticRecommendationConfig;
  presets: readonly DiagnosticPresetOption[];
};

export function AdminDiagnosticRecommendationForm({
  initialConfig,
  presets,
}: AdminDiagnosticRecommendationFormProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [config, setConfig] = useState(initialConfig);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<{
    tone: "success" | "error";
    text: string;
  } | null>(null);

  function updateRule(
    profileId: DiagnosticRecommendationProfileId,
    value: string,
  ) {
    const presetCode = value === "" ? null : value;
    setConfig((current) => ({
      ...current,
      rules: current.rules.map((rule) =>
        rule.profileId === profileId ? { ...rule, presetCode } : rule
      ),
    }));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmittingRef.current) {
      return;
    }

    const payload: ManagedContentPayload = {
      bodyMarkdown: JSON.stringify(config),
      versionLabel: null,
    };

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);

    const result = await requestBffJson<ManagedContentMutationResponse>(
      `/api/admin/content/${encodeURIComponent(DIAGNOSTIC_RECOMMENDATION_CONTENT_KEY)}`,
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      },
    );

    if (result.ok) {
      setMessage({
        tone: "success",
        text: "Les règles de recommandation du diagnostic ont été enregistrées.",
      });
      router.refresh();
    } else {
      setMessage({
        tone: "error",
        text: result.error.message,
      });
    }

    isSubmittingRef.current = false;
    setIsSubmitting(false);
  }

  return (
    <form className="admin-diagnostic-rules-form" onSubmit={handleSubmit}>
      <div className="admin-diagnostic-rules-grid">
        {DIAGNOSTIC_RECOMMENDATION_PROFILE_IDS.map((profileId) => {
          const metadata = DIAGNOSTIC_RECOMMENDATION_PROFILE_LABELS[profileId];
          const rule = config.rules.find((entry) => entry.profileId === profileId);
          const selectedCode = rule?.presetCode ?? "";
          const selectedOption = selectedCode
            ? presets.find((preset) => preset.code === selectedCode)
            : null;

          return (
            <section className="admin-diagnostic-rule-card" key={profileId}>
              <div>
                <span className="card-kicker">{metadata.label}</span>
                <p>{metadata.description}</p>
              </div>
              <label>
                Formule recommandée
                <select
                  onChange={(event) => updateRule(profileId, event.target.value)}
                  value={selectedCode}
                >
                  <option value="">Aucun parcours standard — demander un devis</option>
                  {presets.map((preset) => (
                    <option
                      disabled={!preset.available && preset.code !== selectedCode}
                      key={preset.code}
                      value={preset.code}
                    >
                      {preset.label}{preset.available ? "" : " — indisponible"}
                    </option>
                  ))}
                </select>
              </label>
              {selectedOption && !selectedOption.available ? (
                <p className="field-hint">
                  Cette formule n&apos;est plus disponible dans le catalogue public.
                  Le diagnostic basculera vers un cadrage tant qu&apos;elle n&apos;est
                  pas republiée ou remplacée.
                </p>
              ) : null}
            </section>
          );
        })}
      </div>

      <div className="content-panel admin-diagnostic-rules-safety">
        <strong>Ce réglage choisit la formule de base, pas le prix.</strong>
        <p>
          Le volume de stockage, le nombre d&apos;utilisateurs et les besoins VPN
          ou bureau Windows continuent d&apos;ajuster la sélection Billing V2.
          Le tarif reste calculé exclusivement côté serveur.
        </p>
      </div>

      {message ? (
        <FormMessage
          title={
            message.tone === "success"
              ? "Configuration enregistrée"
              : "Enregistrement impossible"
          }
          tone={message.tone}
        >
          <p>{message.text}</p>
        </FormMessage>
      ) : null}

      <div className="stack-row">
        <SubmitButton
          idleLabel="Enregistrer les règles"
          isSubmitting={isSubmitting}
          submittingLabel="Enregistrement..."
        />
      </div>
    </form>
  );
}
