import Link from "next/link";

import { AdminDataTable } from "@/components/AdminDataTable";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import {
  formatCurrencyFromCents,
  formatDateTime,
} from "@/lib/formatters";
import { getAdminCommercialDocuments } from "@/lib/internal-api";

export const metadata = {
  title: "Suivi des paiements - Administration",
};

export const dynamic = "force-dynamic";

const paymentStatuses = ["all", "unpaid", "paid"] as const;
type PaymentStatusFilter = (typeof paymentStatuses)[number];

type PageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default async function AdminPaymentsPage({ searchParams }: PageProps) {
  await requireAdminSession();
  const filters = await searchParams;
  const rawStatus = first(filters.status);
  const status: PaymentStatusFilter =
    rawStatus && (paymentStatuses as readonly string[]).includes(rawStatus)
      ? (rawStatus as PaymentStatusFilter)
      : "all";

  const documentsResult = await getAdminCommercialDocuments();
  const invoices = documentsResult.data
    .filter((doc) => doc.status === "issued" || doc.status === "paid")
    .filter((doc) => {
      if (status === "unpaid") return doc.status === "issued";
      if (status === "paid") return doc.status === "paid";
      return true;
    });

  const totals = documentsResult.data
    .filter((doc) => doc.status === "issued" || doc.status === "paid")
    .reduce(
      (acc, doc) => {
        if (doc.status === "issued") {
          acc.unpaidCount += 1;
          acc.unpaidAmountCents += doc.totalAmountCents;
        } else {
          acc.paidCount += 1;
          acc.paidAmountCents += doc.totalAmountCents;
        }
        return acc;
      },
      { unpaidCount: 0, unpaidAmountCents: 0, paidCount: 0, paidAmountCents: 0 },
    );

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Suivi paiements" tone="info" />}
        description="Factures émises chez BPCE, en attente de règlement ou réglées (PayPal ou marquage manuel)."
        eyebrow="Administration interne"
        title="Paiements"
      />

      <section className="content-panel">
        <dl className="request-details">
          <div>
            <dt>À régler</dt>
            <dd>
              <strong>{formatCurrencyFromCents(totals.unpaidAmountCents)}</strong>
              {" "}({totals.unpaidCount} facture{totals.unpaidCount > 1 ? "s" : ""})
            </dd>
          </div>
          <div>
            <dt>Réglé</dt>
            <dd>
              {formatCurrencyFromCents(totals.paidAmountCents)}
              {" "}({totals.paidCount} facture{totals.paidCount > 1 ? "s" : ""})
            </dd>
          </div>
        </dl>
      </section>

      <form className="admin-filters" method="get">
        <div className="field">
          <label htmlFor="status-filter">Statut paiement</label>
          <select defaultValue={status} id="status-filter" name="status">
            <option value="all">Toutes</option>
            <option value="unpaid">À régler</option>
            <option value="paid">Réglées</option>
          </select>
        </div>
        <button className="button button-secondary" type="submit">
          Appliquer
        </button>
      </form>

      {documentsResult.error ? (
        <ErrorState
          description="Impossible de charger les paiements pour le moment."
          reference={documentsResult.correlationId}
          title="Paiements indisponibles"
        />
      ) : invoices.length === 0 ? (
        <EmptyState
          description={
            status === "unpaid"
              ? "Aucune facture en attente de règlement."
              : status === "paid"
                ? "Aucune facture réglée."
                : "Aucune facture émise pour le moment."
          }
          title="Aucun paiement"
        />
      ) : (
        <AdminDataTable
          caption="Factures émises"
          columns={[
            "Référence",
            "Client",
            "Titre",
            "Statut",
            "Total",
            "Émise le",
            "Action",
          ]}
          rows={invoices.map((doc) => {
            const paid = doc.status === "paid";
            return [
              <code key={`${doc.id}-reference`}>{doc.internalReference}</code>,
              `${doc.customerName} (${doc.customerReference})`,
              doc.title,
              <StatusBadge
                key={`${doc.id}-status`}
                label={paid ? "Réglée" : "À régler"}
                tone={paid ? "success" : "warning"}
              />,
              formatCurrencyFromCents(doc.totalAmountCents),
              formatDateTime(doc.updatedAt),
              <Link
                className="table-action"
                href={`/admin/commercial-documents/${encodeURIComponent(doc.id)}`}
                key={`${doc.id}-detail`}
              >
                {paid ? "Consulter" : "Régler / consulter"}
              </Link>,
            ];
          })}
        />
      )}

      <MockNotice
        correlationId={documentsResult.correlationId}
        source={documentsResult.source}
      />
    </>
  );
}

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
