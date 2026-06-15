import type { CommercialDocumentLine } from "@kermaria/shared";

import {
  formatCurrencyFromCents,
} from "@/lib/formatters";

type CommercialDocumentLineTableProps = {
  lines: CommercialDocumentLine[];
};

export function CommercialDocumentLineTable({
  lines,
}: CommercialDocumentLineTableProps) {
  return (
    <div className="table-card">
      <div className="table-heading">
        <div>
          <h2>Lignes du document</h2>
          <p>Détail informatif du contenu actuellement partagé ou préparé.</p>
        </div>
      </div>
      <div className="table-scroll">
        <table className="invoice-table">
          <caption className="sr-only">Lignes du document commercial</caption>
          <thead>
            <tr>
              <th>Libellé</th>
              <th>Quantité</th>
              <th>Unité</th>
              <th>Prix unitaire</th>
              <th>TVA</th>
              <th>Total ligne</th>
            </tr>
          </thead>
          <tbody>
            {lines.map((line) => (
              <tr key={line.id}>
                <td data-label="Libellé">
                  <strong>{line.label}</strong>
                  {line.description ? <div>{line.description}</div> : null}
                </td>
                <td data-label="Quantité">{line.quantity}</td>
                <td data-label="Unité">{line.unitLabel}</td>
                <td data-label="Prix unitaire">
                  {formatCurrencyFromCents(line.unitPriceCents)}
                </td>
                <td data-label="TVA">
                  {line.taxRateBasisPoints === null
                    ? "Non précisée"
                    : `${line.taxRateBasisPoints / 100}%`}
                </td>
                <td data-label="Total ligne">
                  <strong>{formatCurrencyFromCents(line.lineTotalCents)}</strong>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
