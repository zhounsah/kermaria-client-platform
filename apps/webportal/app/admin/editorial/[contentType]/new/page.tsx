import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";

import { AdminEditorialForm } from "@/components/AdminEditorialForm";
import { requireAdminSession } from "@/lib/auth";
import {
  contentTypeFromSegment,
  editorialSectionTitle,
} from "@/lib/editorial-admin";
import { getAdminEditorialList } from "@/lib/internal-api";

type AdminEditorialNewPageProps = {
  params: Promise<{ contentType: string }>;
};

export const metadata: Metadata = {
  title: "Nouveau contenu - Administration",
};

export default async function AdminEditorialNewPage({
  params,
}: AdminEditorialNewPageProps) {
  await requireAdminSession();
  const { contentType: segment } = await params;
  const contentType = contentTypeFromSegment(segment);
  if (!contentType) {
    notFound();
  }

  const list = await getAdminEditorialList(`contentType=${contentType}`);

  return (
    <div className="stack-panels">
      <div className="page-heading">
        <div>
          <Link className="text-link" href={`/admin/editorial/${segment}`}>
            Retour
          </Link>
          <span className="card-kicker">{editorialSectionTitle(contentType)}</span>
          <h1>Nouveau contenu</h1>
        </div>
      </div>
      <AdminEditorialForm
        categories={list.data.categories}
        contentType={contentType}
        mode="create"
      />
    </div>
  );
}
