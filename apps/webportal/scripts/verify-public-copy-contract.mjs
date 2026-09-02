import assert from "node:assert/strict";
import { readdir, readFile } from "node:fs/promises";

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

const forbiddenPublicTerms = [
  /\bBilling\s+V2(?:\.1)?\b/i,
  /\bauthoritative\b/i,
  /\bautoritatif\b/i,
  /\bprojection\s+Billing\b/i,
  /\bcatalogue\s+Billing\b/i,
  /\bprovisioning\b/i,
  /\boutbox\b/i,
  /\breconciliation\s+worker\b/i,
  /\bfeature\s*flags?\b/i,
  /\brail\s+Stripe\b/i,
  /\bexecutor\b/i,
  /\bAPI[-\s]INTERNAL\b/i,
  /\bmigration\s+\d{3}\b/i,
  /\bclients\.home\.bzh\b/i,
  /\bécriture\s+AD\b/i,
];

/**
 * Litteraux `"…"` et `` `…` ``, hors commentaires.
 *
 * Seuls ceux qui contiennent une espace sont examines : un texte rendu est
 * une phrase, tandis qu'une valeur d'etat (`"provisioning"`, `"outbox"`) est
 * un jeton unique. Cette seule distinction supprime les faux positifs que la
 * version precedente devait neutraliser a coups de `replace`.
 */
const STRING_LITERALS = /"((?:[^"\\\n]|\\.)*)"|`((?:[^`\\]|\\.)*)`/g;

/**
 * Surfaces exemptees, avec la raison. Cette liste doit rester courte : chaque
 * entree est une surface ou le vocabulaire interne est legitime.
 */
const INTERNAL_SURFACES = new Map([
  [
    "components/DemoAccountConvertButton.tsx",
    "Composant d'administration : rendu uniquement par app/admin/demo/page.tsx.",
  ],
  [
    "app/api/subscriptions/billing-v2/return/route.ts",
    "Prefixe de `console.error` : journal serveur, jamais rendu au visiteur.",
  ],
]);

// Toute surface non-administration est balayee, et non une liste tenue a la
// main : c'est l'ajout d'un fichier qui creait la regression, pas la
// modification des sept fichiers deja surveilles.
const scanned = [];
for (const root of ["app", "components"]) {
  for (const entry of await readdir(new URL(`../${root}`, import.meta.url), {
    recursive: true,
  })) {
    const path = `${root}/${entry}`.split("\\").join("/");
    if (!/\.(tsx|ts)$/.test(path)) continue;
    // `/admin`, `app/api/admin`, `components/admin/`, `components/Admin*.tsx`.
    if (/(^|\/)admin|Admin/.test(path)) continue;
    scanned.push(path);
  }
}

assert.ok(
  scanned.length > 100,
  `Balayage anormalement etroit (${scanned.length} fichiers) : verifier le filtre.`,
);

for (const path of scanned) {
  const source = (await read(path))
    .replace(/\/\*[\s\S]*?\*\//g, "")
    .replace(/\/\/.*$/gm, "");

  for (const match of source.matchAll(STRING_LITERALS)) {
    const literal = match[1] ?? match[2] ?? "";
    if (!/\s/.test(literal)) continue;

    for (const term of forbiddenPublicTerms) {
      if (!term.test(literal)) continue;
      assert.ok(
        INTERNAL_SURFACES.has(path),
        `Le rendu public ${path} ne doit pas exposer ${term} : "${literal.slice(0, 120)}".`,
      );
    }
  }
}

// Les exemptions doivent rester justifiees : une entree qui ne designe plus
// un fichier existant laisserait une surface reelle non surveillee.
for (const [path, reason] of INTERNAL_SURFACES) {
  assert.ok(
    scanned.includes(path),
    `L'exemption « ${reason} » vise ${path}, qui n'est plus balaye.`,
  );
}

const configurator = await read("components/PublicVpsConfigurator.tsx");
assert.match(configurator, /role="dialog"/);
assert.match(configurator, /aria-modal="true"/);
assert.match(configurator, /event\.key === "Escape"/);
assert.match(configurator, /event\.target === event\.currentTarget/);
assert.match(configurator, /identityReturnFocusRef\.current\?\.focus\(\)/);
assert.match(configurator, /saveDraft\(\);[\s\S]*?updateIdentityDialogState\("open"\)/);
assert.match(configurator, /IdentityDialogState = "closed" \| "open" \| "closing"/);
assert.match(configurator, /onAnimationEnd=\{\(event\) => \{/);
assert.match(configurator, /window\.innerWidth - document\.documentElement\.clientWidth/);
assert.match(configurator, /previousPaddingRight[\s\S]*?body\.style\.paddingRight = previousPaddingRight/);
assert.doesNotMatch(configurator, /<aside className="vps-configurator-notice"/);

const styles = await read("app/globals.css");
assert.match(styles, /\.vps-identity-dialog-backdrop[\s\S]*?animation: vps-identity-backdrop-enter 200ms/);
assert.match(styles, /\.vps-identity-dialog[\s\S]*?animation: vps-identity-dialog-enter 200ms/);
assert.match(styles, /vps-identity-backdrop-exit 200ms/);
assert.match(styles, /vps-identity-dialog-exit 200ms/);
assert.match(styles, /@media \(prefers-reduced-motion: reduce\)[\s\S]*?\.vps-identity-dialog/);

console.log("Contrat de copy publique et modale VPS vérifié.");
