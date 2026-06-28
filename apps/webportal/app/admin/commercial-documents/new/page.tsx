import Link from "next/link";

import { AdminCommercialDocumentCreateForm } from "@/components/AdminCommercialDocumentCreateForm";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import {
  getAdminCustomers,
  getAdminServiceRequests,
  resolveDataSource,
} from "@/lib/internal-api";

export const metadata = {
  title: "Nouveau brouillon - Documents commerciaux",
};

export const dynamic = "force-dynamic";

type PageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default async function AdminCommercialDocumentCreatePage({
  searchParams,
}: PageProps) {
  await requireAdminSession();
  const filters = await searchParams;
  const customerReference = first(filters.customerReference);
  const serviceRequestId = first(filters.serviceRequestId);
  const [customersResult, serviceRequestsResult] = await Promise.all([
    getAdminCustomers(),
    getAdminServiceRequests(),
  ]);
  const source = resolveDataSource([
    customersResult.source,
    serviceRequestsResult.source,
  ]);

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Création" tone="info" />}
        description="Sélectionnez le client puis renseignez les éléments du brouillon. Vous pourrez ajouter les lignes après la création."
        eyebrow="Documents commerciaux"
        title="Nouveau brouillon"
      />

      <p>
        <Link className="text-link" href="/admin/commercial-documents">
          ← Retour à la liste des documents
        </Link>
      </p>

      {customersResult.error || serviceRequestsResult.error ? (
        <ErrorState
          description="Les listes clients ou demandes nécessaires à la création sont indisponibles."
          reference={
            customersResult.error
              ? customersResult.correlationId
              : serviceRequestsResult.correlationId
          }
          title="Création indisponible"
        />
      ) : (
        <SectionCard ariaLabel="Création d'un document commercial">
          <h2>Détails du brouillon</h2>
          <AdminCommercialDocumentCreateForm
            customers={customersResult.data}
            initialCustomerReference={customerReference}
            initialServiceRequestId={serviceRequestId}
            serviceRequests={serviceRequestsResult.data}
          />
        </SectionCard>
      )}

      <MockNotice
        correlationId={customersResult.correlationId}
        source={source}
      />
    </>
  );
}

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
