import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const inputPath =
  "C:/Users/zhounsah/Documents/Dev/outputs/019f7a8d-8252-70c0-b7d4-4b11f33d231e/Kermaria_plan_validation_projet_2026-07-19.xlsx";
const outputDir =
  "C:/Users/zhounsah/Documents/Dev/outputs/019f7a8d-8252-70c0-b7d4-4b11f33d231e";
const qaDir =
  "C:/Users/zhounsah/Documents/Dev/kermaria-client-platform/.codex-tmp/xlsx-update-019f7a8d";
const outputPath = `${outputDir}/Kermaria_plan_validation_projet_2026-07-21.xlsx`;

const executionDate = "2026-07-21";
const operator = "Codex";

const passedScripts = [
  "check:secrets",
  "check:web",
  "lint:web",
  "lint:webportal",
  "typecheck:shared",
  "typecheck:webportal",
  "build:web",
  "build:webportal",
  "build:api",
  "test:api",
  "test:forms",
  "test:auth",
  "test:admin",
  "test:operations",
  "test:ux",
  "test:workflow",
  "test:notifications",
  "test:replies",
  "test:activity",
  "test:commercial",
  "test:managed-content",
  "test:ad-security",
  "test:bpce",
  "test:cart",
  "test:downloads",
  "test:email-live",
  "test:payments",
  "test:payments-stripe",
  "test:signup",
  "test:subscriptions",
  "test:timezone",
  "check:health",
  "validate",
];

const failedEnvScripts = new Map([
  [
    "validate:mariadb",
    "Exécuté le 2026-07-21 dans la session locale : sortie 2, variables SQL_*/demo manquantes. À relancer avec SQL_PROVIDER, SQL_HOST, SQL_PORT, SQL_DATABASE, SQL_USERNAME, SQL_PASSWORD, SERVICE_AUTH_TOKEN et comptes DEMO_* chargés.",
  ],
  [
    "validate:staging",
    "Exécuté le 2026-07-21 dans la session locale : sortie 1, variables staging absentes ou placeholders (NODE_ENV, ASPNETCORE_ENVIRONMENT, DOTNET_ENVIRONMENT, INTERNAL_API_URL, SERVICE_AUTH_TOKEN, SESSION_COOKIE_*, SQL_*, AD_INTEGRATION_MODE, LOG_LEVEL, LOGIN_*). À refaire sur SRV-01/SRV-02 avec env chargé.",
  ],
  [
    "validate:preprod",
    "Exécuté le 2026-07-21 dans la session locale : sortie 1, variables preprod/prod absentes ou placeholders, dont ALLOW_LOCAL_INTERNAL_API_URL, SQL_*, SERVICE_AUTH_TOKEN et SESSION_COOKIE_SECURE. À refaire avec configuration cible.",
  ],
]);

const scriptDetails = new Map([
  [
    "check:secrets",
    "OK le 2026-07-21 : garde-fou secrets sans motif sensible évident.",
  ],
  [
    "check:web",
    "OK le 2026-07-21 : typecheck shared, lint webportal, typecheck webportal et build Next.js réussis.",
  ],
  [
    "build:api",
    "OK le 2026-07-21 : build Release API-INTERNAL réussi.",
  ],
  [
    "test:api",
    "OK le 2026-07-21 après correction : catalogue services mock restauré, downloads filtrés selon les droits actifs, 403 service hors client rétabli.",
  ],
  [
    "validate",
    "OK le 2026-07-21 : pipeline local complet réussi (check:secrets, lint, typechecks, build web/API, smoke API et suites contrat incluses).",
  ],
  [
    "check:health",
    "OK le 2026-07-21 : API-INTERNAL /health, /ready, /health/ready et WEBPORTAL /api/health/live, /api/health/ready réussis.",
  ],
  [
    "test:notifications",
    "OK le 2026-07-21 après correction du libellé attendu « Activité récente ».",
  ],
  [
    "test:downloads",
    "OK le 2026-07-21 : contrat téléchargements V0.37 réussi.",
  ],
  [
    "test:email-live",
    "OK le 2026-07-21 : contrat email-live V0.30 allowlist réussi (9 vérifications).",
  ],
  [
    "test:signup",
    "OK le 2026-07-21 : contrat signup V0.38 réussi (36 vérifications).",
  ],
  [
    "test:subscriptions",
    "OK le 2026-07-21 : contrat souscriptions V0.32 réussi.",
  ],
  [
    "test:payments",
    "OK le 2026-07-21 : contrat canaux de paiement V0.21 réussi.",
  ],
  [
    "test:payments-stripe",
    "OK le 2026-07-21 : contrat Stripe V0.29 réussi.",
  ],
  [
    "test:timezone",
    "OK le 2026-07-21 : contrat horodatages V0.23.2 réussi.",
  ],
]);

