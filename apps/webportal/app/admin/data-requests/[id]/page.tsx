import Link from "next/link";
import { notFound } from "next/navigation";
import { DataSubjectRequestReply } from "@/components/DataSubjectRequestReply";
import { DataSubjectRequestFileUpload } from "@/components/DataSubjectRequestFileUpload";
import { PageHeader } from "@/components/PageHeader";
import { SitePageFrame } from "@/components/SitePageFrame";
import { requireAdminSession } from "@/lib/auth";
import { formatDate, formatDateTime } from "@/lib/formatters";
import { dataRequestStatusLabels, dataRequestTypeLabels } from "@/lib/data-subject-request-labels";
import { getAdminDataRequest, getAdminDataRequestDetailPageLayout } from "@/lib/internal-api";

export const dynamic = "force-dynamic";
export default async function AdminDataRequestDetailPage({ params }: { params: Promise<{ id: string }> }) {
  await requireAdminSession();
  const { id } = await params;
  const [result, layout] = await Promise.all([
    getAdminDataRequest(id), getAdminDataRequestDetailPageLayout(),
  ]);
  if (!result.data || result.error) notFound();
  const request = result.data;
  const slots = {
    admin_data_detail_intro: <><Link className="back-link" href="/admin/data-requests">← Toutes les demandes</Link>
      <PageHeader title={`Demande ${request.reference}`} description={`Reçue le ${formatDateTime(request.createdAt)} · Échéance ${formatDate(request.dueAt)}`} eyebrow="Administration interne" /></>,
    admin_data_detail_summary: <section className="content-panel data-request-detail"><h2>Demande initiale</h2><p>{request.details}</p>
      <p>Type : <strong>{dataRequestTypeLabels[request.requestType]}</strong> · État : <strong>{dataRequestStatusLabels[request.status]}</strong></p></section>,
    admin_data_detail_messages: <section className="content-panel data-request-messages"><h2>Échanges visibles par le client</h2>
      {request.messages.length === 0 ? <p>Aucun message.</p> : <ol>{request.messages.map((message) => <li key={message.id}>
        <strong>{message.authorRole === "team" ? "Équipe Zachary IT" : "Client"}</strong>
        <time dateTime={message.createdAt}>{formatDateTime(message.createdAt)}</time><p>{message.body}</p>
      </li>)}</ol>}
    </section>,
    admin_data_detail_reply: <DataSubjectRequestReply id={id} admin canExtendDeadline={!request.deadlineExtendedAt} />,
    admin_data_detail_file: <DataSubjectRequestFileUpload id={id} />,
    admin_data_detail_help: <section className="content-panel data-request-info-card">
      <h2>Traitement de la demande</h2><p>Répondez dans le fil sécurisé et vérifiez les données avant de remettre un document à la personne concernée.</p>
    </section>,
  };
  return <SitePageFrame area="admin" pageKey="/admin/data-requests/[id]" initialLayout={layout.data} slots={slots}>{null}</SitePageFrame>;
}
