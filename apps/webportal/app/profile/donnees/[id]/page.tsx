import Link from "next/link";
import { notFound } from "next/navigation";
import { DataSubjectRequestReply } from "@/components/DataSubjectRequestReply";
import { PageHeader } from "@/components/PageHeader";
import { SitePageFrame } from "@/components/SitePageFrame";
import { requireClientSession } from "@/lib/auth";
import { formatDate, formatDateTime } from "@/lib/formatters";
import { dataRequestStatusLabels } from "@/lib/data-subject-request-labels";
import { getClientDataRequest, getClientDataRequestDetailPageLayout } from "@/lib/internal-api";

export const dynamic = "force-dynamic";
export default async function ClientDataRequestDetailPage({ params }: { params: Promise<{ id: string }> }) {
  await requireClientSession();
  const { id } = await params;
  const [result, layout] = await Promise.all([
    getClientDataRequest(id), getClientDataRequestDetailPageLayout(),
  ]);
  if (!result.data || result.error) notFound();
  const request = result.data;
  const slots = {
    client_data_detail_intro: <><Link className="back-link" href="/profile/donnees">← Toutes mes demandes</Link>
      <PageHeader title={`Demande ${request.reference}`} description={`Reçue le ${formatDateTime(request.createdAt)} · Réponse prévue avant le ${formatDate(request.dueAt)}`} eyebrow="Mes données" /></>,
    client_data_detail_summary: <section className="content-panel data-request-detail"><h2>Votre demande</h2><p>{request.details}</p><p>État : <strong>{dataRequestStatusLabels[request.status]}</strong></p>
      {request.deadlineExtendedAt ? <p>Le délai a été prolongé. La nouvelle date figure ci-dessus ; le motif se trouve dans les échanges.</p> : null}</section>,
    client_data_detail_messages: <section className="content-panel data-request-messages"><h2>Échanges</h2>
      {request.messages.length === 0 ? <p>Votre demande a bien été reçue.</p> : <ol>{request.messages.map((message) => <li key={message.id}>
        <strong>{message.authorRole === "team" ? "Équipe Zachary IT" : "Vous"}</strong>
        <time dateTime={message.createdAt}>{formatDateTime(message.createdAt)}</time><p>{message.body}</p>
      </li>)}</ol>}
    </section>,
    client_data_detail_file: request.responseFileName ? <p className="content-panel data-request-download">
      <strong>Document disponible : {request.responseFileName}</strong>
      <a className="button" download href={`/api/data-requests/${encodeURIComponent(id)}/file`}>Télécharger mon document</a>
    </p> : null,
    client_data_detail_reply: request.status !== "closed" && request.status !== "refused"
      ? <DataSubjectRequestReply id={id} admin={false} /> : null,
    client_data_detail_help: <section className="content-panel data-request-info-card">
      <h2>Une question sur la réponse ?</h2><p>Ajoutez une précision à la demande tant qu&apos;elle est ouverte, ou <Link href="/contact">contactez-nous</Link> si vous n&apos;avez plus accès à votre compte.</p>
    </section>,
  };
  return <SitePageFrame area="client" pageKey="/profile/donnees/[id]" initialLayout={layout.data} slots={slots}>{null}</SitePageFrame>;
}