const executionRows = [
  [
    "Commande",
    "Statut",
    "Résultat / preuve",
    "Suite à donner",
  ],
  [
    "npm run validate",
    "OK",
    "Pipeline local officiel réussi le 2026-07-21.",
    "Conserver comme baseline locale avant recette live.",
  ],
  [
    "npm run check:web",
    "OK",
    "Typecheck shared, lint webportal, typecheck webportal et build Next.js réussis.",
    "Aucun blocage local.",
  ],
  [
    "npm run test:api",
    "OK",
    "Smoke API-INTERNAL V0.20 réussi après correction du catalogue services mock, des droits downloads et du 403 hors client.",
    "Surveiller les warnings CA1416 .NET, non bloquants ici.",
  ],
  [
    "npm run test:notifications",
    "OK",
    "Contrat notifications V0.12 réussi après restauration du libellé « Activité récente ».",
    "Aucun.",
  ],
  [
    "npm run test:downloads",
    "OK",
    "Contrat téléchargements V0.37 réussi.",
    "Compléter avec recette droits réels en staging.",
  ],
  [
    "npm run test:email-live",
    "OK",
    "Contrat email-live V0.30 allowlist réussi.",
    "Tester SMTP live uniquement avec allowlist explicite.",
  ],
  [
    "npm run test:payments / test:payments-stripe",
    "OK",
    "Contrats canaux de paiement V0.21 et Stripe V0.29 réussis.",
    "Sandbox/live restent à valider manuellement.",
  ],
  [
    "npm run test:signup / test:subscriptions / test:timezone",
    "OK",
    "Contrats signup V0.38, souscriptions V0.32 et horodatages V0.23.2 réussis.",
    "Compléter par parcours navigateur complet.",
  ],
  [
    "npm run check:health",
    "OK",
    "Health API-INTERNAL et WEBPORTAL réussis dans la session locale.",
    "À refaire après chaque redémarrage/déploiement.",
  ],
  [
    "npm audit --production",
    "KO",
    "2 vulnérabilités modérées : postcss <8.5.10 via next. npm propose --force vers next@9.3.3, non appliqué car breaking.",
    "Décider upgrade Next/postcss via une branche dédiée.",
  ],
  [
    "dotnet list ... --vulnerable --include-transitive",
    "OK",
    "Aucun package .NET vulnérable selon les sources NuGet disponibles.",
    "Aucun.",
  ],
  [
    "npm run validate:mariadb",
    "KO",
    "Non exécutable dans cette session : variables SQL_*/demo manquantes.",
    "Relancer avec environnement MariaDB de test chargé.",
  ],
  [
    "npm run validate:staging",
    "KO",
    "Échec attendu en local : variables staging absentes/placeholders.",
    "Relancer sur SRV-01/SRV-02 ou session avec config staging.",
  ],
  [
    "npm run validate:preprod",
    "KO",
    "Échec attendu en local : variables preprod/prod absentes/placeholders.",
    "Relancer avec configuration cible avant bascule.",
  ],
];

function statusForRow(id, command, currentStatus) {
  if (id === "AUTO-010" || command.includes("npm audit")) {
    return {
      status: "KO",
      comment:
        "Exécuté le 2026-07-21 : npm audit signale 2 vulnérabilités modérées postcss via next; dotnet vulnerable OK. Ne pas appliquer npm audit fix --force sans décision d'upgrade Next.",
    };
  }

  for (const [script, comment] of failedEnvScripts.entries()) {
    if (command.includes(script)) {
      return { status: "KO", comment };
    }
  }

  for (const script of passedScripts) {
    if (command.includes(script)) {
      return {
        status: "OK",
        comment:
          scriptDetails.get(script) ??
          `OK le ${executionDate} : commande ${script} réussie dans la validation locale.`,
      };
    }
  }

  return {
    status: currentStatus ?? "A faire",
    comment: null,
  };
}

function computeCoverage(rows) {
  const domains = new Map();
  for (const row of rows.slice(1)) {
    const domain = row[1];
    if (!domain) continue;
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
        "A faire": 0,
      };
    entry.Total += 1;
    if (row[2] === "P0") entry.P0 += 1;
    if (row[2] === "P1") entry.P1 += 1;
    if (row[2] === "P2") entry.P2 += 1;
    if (String(row[3] ?? "").toLowerCase().includes("autom")) {
      entry.Automatise += 1;
    } else {
      entry["Manuel/Revue"] += 1;
    }
    if (row[11] === "OK") entry.OK += 1;
    if (row[11] === "KO") entry.KO += 1;
    if (row[11] === "A faire" || row[11] === null || row[11] === "") {
      entry["A faire"] += 1;
    }
    domains.set(domain, entry);
  }
  return [
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
        entry["A faire"],
      ]),
  ];
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
    text: "A faire",
    format: { fill: "#FEF3C7", font: { color: "#92400E", bold: true } },
  });
}

