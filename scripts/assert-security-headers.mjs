import { readFile } from "node:fs/promises";
import process from "node:process";

/**
 * Compare les en-tetes de securite REELLEMENT livres par une URL au contrat
 * declare dans `apps/webportal/next.config.ts`.
 *
 * Complement indispensable a `test:operations` et `test:seo`, qui lisent le
 * code source et ne voient donc jamais le reverse proxy SRV-11. Un
 * `add_header` nginx n'ecrase pas l'en-tete amont : il en ajoute un second.
 * `fetch` concatene les doublons avec ", ", donc toute duplication casse
 * l'egalite stricte et sort dans le message d'erreur.
 *
 * Usage :
 *   node scripts/assert-security-headers.mjs --url https://www.zacharyhounsa.ovh/
 *
 * L'URL doit pointer une page de la VITRINE PUBLIQUE : le script exige
 * l'absence de `X-Robots-Tag` (regression du noindex global, corrigee le
 * 2026-08-04).
 */

const args = parseArgs(process.argv.slice(2));
const url = args.url;
const timeoutMs = Number(args.timeout ?? "15000");

if (!url) {
  process.stderr.write("Parametre requis: --url\n");
  process.exit(1);
}

const configUrl = new URL("../apps/webportal/next.config.ts", import.meta.url);
const expected = extractSecurityHeaders(await readFile(configUrl, "utf8"));

if (expected.length === 0) {
  process.stderr.write(
    "Contrat introuvable: SECURITY_HEADERS absent de next.config.ts\n",
  );
  process.exit(1);
}

const failures = [];

try {
  const response = await fetch(url, {
    redirect: "manual",
    signal: AbortSignal.timeout(timeoutMs),
    headers: {
      Accept: "text/html,application/xhtml+xml",
      "User-Agent": "kermaria-security-headers-assert/1.0",
    },
  });

  // Vider le corps avant toute sortie : un socket keep-alive encore ouvert
  // fait planter la fermeture de libuv sous Windows (`UV_HANDLE_CLOSING`).
  await response.arrayBuffer();

  if (response.status !== 200) {
    throw new Error(
      `HTTP ${response.status} pour ${url} (200 attendu, sans redirection)`,
    );
  }

  for (const { key, value } of expected) {
    const received = response.headers.get(key);

    if (received === null) {
      failures.push(`${key} : absent (attendu \`${value}\`)`);
      continue;
    }

    if (received !== value) {
      failures.push(
        `${key} : \`${received}\` (attendu \`${value}\`)` +
          (received.includes(value)
            ? " — valeur en double, un intermediaire ajoute la sienne"
            : ""),
      );
    }
  }

  const robotsTag = response.headers.get("X-Robots-Tag");
  if (robotsTag !== null) {
    failures.push(
      `X-Robots-Tag : \`${robotsTag}\` — interdit sur la vitrine publique, ` +
        "elle doit rester indexable",
    );
  }

  if (failures.length > 0) {
    throw new Error(
      `${failures.length} en-tete(s) non conforme(s) sur ${url} :\n  - ` +
        failures.join("\n  - "),
    );
  }

  process.stdout.write(
    `OK ${url} — ${expected.length} en-tetes de securite conformes, ` +
      "aucun doublon, aucun X-Robots-Tag.\n",
  );
} catch (error) {
  const message = error instanceof Error ? error.message : String(error);
  process.stderr.write(`Echec assert en-tetes de securite: ${message}\n`);
  process.exitCode = 1;
}

function extractSecurityHeaders(source) {
  const start = source.indexOf("const SECURITY_HEADERS = [");
  if (start === -1) {
    return [];
  }

  const end = source.indexOf("];", start);
  if (end === -1) {
    return [];
  }

  const block = source.slice(start, end);
  const entries = [];
  const pattern = /key:\s*"([^"]+)",\s*value:\s*"([^"]*)"/gu;

  let match = pattern.exec(block);
  while (match !== null) {
    entries.push({ key: match[1], value: match[2] });
    match = pattern.exec(block);
  }

  return entries;
}

function parseArgs(argv) {
  const parsed = {};

  for (let index = 0; index < argv.length; index += 1) {
    const token = argv[index];
    if (!token.startsWith("--")) {
      throw new Error(`Argument invalide: ${token}`);
    }

    const key = token.slice(2);
    const next = argv[index + 1];
    if (!next || next.startsWith("--")) {
      parsed[key] = "true";
      continue;
    }

    parsed[key] = next;
    index += 1;
  }

  return parsed;
}
