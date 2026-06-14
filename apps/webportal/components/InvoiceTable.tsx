import type { InvoiceSummary } from "@kermaria/shared";

import { EmptyState } from "@/components/EmptyState";
import { StatusBadge } from "@/components/StatusBadge";
import {
  formatCurrency,
  formatDate,
  invoiceStatus,
} from "@/lib/formatters";

type InvoiceTableProps = {
  invoices: InvoiceSummary[];
};

export function InvoiceTable({ invoices }: InvoiceTableProps) {
  if (invoices.length === 0) {
    return (
      <EmptyState
        description="Aucun document fictif n'est disponible pour cette période."
        title="Aucune facture"
      />
    );
  }

  return (
    <div className="table-card">
      <div className="table-heading">
        <div>
          <h2>Historique des factures</h2>
          <p>
            Informations indicatives. Les documents officiels et téléchargements
            ne sont pas activés.
          </p>
        </div>
      </div>
      <div className="table-scroll">
        <table className="invoice-table">
          <caption className="sr-only">
            Liste des informations de facturation disponibles
          </caption>
          <thead>
            <tr>
              <th>Facture</th>
              <th>Période</th>
              <th>Émission</th>
              <th>Échéance</th>
              <th>Montant</th>
              <th>Statut</th>
              <th>Document</th>
            </tr>
          </thead>
          <tbody>
            {invoices.map((invoice) => {
              const status = invoiceStatus[invoice.status];

              return (
                <tr key={invoice.id}>
                  <td data-label="Facture">
                    <strong>{invoice.number}</strong>
                  </td>
                  <td data-label="Période">{invoice.period}</td>
                  <td data-label="Émission">{formatDate(invoice.issuedAt)}</td>
                  <td data-label="Échéance">{formatDate(invoice.dueAt)}</td>
                  <td data-label="Montant">
                    <strong>{formatCurrency(invoice.totalAmount)}</strong>
                  </td>
                  <td data-label="Statut">
                    <StatusBadge label={status.label} tone={status.tone} />
                  </td>
                  <td data-label="Document">
                    <button
                      className="button button-ghost button-compact"
                      disabled
                      title="Téléchargement non disponible dans cette version"
                      type="button"
                    >
                      Indisponible
                    </button>
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
