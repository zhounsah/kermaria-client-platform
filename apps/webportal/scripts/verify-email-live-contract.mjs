import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const emailConfig = await read(
  "../../apps/api-internal/Data/Configuration/EmailRuntimeConfiguration.cs",
);
const liveEmailService = await read(
  "../../apps/api-internal/Services/Email/LiveEmailService.cs",
);
const envExample = await read("../../.env.example");

const checks = [];
function check(name, fn) {
  checks.push([name, fn]);
}

// --- Configuration ---
check("allowlist_only défaut true (fail-closed)", () => {
  assert.match(
    emailConfig,
    /ParseBool\(\s*configuration\["EMAIL_LIVE_ALLOWLIST_ONLY"\],\s*true\s*\)/,
  );
});
check("allowlist parsée depuis EMAIL_LIVE_ALLOWLIST", () => {
  assert.match(emailConfig, /EMAIL_LIVE_ALLOWLIST/);
  assert.match(emailConfig, /ParseAllowlist/);
});
check("IsRecipientAllowed supporte adresse et @domaine", () => {
  assert.match(emailConfig, /public bool IsRecipientAllowed/);
  assert.match(emailConfig, /pattern\.StartsWith\('@'\)/);
  assert.match(emailConfig, /EndsWith\(pattern, StringComparison\.Ordinal\)/);
});
check("allowlist vide => tout envoi live est refusé", () => {
  // La boucle foreach parcourt les entrées ; si liste vide, aucun match,
  // donc return false. Aucun path early-return sur liste vide.
  assert.match(
    emailConfig,
    /foreach \(var entry in LiveAllowlist\)[\s\S]+return false;/,
  );
});

// --- Gate LiveEmailService ---
check("LiveEmailService bloque avant tout appel SMTP", () => {
  assert.match(liveEmailService, /IsRecipientAllowed\(message\.Recipient\)/);
  assert.match(liveEmailService, /"blocked_allowlist"/);
  // Le check doit être avant la construction de SmtpClient.
  const idxCheck = liveEmailService.indexOf("blocked_allowlist");
  const idxSmtp = liveEmailService.indexOf("new SmtpClient");
  assert.ok(
    idxCheck > 0 && idxCheck < idxSmtp,
    "le gate allowlist doit précéder la construction du SmtpClient",
  );
});
check("blocage journalisé (LogWarning + status blocked_allowlist)", () => {
  assert.match(
    liveEmailService,
    /LogWarning\([\s\S]*?Live email blocked by allowlist/,
  );
});

// --- .env.example ---
check(".env.example documente EMAIL_LIVE_ALLOWLIST_ONLY=true", () => {
  assert.match(envExample, /EMAIL_LIVE_ALLOWLIST_ONLY=true/);
});
check(".env.example expose EMAIL_LIVE_ALLOWLIST", () => {
  assert.match(envExample, /^EMAIL_LIVE_ALLOWLIST=/m);
});
check(".env.example mentionne OVH MX Plan (aide test)", () => {
  assert.match(envExample, /OVH MX Plan/);
  assert.match(envExample, /ssl0\.ovh\.net/);
});

let failures = 0;
for (const [name, fn] of checks) {
  try {
    fn();
    console.log(`  ok   ${name}`);
  } catch (error) {
    failures += 1;
    console.error(`  FAIL ${name}`);
    console.error(`       ${error.message.split("\n")[0]}`);
  }
}

if (failures > 0) {
  console.error(
    `\n${failures} vérification(s) de contrat email-live en échec.`,
  );
  process.exit(1);
}

console.log(
  `\nContrat email-live V0.30 (partiel : allowlist) validé (${checks.length} vérifications).`,
);
