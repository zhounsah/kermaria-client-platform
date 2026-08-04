"use client";

import type {
  ClientSolution,
  ClientSolutionMutationResponse,
  ClientSolutionPayload,
} from "@kermaria/shared";
import {
  CLIENT_SOLUTION_LOGO_CONTENT_TYPES,
  CLIENT_SOLUTION_LOGO_MAX_SIZE_BYTES,
} from "@kermaria/shared";
import { FormEvent, startTransition, useRef, useState } from "react";
import { useRouter } from "next/navigation";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type AdminClientSolutionFormProps = {
  mode: "create" | "edit";
  solution?: ClientSolution | null;
};

type FormState = {
  title: string;
  slug: string;
  tagline: string;
  targetUrl: string;
  opensInNewTab: boolean;
  status: ClientSolutionPayload["status"];
  displayOrder: string;
};

const statusLabels: Record<FormState["status"], string> = {
  published: "Publiée (visible sur le site)",
  draft: "Brouillon (masquée)",
};

const LOGO_ACCEPT = CLIENT_SOLUTION_LOGO_CONTENT_TYPES.join(",");

export function AdminClientSolutionForm({
  mode,
  solution,
}: AdminClientSolutionFormProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [formState, setFormState] = useState<FormState>({
    title: solution?.title ?? "",
    slug: solution?.slug ?? "",
    tagline: solution?.tagline ?? "",
    targetUrl: solution?.targetUrl ?? "",
    opensInNewTab: solution?.opensInNewTab ?? true,
    status: solution?.status ?? "draft",
    displayOrder: String(solution?.displayOrder ?? 0),
  });
  const [selectedLogo, setSelectedLogo] = useState<File | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<{
    tone: "success" | "error" | "info";
    title: string;
    text: string;
  } | null>(null);

  function updateField<Key extends keyof FormState>(
    key: Key,
    value: FormState[Key],
  ) {
    setFormState((current) => ({ ...current, [key]: value }));
  }

  function buildPayload(): ClientSolutionPayload {
    return {
      slug: formState.slug.trim().toLowerCase() || null,
      title: formState.title.trim(),
      tagline: formState.tagline.trim() || null,
      targetUrl: formState.targetUrl.trim(),
      opensInNewTab: formState.opensInNewTab,
      status: formState.status,
      displayOrder: Number.parseInt(formState.displayOrder, 10) || 0,
    };
  }

  async function uploadLogo(solutionId: string) {
    if (!selectedLogo) {
      return null;
    }

    const body = new FormData();
    body.set("logo", selectedLogo);

    return requestBffJson<ClientSolutionMutationResponse>(
      `/api/admin/client-solutions/${encodeURIComponent(solutionId)}/logo`,
      { method: "POST", body },
    );
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmittingRef.current) {
      return;
    }

    if (selectedLogo && selectedLogo.size > CLIENT_SOLUTION_LOGO_MAX_SIZE_BYTES) {
      setMessage({
        tone: "error",
        title: "Logo trop volumineux",
        text: "Le logo ne doit pas dépasser 512 Ko.",
      });
      return;
    }

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);

    const endpoint =
      mode === "create"
        ? "/api/admin/client-solutions"
        : `/api/admin/client-solutions/${encodeURIComponent(solution!.id)}`;

    const saveResult = await requestBffJson<ClientSolutionMutationResponse>(
      endpoint as `/api/${string}`,
      {
        method: mode === "create" ? "POST" : "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(buildPayload()),
      },
    );

    if (!saveResult.ok) {
      setMessage({
        tone: "error",
        title: "Enregistrement impossible",
        text: saveResult.error.message,
      });
      isSubmittingRef.current = false;
      setIsSubmitting(false);
      return;
    }

    const solutionId = mode === "create" ? saveResult.data.id : solution!.id;

    if (selectedLogo) {
      const uploadResult = await uploadLogo(solutionId);
      if (!uploadResult?.ok) {
        setMessage({
          tone: "error",
          title: "Logo non envoyé",
          text:
            uploadResult?.error.message
            ?? "Le logo n'a pas pu être envoyé. La solution a bien été enregistrée.",
        });
        isSubmittingRef.current = false;
        setIsSubmitting(false);
        return;
      }
    }

    setSelectedLogo(null);
    setMessage({
      tone: "success",
      title: mode === "create" ? "Solution créée" : "Solution mise à jour",
      text:
        formState.status === "published"
          ? "La tuile est visible sur la page publique /solutions."
          : "La tuile est enregistrée en brouillon : elle reste masquée du site public.",
    });

    startTransition(() => {
      if (mode === "create") {
        router.replace(
          `/admin/solutions/${encodeURIComponent(solutionId)}`,
        );
      } else {
        router.refresh();
      }
    });

    isSubmittingRef.current = false;
    setIsSubmitting(false);
  }

  async function handleDeleteLogo() {
    if (!solution || isSubmittingRef.current) {
      return;
    }

    if (!window.confirm("Supprimer le logo actuel de cette solution ?")) {
      return;
    }

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);

    const result = await requestBffJson<ClientSolutionMutationResponse>(
      `/api/admin/client-solutions/${encodeURIComponent(solution.id)}/logo`,
      { method: "DELETE" },
    );

    if (result.ok) {
      setSelectedLogo(null);
      setMessage({
        tone: "success",
        title: "Logo supprimé",
        text: "La tuile affiche désormais les initiales de la solution.",
      });
      startTransition(() => router.refresh());
    } else {
      setMessage({
        tone: "error",
        title: "Suppression impossible",
        text: result.error.message,
      });
    }

    isSubmittingRef.current = false;
    setIsSubmitting(false);
  }

  async function handleDeleteSolution() {
    if (!solution || isSubmittingRef.current) {
      return;
    }

    if (
      !window.confirm(
        "Supprimer définitivement cette solution de la page publique ?",
      )
    ) {
      return;
    }

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);

    const result = await requestBffJson<ClientSolutionMutationResponse>(
      `/api/admin/client-solutions/${encodeURIComponent(solution.id)}`,
      { method: "DELETE" },
    );

    if (result.ok) {
      startTransition(() => router.replace("/admin/solutions"));
      return;
    }

    setMessage({
      tone: "error",
      title: "Suppression impossible",
      text: result.error.message,
    });
    isSubmittingRef.current = false;
    setIsSubmitting(false);
  }

  return (
    <form className="form-card admin-solution-form" onSubmit={handleSubmit}>
      <div className="admin-solution-layout">
        <div className="admin-solution-main">
          <section className="admin-solution-section">
            <div className="section-heading">
              <div>
                <span className="card-kicker">Tuile</span>
                <h2>Nom et destination</h2>
                <p>
                  Le nom s&apos;affiche en haut de la tuile, le lien s&apos;ouvre
                  au clic.
                </p>
              </div>
            </div>

            <label>
              Nom affiché
              <input
                maxLength={120}
                onChange={(event) => updateField("title", event.target.value)}
                placeholder="Ex. Bureau à distance (RDS)"
                required
                value={formState.title}
              />
            </label>

            <label>
              Lien du service
              <input
                inputMode="url"
                maxLength={2048}
                onChange={(event) =>
                  updateField("targetUrl", event.target.value)
                }
                placeholder="https://exemple.tld/acces"
                required
                type="url"
                value={formState.targetUrl}
              />
            </label>

            <label>
              Phrase courte (optionnelle)
              <textarea
                maxLength={280}
                onChange={(event) => updateField("tagline", event.target.value)}
                rows={2}
                value={formState.tagline}
              />
            </label>

            <div className="form-grid">
              <label>
                État
                <select
                  onChange={(event) =>
                    updateField(
                      "status",
                      event.target.value as FormState["status"],
                    )
                  }
                  value={formState.status}
                >
                  {Object.entries(statusLabels).map(([value, label]) => (
                    <option key={value} value={value}>
                      {label}
                    </option>
                  ))}
                </select>
              </label>

              <label>
                Ordre d&apos;affichage
                <input
                  inputMode="numeric"
                  max={9999}
                  min={0}
                  onChange={(event) =>
                    updateField("displayOrder", event.target.value)
                  }
                  type="number"
                  value={formState.displayOrder}
                />
              </label>
            </div>

            <label className="admin-solution-checkbox">
              <input
                checked={formState.opensInNewTab}
                onChange={(event) =>
                  updateField("opensInNewTab", event.target.checked)
                }
                type="checkbox"
              />
              <span>Ouvrir le service dans un nouvel onglet</span>
            </label>

            <label>
              Identifiant d&apos;URL (optionnel)
              <input
                maxLength={80}
                onChange={(event) => updateField("slug", event.target.value)}
                placeholder="Généré depuis le nom si laissé vide"
                value={formState.slug}
              />
              <span className="field-hint">
                Minuscules, chiffres et tirets. Doit rester unique.
              </span>
            </label>
          </section>

          <section className="admin-solution-section">
            <div className="section-heading">
              <div>
                <span className="card-kicker">Logo</span>
                <h2>Image de la tuile</h2>
                <p>PNG, JPEG, WebP ou SVG, 512 Ko maximum.</p>
              </div>
            </div>

            <label>
              Fichier du logo
              <input
                accept={LOGO_ACCEPT}
                onChange={(event) =>
                  setSelectedLogo(event.target.files?.[0] ?? null)
                }
                type="file"
              />
            </label>
            <p className="field-hint">
              Le logo est stocké côté API interne et servi par la route publique
              {" "}
              <code>/api/solutions/{"{"}id{"}"}/logo</code>. Sans logo, la tuile
              affiche les initiales du nom.
            </p>

            {solution?.hasLogo ? (
              <div className="admin-solution-logo-current">
                <strong>Logo actuel</strong>
                <span>
                  {solution.logoOriginalName}
                  {solution.logoSizeBytes
                    ? ` · ${formatFileSize(solution.logoSizeBytes)}`
                    : ""}
                </span>
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img
                  alt=""
                  className="admin-solution-logo-preview"
                  src={`/api/solutions/${encodeURIComponent(solution.id)}/logo?v=${encodeURIComponent(solution.logoUpdatedAt ?? "")}`}
                />
              </div>
            ) : null}

            {selectedLogo ? (
              <div className="admin-solution-logo-current">
                <strong>Logo prêt à être envoyé</strong>
                <span>
                  {selectedLogo.name} · {formatFileSize(selectedLogo.size)}
                </span>
              </div>
            ) : null}
          </section>
        </div>

        <aside className="admin-solution-sidebar">
          <div className="content-panel">
            <span className="card-kicker">Aperçu</span>
            <h2>{formState.title.trim() || "Nouvelle solution"}</h2>
            <p>
              {formState.tagline.trim()
                || "Ajoutez une phrase courte pour préciser à quoi sert ce service."}
            </p>
            <dl className="profile-details">
              <div>
                <dt>Destination</dt>
                <dd className="multiline-text">
                  {formState.targetUrl.trim() || "Non renseignée"}
                </dd>
              </div>
              <div>
                <dt>État</dt>
                <dd>{statusLabels[formState.status]}</dd>
              </div>
              <div>
                <dt>Ordre</dt>
                <dd>{formState.displayOrder || "0"}</dd>
              </div>
            </dl>
          </div>

          {solution ? (
            <div className="content-panel">
              <span className="card-kicker">Maintenance</span>
              <h2>Actions sensibles</h2>
              <p>
                Retirez le logo ou supprimez définitivement la tuile de la page
                publique.
              </p>
              <div className="stack-row">
                {solution.hasLogo ? (
                  <button
                    className="button button-secondary"
                    onClick={handleDeleteLogo}
                    type="button"
                  >
                    Supprimer le logo
                  </button>
                ) : null}
                <button
                  className="button button-danger"
                  onClick={handleDeleteSolution}
                  type="button"
                >
                  Supprimer la solution
                </button>
              </div>
            </div>
          ) : null}
        </aside>
      </div>

      {message ? (
        <FormMessage title={message.title} tone={message.tone}>
          <p>{message.text}</p>
        </FormMessage>
      ) : null}

      <div className="stack-row">
        <SubmitButton
          idleLabel={mode === "create" ? "Créer la solution" : "Enregistrer"}
          isSubmitting={isSubmitting}
          submittingLabel={
            mode === "create" ? "Création..." : "Enregistrement..."
          }
        />
      </div>
    </form>
  );
}

function formatFileSize(sizeBytes: number) {
  if (sizeBytes >= 1024 * 1024) {
    return `${(sizeBytes / (1024 * 1024)).toFixed(1)} Mo`;
  }

  if (sizeBytes >= 1024) {
    return `${Math.round(sizeBytes / 1024)} Ko`;
  }

  return `${sizeBytes} o`;
}