const input = await FileBlob.load(inputPath);
const workbook = await SpreadsheetFile.importXlsx(input);

const catalogue = workbook.worksheets.getItem("Catalogue tests");
const catalogueRange = catalogue.getRange("A1:O95");
const catalogueValues = catalogueRange.values;

for (let i = 1; i < catalogueValues.length; i += 1) {
  const row = catalogueValues[i];
  const id = row[0] ?? "";
  const command = row[9] ?? "";
  const currentStatus = row[11] ?? "A faire";
  const update = statusForRow(String(id), String(command), currentStatus);
  if (update.comment) {
    row[11] = update.status;
    row[12] = update.comment;
    row[13] = executionDate;
    row[14] = operator;
  }
}
catalogueRange.values = catalogueValues;
styleStatusRange(catalogue.getRange("L2:L95"));
catalogue.getRange("M2:M95").format.wrapText = true;
catalogue.getRange("M2:M95").format.columnWidth = 70;
catalogue.getRange("N2:N95").format.numberFormat = "yyyy-mm-dd";

const synthesis = workbook.worksheets.getItem("Synthese");
synthesis.getRange("A1:C2").values = [
  ["Plan de validation Kermaria", null, null],
  [
    "Classeur mis à jour le 2026-07-21 après exécution complète des tests locaux et classification des validations d'environnement.",
    null,
    null,
  ],
];
synthesis.getRange("A4:C19").values = [
  ["Indicateur", "Valeur", "Commentaire"],
  [
    "Tests catalogues",
    catalogueValues.length - 1,
    "Nombre de lignes dans l'onglet Catalogue tests",
  ],
  [
    "P0",
    catalogueValues.slice(1).filter((row) => row[2] === "P0").length,
    "Tests bloquants avant release/staging sign-off",
  ],
  [
    "P1",
    catalogueValues.slice(1).filter((row) => row[2] === "P1").length,
    "Tests importants avant bascule ou recette complète",
  ],
  [
    "P2",
    catalogueValues.slice(1).filter((row) => row[2] === "P2").length,
    "Tests de confort ou couverture complémentaire",
  ],
  [
    "OK",
    catalogueValues.slice(1).filter((row) => row[11] === "OK").length,
    "Validé par commande/revue consignée",
  ],
  [
    "KO",
    catalogueValues.slice(1).filter((row) => row[11] === "KO").length,
    "Échec réel ou validation environnement non exécutable dans la session locale",
  ],
  [
    "A faire",
    catalogueValues
      .slice(1)
      .filter((row) => !row[11] || row[11] === "A faire").length,
    "Backlog de validation manuelle/staging/sandbox",
  ],
  [null, null, null],
  ["Regle d'utilisation", "Detail", "Pourquoi"],
  [
    "Ne pas cocher sans preuve",
    "Renseigner Commande/Preuve, Commentaire, Date et Operateur.",
    "Le fichier doit servir de suivi de recette.",
  ],
  [
    "Local vert",
    "npm run validate et suites additionnelles locales sont passés le 2026-07-21.",
    "Baseline technique locale solide.",
  ],
  [
    "Distinguer environnement",
    "validate:mariadb/staging/preprod restent KO faute de variables réelles chargées ici.",
    "Ces tests doivent être refaits sur l'environnement cible.",
  ],
  [
    "Dépendances npm",
    "npm audit remonte 2 vulnérabilités modérées postcss via next; fix --force non appliqué.",
    "À traiter via upgrade maîtrisé.",
  ],
  [
    "Sauvegarder avant intrusif",
    "Backup MariaDB avant migrations, restauration ou tests destructifs.",
    "Rollback et PRA.",
  ],
  [
    "Respecter l'architecture",
    "WEBPORTAL ne contacte jamais MariaDB/AD/BPCE/SMTP directement.",
    "Frontière de sécurité principale.",
  ],
];
synthesis.getRange("A21:C24").values = [
  ["Correctifs appliqués avant passage au vert", null, null],
  [
    "Frontend",
    "Apostrophes JSX, hook HeaderCartDrawer, libellé « Activité récente ».",
    "lint:web et test:notifications verts.",
  ],
  [
    "API",
    "Catalogue services mock restauré, downloads filtrés par droits actifs, 403 service hors client.",
    "test:api vert.",
  ],
  [
    "Limites restantes",
    "validate:mariadb/staging/preprod et npm audit nécessitent décision/env dédiés.",
    "Ne pas les confondre avec une régression locale applicative.",
  ],
];
synthesis.getRange("A21:C21").format = {
  fill: "#1F4E78",
  font: { color: "#FFFFFF", bold: true },
};
synthesis.getRange("A22:C24").format = {
  borders: { preset: "inside", style: "thin", color: "#E5E7EB" },
  wrapText: true,
};
synthesis.getRange("A1:C24").format.wrapText = true;
synthesis.getRange("A1:A24").format.columnWidth = 28;
synthesis.getRange("B1:B24").format.columnWidth = 60;
synthesis.getRange("C1:C24").format.columnWidth = 68;
synthesis.getRange("A22:C24").format.rowHeight = 34;

