"use client";

import type {
  CommercialOfferSummary,
  DownloadCategory,
  DownloadResource,
  DownloadResourceMutationResponse,
  DownloadResourcePayload,
  DownloadVisibilityRulePayload,
} from "@kermaria/shared";
import {
  DOWNLOAD_RESOURCE_TYPES,
  DOWNLOAD_SERVICE_TYPES,
  PUBLIC_PACKS,
} from "@kermaria/shared";
import { FormEvent, startTransition, useRef, useState } from "react";
import { useRouter } from "next/navigation";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type AdminDownloadFormProps = {
  mode: "create" | "edit";
  categories: DownloadCategory[];
  offers: CommercialOfferSummary[];
  offerCatalogAvailable: boolean;
  download?: DownloadResource | null;
};

type FormState = {
  categoryId: string;
  title: string;
  shortDescription: string;
  resourceType: DownloadResourcePayload["resourceType"];
  sourceKind: DownloadResourcePayload["sourceKind"];
  visibilityMode: DownloadResourcePayload["visibilityMode"];
  status: DownloadResourcePayload["status"];
  externalUrl: string;
  versionLabel: string;
  installationInstructions: string;
  displayOrder: string;
  selectedPackCodes: string[];
  selectedOfferReferences: string[];
  selectedServiceTypes: string[];
};

const resourceTypeLabels: Record<FormState["resourceType"], string> = {
  software: "Logiciel",
  script: "Script",
  rdp: "Fichier RDP",
  document: "Documentation",
  tool: "Outil complémentaire",
  other: "Autre ressource",
};

const sourceKindLabels: Record<FormState["sourceKind"], string> = {
  internal_file: "Fichier interne hébergé",
  external_url: "Lien externe officiel",
};

const visibilityModeLabels: Record<FormState["visibilityMode"], string> = {
  all_clients: "Tous les clients connectés",
  targeted: "Clients selon packs, offres ou services",
};

const statusLabels: Record<FormState["status"], string> = {
  active: "Active",
  inactive: "Inactive",
};

const serviceTypeLabels: Record<string, string> = {
  personal_hosting: "Hébergement personnel",
  backup: "Sauvegarde",
  vpn: "Accès VPN",
  rds: "Bureau Windows à distance",
  support: "Support",
};

function buildInitialState(
  categories: DownloadCategory[],
  download?: DownloadResource | null,
): FormState {
  return {
    categoryId: download?.categoryId ?? categories[0]?.id ?? "",
    title: download?.title ?? "",
    shortDescription: download?.shortDescription ?? "",
    resourceType: download?.resourceType ?? "software",
    sourceKind: download?.sourceKind ?? "internal_file",
    visibilityMode: download?.visibilityMode ?? "all_clients",
    status: download?.status ?? "inactive",
    externalUrl: download?.externalUrl ?? "",
    versionLabel: download?.versionLabel ?? "",
    installationInstructions: download?.installationInstructions ?? "",
    displayOrder: String(download?.displayOrder ?? 0),
    selectedPackCodes:
      download?.rules
        .filter((rule) => rule.targetType === "public_pack_code")
        .map((rule) => rule.targetValue) ?? [],
    selectedOfferReferences:
      download?.rules
        .filter((rule) => rule.targetType === "offer_external_reference")
        .map((rule) => rule.targetValue) ?? [],
    selectedServiceTypes:
      download?.rules
        .filter((rule) => rule.targetType === "service_type")
        .map((rule) => rule.targetValue) ?? [],
  };
}

