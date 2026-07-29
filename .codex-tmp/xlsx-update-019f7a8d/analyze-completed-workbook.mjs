import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const workbookPath =
  "C:/Users/zhounsah/Documents/Dev/outputs/019f7a8d-8252-70c0-b7d4-4b11f33d231e/Kermaria_plan_validation_projet_2026-07-21.xlsx";

const input = await FileBlob.load(workbookPath);
const workbook = await SpreadsheetFile.importXlsx(input);

const sheetInfo = await workbook.inspect({
  kind: "sheet",
  include: "id,name",
  maxChars: 6000,
});

const catalogue = workbook.worksheets.getItem("Catalogue tests");
const catalogueValues = catalogue.getRange("A1:O95").values;
const headers = catalogueValues[0];
const rows = catalogueValues.slice(1).filter((row) => row[0]);
const idx = Object.fromEntries(headers.map((header, index) => [header, index]));

const statusCounts = new Map();
const priorityCounts = new Map();
const priorityStatusCounts = new Map();
const domainStatusCounts = new Map();
const missingEvidence = [];
const koRows = [];
const todoP0 = [];
const inProgressRows = [];
const unknownStatusRows = [];
const acceptableStatuses = new Set(["OK", "KO", "A faire", "En cours"]);

for (const row of rows) {
  const id = row[idx.ID];
  const domain = row[idx.Domaine] ?? "";
  const priority = row[idx.Priorite] ?? "";
  const status = row[idx.Statut] || "A faire";
  const comment = row[idx.Commentaire] ?? "";
  const date = row[idx.Date] ?? "";
  const operator = row[idx.Operateur] ?? "";

  statusCounts.set(status, (statusCounts.get(status) ?? 0) + 1);
  priorityCounts.set(priority, (priorityCounts.get(priority) ?? 0) + 1);
  const priorityStatusKey = `${priority}|${status}`;
  priorityStatusCounts.set(
    priorityStatusKey,
    (priorityStatusCounts.get(priorityStatusKey) ?? 0) + 1,
  );
  const domainStatusKey = `${domain}|${status}`;
  domainStatusCounts.set(
    domainStatusKey,
    (domainStatusCounts.get(domainStatusKey) ?? 0) + 1,
  );

  if (!acceptableStatuses.has(status)) {
    unknownStatusRows.push({ id, domain, priority, status });
  }
  if ((status === "OK" || status === "KO") && (!comment || !date || !operator)) {
    missingEvidence.push({
      id,
      domain,
      priority,
      status,
      missing: [
        !comment ? "Commentaire" : null,
        !date ? "Date" : null,
        !operator ? "Operateur" : null,
      ].filter(Boolean),
    });
  }
  if (status === "KO") {
    koRows.push({
      id,
      domain,
      priority,
      type: row[idx.Type],
      environment: row[idx.Environnement],
      command: row[idx["Commande / preuve"]],
      comment,
    });
  }
  if (priority === "P0" && (status === "A faire" || !status)) {
    todoP0.push({
      id,
      domain,
      type: row[idx.Type],
      environment: row[idx.Environnement],
      objective: row[idx.Objectif],
      command: row[idx["Commande / preuve"]],
    });
  }
  if (status === "En cours") {
    inProgressRows.push({
      id,
      domain,
      priority,
      command: row[idx["Commande / preuve"]],
      comment,
    });
  }
}

function asObject(map) {
  return Object.fromEntries(
    Array.from(map.entries()).sort(([a], [b]) => a.localeCompare(b, "fr")),
  );
}

const synthesis = await workbook.inspect({
  kind: "table",
  sheetId: "Synthese",
  range: "A4:C24",
  include: "values,formulas",
  tableMaxRows: 25,
  tableMaxCols: 3,
  maxChars: 12000,
});

const coverage = await workbook.inspect({
  kind: "table",
  sheetId: "Couverture",
  range: "A1:J40",
  include: "values,formulas",
  tableMaxRows: 45,
  tableMaxCols: 10,
  maxChars: 20000,
});

const formulaErrors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "formula error scan",
  maxChars: 8000,
});

console.log(
  JSON.stringify(
    {
      workbookPath,
      sheetsNdjson: sheetInfo.ndjson,
      rows: rows.length,
      statusCounts: asObject(statusCounts),
      priorityCounts: asObject(priorityCounts),
      priorityStatusCounts: asObject(priorityStatusCounts),
      koRows,
      todoP0,
      inProgressRows,
      missingEvidence,
      unknownStatusRows,
      formulaErrors: formulaErrors.ndjson,
      synthesis: synthesis.ndjson,
      coverage: coverage.ndjson,
    },
    null,
    2,
  ),
);