const coverage = workbook.worksheets.getItem("Couverture");
const coverageValues = computeCoverage(catalogueValues);
coverage.getRange("A1:J40").clear({ applyTo: "contents" });
coverage.getRangeByIndexes(0, 0, coverageValues.length, 10).values =
  coverageValues;
styleStatusRange(coverage.getRange("H2:J40"));

const execution = workbook.worksheets.getOrAdd("Execution 2026-07-21");
execution.getUsedRange()?.clear({ applyTo: "all" });
execution.showGridLines = false;
execution.getRangeByIndexes(0, 0, executionRows.length, 4).values =
  executionRows;
execution.getRange("A1:D1").format = {
  fill: "#1F4E78",
  font: { color: "#FFFFFF", bold: true },
  wrapText: true,
};
execution.getRange(`A2:D${executionRows.length}`).format = {
  borders: { preset: "inside", style: "thin", color: "#E5E7EB" },
  wrapText: true,
};
execution.getRange(`B2:B${executionRows.length}`).format = {
  font: { bold: true },
};
styleStatusRange(execution.getRange(`B2:B${executionRows.length}`));
execution.freezePanes.freezeRows(1);
execution.getRange(`A1:A${executionRows.length}`).format.columnWidth = 34;
execution.getRange(`B1:B${executionRows.length}`).format.columnWidth = 14;
execution.getRange(`C1:C${executionRows.length}`).format.columnWidth = 76;
execution.getRange(`D1:D${executionRows.length}`).format.columnWidth = 55;
execution.getRange(`A1:D${executionRows.length}`).format.autofitRows();

for (const sheetName of [
  "Synthese",
  "Catalogue tests",
  "Couverture",
  "Commandes auto",
  "Prerequis env",
  "Parcours critiques",
  "Execution 2026-07-21",
]) {
  const sheet = workbook.worksheets.getItem(sheetName);
  sheet.showGridLines = false;
  try {
    sheet.freezePanes.freezeRows(1);
  } catch {
    // Imported workbooks may already have panes configured; keep going.
  }
}

const formulaErrors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "final formula error scan",
  maxChars: 6000,
});
await fs.mkdir(qaDir, { recursive: true });
await fs.writeFile(`${qaDir}/formula-errors.ndjson`, formulaErrors.ndjson, "utf8");

const finalSummary = await workbook.inspect({
  kind: "table",
  sheetId: "Synthese",
  range: "A1:C24",
  include: "values,formulas",
  tableMaxRows: 30,
  tableMaxCols: 3,
  maxChars: 12000,
});
await fs.writeFile(`${qaDir}/final-summary.ndjson`, finalSummary.ndjson, "utf8");

const finalExecution = await workbook.inspect({
  kind: "table",
  sheetId: "Execution 2026-07-21",
  range: `A1:D${executionRows.length}`,
  include: "values,formulas",
  tableMaxRows: executionRows.length,
  tableMaxCols: 4,
  maxChars: 16000,
});
await fs.writeFile(`${qaDir}/final-execution.ndjson`, finalExecution.ndjson, "utf8");

for (const sheetName of [
  "Synthese",
  "Catalogue tests",
  "Couverture",
  "Commandes auto",
  "Prerequis env",
  "Parcours critiques",
  "Execution 2026-07-21",
]) {
  const preview = await workbook.render({
    sheetName,
    autoCrop: "all",
    scale: 1,
    format: "png",
  });
  await fs.writeFile(
    `${qaDir}/final-preview-${sheetName.replaceAll(" ", "-")}.png`,
    new Uint8Array(await preview.arrayBuffer()),
  );
}

await fs.mkdir(outputDir, { recursive: true });
const xlsx = await SpreadsheetFile.exportXlsx(workbook);
await xlsx.save(outputPath);
console.log(outputPath);
console.log(finalSummary.ndjson);
console.log(finalExecution.ndjson.slice(0, 4000));
console.log(formulaErrors.ndjson || "no-formula-errors");
