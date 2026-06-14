import { ErrorState } from "@/components/ErrorState";
import { NotificationCenter } from "@/components/NotificationCenter";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { StatusBadge } from "@/components/StatusBadge";
import { requireClientSession } from "@/lib/auth";
import { getNotifications } from "@/lib/internal-api";

export const metadata = { title: "Notifications" };
export const dynamic = "force-dynamic";

export default async function NotificationsPage() {
  await requireClientSession();
  const result = await getNotifications();
  const unreadCount = result.data.filter((item) => !item.isRead).length;

  return (
    <>
      <PageHeader
        action={
          <StatusBadge
            label={`${unreadCount} non lue${unreadCount > 1 ? "s" : ""}`}
            tone={unreadCount > 0 ? "warning" : "neutral"}
          />
        }
        description="Retrouvez les changements de statut et messages publiés sur vos demandes."
        eyebrow="Centre d'activité"
        title="Notifications"
      />

      <SectionCard ariaLabel="Notifications du compte">
        {result.error ? (
          <ErrorState
            description="Impossible de charger vos notifications pour le moment."
            reference={result.correlationId}
            title="Notifications indisponibles"
          />
        ) : (
          <NotificationCenter initialNotifications={result.data} />
        )}
      </SectionCard>
    </>
  );
}
