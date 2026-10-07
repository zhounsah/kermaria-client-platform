import Link from "next/link";
import { PageHeader } from "@/components/PageHeader";
import { ErrorState } from "@/components/ErrorState";
import { SitePageFrame } from "@/components/SitePageFrame";
import { requireAdminSession } from "@/lib/auth";
import { formatDate } from "@/lib/formatters";
import { dataRequestStatusLabels, dataRequestTypeLabels } from "@/lib/data-subject-request-labels";
import { getAdminDataRequests, getAdminDataRequestPageLayout } from "@/lib/internal-api";

export const metadata = { title: "Demandes de données - Administration" };
export const dynamic = "force-dynamic";

export default async function AdminDataRequestsPage() {
  await requireAdminSession();
  const [result, layout] = await Promise.all([
    getAdminDataRequests(), getAdminDataRequestPageLayout(),
  ]);
  const slots = {
    admin_data_request_intro: <PageHeader title="Demandes de données" description="Traitez chaque demande et répondez dans l’espace client sécurisé." eyebrow="Administration interne" />,
    admin_data_request_list: result.error ? <ErrorState title="Demandes indisponibles" description="La liste ne peut pas être chargée." reference={result.correlationId} />
      : <div className="content-panel data-request-list"><h2>Demandes reçues</h2>
        {result.data.length === 0 ? <p>Aucune demande pour le moment.</p> : <ul>{result.data.map((item) => <li key={item.id}>
          <Link href={`/admin/data-requests/${encodeURIComponent(item.id)}`}><strong>{item.reference}</strong> · {dataRequestTypeLabels[item.requestType]}</Link>
          <span>{dataRequestStatusLabels[item.status]} · Échéance {formatDate(item.dueAt)}</span>
        </li>)}</ul>}
      </div>,
    admin_data_request_help: <section className="content-panel data-request-info-card">
      <h2>Bonnes pratiques</h2><p>Répondez dans l’espace sécurisé du demandeur. Si une pièce ou une précision est nécessaire, expliquez pourquoi dans le fil de la demande.</p>
    </section>,
  };
  return <SitePageFrame area="admin" pageKey="/admin/data-requests" initialLayout={layout.data} slots={slots}>{null}</SitePageFrame>;
}
