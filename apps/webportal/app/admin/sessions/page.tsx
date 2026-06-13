import { AdminDataTable } from "@/components/AdminDataTable";
import { EmptyState } from "@/components/EmptyState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SessionStatusBadge } from "@/components/SessionStatusBadge";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import { getAdminSessions } from "@/lib/internal-api";

export const metadata = { title: "Sessions - Administration" };
export const dynamic = "force-dynamic";

export default async function AdminSessionsPage() {
  await requireAdminSession();
  const result = await getAdminSessions();

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Tokens masqués" tone="success" />}
        description="Aucun token brut ni hash de session n'est affiché. Les adresses réseau sont réduites."
        eyebrow="Administration interne"
        title="Sessions portail"
      />
      {result.data.length > 0 ? (
        <AdminDataTable
          caption="Sessions du portail"
          columns={[
            "Utilisateur",
            "Rôle",
            "Client",
            "Création",
            "Expiration",
            "Dernière activité",
            "Adresse",
            "Client logiciel",
            "Statut",
          ]}
          rows={result.data.map((session, index) => [
            <span key={`${session.userEmail}-${index}`}>
              <strong>{session.userDisplayName}</strong>
              <br />
              <small>{session.userEmail}</small>
            </span>,
            session.role,
            session.customerReference ?? "Interne",
            formatDateTime(session.createdAt),
            formatDateTime(session.expiresAt),
            session.lastSeenAt
              ? formatDateTime(session.lastSeenAt)
              : "Non disponible",
            session.sourceAddress ?? "Non disponible",
            session.userAgent ?? "Non disponible",
            <SessionStatusBadge
              key={`${session.userEmail}-${index}-status`}
              status={session.status}
            />,
          ])}
        />
      ) : (
        <EmptyState
          description="Aucune session n'est disponible."
          title="Aucune session"
        />
      )}
      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