export function AdminDownloadForm({
  mode,
  categories,
  offers,
  offerCatalogAvailable,
  download,
}: AdminDownloadFormProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const hiddenRules = download?.rules.filter((rule) =>
    rule.targetType === "provisioning_group"
  ) ?? [];
  const [formState, setFormState] = useState<FormState>(
    buildInitialState(categories, download),
  );
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [message, setMessage] = useState<{
    tone: "success" | "error" | "info";
    title: string;
    text: string;
  } | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const offerOptions = offers
    .filter((offer) => offer.externalReference)
    .sort((left, right) => left.name.localeCompare(right.name, "fr-FR"));

  function updateField<Key extends keyof FormState>(
    key: Key,
    value: FormState[Key],
  ) {
    setFormState((current) => ({ ...current, [key]: value }));
  }

  function toggleSelection(
    key:
      | "selectedPackCodes"
      | "selectedOfferReferences"
      | "selectedServiceTypes",
    value: string,
  ) {
    setFormState((current) => {
      const values = current[key];
      const nextValues = values.includes(value)
        ? values.filter((entry) => entry !== value)
        : [...values, value];

      return {
        ...current,
        [key]: nextValues,
      };
    });
  }

  function buildVisibilityRules(): DownloadVisibilityRulePayload[] {
    return [
      ...formState.selectedPackCodes.map((targetValue) => ({
        targetType: "public_pack_code" as const,
        targetValue,
      })),
      ...formState.selectedOfferReferences.map((targetValue) => ({
        targetType: "offer_external_reference" as const,
        targetValue,
      })),
      ...formState.selectedServiceTypes.map((targetValue) => ({
        targetType: "service_type" as const,
        targetValue,
      })),
      ...hiddenRules.map((rule) => ({
        targetType: rule.targetType,
        targetValue: rule.targetValue,
      })),
    ];
  }

  function buildPayload(
    statusOverride?: DownloadResourcePayload["status"],
  ): DownloadResourcePayload {
    return {
      categoryId: formState.categoryId,
      title: formState.title.trim(),
      shortDescription: formState.shortDescription.trim(),
      resourceType: formState.resourceType,
      sourceKind: formState.sourceKind,
      visibilityMode: formState.visibilityMode,
      status: statusOverride ?? formState.status,
      externalUrl: formState.externalUrl.trim() || null,
      versionLabel: formState.versionLabel.trim() || null,
      installationInstructions:
        formState.installationInstructions.trim() || null,
      displayOrder: Number.parseInt(formState.displayOrder, 10) || 0,
      visibilityRules: buildVisibilityRules(),
    };
  }

  async function uploadSelectedFile(resourceId: string) {
    if (!selectedFile) {
      return null;
    }

    const body = new FormData();
    body.set("file", selectedFile);

    return requestBffJson<DownloadResourceMutationResponse>(
      `/api/admin/downloads/${encodeURIComponent(resourceId)}/file`,
      {
        method: "POST",
        body,
      },
    );
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmittingRef.current) {
      return;
    }

    if (!formState.categoryId) {
      setMessage({
        tone: "error",
        title: "Catégorie requise",
        text: "Choisissez d'abord une catégorie de destination.",
      });
      return;
    }

    const hasStoredFile = Boolean(download?.hasInternalFile);
    if (
      formState.sourceKind === "internal_file"
      && formState.status === "active"
      && !selectedFile
      && !hasStoredFile
    ) {
      setMessage({
        tone: "error",
        title: "Fichier requis",
        text:
          "Une ressource interne active doit disposer d'un fichier uploadé avant activation.",
      });
      return;
    }

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);

    const requestedStatus = formState.status;
    const requiresIntermediateInactive =
      formState.sourceKind === "internal_file"
      && requestedStatus === "active"
      && (selectedFile !== null || !hasStoredFile);
    const initialPayload = buildPayload(
      requiresIntermediateInactive ? "inactive" : undefined,
    );

    const endpoint =
      mode === "create"
        ? "/api/admin/downloads"
        : `/api/admin/downloads/${encodeURIComponent(download!.id)}`;
    const typedEndpoint = endpoint as `/api/${string}`;
    const method: "POST" | "PATCH" = mode === "create" ? "POST" : "PATCH";

    const saveResult = await requestBffJson<DownloadResourceMutationResponse>(
      typedEndpoint,
      {
        method,
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(initialPayload),
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

    const resourceId = mode === "create" ? saveResult.data.id : download!.id;

    if (selectedFile) {
      const uploadResult = await uploadSelectedFile(resourceId);
      if (!uploadResult?.ok) {
        setMessage({
          tone: "error",
          title: "Fichier non envoyé",
          text:
            uploadResult?.error.message
            ?? "Le fichier n'a pas pu être envoyé.",
        });
        isSubmittingRef.current = false;
        setIsSubmitting(false);
        return;
      }
    }

    if (requiresIntermediateInactive) {
      const activationResult =
        await requestBffJson<DownloadResourceMutationResponse>(
          `/api/admin/downloads/${encodeURIComponent(resourceId)}`,
          {
            method: "PATCH",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(buildPayload("active")),
          },
        );
      if (!activationResult.ok) {
        setMessage({
          tone: "error",
          title: "Activation incomplète",
          text: activationResult.error.message,
        });
        isSubmittingRef.current = false;
        setIsSubmitting(false);
        return;
      }
    }

    setSelectedFile(null);
    setMessage({
      tone: "success",
      title: mode === "create" ? "Téléchargement créé" : "Téléchargement mis à jour",
      text:
        mode === "create"
          ? "La ressource a été créée et vous êtes redirigé vers sa fiche."
          : "Les métadonnées et les règles de visibilité ont été enregistrées.",
    });

    startTransition(() => {
      if (mode === "create") {
        router.replace(`/admin/downloads/${encodeURIComponent(resourceId)}`);
      } else {
        router.refresh();
      }
    });

    isSubmittingRef.current = false;
    setIsSubmitting(false);
  }

  async function handleDeleteFile() {
    if (!download || isSubmittingRef.current) {
      return;
    }

    if (!window.confirm("Supprimer définitivement le fichier interne associé ?")) {
      return;
    }

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);

    const result = await requestBffJson<DownloadResourceMutationResponse>(
      `/api/admin/downloads/${encodeURIComponent(download.id)}/file`,
      {
        method: "DELETE",
      },
    );

    if (result.ok) {
      setSelectedFile(null);
      setMessage({
        tone: "success",
        title: "Fichier supprimé",
        text:
          "Le fichier a été retiré. La ressource est repassée inactive si elle dépendait d'un fichier interne.",
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

  async function handleDeleteResource() {
    if (!download || isSubmittingRef.current) {
      return;
    }

    if (
      !window.confirm(
        "Supprimer définitivement ce téléchargement et ses règles de visibilité ?",
      )
    ) {
      return;
    }

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);

    const result = await requestBffJson<DownloadResourceMutationResponse>(
      `/api/admin/downloads/${encodeURIComponent(download.id)}`,
      {
        method: "DELETE",
      },
    );

    if (result.ok) {
      startTransition(() => router.replace("/admin/downloads"));
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
    <form className="form-card admin-download-form" onSubmit={handleSubmit}>
      <div className="admin-download-layout">
        <div className="admin-download-main">
          <section className="admin-download-section">
            <div className="section-heading">
              <div>
                <span className="card-kicker">Métadonnées</span>
                <h2>Présentation client</h2>
                <p>Nom, description, catégorie et ordre d'affichage.</p>
              </div>
            </div>

            <div className="form-grid">
              <label>
                Catégorie
                <select
                  onChange={(event) => updateField("categoryId", event.target.value)}
                  value={formState.categoryId}
                >
                  {categories.map((category) => (
                    <option key={category.id} value={category.id}>
                      {category.title}
                    </option>
                  ))}
                </select>
              </label>

              <label>
                Ordre d'affichage
                <input
                  inputMode="numeric"
                  onChange={(event) =>
                    updateField("displayOrder", event.target.value)
                  }
                  type="number"
                  value={formState.displayOrder}
                />
              </label>
            </div>

            <label>
              Titre affiché
              <input
                maxLength={140}
                onChange={(event) => updateField("title", event.target.value)}
                placeholder="Ex. Client VPN Zachary IT"
                value={formState.title}
              />
            </label>

            <label>
              Description courte
              <textarea
                maxLength={320}
                onChange={(event) =>
                  updateField("shortDescription", event.target.value)
                }
                rows={4}
                value={formState.shortDescription}
              />
            </label>
          </section>

          <section className="admin-download-section">
            <div className="section-heading">
              <div>
                <span className="card-kicker">Distribution</span>
                <h2>Type de ressource et source</h2>
                <p>Le bouton client passera toujours par la route sécurisée du portail.</p>
              </div>
            </div>

            <div className="form-grid">
              <label>
                Type de ressource
                <select
                  onChange={(event) =>
                    updateField(
                      "resourceType",
                      event.target.value as FormState["resourceType"],
                    )
                  }
                  value={formState.resourceType}
                >
                  {DOWNLOAD_RESOURCE_TYPES.map((resourceType) => (
                    <option key={resourceType} value={resourceType}>
                      {resourceTypeLabels[resourceType]}
                    </option>
                  ))}
                </select>
              </label>

              <label>
                État
                <select
                  onChange={(event) =>
                    updateField("status", event.target.value as FormState["status"])
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
            </div>

            <div className="admin-radio-grid">
              {(
                Object.keys(sourceKindLabels) as FormState["sourceKind"][]
              ).map((sourceKind) => (
                <label className="admin-radio-card" key={sourceKind}>
                  <input
                    checked={formState.sourceKind === sourceKind}
                    name="sourceKind"
                    onChange={() => {
                      updateField("sourceKind", sourceKind);
                      if (sourceKind === "external_url") {
                        setSelectedFile(null);
                      }
                    }}
                    type="radio"
                  />
                  <span>{sourceKindLabels[sourceKind]}</span>
                </label>
              ))}
            </div>

            {formState.sourceKind === "external_url" ? (
              <label>
                URL externe absolue
                <input
                  onChange={(event) =>
                    updateField("externalUrl", event.target.value)
                  }
                  placeholder="https://exemple.tld/telechargement"
                  value={formState.externalUrl}
                />
              </label>
            ) : (
              <div className="admin-download-file-panel">
                <label>
                  Fichier interne privé
                  <input
                    accept=".exe,.msi,.zip,.rdp,.pdf,.ps1,.bat,.cmd,.txt,.doc,.docx,.cer,.ovpn"
                    onChange={(event) =>
                      setSelectedFile(event.target.files?.[0] ?? null)
                    }
                    type="file"
                  />
                </label>
                <p className="field-hint">
                  Le fichier est stocké côté API interne, hors web root, puis servi
                  uniquement après contrôle d'accès.
                </p>
                {download?.hasInternalFile ? (
                  <div className="admin-download-file-current">
                    <strong>Fichier actuel</strong>
                    <span>
                      {download.fileOriginalName}
                      {download.fileSizeBytes
                        ? ` · ${formatFileSize(download.fileSizeBytes)}`
                        : ""}
                    </span>
                  </div>
                ) : null}
                {selectedFile ? (
                  <div className="admin-download-file-current">
                    <strong>Fichier prêt à être envoyé</strong>
                    <span>
                      {selectedFile.name} · {formatFileSize(selectedFile.size)}
                    </span>
                  </div>
                ) : null}
              </div>
            )}
          </section>

          <section className="admin-download-section">
            <div className="section-heading">
              <div>
                <span className="card-kicker">Visibilité</span>
                <h2>Règles d'accès</h2>
                <p>Réservez la ressource à certains packs, offres ou services actifs.</p>
              </div>
            </div>

            <div className="admin-radio-grid">
              {(
                Object.keys(visibilityModeLabels) as FormState["visibilityMode"][]
              ).map((visibilityMode) => (
                <label className="admin-radio-card" key={visibilityMode}>
                  <input
                    checked={formState.visibilityMode === visibilityMode}
                    name="visibilityMode"
                    onChange={() => updateField("visibilityMode", visibilityMode)}
                    type="radio"
                  />
                  <span>{visibilityModeLabels[visibilityMode]}</span>
                </label>
              ))}
            </div>

            {formState.visibilityMode === "targeted" ? (
              <div className="admin-targeting-grid">
                <fieldset className="admin-checkbox-group">
                  <legend>Packs publics</legend>
                  {PUBLIC_PACKS.map((pack) => (
                    <label key={pack.key}>
                      <input
                        checked={formState.selectedPackCodes.includes(pack.key)}
                        onChange={() =>
                          toggleSelection("selectedPackCodes", pack.key)
                        }
                        type="checkbox"
                      />
                      <span>{pack.label}</span>
                    </label>
                  ))}
                </fieldset>

                <fieldset className="admin-checkbox-group">
                  <legend>Offres catalogue</legend>
                  {!offerCatalogAvailable ? (
                    <FormMessage title="Catalogue temporairement indisponible" tone="info">
                      Aucune offre catalogue avec référence externe n'est encore disponible.
                    </FormMessage>
                  ) : offerOptions.length === 0 ? (
                    <p className="field-hint">
                      Aucune offre catalogue avec rÃ©fÃ©rence externe n'est encore disponible.
                    </p>
                  ) : (
                    offerOptions.map((offer) => (
                      <label key={offer.id}>
                        <input
                          checked={formState.selectedOfferReferences.includes(
                            offer.externalReference!,
                          )}
                          onChange={() =>
                            toggleSelection(
                              "selectedOfferReferences",
                              offer.externalReference!,
                            )
                          }
                          type="checkbox"
                        />
                        <span>
                          {offer.name}
                          {" · "}
                          {offer.externalReference}
                        </span>
                      </label>
                    ))
                  )}
                </fieldset>

                <fieldset className="admin-checkbox-group">
                  <legend>Services actifs</legend>
                  {DOWNLOAD_SERVICE_TYPES.map((serviceType) => (
                    <label key={serviceType}>
                      <input
                        checked={formState.selectedServiceTypes.includes(serviceType)}
                        onChange={() =>
                          toggleSelection("selectedServiceTypes", serviceType)
                        }
                        type="checkbox"
                      />
                      <span>{serviceTypeLabels[serviceType]}</span>
                    </label>
                  ))}
                </fieldset>
              </div>
            ) : null}
          </section>

          <section className="admin-download-section">
            <div className="section-heading">
              <div>
                <span className="card-kicker">Compléments</span>
                <h2>Informations optionnelles</h2>
                <p>Version, date implicite via mise à jour, et consignes d'installation.</p>
              </div>
            </div>

            <div className="form-grid">
              <label>
                Version
                <input
                  maxLength={80}
                  onChange={(event) =>
                    updateField("versionLabel", event.target.value)
                  }
                  placeholder="Ex. v2.4.1"
                  value={formState.versionLabel}
                />
              </label>
            </div>

            <label>
              Consignes d'installation
              <textarea
                maxLength={4000}
                onChange={(event) =>
                  updateField("installationInstructions", event.target.value)
                }
                rows={5}
                value={formState.installationInstructions}
              />
            </label>
          </section>
        </div>

        <aside className="admin-download-sidebar">
          <div className="content-panel admin-download-summary">
            <span className="card-kicker">Aperçu logique</span>
            <h2>{formState.title.trim() || "Nouveau téléchargement"}</h2>
            <p>
              {formState.shortDescription.trim()
                || "Ajoutez une courte phrase rassurante pour expliquer l'usage côté client."}
            </p>
            <dl className="profile-details">
              <div>
                <dt>Type</dt>
                <dd>{resourceTypeLabels[formState.resourceType]}</dd>
              </div>
              <div>
                <dt>Source</dt>
                <dd>{sourceKindLabels[formState.sourceKind]}</dd>
              </div>
              <div>
                <dt>Visibilité</dt>
                <dd>{visibilityModeLabels[formState.visibilityMode]}</dd>
              </div>
              <div>
                <dt>État</dt>
                <dd>{statusLabels[formState.status]}</dd>
              </div>
            </dl>
            <p className="field-hint">
              Les liens client passent toujours par la route sécurisée
              {" "}
              <code>/api/downloads/{"{"}id{"}"}/file</code>.
            </p>
          </div>

          {download ? (
            <div className="content-panel admin-download-summary">
              <span className="card-kicker">Maintenance</span>
              <h2>Actions sensibles</h2>
              <p>
                Retirez le binaire interne ou supprimez définitivement la ressource
                si elle n'est plus utilisée.
              </p>
              <div className="stack-row">
                {download.hasInternalFile ? (
                  <button
                    className="button button-secondary"
                    onClick={handleDeleteFile}
                    type="button"
                  >
                    Supprimer le fichier
                  </button>
                ) : null}
                <button
                  className="button button-danger"
                  onClick={handleDeleteResource}
                  type="button"
                >
                  Supprimer la ressource
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
          disabled={categories.length === 0}
          idleLabel={mode === "create" ? "Créer le téléchargement" : "Enregistrer"}
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
