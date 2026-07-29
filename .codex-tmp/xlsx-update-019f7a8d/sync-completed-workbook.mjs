import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const inputPath =
  "C:/Users/zhounsah/Documents/Dev/outputs/019f7a8d-8252-70c0-b7d4-4b11f33d231e/Kermaria_plan_validation_projet_2026-07-21.xlsx";
const outputDir =
  "C:/Users/zhounsah/Documents/Dev/outputs/019f7a8d-8252-70c0-b7d4-4b11f33d231e";
const qaDir =
  "C:/Users/zhounsah/Documents/Dev/kermaria-client-platform/.codex-tmp/xlsx-update-019f7a8d";
const outputPath = `${outputDir}/Kermaria_plan_validation_projet_2026-07-21_revu.xlsx`;

const input = await FileBlob.load(inputPath);
const workbook = await SpreadsheetFile.importXlsx(input);

const catalogue = workbook.worksheets.getItem("Catalogue tests");
const catalogueValues = catalogue.getRange("A1:O95").values;
const headers = catalogueValues[0];
const rows = catalogueValues.slice(1).filter((row) => row[0]);
const idx = Object.fromEntries(headers.map((header, index) => [header, index]));
const statuses = ["OK", "KO", "En cours", "A faire"];

function countBy(predicate) {
  return rows.filter(predicate).length;
}

function rowStatus(row) {
  return row[idx.Statut] || "A faire";
}

function styleHeader(range, fill = "#1F4E78") {
  range.format = {
    fill,
    font: { color: "#FFFFFF", bold: true },
    wrapText: true,
  };
}

function styleStatusRange(range) {
  range.conditionalFormats.deleteAll();
  range.conditionalFormats.add("containsText", {
    text: "OK",
    format: { fill: "#DCFCE7", font: { color: "#166534", bold: true } },
  });
  range.conditionalFormats.add("containsText", {
    text: "KO",
    format: { fill: "#FEE2E2", font: { color: "#991B1B", bold: true } },
  });
  range.conditionalFormats.add("containsText", {
    text: "En cours",
    format: { fill: "#DBEAFE", font: { color: "#1D4ED8", bold: true } },
  });
  range.conditionalFormats.add("containsText", {
    text: "A faire",
    format: { fill: "#FEF3C7", font: { color: "#92400E", bold: true } },
  });
}

const statusCounts = Object.fromEntries(
  statuses.map((status) => [status, countBy((row) => rowStatus(row) === status)]),
);
const priorityCounts = {
  P0: countBy((row) => row[idx.Priorite] === "P0"),
  P1: countBy((row) => row[idx.Priorite] === "P1"),
  P2: countBy((row) => row[idx.Priorite] === "P2"),
};

