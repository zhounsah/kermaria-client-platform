import Link from "next/link";
import { notFound } from "next/navigation";

import { ErrorState } from "@/components/ErrorState";
import { AdminRequestFollowUp } from "@/components/AdminRequestFollowUp";
import { InternalNoteForm } from "@/components/InternalNoteForm";
import { InternalNoteList } from "@/components/InternalNoteList";
import { PageHeader } from "@/components/PageHeader";
import { PublicMessageForm } from "@/components/PublicMessageForm";
import { PublicConversation } from "@/components/PublicConversation";
import { RequestStatusBadge } from "@/components/RequestStatusBadge";
import { RequestTimeline } from "@/components/RequestTimeline";
import { SectionCard } from "@/components/SectionCard";
import { StatusChangeForm } from "@/components/StatusChangeForm";
import { requireAdminSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import { getAdminSupportRequest } from "@/lib/internal-api";

export const metadata = { title: "Détail support - Administration" };
export const dynamic = "force-dynamic";

type PageProps = { params: Promise<{ id: string }> };

export default async function AdminSupportRequestDetailPage({
  params,
}: PageProps) {
  await requireAdminSession();
  const { id } = await params;
  const result = await getAdminSupportRequest(id);

  if (result.error) {
    return (
      <ErrorState
        action={<Link className="button" href="/admin/support-requests">Retour</Link>}
        description="Impossible de charger cette demande support."
        reference={result.correlationId}
        title="Demande indisponible"
      />
    );
  }

  if (!result.data) {
    notFound();
  }

  const request = result.data;
  return (
    <>
      <PageHeader
        action={
          <RequestStatusBadge
            requestType="support"
            status={request.status}
          />
        }
        description={`${request.customerName} · ${request.customerReference}`}
        eyebrow={request.reference}
        title={request.subject}
      />

      <div className="request-detail-layout">
        <SectionCard ariaLabel="Informations de la demande support">
          <h2>Demande</h2>
          <dl className="request-details">
            <div><dt>Service</dt><dd>{request.serviceName}</dd></div>
            <div><dt>Priorité</dt><dd>{request.priority}</dd></div>
            <div><dt>Créée</dt><dd>{formatDateTime(request.createdAt)}</dd></div>
            <div><dt>Mise à jour</dt><dd>{formatDateTime(request.updatedAt)}</dd></div>
          </dl>
          <h3>Description</h3>
          <p className="request-description">{request.description}</p>
        </SectionCard>

        <SectionCard ariaLabel="Changement de statut">
          <h2>Changer le statut</h2>
          <StatusChangeForm
            currentStatus={request.status}
            requestId={request.id}
            requestType="support"
          />
        </SectionCard>
      </div>

      <AdminRequestFollowUp
        messages={request.publicMessages}
        requiresAttention={
          request.status === "open" || request.status === "in_progress"
        }
      />

      <div className="request-detail-layout">
        <SectionCard ariaLabel="Note interne">
          <h2>Ajouter une note interne</h2>
          <InternalNoteForm requestId={request.id} requestType="support" />
        </SectionCard>
        <SectionCard ariaLabel="Message au client">
          <h2>Ajouter un message au client</h2>
          <PublicMessageForm requestId={request.id} requestType="support" />
        </SectionCard>
      </div>

      <div className="request-detail-layout">
        <SectionCard ariaLabel="Conversation publique de la demande">
          <h2>Conversation publique</h2>
          <PublicConversation messages={request.publicMessages} />
        </SectionCard>
        <SectionCard ariaLabel="Notes internes existantes">
          <h2>Notes internes</h2>
          <InternalNoteList notes={request.internalNotes} />
        </SectionCard>
      </div>
      <SectionCard
        ariaLabel="Historique de la demande"
        className="request-history-section"
      >
        <h2>Historique</h2>
        <RequestTimeline events={request.events} requestType="support" />
      </SectionCard>
    </>
  );
}
