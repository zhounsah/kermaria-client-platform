import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const inputPath =
  "C:/Users/zhounsah/Documents/Dev/outputs/019f7a8d-8252-70c0-b7d4-4b11f33d231e/Kermaria_plan_validation_projet_2026-07-19.xlsx";
const outputDir =
  "C:/Users/zhounsah/Documents/Dev/kermaria-client-platform/.codex-tmp/xlsx-update-019f7a8d";

const input = await FileBlob.load(inputPath);
const workbook = await SpreadsheetFile.importXlsx(input);
const catalogue = await workbook.inspect({
  kind: "table",
  sheetId: "Catalogue tests",
  range: "A1:O95",
  include: "values,formulas",
  tableMaxRows: 100,
  tableMaxCols: 15,
  tableMaxCellChars: 300,
  maxChars: 80000,
});
const sheets = await workbook.inspect({
  kind: "sheet",
  include: "id,name",
  maxChars: 8000,
});
await fs.mkdir(outputDir, { recursive: true });
await fs.writeFile(`${outputDir}/catalogue.ndjson`, catalogue.ndjson, "utf8");
await fs.writeFile(`${outputDir}/sheets.ndjson`, sheets.ndjson, "utf8");
console.log(sheets.ndjson);
console.log(catalogue.ndjson.slice(0, 12000));
