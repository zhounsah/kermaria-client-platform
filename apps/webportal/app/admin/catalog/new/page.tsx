import Link from "next/link";

import { AdminCatalogOfferForm } from "@/components/AdminCatalogOfferForm";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";

export const metadata = {
  title: "Nouvelle offre - Catalogue",
};

export const dynamic = "force-dynamic";

export default async function AdminCatalogCreatePage() {
  await requireAdminSession();

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Création" tone="info" />}
        description="Renseignez les champs pour créer une nouvelle offre informative."
        eyebrow="Catalogue commercial"
        title="Nouvelle offre"
      />

      <p>
        <Link className="text-link" href="/admin/catalog">
          ← Retour au catalogue
        </Link>
      </p>

      <SectionCard ariaLabel="Création d'une offre commerciale">
        <h2>Détails de l&apos;offre</h2>
        <AdminCatalogOfferForm />
      </SectionCard>
    </>
  );
}
