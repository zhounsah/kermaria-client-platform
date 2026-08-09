"use client";

import type {
  EditorialCategory,
  EditorialCategoryPayload,
  EditorialContentType,
} from "@kermaria/shared";
import { FormEvent, useRef, useState } from "react";
import { useRouter } from "next/navigation";

import { requestBffJson } from "@/lib/client-api";

type AdminEditorialCategoryFormProps = {
  categories: EditorialCategory[];
  contentType: EditorialContentType;
};

export function AdminEditorialCategoryForm({
  categories,
  contentType,
}: AdminEditorialCategoryFormProps) {
  const router = useRouter();
  const submittingRef = useRef(false);
  const [visibleCategories, setVisibleCategories] =
    useState<EditorialCategory[]>(categories);
  const [name, setName] = useState("");
  const [slug, setSlug] = useState("");
  const [description, setDescription] = useState("");
  const [sortOrder, setSortOrder] = useState("0");
  const [message, setMessage] = useState<string | null>(null);
  const [isOpen, setIsOpen] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submittingRef.current) {
      return;
    }

    const payload: EditorialCategoryPayload = {
      contentType,
      name: name.trim(),
      slug: slug.trim().toLowerCase(),
      description: description.trim() || null,
      sortOrder: Number.parseInt(sortOrder, 10) || 0,
    };

    submittingRef.current = true;
    const result = await requestBffJson<EditorialCategory>(
      "/api/admin/editorial/categories",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      },
    );
    submittingRef.current = false;

    if (result.ok) {
      setVisibleCategories((current) => [
        ...current.filter((category) => category.id !== result.data.id),
        result.data,
      ].sort((left, right) =>
        left.sortOrder - right.sortOrder
        || left.name.localeCompare(right.name, "fr"),
      ));
      setName("");
      setSlug("");
      setDescription("");
      setSortOrder("0");
      setIsOpen(false);
      setMessage("Catégorie créée.");
      router.refresh();
    } else {
      setMessage(result.error.message);
    }
  }

  return (
    <section className="editorial-category-panel">
      <div className="section-heading">
        <div>
          <span className="card-kicker">Catégories</span>
          <h2>Catégories</h2>
          <p>
            {visibleCategories.length > 0
              ? `${visibleCategories.length} catégorie${visibleCategories.length > 1 ? "s" : ""}`
              : "Aucune catégorie créée"}
          </p>
        </div>
        <button
          aria-expanded={isOpen}
          className="button button-secondary"
          onClick={() => setIsOpen((current) => !current)}
          type="button"
        >
          {isOpen ? "Fermer" : "+ Ajouter une catégorie"}
        </button>
      </div>
      {visibleCategories.length > 0 ? (
        <div className="editorial-category-list" aria-label="Catégories existantes">
          {visibleCategories.map((category) => (
            <div className="editorial-category-item" key={category.id}>
              <div>
                <strong>{category.name}</strong>
                {category.description ? <small>{category.description}</small> : null}
              </div>
              <code>{category.slug}</code>
            </div>
          ))}
        </div>
      ) : !isOpen ? (
        <p className="empty-copy">Aucune catégorie pour le moment.</p>
      ) : null}
      {isOpen ? (
        <form className="form-card compact-form-card" onSubmit={submit}>
          <div className="form-grid">
            <label>
              Nom
              <input
                maxLength={160}
                onChange={(event) => setName(event.target.value)}
                required
                value={name}
              />
            </label>
            <label>
              Slug
              <input
                maxLength={100}
                onChange={(event) => setSlug(event.target.value)}
                pattern="[a-z0-9]+(?:-[a-z0-9]+)*"
                required
                value={slug}
              />
            </label>
          </div>
          <label>
            Description
            <input
              maxLength={500}
              onChange={(event) => setDescription(event.target.value)}
              value={description}
            />
          </label>
          <label>
            Ordre
            <input
              min={0}
              onChange={(event) => setSortOrder(event.target.value)}
              type="number"
              value={sortOrder}
            />
          </label>
          <div className="stack-row">
            <button className="button button-secondary" type="submit">
              Créer la catégorie
            </button>
            {message ? <span className="field-hint">{message}</span> : null}
          </div>
        </form>
      ) : message ? (
        <p className="field-hint">{message}</p>
      ) : null}
    </section>
  );
}