const synthesis = workbook.worksheets.getItem("Synthese");
synthesis.getRange("A1:C2").values = [
  ["Plan de validation Kermaria", null, null],
  [
    "Classeur revu le 2026-07-21 après complétion utilisateur : synthèse et couverture réalignées sur le Catalogue tests.",
    null,
    null,
  ],
];
synthesis.getRange("A4:C20").values = [
  ["Indicateur", "Valeur", "Commentaire"],
  ["Tests catalogues", rows.length, "Nombre de lignes dans l'onglet Catalogue tests"],
  ["P0", priorityCounts.P0, "Tests bloquants avant release/staging sign-off"],
  ["P1", priorityCounts.P1, "Tests importants avant bascule ou recette complète"],
  ["P2", priorityCounts.P2, "Tests de confort ou couverture complémentaire"],
  ["OK", statusCounts.OK, "Validé dans le catalogue"],
  ["KO", statusCounts.KO, "Échec réel ou validation environnement non exécutable"],
  ["En cours", statusCounts["En cours"], "Validation démarrée mais pas clôturée"],
  ["A faire", statusCounts["A faire"], "Backlog de validation restant"],
  [null, null, null],
  ["Regle d'utilisation", "Detail", "Pourquoi"],
  [
    "Ne pas cocher sans preuve",
    "Pour chaque OK/KO, garder Commentaire, Date et Operateur.",
    "Évite les validations impossibles à rejouer.",
  ],
  [
    "Priorité de clôture",
    "Traiter d'abord les P0 KO, puis P0 En cours, puis P0 A faire.",
    "C'est le chemin le plus court vers une recette exploitable.",
  ],
  [
    "Distinguer environnement",
    "validate:mariadb/staging/preprod restent liés à des environnements/configurations cibles.",
    "Ils ne doivent pas être confondus avec le pipeline local.",
  ],
  [
    "Dépendances npm",
    "npm audit signale 2 vulnérabilités modérées postcss via next.",
    "À traiter via upgrade maîtrisé, pas via --force aveugle.",
  ],
  [
    "Preuves manquantes",
    "Voir l'onglet Controle qualite.",
    "Certains OK/KO ont un statut mais pas de commentaire/date/opérateur.",
  ],
  [
    "Baseline locale",
    "Les tests automatisés locaux restent largement verts.",
    "Les derniers risques se concentrent sur staging/live/manuel.",
  ],
];
styleHeader(synthesis.getRange("A1:C1"), "#0F3A40");
styleHeader(synthesis.getRange("A4:C4"), "#1F7A83");
styleHeader(synthesis.getRange("A14:C14"), "#1F7A83");
synthesis.getRange("A1:C24").format.wrapText = true;
synthesis.getRange("A1:A24").format.columnWidth = 28;
synthesis.getRange("B1:B24").format.columnWidth = 58;
synthesis.getRange("C1:C24").format.columnWidth = 70;
synthesis.getRange("B5:B12").format.font = { bold: true };

const domains = new Map();
for (const row of rows) {
  const domain = row[idx.Domaine] || "(sans domaine)";
  const entry =
    domains.get(domain) ??
    {
      Domaine: domain,
      Total: 0,
      P0: 0,
      P1: 0,
      P2: 0,
      Automatise: 0,
      "Manuel/Revue": 0,
      OK: 0,
      KO: 0,
      "En cours": 0,
      "A faire": 0,
    };
  entry.Total += 1;
  if (row[idx.Priorite] === "P0") entry.P0 += 1;
  if (row[idx.Priorite] === "P1") entry.P1 += 1;
  if (row[idx.Priorite] === "P2") entry.P2 += 1;
  if (String(row[idx.Type] ?? "").toLowerCase().includes("autom")) {
    entry.Automatise += 1;
  } else {
    entry["Manuel/Revue"] += 1;
  }
  entry[rowStatus(row)] += 1;
  domains.set(domain, entry);
}

const coverageValues = [
  [
    "Domaine",
    "Total",
    "P0",
    "P1",
    "P2",
    "Automatise",
    "Manuel/Revue",
    "OK",
    "KO",
    "En cours",
    "A faire",
  ],
  ...Array.from(domains.values())
    .sort((a, b) => a.Domaine.localeCompare(b.Domaine, "fr"))
    .map((entry) => [
      entry.Domaine,
      entry.Total,
      entry.P0,
      entry.P1,
      entry.P2,
      entry.Automatise,
      entry["Manuel/Revue"],
      entry.OK,
      entry.KO,
      entry["En cours"],
      entry["A faire"],
    ]),
];
const coverage = workbook.worksheets.getItem("Couverture");
coverage.getRange("A1:K45").clear({ applyTo: "contents" });
coverage.getRangeByIndexes(0, 0, coverageValues.length, 11).values =
  coverageValues;
styleHeader(coverage.getRange("A1:K1"), "#1F7A83");
styleStatusRange(coverage.getRange("H2:K40"));
coverage.getRange("A1:K40").format.borders = {
  preset: "inside",
  style: "thin",
  color: "#E5E7EB",
};
coverage.getRange("A1:K40").format.columnWidth = 13;
coverage.getRange("A1:A40").format.columnWidth = 24;
coverage.freezePanes.freezeRows(1);

const koRows = rows
  .filter((row) => rowStatus(row) === "KO")
  .map((row) => [
    "KO",
    row[idx.ID],
    row[idx.Domaine],
    row[idx.Priorite],
    row[idx.Environnement],
    row[idx["Commande / preuve"]],
    row[idx.Commentaire] || "",
  ]);
