import type { CommercialDocumentSummary } from "@kermaria/shared";
import Link from "next/link";

import { EmptyState } from "@/components/EmptyState";
import { StatusBadge } from "@/components/StatusBadge";
import {
  commercialDocumentStatus,
  commercialDocumentType,
  formatCurrencyFromCents,
  formatDate,
} from "@/lib/formatters";

type InvoiceTableProps = {
  invoices: CommercialDocumentSummary[];
};

export function InvoiceTable({ invoices }: InvoiceTableProps) {
  if (invoices.length === 0) {
    return (
      <EmptyState
        description="Aucun document commercial informatif n'est disponible pour cette période."
        title="Aucun document"
      />
    );
  }

  return (
    <div className="table-card">
      <div className="table-heading">
        <div>
          <h2>Historique des documents</h2>
          <p>
            Informations indicatives. Aucun paiement, PDF officiel ou émission
            légale n&apos;est disponible.
          </p>
        </div>
      </div>
      <div className="table-scroll">
        <table className="invoice-table">
          <caption className="sr-only">
            Liste des documents commerciaux informatifs disponibles
          </caption>
          <thead>
            <tr>
              <th>Document</th>
              <th>Type</th>
              <th>Création</th>
              <th>Partage</th>
              <th>Montant</th>
              <th>Statut</th>
              <th>Détail</th>
            </tr>
          </thead>
          <tbody>
            {invoices.map((invoice) => {
              const status = commercialDocumentStatus[invoice.status];

              return (
                <tr key={invoice.id}>
                  <td data-label="Document">
                    <strong>{invoice.internalReference}</strong>
                    <div>{invoice.title}</div>
                  </td>
                  <td data-label="Type">
                    {commercialDocumentType[invoice.documentType]}
                  </td>
                  <td data-label="Création">{formatDate(invoice.createdAt)}</td>
                  <td data-label="Partage">
                    {invoice.sharedAt ? formatDate(invoice.sharedAt) : "Non partagé"}
                  </td>
                  <td data-label="Montant">
                    <strong>{formatCurrencyFromCents(invoice.totalAmountCents)}</strong>
                  </td>
                  <td data-label="Statut">
                    <StatusBadge label={status.label} tone={status.tone} />
                  </td>
                  <td data-label="Détail">
                    <Link
                      className="button button-ghost button-compact"
                      href={`/commercial-documents/${encodeURIComponent(invoice.id)}`}
                    >
                      Consulter
                    </Link>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
