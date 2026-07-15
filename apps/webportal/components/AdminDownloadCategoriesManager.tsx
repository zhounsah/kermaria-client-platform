"use client";

import type {
  DownloadCategory,
  DownloadCategoryMutationResponse,
} from "@kermaria/shared";
import { FormEvent, startTransition, useRef, useState } from "react";
import { useRouter } from "next/navigation";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type EditableCategoryState = {
  slug: string;
  title: string;
  description: string;
  status: DownloadCategory["status"];
  displayOrder: string;
};

type AdminDownloadCategoriesManagerProps = {
  categories: DownloadCategory[];
};

function toEditableState(category: DownloadCategory): EditableCategoryState {
  return {
    slug: category.slug,
    title: category.title,
    description: category.description ?? "",
    status: category.status,
    displayOrder: String(category.displayOrder),
  };
}

export function AdminDownloadCategoriesManager({
  categories,
}: AdminDownloadCategoriesManagerProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [items, setItems] = useState<Record<string, EditableCategoryState>>(
    () =>
      Object.fromEntries(
        categories.map((category) => [category.id, toEditableState(category)]),
      ),
  );
  const [newCategory, setNewCategory] = useState<EditableCategoryState>({
    slug: "",
    title: "",
    description: "",
    status: "active",
    displayOrder: String(categories.length * 10 + 10),
  });
  const [message, setMessage] = useState<{
    tone: "success" | "error" | "info";
    title: string;
    text: string;
  } | null>(null);
  const [submittingId, setSubmittingId] = useState<string | null>(null);

  function updateItem(
    id: string,
    patch: Partial<EditableCategoryState>,
  ) {
    setItems((current) => ({
      ...current,
      [id]: {
        ...current[id],
        ...patch,
      },
    }));
  }

  function toPayload(state: EditableCategoryState) {
    return {
      slug: state.slug.trim().toLowerCase(),
      title: state.title.trim(),
      description: state.description.trim() || null,
      status: state.status,
      displayOrder: Number.parseInt(state.displayOrder, 10) || 0,
    };
  }

  async function handleCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmittingRef.current) {
      return;
    }

    isSubmittingRef.current = true;
    setSubmittingId("new");
    setMessage(null);

    const result = await requestBffJson<DownloadCategoryMutationResponse>(
      "/api/admin/download-categories",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(toPayload(newCategory)),
      },
    );

    if (result.ok) {
      setMessage({
        tone: "success",
        title: "Catégorie créée",
        text: "La nouvelle catégorie a été ajoutée.",
      });
      setNewCategory({
        slug: "",
        title: "",
        description: "",
        status: "active",
        displayOrder: String(categories.length * 10 + 20),
      });
      startTransition(() => router.refresh());
    } else {
      setMessage({
        tone: "error",
        title: "Création impossible",
        text: result.error.message,
      });
    }

    isSubmittingRef.current = false;
    setSubmittingId(null);
  }

  async function handleSave(id: string) {
    if (isSubmittingRef.current) {
      return;
    }

    isSubmittingRef.current = true;
    setSubmittingId(id);
    setMessage(null);

    const result = await requestBffJson<DownloadCategoryMutationResponse>(
      `/api/admin/download-categories/${encodeURIComponent(id)}`,
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(toPayload(items[id])),
      },
    );

    if (result.ok) {
      setMessage({
        tone: "success",
        title: "Catégorie enregistrée",
        text: "Les métadonnées de la catégorie ont été mises à jour.",
      });
      startTransition(() => router.refresh());
    } else {
      setMessage({
        tone: "error",
        title: "Enregistrement impossible",
        text: result.error.message,
      });
    }

    isSubmittingRef.current = false;
    setSubmittingId(null);
  }

  async function handleDelete(category: DownloadCategory) {
    if (isSubmittingRef.current) {
      return;
    }

    if (
      !window.confirm(
        `Supprimer définitivement la catégorie "${category.title}" ?`,
      )
    ) {
      return;
    }

    isSubmittingRef.current = true;
    setSubmittingId(category.id);
    setMessage(null);

    const result = await requestBffJson<DownloadCategoryMutationResponse>(
      `/api/admin/download-categories/${encodeURIComponent(category.id)}`,
      {
        method: "DELETE",
      },
    );

    if (result.ok) {
      setMessage({
        tone: "success",
        title: "Catégorie supprimée",
        text: "La catégorie a été retirée du back-office.",
      });
      startTransition(() => router.refresh());
      return;
    }

    setMessage({
      tone: "error",
      title: "Suppression impossible",
      text: result.error.message,
    });
    isSubmittingRef.current = false;
    setSubmittingId(null);
  }

  return (
    <div className="stack-panels">
      <form className="form-card compact-form-card" onSubmit={handleCreate}>
        <div className="section-heading">
          <div>
            <span className="card-kicker">Nouvelle catégorie</span>
            <h2>Ajouter une catégorie</h2>
            <p>Créez une entrée claire qui servira de regroupement côté client.</p>
          </div>
        </div>

        <div className="form-grid">
          <label>
            Slug
            <input
              onChange={(event) =>
                setNewCategory((current) => ({
                  ...current,
                  slug: event.target.value,
                }))
              }
              placeholder="ex. documentation"
              value={newCategory.slug}
            />
          </label>
          <label>
            Titre
            <input
              onChange={(event) =>
                setNewCategory((current) => ({
                  ...current,
                  title: event.target.value,
                }))
              }
              placeholder="Ex. Documentation"
              value={newCategory.title}
            />
          </label>
        </div>

        <label>
          Description courte
          <input
            onChange={(event) =>
              setNewCategory((current) => ({
                ...current,
                description: event.target.value,
              }))
            }
            value={newCategory.description}
          />
        </label>

        <div className="form-grid">
          <label>
            État
            <select
              onChange={(event) =>
                setNewCategory((current) => ({
                  ...current,
                  status: event.target.value as EditableCategoryState["status"],
                }))
              }
              value={newCategory.status}
            >
              <option value="active">Active</option>
              <option value="inactive">Inactive</option>
            </select>
          </label>
          <label>
            Ordre
            <input
              onChange={(event) =>
                setNewCategory((current) => ({
                  ...current,
                  displayOrder: event.target.value,
                }))
              }
              type="number"
              value={newCategory.displayOrder}
            />
          </label>
        </div>

        <div className="stack-row">
          <SubmitButton
            idleLabel="Créer la catégorie"
            isSubmitting={submittingId === "new"}
            submittingLabel="Création..."
          />
        </div>
      </form>

      {categories.map((category) => {
        const state = items[category.id];

        return (
          <article className="form-card compact-form-card" key={category.id}>
            <div className="section-heading">
              <div>
                <span className="card-kicker">Catégorie existante</span>
                <h2>{category.title}</h2>
                <p>
                  {category.resourceCount} téléchargement
                  {category.resourceCount > 1 ? "s" : ""} rattaché
                  {category.resourceCount > 1 ? "s" : ""}
                </p>
              </div>
            </div>

            <div className="form-grid">
              <label>
                Slug
                <input
                  onChange={(event) =>
                    updateItem(category.id, { slug: event.target.value })
                  }
                  value={state.slug}
                />
              </label>
              <label>
                Titre
                <input
                  onChange={(event) =>
                    updateItem(category.id, { title: event.target.value })
                  }
                  value={state.title}
                />
              </label>
            </div>

            <label>
              Description courte
              <input
                onChange={(event) =>
                  updateItem(category.id, { description: event.target.value })
                }
                value={state.description}
              />
            </label>

            <div className="form-grid">
              <label>
                État
                <select
                  onChange={(event) =>
                    updateItem(category.id, {
                      status: event.target.value as EditableCategoryState["status"],
                    })
                  }
                  value={state.status}
                >
                  <option value="active">Active</option>
                  <option value="inactive">Inactive</option>
                </select>
              </label>
              <label>
                Ordre
                <input
                  onChange={(event) =>
                    updateItem(category.id, {
                      displayOrder: event.target.value,
                    })
                  }
                  type="number"
                  value={state.displayOrder}
                />
              </label>
            </div>

            <div className="stack-row">
              <SubmitButton
                idleLabel="Enregistrer"
                isSubmitting={submittingId === category.id}
                onClick={() => void handleSave(category.id)}
                submittingLabel="Enregistrement..."
                type="button"
              />
              <button
                className="button button-secondary"
                disabled={category.resourceCount > 0}
                onClick={() => void handleDelete(category)}
                type="button"
              >
                Supprimer
              </button>
            </div>
            {category.resourceCount > 0 ? (
              <p className="field-hint">
                La suppression reste bloquée tant que des téléchargements utilisent
                cette catégorie.
              </p>
            ) : null}
          </article>
        );
      })}

      {message ? (
        <FormMessage title={message.title} tone={message.tone}>
          <p>{message.text}</p>
        </FormMessage>
      ) : null}
    </div>
  );
}
