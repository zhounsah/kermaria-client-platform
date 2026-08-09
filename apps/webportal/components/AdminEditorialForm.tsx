"use client";

import type {
  EditorialCategory,
  EditorialContentDetail,
  EditorialContentPayload,
  EditorialContentType,
  EditorialMutationResponse,
} from "@kermaria/shared";
import { FormEvent, startTransition, useRef, useState } from "react";
import { useRouter } from "next/navigation";

import { FormMessage } from "@/components/FormMessage";
import { ManagedMarkdown } from "@/components/ManagedMarkdown";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";

type AdminEditorialFormProps = {
  contentType: EditorialContentType;
  mode: "create" | "edit";
  content?: EditorialContentDetail | null;
  categories: EditorialCategory[];
};

type FormState = {
  title: string;
  slug: string;
  summary: string;
  bodyMarkdown: string;
  categoryId: string;
  status: EditorialContentPayload["status"];
  seoTitle: string;
  seoDescription: string;
  canonicalUrl: string;
  noIndex: boolean;
  sortOrder: string;
  faqScopes: string;
};

const statusLabels: Record<FormState["status"], string> = {
  draft: "Brouillon",
  published: "Publié",
  archived: "Archivé",
  scheduled: "Planifié",
};

export function AdminEditorialForm({
  contentType,
  mode,
  content,
  categories,
}: AdminEditorialFormProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [state, setState] = useState<FormState>({
    title: content?.title ?? "",
    slug: content?.slug ?? "",
    summary: content?.summary ?? "",
    bodyMarkdown: content?.bodyMarkdown ?? "",
    categoryId: content?.categoryId ?? "",
    status: content?.status ?? "draft",
    seoTitle: content?.seoTitle ?? "",
    seoDescription: content?.seoDescription ?? "",
    canonicalUrl: content?.canonicalUrl ?? "",
    noIndex: content?.noIndex ?? false,
    sortOrder: String(content?.sortOrder ?? 0),
    faqScopes: content?.faqScopes.join(", ") ?? "",
  });
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<{
    tone: "success" | "error" | "info";
    title: string;
    text: string;
  } | null>(null);

  function update<Key extends keyof FormState>(key: Key, value: FormState[Key]) {
    setState((current) => ({ ...current, [key]: value }));
  }

  function buildPayload(statusOverride?: FormState["status"]): EditorialContentPayload {
    return {
      contentType,
      title: state.title.trim(),
      slug: state.slug.trim().toLowerCase(),
      summary: state.summary.trim() || null,
      bodyMarkdown: state.bodyMarkdown,
      categoryId: state.categoryId || null,
      status: statusOverride ?? state.status,
      seoTitle: state.seoTitle.trim() || null,
      seoDescription: state.seoDescription.trim() || null,
      canonicalUrl: state.canonicalUrl.trim() || null,
      noIndex: state.noIndex,
      sortOrder: Number.parseInt(state.sortOrder, 10) || 0,
      faqScopes: state.faqScopes
        .split(",")
        .map((scope) => scope.trim().toLowerCase())
        .filter(Boolean),
    };
  }

  async function save(statusOverride?: FormState["status"]) {
    if (isSubmittingRef.current) {
      return;
    }

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);

    const endpoint =
      mode === "create"
        ? "/api/admin/editorial"
        : `/api/admin/editorial/${encodeURIComponent(content!.id)}`;
    const result = await requestBffJson<EditorialMutationResponse>(
      endpoint as `/api/${string}`,
      {
        method: mode === "create" ? "POST" : "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(buildPayload(statusOverride)),
      },
    );

    if (result.ok) {
      setMessage({
        tone: "success",
        title: "Contenu enregistré",
        text: result.data.changed
          ? "Les modifications ont été enregistrées."
          : "Aucune modification supplémentaire n'a été détectée.",
      });
      startTransition(() => {
        if (mode === "create") {
          router.replace(
            `/admin/editorial/${adminTypeSegment(contentType)}/${encodeURIComponent(result.data.id)}`,
          );
        } else {
          router.refresh();
        }
      });
    } else {
      setMessage({
        tone: "error",
        title: "Enregistrement impossible",
        text: result.error.message,
      });
    }

    isSubmittingRef.current = false;
    setIsSubmitting(false);
  }

  async function mutateAction(action: "publish" | "archive") {
    if (!content || isSubmittingRef.current) {
      return;
    }

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);
    const result = await requestBffJson<EditorialMutationResponse>(
      `/api/admin/editorial/${encodeURIComponent(content.id)}/${action}`,
      { method: "POST" },
    );

    if (result.ok) {
      setMessage({
        tone: "success",
        title: action === "publish" ? "Publication" : "Archivage",
        text:
          action === "publish"
            ? "Le contenu publié est disponible côté public."
            : "Le contenu est archivé et retiré du public.",
      });
      startTransition(() => router.refresh());
    } else {
      setMessage({
        tone: "error",
        title: "Action impossible",
        text: result.error.message,
      });
    }

    isSubmittingRef.current = false;
    setIsSubmitting(false);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await save();
  }

  async function importMarkdown(file: File | null) {
    if (!file) {
      return;
    }

    if (!/\.(md|markdown)$/i.test(file.name) || file.size > 512 * 1024) {
      setMessage({
        tone: "error",
        title: "Import refusé",
        text: "Le fichier doit être un Markdown .md ou .markdown de 512 Ko maximum.",
      });
      return;
    }

    const imported = parseMarkdownFile(await file.text());
    setState((current) => ({
      ...current,
      title: imported.title ?? current.title,
      slug: imported.slug ?? current.slug,
      summary: imported.description ?? current.summary,
      bodyMarkdown: imported.bodyMarkdown,
    }));
    setMessage({
      tone: imported.warnings.length > 0 ? "info" : "success",
      title: "Markdown importé",
      text: imported.warnings.join(" ") || "Le brouillon a été rempli.",
    });
  }

  function exportMarkdown() {
    const frontmatter = [
      "---",
      `title: ${state.title}`,
      `slug: ${state.slug}`,
      state.summary.trim() ? `description: ${state.summary.trim()}` : null,
      "---",
      "",
    ].filter((line): line is string => line !== null);
    const blob = new Blob([`${frontmatter.join("\n")}${state.bodyMarkdown}`], {
      type: "text/markdown;charset=utf-8",
    });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `${state.slug || "contenu"}.md`;
    link.click();
    URL.revokeObjectURL(url);
  }

  return (
    <form className="form-card editorial-form" onSubmit={handleSubmit}>
      <div className="editorial-form-grid">
        <div className="editorial-form-main">
          <div className="form-grid">
            <label>
              Titre
              <input
                maxLength={220}
                onChange={(event) => update("title", event.target.value)}
                required
                value={state.title}
              />
            </label>
            <label>
              Slug
              <input
                maxLength={120}
                onChange={(event) => update("slug", event.target.value)}
                pattern="[a-z0-9]+(?:-[a-z0-9]+)*"
                required
                value={state.slug}
              />
            </label>
          </div>

          <label>
            Résumé / introduction
            <textarea
              maxLength={600}
              onChange={(event) => update("summary", event.target.value)}
              rows={3}
              value={state.summary}
            />
          </label>

          <label>
            Contenu Markdown
            <textarea
              maxLength={160000}
              onChange={(event) => update("bodyMarkdown", event.target.value)}
              rows={24}
              value={state.bodyMarkdown}
            />
          </label>
        </div>

        <aside className="editorial-form-side">
          <label>
            État
            <select
              onChange={(event) =>
                update("status", event.target.value as FormState["status"])
              }
              value={state.status}
            >
              {Object.entries(statusLabels).map(([value, label]) => (
                <option key={value} value={value}>{label}</option>
              ))}
            </select>
          </label>

          {contentType !== "seo_page" ? (
            <label>
              Catégorie
              <select
                onChange={(event) => update("categoryId", event.target.value)}
                value={state.categoryId}
              >
                <option value="">Aucune</option>
                {categories
                  .filter((category) => category.contentType === contentType)
                  .map((category) => (
                    <option key={category.id} value={category.id}>
                      {category.name}
                    </option>
                  ))}
              </select>
            </label>
          ) : null}

          {contentType === "faq" ? (
            <label>
              Scopes FAQ
              <input
                onChange={(event) => update("faqScopes", event.target.value)}
                placeholder="global, offres"
                value={state.faqScopes}
              />
            </label>
          ) : null}

          <label>
            Ordre
            <input
              min={0}
              onChange={(event) => update("sortOrder", event.target.value)}
              type="number"
              value={state.sortOrder}
            />
          </label>

          <label>
            Titre SEO
            <input
              maxLength={220}
              onChange={(event) => update("seoTitle", event.target.value)}
              value={state.seoTitle}
            />
          </label>
          <label>
            Meta description
            <textarea
              maxLength={320}
              onChange={(event) => update("seoDescription", event.target.value)}
              rows={3}
              value={state.seoDescription}
            />
          </label>
          <label>
            Canonical absolue optionnelle
            <input
              onChange={(event) => update("canonicalUrl", event.target.value)}
              type="url"
              value={state.canonicalUrl}
            />
          </label>
          <label className="admin-solution-checkbox">
            <input
              checked={state.noIndex}
              onChange={(event) => update("noIndex", event.target.checked)}
              type="checkbox"
            />
            <span>Noindex</span>
          </label>

          <label>
            Importer un fichier Markdown
            <input
              accept=".md,.markdown,text/markdown,text/plain"
              onChange={(event) => void importMarkdown(event.target.files?.[0] ?? null)}
              type="file"
            />
          </label>
          <button
            className="button button-secondary"
            onClick={exportMarkdown}
            type="button"
          >
            Exporter en Markdown
          </button>
        </aside>
      </div>

      <section className="managed-content-preview-card">
        <div className="managed-content-preview-header">
          <span className="card-kicker">Aperçu rendu</span>
          <h3>{state.title || "Nouveau contenu"}</h3>
        </div>
        <ManagedMarkdown markdown={state.bodyMarkdown || "_Aucun contenu._"} withAnchors />
      </section>

      {message ? (
        <FormMessage title={message.title} tone={message.tone}>
          <p>{message.text}</p>
        </FormMessage>
      ) : null}

      <div className="stack-row">
        <SubmitButton
          idleLabel="Enregistrer"
          isSubmitting={isSubmitting}
          submittingLabel="Enregistrement..."
        />
        <button
          className="button button-secondary"
          onClick={() => void save("draft")}
          type="button"
        >
          Sauvegarder brouillon
        </button>
        {mode === "edit" ? (
          <>
            <button
              className="button button-secondary"
              onClick={() => void mutateAction("publish")}
              type="button"
            >
              Publier
            </button>
            <button
              className="button button-secondary"
              onClick={() => void mutateAction("archive")}
              type="button"
            >
              Archiver
            </button>
          </>
        ) : null}
      </div>
    </form>
  );
}

