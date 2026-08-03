"use client";

import type {
  DemoContentTemplateSummary,
  DemoKind,
  DemoProfilePayload,
  DemoProfileSummary,
} from "@kermaria/shared";
import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type DemoProfileFormProps = {
  templates: DemoContentTemplateSummary[];
};

export function DemoProfileForm({ templates }: DemoProfileFormProps) {
  const router = useRouter();
  const [key, setKey] = useState("");
  const [label, setLabel] = useState("");
  const [kind, setKind] = useState<DemoKind>("showcase");
  const [contentTemplateKey, setContentTemplateKey] = useState("");
  const [lifetimeDays, setLifetimeDays] = useState("14");
  const [status, setStatus] = useState("active");
  const [adProvisioningMode, setAdProvisioningMode] = useState("real_scoped");
  const [adGroups, setAdGroups] = useState("GG_DEMO_RDS\nGG_DEMO_VPN");
  const [storageQuotaGo, setStorageQuotaGo] = useState("5");
  const [rdsSessionMode, setRdsSessionMode] = useState("native");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [savedKey, setSavedKey] = useState<string | null>(null);

  const isTrial = kind === "trial";

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmitting) {
      return;
    }

    setIsSubmitting(true);
    setErrorMessage(null);
    setSavedKey(null);

    const parsedGroups = adGroups
      .split(/[\n,]/)
      .map((group) => group.trim())
      .filter((group) => group.length > 0);

    const payload: DemoProfilePayload = {
      key,
      label,
      kind,
      contentTemplateKey: contentTemplateKey || null,
      lifetimeDays: lifetimeDays === "" ? null : Number(lifetimeDays),
      status,
      adProvisioningMode: isTrial ? adProvisioningMode : "off",
      adGroups: isTrial ? parsedGroups : [],
      storageQuotaGo: isTrial && storageQuotaGo !== "" ? Number(storageQuotaGo) : null,
      rdsSessionMode: isTrial ? rdsSessionMode : "off",
    };

    const result = await requestBffJson<DemoProfileSummary>(
      "/api/admin/demo/profiles",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      },
    );

    setIsSubmitting(false);

    if (!result.ok) {
      setErrorMessage(result.error.message);
      return;
    }

    setSavedKey(result.data.key);
    router.refresh();
  }

  return (
    <form className="form-grid" onSubmit={handleSubmit}>
      <div className="form-field">
        <label htmlFor="profile-key">Clé (identifiant stable)</label>
        <input
          id="profile-key"
          maxLength={64}
          onChange={(event) => setKey(event.target.value)}
          pattern="[a-z0-9]+(?:-[a-z0-9]+)*"
          placeholder="ex. showcase-tpe"
          required
          type="text"
          value={key}
        />
        <p className="form-hint">
          Minuscules, chiffres et tirets. Réutiliser une clé existante met à
          jour le profil.
        </p>
      </div>

      <div className="form-field">
        <label htmlFor="profile-label">Libellé</label>
        <input
          id="profile-label"
          maxLength={200}
          onChange={(event) => setLabel(event.target.value)}
          required
          type="text"
          value={label}
        />
      </div>

      <div className="form-field">
        <label htmlFor="profile-kind">Type</label>
        <select
          id="profile-kind"
          onChange={(event) => setKind(event.target.value as DemoKind)}
          value={kind}
        >
          <option value="showcase">Vitrine (inerte)</option>
          <option value="trial">Essai réel restreint</option>
        </select>
      </div>

      <div className="form-field">
        <label htmlFor="profile-template">Template de contenu</label>
        <select
          id="profile-template"
          onChange={(event) => setContentTemplateKey(event.target.value)}
          value={contentTemplateKey}
        >
          <option value="">Aucun</option>
          {templates.map((template) => (
            <option key={template.key} value={template.key}>
              {template.label}
            </option>
          ))}
        </select>
      </div>

      <div className="form-field">
        <label htmlFor="profile-lifetime">Durée de vie (jours)</label>
        <input
          id="profile-lifetime"
          max={365}
          min={0}
          onChange={(event) => setLifetimeDays(event.target.value)}
          type="number"
          value={lifetimeDays}
        />
      </div>

      <div className="form-field">
        <label htmlFor="profile-status">Statut</label>
        <select
          id="profile-status"
          onChange={(event) => setStatus(event.target.value)}
          value={status}
        >
          <option value="active">Actif</option>
          <option value="inactive">Inactif</option>
        </select>
      </div>

      {isTrial ? (
        <>
          <div className="form-field">
            <label htmlFor="profile-ad-mode">Provisioning AD</label>
            <select
              id="profile-ad-mode"
              onChange={(event) => setAdProvisioningMode(event.target.value)}
              value={adProvisioningMode}
            >
              <option value="off">Désactivé</option>
              <option value="mock">Simulé</option>
              <option value="real_scoped">Réel cadré</option>
            </select>
          </div>

          <div className="form-field">
            <label htmlFor="profile-ad-groups">
              Groupes AD (un par ligne)
            </label>
            <textarea
              id="profile-ad-groups"
              onChange={(event) => setAdGroups(event.target.value)}
              rows={3}
              value={adGroups}
            />
            <p className="form-hint">
              ASCII sans accent (ex. GG_DEMO_RDS). Domaine clients.home.bzh.
            </p>
          </div>

          <div className="form-field">
            <label htmlFor="profile-quota">Quota stockage (Go)</label>
            <input
              id="profile-quota"
              min={0}
              onChange={(event) => setStorageQuotaGo(event.target.value)}
              type="number"
              value={storageQuotaGo}
            />
          </div>

          <div className="form-field">
            <label htmlFor="profile-rds">Session RDS</label>
            <select
              id="profile-rds"
              onChange={(event) => setRdsSessionMode(event.target.value)}
              value={rdsSessionMode}
            >
              <option value="off">Désactivée</option>
              <option value="native">Limites natives (GPO)</option>
            </select>
          </div>
        </>
      ) : (
        <p className="form-hint">
          Profil vitrine : aucun accès réel (AD, stockage, RDS restent inertes).
        </p>
      )}

      {errorMessage ? (
        <FormMessage title="Enregistrement impossible" tone="error">
          {errorMessage}
        </FormMessage>
      ) : null}

      {savedKey ? (
        <FormMessage title="Profil enregistré" tone="success">
          Le profil <strong>{savedKey}</strong> a été enregistré.
        </FormMessage>
      ) : null}

      <div className="form-actions">
        <SubmitButton
          idleLabel="Enregistrer le profil"
          isSubmitting={isSubmitting}
          submittingLabel="Enregistrement…"
        />
      </div>
    </form>
  );
}
