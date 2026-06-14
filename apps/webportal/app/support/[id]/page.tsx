import Link from "next/link";
import { notFound } from "next/navigation";

import { ErrorState } from "@/components/ErrorState";
import { ClientReplyForm } from "@/components/ClientReplyForm";
import { PageHeader } from "@/components/PageHeader";
import { PublicConversation } from "@/components/PublicConversation";
import { RequestStatusBadge } from "@/components/RequestStatusBadge";
import { RequestTimeline } from "@/components/RequestTimeline";
import { SectionCard } from "@/components/SectionCard";
import { requireClientSession } from "@/lib/auth";
import { formatDateTime, supportStatus } from "@/lib/formatters";
import { getSupportRequest } from "@/lib/internal-api";

export const metadata = { title: "Suivi support" };
export const dynamic = "force-dynamic";

type PageProps = { params: Promise<{ id: string }> };

export default async function SupportRequestDetailPage({ params }: PageProps) {
  await requireClientSession();
  const { id } = await params;
  const result = await getSupportRequest(id);

  if (result.error) {
    return (
      <ErrorState
        action={<Link className="button" href="/support">Retour au support</Link>}
        description="Impossible de charger le suivi de cette demande."
        reference={result.correlationId}
        title="Suivi indisponible"
      />
    );
  }

  if (!result.data) {
    notFound();
  }

  const request = result.data;
  const status = supportStatus[request.status];
  return (
    <>
      <PageHeader
        action={
          <RequestStatusBadge
            requestType="support"
            status={request.status}
          />
        }
        description={status.description}
        eyebrow={request.reference}
        title={request.subject}
      />
      <div className="request-detail-layout">
        <SectionCard ariaLabel="Informations de la demande">
          <h2>Informations</h2>
          <dl className="request-details">
            <div><dt>Service</dt><dd>{request.serviceName}</dd></div>
            <div><dt>Priorité</dt><dd>{request.priority}</dd></div>
            <div><dt>Créée</dt><dd>{formatDateTime(request.createdAt)}</dd></div>
            <div><dt>Mise à jour</dt><dd>{formatDateTime(request.updatedAt)}</dd></div>
          </dl>
          <h3>Description transmise</h3>
          <p className="request-description">{request.description}</p>
        </SectionCard>
        <SectionCard ariaLabel="Conversation publique de la demande">
          <h2>Conversation</h2>
          <PublicConversation messages={request.publicMessages} />
          <ClientReplyForm requestId={request.id} requestType="support" />
        </SectionCard>
      </div>
      <SectionCard
        ariaLabel="Historique des statuts de la demande"
        className="request-history-section"
      >
        <h2>Historique des statuts</h2>
        <RequestTimeline events={request.events} requestType="support" />
      </SectionCard>
    </>
  );
}
