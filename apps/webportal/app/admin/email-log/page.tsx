import Link from "next/link";

import { AdminDataTable } from "@/components/AdminDataTable";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import { getAdminEmailLog } from "@/lib/internal-api";

export const metadata = {
  title: "Journal d'envoi e-mail - Administration",
};

export const dynamic = "force-dynamic";

const templateLabels: Record<string, string> = {
  invoice_issued: "Facture émise",
  payment_reminder: "Relance paiement",
  payment_confirmed: "Confirmation paiement",
};

const successStatuses = new Set(["sent", "mock_sent"]);

export default async function AdminEmailLogPage() {
  await requireAdminSession();
  const result = await getAdminEmailLog(200);

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Suivi e-mail" tone="info" />}
        description="Historique des e-mails transactionnels (factures, relances, confirmations). Le canal vit en mode disabled/mock/live selon la configuration."
        eyebrow="Administration interne"
        title="Journal d'envoi e-mail"
      />

      {result.error ? (
        <ErrorState
          description="Impossible de charger le journal pour le moment."
          reference={result.correlationId}
          title="Journal indisponible"
        />
      ) : result.data.length === 0 ? (
        <EmptyState
          description="Aucun e-mail n'a encore été enregistré."
          title="Journal vide"
        />
      ) : (
        <AdminDataTable
          caption="Journal d'envoi e-mail"
          columns={[
            "Date",
            "Modèle",
            "Destinataire",
            "Sujet",
            "Statut",
            "Document",
            "Corrélation",
          ]}
          rows={result.data.map((entry) => {
            const succeeded = successStatuses.has(entry.status);
            return [
              formatDateTime(entry.createdAt),
              templateLabels[entry.template] ?? entry.template,
              entry.recipient || "—",
              entry.subject,
              <span key={`${entry.id}-status`} title={entry.errorMessage ?? undefined}>
                <StatusBadge
                  label={entry.status}
                  tone={succeeded ? "success" : "warning"}
                />
              </span>,
              entry.relatedDocumentId ? (
                <Link
                  className="table-action"
                  href={`/admin/commercial-documents/${encodeURIComponent(entry.relatedDocumentId)}`}
                  key={`${entry.id}-doc`}
                >
                  Voir
                </Link>
              ) : (
                "—"
              ),
              <code key={`${entry.id}-corr`}>{entry.correlationId}</code>,
            ];
          })}
        />
      )}

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
