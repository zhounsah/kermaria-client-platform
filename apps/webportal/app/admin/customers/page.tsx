import Link from "next/link";

import { AdminDataTable } from "@/components/AdminDataTable";
import { EmptyState } from "@/components/EmptyState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import { formatDate } from "@/lib/formatters";
import { getAdminCustomers } from "@/lib/internal-api";

export const metadata = { title: "Clients - Administration" };
export const dynamic = "force-dynamic";

export default async function AdminCustomersPage() {
  await requireAdminSession();
  const result = await getAdminCustomers();

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Lecture seule" tone="info" />}
        description="Liste limitée aux informations utiles au suivi du portail."
        eyebrow="Administration interne"
        title="Clients"
      />
      {result.data.length > 0 ? (
        <AdminDataTable
          caption="Clients du portail"
          columns={[
            "Référence",
            "Client",
            "Statut",
            "Services",
            "Support ouvert",
            "Création",
            "Dernière activité",
            "Détail",
          ]}
          rows={result.data.map((customer) => [
            <code key={`${customer.customerReference}-reference`}>
              {customer.customerReference}
            </code>,
            <Link
              className="table-action"
              href={`/admin/customers/${encodeURIComponent(customer.customerReference)}`}
              key={`${customer.customerReference}-detail-link`}
            >
              {customer.displayName}
            </Link>,
            customer.status,
            String(customer.serviceCount),
            String(customer.openSupportRequestCount),
            formatDate(customer.createdAt),
            formatDate(customer.lastActivityAt),
            <Link
              className="table-action"
              href={`/admin/customers/${encodeURIComponent(customer.customerReference)}`}
              key={`${customer.customerReference}-detail`}
            >
              Consulter
            </Link>,
          ])}
        />
      ) : (
        <EmptyState
          description="Aucun client n'est disponible pour cette vue."
          title="Aucun client"
        />
      )}
      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
