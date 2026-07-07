"use client";

import type {
  PublicPackCatalogContent,
  PublicPackCatalogContentPayload,
  PublicPackCatalogMutationResponse,
  PublicPackCode,
  PublicPackComparisonValueKind,
} from "@kermaria/shared";
import { FormEvent, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type AdminPublicPackCatalogFormProps = {
  initialContent: PublicPackCatalogContent;
};

function linesToItems(value: string) {
  return value
    .split(/\r?\n/)
    .map((entry) => entry.trim())
    .filter((entry) => entry.length > 0);
}

function itemsToLines(values: readonly string[]) {
  return values.join("\n");
}

export function AdminPublicPackCatalogForm({
  initialContent,
}: AdminPublicPackCatalogFormProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [content, setContent] = useState(initialContent);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<{
    tone: "success" | "error";
    text: string;
  } | null>(null);

  const packCodes = useMemo(
    () => content.packs.map((pack) => pack.packCode),
    [content.packs],
  );

  function updateContent(
    updater: (current: PublicPackCatalogContent) => PublicPackCatalogContent,
  ) {
    setContent((current) => updater(current));
  }

  function updatePack(
    packCode: PublicPackCode,
    field:
      | "label"
      | "shortLabel"
      | "headline"
      | "audience"
      | "description"
      | "highlightLabel"
      | "displayOrder",
    value: string,
  ) {
    updateContent((current) => ({
      ...current,
      packs: current.packs.map((pack) =>
        pack.packCode === packCode
          ? {
              ...pack,
              [field]:
                field === "displayOrder" ? Number.parseInt(value, 10) || 0 : value,
            }
          : pack,
      ),
    }));
  }

  function updatePackList(
    packCode: PublicPackCode,
    field: "included" | "highlights",
    rawValue: string,
  ) {
    const nextItems = linesToItems(rawValue);
    updateContent((current) => ({
      ...current,
      packs: current.packs.map((pack) =>
        pack.packCode === packCode
          ? {
              ...pack,
              [field]: nextItems,
            }
          : pack,
      ),
    }));
  }

  function updateRow(
    rowId: string,
    field: "label" | "sortOrder",
    value: string,
  ) {
    updateContent((current) => ({
      ...current,
      comparisonRows: current.comparisonRows.map((row) =>
        row.id === rowId
          ? {
              ...row,
              [field]:
                field === "sortOrder" ? Number.parseInt(value, 10) || 0 : value,
            }
          : row,
      ),
    }));
  }

  function updateRowValueKind(
    rowId: string,
    packCode: PublicPackCode,
    kind: PublicPackComparisonValueKind,
  ) {
    updateContent((current) => ({
      ...current,
      comparisonRows: current.comparisonRows.map((row) =>
        row.id === rowId
          ? {
              ...row,
              values: {
                ...row.values,
                [packCode]: {
                  kind,
                  text: kind === "text" ? row.values[packCode].text : null,
                },
              },
            }
          : row,
      ),
    }));
  }

  function updateRowValueText(
    rowId: string,
    packCode: PublicPackCode,
    text: string,
  ) {
    updateContent((current) => ({
      ...current,
      comparisonRows: current.comparisonRows.map((row) =>
        row.id === rowId
          ? {
              ...row,
              values: {
                ...row.values,
                [packCode]: {
                  ...row.values[packCode],
                  text,
                },
              },
            }
          : row,
      ),
    }));
  }

  function addComparisonRow() {
    const rowId = `row-${crypto.randomUUID().slice(0, 8)}`;
    updateContent((current) => ({
      ...current,
      comparisonRows: [
        ...current.comparisonRows,
        {
          id: rowId,
          label: "Nouvelle fonctionnalité",
          sortOrder:
            Math.max(0, ...current.comparisonRows.map((row) => row.sortOrder)) + 10,
          values: packCodes.reduce(
            (accumulator, packCode) => ({
              ...accumulator,
              [packCode]: { kind: "excluded", text: null },
            }),
            {} as PublicPackCatalogContent["comparisonRows"][number]["values"],
          ),
        },
      ],
    }));
  }

  function removeComparisonRow(rowId: string) {
    updateContent((current) => ({
      ...current,
      comparisonRows: current.comparisonRows.filter((row) => row.id !== rowId),
    }));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmittingRef.current) {
      return;
    }

    const payload: PublicPackCatalogContentPayload = {
      pageEyebrow: content.pageEyebrow.trim(),
      pageTitle: content.pageTitle.trim(),
      pageDescription: content.pageDescription.trim(),
      comparisonColumnLabel: content.comparisonColumnLabel.trim(),
      footnotePrimary: content.footnotePrimary.trim(),
      footnoteSecondary: content.footnoteSecondary.trim(),
      packs: content.packs.map((pack) => ({
        ...pack,
        label: pack.label.trim(),
        shortLabel: pack.shortLabel.trim(),
        headline: pack.headline.trim(),
        audience: pack.audience.trim(),
        description: pack.description.trim(),
        included: pack.included.map((entry) => entry.trim()).filter(Boolean),
        highlights: pack.highlights.map((entry) => entry.trim()).filter(Boolean),
        highlightLabel: pack.highlightLabel?.trim() || null,
      })),
      comparisonRows: content.comparisonRows.map((row) => ({
        ...row,
        id: row.id.trim(),
        label: row.label.trim(),
        values: Object.fromEntries(
          packCodes.map((packCode) => [
            packCode,
            {
              kind: row.values[packCode].kind,
              text: row.values[packCode].text?.trim() || null,
            },
          ]),
        ) as PublicPackCatalogContentPayload["comparisonRows"][number]["values"],
      })),
    };

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);

    const result = await requestBffJson<PublicPackCatalogMutationResponse>(
      "/api/admin/public-pack-catalog",
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      },
    );

    if (result.ok) {
      setContent((current) => ({
        ...current,
        updatedAt: result.data.updatedAt,
      }));
      setMessage({
        tone: "success",
        text: "La vitrine packs a été enregistrée.",
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
    <form className="form-card" onSubmit={handleSubmit}>
      <div className="form-grid">
        <label>
          Surtitre de la page
          <input
            maxLength={160}
            onChange={(event) =>
              updateContent((current) => ({
                ...current,
                pageEyebrow: event.target.value,
              }))
            }
            value={content.pageEyebrow}
          />
        </label>
        <label>
          Libellé colonne fonctionnalités
          <input
            maxLength={160}
            onChange={(event) =>
              updateContent((current) => ({
                ...current,
                comparisonColumnLabel: event.target.value,
              }))
            }
            value={content.comparisonColumnLabel}
          />
        </label>
      </div>

      <label>
        Titre public
        <input
          maxLength={200}
          onChange={(event) =>
            updateContent((current) => ({
              ...current,
              pageTitle: event.target.value,
            }))
          }
          value={content.pageTitle}
        />
      </label>

      <label>
        Description publique
        <textarea
          maxLength={4000}
          onChange={(event) =>
            updateContent((current) => ({
              ...current,
              pageDescription: event.target.value,
            }))
          }
          rows={3}
          value={content.pageDescription}
        />
      </label>

      <div className="public-pack-admin-grid">
        {content.packs.map((pack) => (
          <section className="public-pack-admin-card" key={pack.packCode}>
            <h2>{pack.packCode}</h2>
            <div className="form-grid">
              <label>
                Libellé
                <input
                  maxLength={200}
                  onChange={(event) =>
                    updatePack(pack.packCode, "label", event.target.value)
                  }
                  value={pack.label}
                />
              </label>
              <label>
                Libellé court
                <input
                  maxLength={160}
                  onChange={(event) =>
                    updatePack(pack.packCode, "shortLabel", event.target.value)
                  }
                  value={pack.shortLabel}
                />
              </label>
            </div>
            <div className="form-grid">
              <label>
                Ordre
                <input
                  inputMode="numeric"
                  onChange={(event) =>
                    updatePack(pack.packCode, "displayOrder", event.target.value)
                  }
                  value={String(pack.displayOrder)}
                />
              </label>
              <label>
                Badge optionnel
                <input
                  maxLength={60}
                  onChange={(event) =>
                    updatePack(pack.packCode, "highlightLabel", event.target.value)
                  }
                  value={pack.highlightLabel ?? ""}
                />
              </label>
            </div>
            <label>
              Pour qui ?
              <textarea
                maxLength={4000}
                onChange={(event) =>
                  updatePack(pack.packCode, "audience", event.target.value)
                }
                rows={2}
                value={pack.audience}
              />
            </label>
            <label>
              Accroche
              <textarea
                maxLength={200}
                onChange={(event) =>
                  updatePack(pack.packCode, "headline", event.target.value)
                }
                rows={2}
                value={pack.headline}
              />
            </label>
            <label>
              Description
              <textarea
                maxLength={4000}
                onChange={(event) =>
                  updatePack(pack.packCode, "description", event.target.value)
                }
                rows={3}
                value={pack.description}
              />
            </label>
            <div className="form-grid">
              <label>
                Inclus
                <textarea
                  maxLength={4000}
                  onChange={(event) =>
                    updatePackList(pack.packCode, "included", event.target.value)
                  }
                  rows={6}
                  value={itemsToLines(pack.included)}
                />
                <span className="field-hint">
                  Une ligne par élément inclus.
                </span>
              </label>
              <label>
                Différences clés
                <textarea
                  maxLength={4000}
                  onChange={(event) =>
                    updatePackList(pack.packCode, "highlights", event.target.value)
                  }
                  rows={6}
                  value={itemsToLines(pack.highlights)}
                />
                <span className="field-hint">
                  Une ligne par différence mise en avant.
                </span>
              </label>
            </div>
          </section>
        ))}
      </div>

      <section className="public-pack-admin-card">
        <div className="public-pack-admin-table-header">
          <div>
            <h2>Tableau comparatif</h2>
            <p className="field-hint">
              Les codes pack restent fixes pour préserver la compatibilité avec le
              provisionnement automatique.
            </p>
          </div>
          <button
            className="button button-secondary"
            onClick={addComparisonRow}
            type="button"
          >
            Ajouter une ligne
          </button>
        </div>

        <div className="public-pack-admin-table-wrap">
          <table className="public-pack-admin-table">
            <thead>
              <tr>
                <th>Libellé</th>
                <th>Ordre</th>
                {content.packs.map((pack) => (
                  <th key={`head-${pack.packCode}`}>{pack.shortLabel}</th>
                ))}
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {content.comparisonRows
                .slice()
                .sort((left, right) => left.sortOrder - right.sortOrder)
                .map((row) => (
                  <tr key={row.id}>
                    <td>
                      <input
                        maxLength={160}
                        onChange={(event) =>
                          updateRow(row.id, "label", event.target.value)
                        }
                        value={row.label}
                      />
                    </td>
                    <td>
                      <input
                        inputMode="numeric"
                        onChange={(event) =>
                          updateRow(row.id, "sortOrder", event.target.value)
                        }
                        value={String(row.sortOrder)}
                      />
                    </td>
                    {packCodes.map((packCode) => {
                      const value = row.values[packCode];
                      return (
                        <td key={`${row.id}-${packCode}`}>
                          <div className="public-pack-admin-cell">
                            <select
                              onChange={(event) =>
                                updateRowValueKind(
                                  row.id,
                                  packCode,
                                  event.target
                                    .value as PublicPackComparisonValueKind,
                                )
                              }
                              value={value.kind}
                            >
                              <option value="included">Oui</option>
                              <option value="excluded">Non</option>
                              <option value="text">Texte</option>
                            </select>
                            {value.kind === "text" ? (
                              <input
                                maxLength={80}
                                onChange={(event) =>
                                  updateRowValueText(
                                    row.id,
                                    packCode,
                                    event.target.value,
                                  )
                                }
                                placeholder="Ex. 32 Go"
                                value={value.text ?? ""}
                              />
                            ) : null}
                          </div>
                        </td>
                      );
                    })}
                    <td>
                      <button
                        className="button button-ghost button-compact"
                        onClick={() => removeComparisonRow(row.id)}
                        type="button"
                      >
                        Supprimer
                      </button>
                    </td>
                  </tr>
                ))}
            </tbody>
          </table>
        </div>
      </section>

      <div className="form-grid">
        <label>
          Note de bas de page
          <textarea
            maxLength={4000}
            onChange={(event) =>
              updateContent((current) => ({
                ...current,
                footnotePrimary: event.target.value,
              }))
            }
            rows={3}
            value={content.footnotePrimary}
          />
        </label>
        <label>
          Note complémentaire
          <textarea
            maxLength={4000}
            onChange={(event) =>
              updateContent((current) => ({
                ...current,
                footnoteSecondary: event.target.value,
              }))
            }
            rows={3}
            value={content.footnoteSecondary}
          />
        </label>
      </div>

      {message ? (
        <FormMessage
          title={message.tone === "success" ? "Configuration enregistrée" : "Échec"}
          tone={message.tone}
        >
          <p>{message.text}</p>
        </FormMessage>
      ) : null}

      <SubmitButton
        idleLabel="Enregistrer la vitrine packs"
        isSubmitting={isSubmitting}
        submittingLabel="Enregistrement..."
      />
    </form>
  );
}
