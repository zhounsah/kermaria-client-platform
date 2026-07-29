import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const inputPath =
  "C:/Users/zhounsah/Documents/Dev/outputs/019f7a8d-8252-70c0-b7d4-4b11f33d231e/Kermaria_plan_validation_projet_2026-07-19.xlsx";
const outputDir =
  "C:/Users/zhounsah/Documents/Dev/kermaria-client-platform/.codex-tmp/xlsx-update-019f7a8d";

const input = await FileBlob.load(inputPath);
const workbook = await SpreadsheetFile.importXlsx(input);

const summary = await workbook.inspect({
  kind: "workbook,sheet,table",
  maxChars: 12000,
  tableMaxRows: 8,
  tableMaxCols: 10,
  tableMaxCellChars: 120,
});
console.log(summary.ndjson);

await fs.mkdir(outputDir, { recursive: true });
const sheetInfo = await workbook.inspect({
  kind: "sheet",
  include: "id,name",
  maxChars: 4000,
});
await fs.writeFile(`${outputDir}/sheet-info.ndjson`, sheetInfo.ndjson, "utf8");

for (const sheetName of [
  "Synthese",
  "Campagnes",
  "Tests detailles",
  "Execution",
]) {
  try {
    const preview = await workbook.render({
      sheetName,
      autoCrop: "all",
      scale: 1,
      format: "png",
    });
    await fs.writeFile(
      `${outputDir}/preview-${sheetName.replaceAll(" ", "-")}.png`,
      new Uint8Array(await preview.arrayBuffer()),
    );
  } catch (error) {
    console.error(`render-skip:${sheetName}:${error.message}`);
  }
}
