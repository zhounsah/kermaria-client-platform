import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function readRepo(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

async function readRepoAbs(path) {
  return readFile(new URL(`../../../${path}`, import.meta.url), "utf8");
}

const formatters = await readRepo("lib/formatters.ts");
assert.match(
  formatters,
  /export const DISPLAY_TIME_ZONE = "Europe\/Paris";/,
  "formatters.ts doit exporter DISPLAY_TIME_ZONE = Europe/Paris.",
);
assert.match(
  formatters,
  /formatDate[\s\S]*?timeZone: DISPLAY_TIME_ZONE/,
  "formatDate doit forcer timeZone: DISPLAY_TIME_ZONE.",
);
assert.match(
  formatters,
  /formatDateTime[\s\S]*?timeZone: DISPLAY_TIME_ZONE/,
  "formatDateTime doit forcer timeZone: DISPLAY_TIME_ZONE.",
);

const tzHelper = await readRepoAbs(
  "apps/api-internal/Infrastructure/KermariaTimeZone.cs",
);
assert.match(tzHelper, /IanaId = "Europe\/Paris"/);
assert.match(tzHelper, /WindowsId = "Romance Standard Time"/);
assert.match(tzHelper, /TimeZoneInfo\.ConvertTimeFromUtc/);

const invoiceService = await readRepoAbs(
  "apps/api-internal/Services/InvoiceIssuingService.cs",
);
assert.match(
  invoiceService,
  /KermariaTimeZone\.Now\.ToString\("yyyy-MM-dd"\)/,
  "issueDate BPCE doit être en heure Paris (date fiscale locale).",
);
assert.doesNotMatch(
  invoiceService,
  /var issueDate = DateTime\.UtcNow\.ToString/,
  "Aucun issueDate en UTC ne doit subsister dans InvoiceIssuingService.",
);

const fileLogger = await readRepoAbs(
  "apps/api-internal/Infrastructure/FileLoggerProvider.cs",
);
assert.match(fileLogger, /KermariaTimeZone\.Now\.ToString\("yyyy-MM-dd"\)/);
assert.match(
  fileLogger,
  /KermariaTimeZone\.Now\.ToString\("yyyy-MM-ddTHH:mm:ss\.fffzzz"\)/,
);

const program = await readRepoAbs("apps/api-internal/Program.cs");
assert.match(
  program,
  /TimestampFormat = "yyyy-MM-ddTHH:mm:ss\.fffzzz"/,
  "AddJsonConsole doit émettre un timestamp avec offset (zzz).",
);
assert.match(
  program,
  /UseUtcTimestamp = false/,
  "AddJsonConsole doit utiliser l'heure locale (TZ=Europe/Paris).",
);

function formatInParis(iso) {
  return new Intl.DateTimeFormat("fr-FR", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "Europe/Paris",
  }).format(new Date(iso));
}

const summerUtc = "2026-07-02T13:00:00Z";
const summerRendered = formatInParis(summerUtc);
assert.match(
  summerRendered,
  /15:00/,
  `Attendu 15:00 en Europe/Paris été (UTC+2) pour ${summerUtc}, obtenu "${summerRendered}".`,
);

const winterUtc = "2026-01-15T13:00:00Z";
const winterRendered = formatInParis(winterUtc);
assert.match(
  winterRendered,
  /14:00/,
  `Attendu 14:00 en Europe/Paris hiver (UTC+1) pour ${winterUtc}, obtenu "${winterRendered}".`,
);

const dstSpringUtc = "2026-03-29T01:30:00Z";
const dstSpringRendered = formatInParis(dstSpringUtc);
assert.match(
  dstSpringRendered,
  /03:30/,
  `Bascule heure d'été 2026-03-29 02h→03h : attendu 03:30 pour 01:30 UTC, obtenu "${dstSpringRendered}".`,
);

console.log("Vérification du contrat horodatages V0.23.2 réussie.");
