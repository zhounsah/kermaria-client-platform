import type { ReactNode } from "react";

type AdminDataTableProps = {
  caption: string;
  columns: string[];
  rows: ReactNode[][];
};

export function AdminDataTable({
  caption,
  columns,
  rows,
}: AdminDataTableProps) {
  return (
    <div className="table-card admin-table">
      <div className="table-scroll">
        <table>
          <caption className="sr-only">{caption}</caption>
          <thead>
            <tr>
              {columns.map((column) => (
                <th key={column} scope="col">
                  {column}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row, rowIndex) => (
              <tr key={rowIndex}>
                {row.map((cell, cellIndex) => (
                  <td key={cellIndex}>{cell}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
