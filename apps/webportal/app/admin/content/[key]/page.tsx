import Link from "next/link";
import { notFound } from "next/navigation";
import { isManagedContentKey } from "@kermaria/shared";

import { AdminManagedContentForm } from "@/components/AdminManagedContentForm";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { requireAdminSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import { getAdminManagedContent } from "@/lib/internal-api";

export const metadata = {
  title: "Édition contenu - Administration",
};

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ key: string }>;
};

function normalizeManagedContentKey(value: string): string {
  try {
    return decodeURIComponent(value);
  } catch {
    return value;
  }
}

export default async function AdminManagedContentDetailPage({
  params,
}: PageProps) {
  await requireAdminSession();
  const { key } = await params;
  const normalizedKey = normalizeManagedContentKey(key);

  if (!isManagedContentKey(normalizedKey)) {
    notFound();
  }

  const result = await getAdminManagedContent(normalizedKey);
  if (result.error || !result.data) {
    return (
      <>
        <PageHeader
          description="Le contenu demandé est temporairement indisponible."
          eyebrow="Contenus administrables"
          title="Édition du contenu"
        />
        <ErrorState
          description="Impossible de charger ce contenu administrable pour le moment."
          reference={result.correlationId}
          title="Contenu indisponible"
        />
      </>
    );
  }

  const content = result.data;

  return (
    <>
      <PageHeader
        description={`URL publique : ${content.publicPath}`}
        eyebrow={
          content.contentType === "legal"
            ? "Contenu légal"
            : content.contentType === "page"
              ? "Page du site"
              : "Fiche technique pack"
        }
        title={content.title}
      />

      <div className="stack-row">
        <Link className="text-link" href="/admin/content">
          ← Retour aux contenus
        </Link>
        <Link
          className="text-link"
          href={content.publicPath}
          prefetch={false}
          rel="noreferrer"
          target="_blank"
        >
          Voir la page publique
        </Link>
      </div>

      <section className="content-panel page-header-split">
        <div>
          <span className="card-kicker">Métadonnées fixes</span>
          <h2>Clé, URL et historique</h2>
          <p>
            Le titre, le type et l&apos;URL publique sont définis par la clé de
            contenu. Seuls le Markdown et le libellé de version sont éditables.
          </p>
        </div>
        <dl className="profile-details">
          <div>
            <dt>Clé</dt>
            <dd>{content.key}</dd>
          </div>
          <div>
            <dt>Type</dt>
            <dd>{content.contentType}</dd>
          </div>
          <div>
            <dt>Créé le</dt>
            <dd>{content.createdAt ? formatDateTime(content.createdAt) : "—"}</dd>
          </div>
          <div>
            <dt>Mis à jour le</dt>
            <dd>{content.updatedAt ? formatDateTime(content.updatedAt) : "—"}</dd>
          </div>
        </dl>
      </section>

      <AdminManagedContentForm content={content} />

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
