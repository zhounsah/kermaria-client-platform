import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const forbiddenPublicTerms = [
  /\bBilling\s+V2(?:\.1)?\b/i,
  /\bauthoritative\b/i,
  /\bautoritatif\b/i,
  /\bprojection\s+Billing\b/i,
  /\bprovisioning\b/i,
  /\bclients\.home\.bzh\b/i,
  /\bécriture\s+AD\b/i,
];

const publicCopyFiles = [
  "app/signup/page.tsx",
  "app/tarifs/page.tsx",
  "app/offres/[slug]/page.tsx",
  "app/services/vps/choisir/confirmation/page.tsx",
  "app/set-password/page.tsx",
  "components/PublicVpsConfigurator.tsx",
  "components/ServiceRequestForm.tsx",
];

for (const path of publicCopyFiles) {
  const source = (await read(path))
    .replace(/\/\*[\s\S]*?\*\//g, "")
    .replace(/\/\/.*$/gm, "")
    // L'etat operationnel est un contrat, pas du texte rendu au client.
    .replace(/\bprovisioning\b(?=\s*(?:=|===))/g, "")
    .replace(/===\s*["']provisioning["']/g, "");
  for (const term of forbiddenPublicTerms) {
    assert.doesNotMatch(
      source,
      term,
      `Le rendu public ${path} ne doit pas exposer ${term}.`,
    );
  }
}

const configurator = await read("components/PublicVpsConfigurator.tsx");
assert.match(configurator, /role="dialog"/);
assert.match(configurator, /aria-modal="true"/);
assert.match(configurator, /event\.key === "Escape"/);
assert.match(configurator, /event\.target === event\.currentTarget/);
assert.match(configurator, /identityReturnFocusRef\.current\?\.focus\(\)/);
assert.match(configurator, /saveDraft\(\);[\s\S]*?setIdentityRequired\(true\)/);
assert.doesNotMatch(configurator, /<aside className="vps-configurator-notice"/);

const styles = await read("app/globals.css");
assert.match(styles, /\.vps-identity-dialog-backdrop[\s\S]*?animation: vps-identity-backdrop-enter 200ms/);
assert.match(styles, /\.vps-identity-dialog[\s\S]*?animation: vps-identity-dialog-enter 200ms/);
assert.match(styles, /@media \(prefers-reduced-motion: reduce\)[\s\S]*?\.vps-identity-dialog/);

console.log("Contrat de copy publique et modale VPS vérifié.");
