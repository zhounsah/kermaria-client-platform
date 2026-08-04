import Link from "next/link";

import { AdminClientSolutionForm } from "@/components/AdminClientSolutionForm";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { requireAdminSession } from "@/lib/auth";

export const metadata = {
  title: "Nouvelle solution - Administration",
};

export const dynamic = "force-dynamic";

export default async function AdminNewClientSolutionPage() {
  await requireAdminSession();

  return (
    <>
      <PageHeader
        description="Ajoutez une tuile d'accès sur la page publique /solutions."
        eyebrow="Portail solutions"
        title="Nouvelle solution"
      />

      <div className="stack-row">
        <Link className="text-link" href="/admin/solutions">
          ← Retour à la liste
        </Link>
      </div>

      <SectionCard ariaLabel="Conseils de création">
        <span className="card-kicker">Conseil</span>
        <h2>Créez en brouillon, publiez ensuite</h2>
        <p>
          Enregistrez d&apos;abord la tuile en brouillon, vérifiez le lien et le
          logo, puis passez-la en « Publiée » quand elle est prête. Une tuile en
          brouillon n&apos;apparaît jamais sur le site public.
        </p>
      </SectionCard>

      <AdminClientSolutionForm mode="create" />
    </>
  );
}
