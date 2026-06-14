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
import {
  formatDateTime,
  serviceRequestStatus,
} from "@/lib/formatters";
import { getServiceRequest } from "@/lib/internal-api";

export const metadata = { title: "Suivi demande de service" };
export const dynamic = "force-dynamic";

type PageProps = { params: Promise<{ id: string }> };

export default async function ServiceRequestDetailPage({ params }: PageProps) {
  await requireClientSession();
  const { id } = await params;
  const result = await getServiceRequest(id);

  if (result.error) {
    return (
      <ErrorState
        action={<Link className="button" href="/request-service">Retour</Link>}
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
  const status = serviceRequestStatus[request.status];
  return (
    <>
      <PageHeader
        action={
          <RequestStatusBadge
            requestType="service"
            status={request.status}
          />
        }
        description={status.description}
        eyebrow={request.reference}
        title={request.subject}
      />
      <div className="request-detail-layout">
        <SectionCard ariaLabel="Informations de la demande de service">
          <h2>Informations</h2>
          <dl className="request-details">
            <div><dt>Prestation étudiée</dt><dd>{request.catalogItemName}</dd></div>
            <div><dt>Traitement</dt><dd>Manuel uniquement</dd></div>
            <div><dt>Créée</dt><dd>{formatDateTime(request.createdAt)}</dd></div>
            <div><dt>Mise à jour</dt><dd>{formatDateTime(request.updatedAt)}</dd></div>
          </dl>
          <h3>Description transmise</h3>
          <p className="request-description">{request.description}</p>
        </SectionCard>
        <SectionCard ariaLabel="Conversation publique de la demande">
          <h2>Conversation</h2>
          <PublicConversation messages={request.publicMessages} />
          <ClientReplyForm requestId={request.id} requestType="service" />
        </SectionCard>
      </div>
      <SectionCard
        ariaLabel="Historique des statuts de la demande"
        className="request-history-section"
      >
        <h2>Historique des statuts</h2>
        <RequestTimeline events={request.events} requestType="service" />
      </SectionCard>
    </>
  );
}
