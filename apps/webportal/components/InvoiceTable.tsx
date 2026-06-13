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
          <p>Les téléchargements sont désactivés dans cette V0.8.</p>
        </div>
      </div>
      <div className="table-scroll">
        <table>
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
                  <td>
                    <strong>{invoice.number}</strong>
                  </td>
                  <td>{invoice.period}</td>
                  <td>{formatDate(invoice.issuedAt)}</td>
                  <td>{formatDate(invoice.dueAt)}</td>
                  <td>
                    <strong>{formatCurrency(invoice.totalAmount)}</strong>
                  </td>
                  <td>
                    <StatusBadge label={status.label} tone={status.tone} />
                  </td>
                  <td>
                    <button
                      className="button button-ghost button-compact"
                      disabled
                      title="Téléchargement indisponible dans la démonstration"
                      type="button"
                    >
                      Télécharger
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