function parseMarkdownFile(raw: string) {
  const warnings: string[] = [];
  const match = raw.match(/^---\r?\n([\s\S]*?)\r?\n---\r?\n?/);
  if (!match) {
    return { title: detectTitle(raw), slug: null, description: null, bodyMarkdown: raw, warnings };
  }

  const frontmatter = match[1];
  const bodyMarkdown = raw.slice(match[0].length);
  const known = new Map<string, string>();
  for (const line of frontmatter.split(/\r?\n/)) {
    const item = line.match(/^([a-zA-Z0-9_-]+):\s*(.*)$/);
    if (!item) {
      warnings.push("Certaines lignes de frontmatter ont été ignorées.");
      continue;
    }
    known.set(item[1], item[2].replace(/^["']|["']$/g, "").trim());
  }

  return {
    title: known.get("title") || detectTitle(bodyMarkdown),
    slug: known.get("slug") || null,
    description: known.get("description") || null,
    bodyMarkdown,
    warnings,
  };
}

function detectTitle(markdown: string) {
  return markdown.match(/^#\s+(.+)$/m)?.[1].trim() ?? null;
}

function adminTypeSegment(contentType: EditorialContentType) {
  return contentType === "wiki_article"
    ? "wiki"
    : contentType === "seo_page"
      ? "seo"
      : "faq";
}