const p0OpenRows = rows
  .filter((row) => row[idx.Priorite] === "P0" && rowStatus(row) !== "OK")
  .map((row) => [
    rowStatus(row),
    row[idx.ID],
    row[idx.Domaine],
    row[idx.Priorite],
    row[idx.Environnement],
    row[idx["Commande / preuve"]],
    row[idx.Commentaire] || "",
  ]);
const missingProofRows = rows
  .filter((row) => {
    const status = rowStatus(row);
    return (
      (status === "OK" || status === "KO") &&
      (!row[idx.Commentaire] || !row[idx.Date] || !row[idx.Operateur])
    );
  })
  .map((row) => [
    rowStatus(row),
    row[idx.ID],
    row[idx.Domaine],
    row[idx.Priorite],
    row[idx.Environnement],
    [
      !row[idx.Commentaire] ? "Commentaire" : null,
      !row[idx.Date] ? "Date" : null,
      !row[idx.Operateur] ? "Operateur" : null,
    ]
      .filter(Boolean)
      .join(", "),
    row[idx.Commentaire] || "",
  ]);

const quality = workbook.worksheets.getOrAdd("Controle qualite");
quality.getUsedRange()?.clear({ applyTo: "all" });
quality.showGridLines = false;
const qualityRows = [
  ["Type", "ID", "Domaine", "Priorite", "Environnement", "Point de controle", "Commentaire"],
  ...koRows,
  ...p0OpenRows.map((row) => ["P0 ouvert", ...row.slice(1)]),
  ...missingProofRows.map((row) => ["Preuve incomplete", ...row.slice(1)]),
];
quality.getRangeByIndexes(0, 0, qualityRows.length, 7).values = qualityRows;
styleHeader(quality.getRange("A1:G1"), "#1F4E78");
styleStatusRange(quality.getRange(`A2:A${qualityRows.length}`));
quality.getRange(`A1:G${qualityRows.length}`).format = {
  borders: { preset: "inside", style: "thin", color: "#E5E7EB" },
  wrapText: true,
};
quality.getRange(`A1:A${qualityRows.length}`).format.columnWidth = 18;
quality.getRange(`B1:B${qualityRows.length}`).format.columnWidth = 14;
quality.getRange(`C1:C${qualityRows.length}`).format.columnWidth = 22;
quality.getRange(`D1:D${qualityRows.length}`).format.columnWidth = 12;
quality.getRange(`E1:E${qualityRows.length}`).format.columnWidth = 26;
quality.getRange(`F1:F${qualityRows.length}`).format.columnWidth = 56;
quality.getRange(`G1:G${qualityRows.length}`).format.columnWidth = 70;
quality.freezePanes.freezeRows(1);

styleStatusRange(catalogue.getRange("L2:L95"));

const formulaErrors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "final formula error scan",
  maxChars: 6000,
});
await fs.mkdir(qaDir, { recursive: true });
await fs.writeFile(`${qaDir}/sync-formula-errors.ndjson`, formulaErrors.ndjson, "utf8");

for (const sheetName of [
  "Synthese",
  "Couverture",
  "Controle qualite",
  "Catalogue tests",
]) {
  const preview = await workbook.render({
    sheetName,
    autoCrop: "all",
    scale: 1,
    format: "png",
  });
  await fs.writeFile(
    `${qaDir}/sync-preview-${sheetName.replaceAll(" ", "-")}.png`,
    new Uint8Array(await preview.arrayBuffer()),
  );
}

await fs.mkdir(outputDir, { recursive: true });
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);

const finalSummary = await workbook.inspect({
  kind: "table",
  sheetId: "Synthese",
  range: "A4:C20",
  include: "values,formulas",
  tableMaxRows: 20,
  tableMaxCols: 3,
  maxChars: 12000,
});
console.log(outputPath);
console.log(finalSummary.ndjson);
console.log(formulaErrors.ndjson || "no-formula-errors");
