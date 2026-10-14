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
              <th>Prix unitaire HT</th>
              <th>TVA</th>
              <th>Total ligne HT</th>
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
                <td data-label="Prix unitaire HT">
                  {formatCurrencyFromCents(line.unitPriceCents)}
                </td>
                <td data-label="TVA">
                  {line.fiscalRegime !== "standard"
                    ? line.fiscalMention
                    : line.taxRateBasisPoints === null
                      ? "À vérifier"
                      : `${line.taxRateBasisPoints / 100}%`}
                </td>
                <td data-label="Total ligne HT">
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
