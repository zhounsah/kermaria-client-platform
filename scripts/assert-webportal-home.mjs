import process from "node:process";

const args = parseArgs(process.argv.slice(2));
const url = args.url;
const mustMatch = args["must-match"];
const mustNotMatch = args["must-not-match"];
const timeoutMs = Number(args.timeout ?? "15000");

if (!url) {
  process.stderr.write("Parametre requis: --url\n");
  process.exit(1);
}

const requiredPatterns = asArray(mustMatch);
const forbiddenPatterns = asArray(mustNotMatch);

try {
  const response = await fetch(url, {
    signal: AbortSignal.timeout(timeoutMs),
    headers: {
      Accept: "text/html,application/xhtml+xml",
      "User-Agent": "kermaria-webportal-assert/1.0",
    },
  });

  if (!response.ok) {
    throw new Error(`HTTP ${response.status} pour ${url}`);
  }

  const rawHtml = await response.text();
  const normalizedHtml = normalizeHtml(rawHtml);

  for (const pattern of requiredPatterns) {
    assertPattern(normalizedHtml, pattern, true);
  }

  for (const pattern of forbiddenPatterns) {
    assertPattern(normalizedHtml, pattern, false);
  }

  process.stdout.write(`OK ${url}\n`);
} catch (error) {
  const message = error instanceof Error ? error.message : String(error);
  process.stderr.write(`Echec assert webportal: ${message}\n`);
  process.exit(1);
}

function normalizeHtml(html) {
  return html
    .replace(/<!--[\s\S]*?-->/gu, "")
    .replace(/\s+/gu, " ")
    .trim();
}

function assertPattern(content, pattern, shouldExist) {
  const regex = new RegExp(pattern, "u");
  const exists = regex.test(content);

  if (shouldExist && !exists) {
    throw new Error(`Pattern attendu absent: ${pattern}`);
  }

  if (!shouldExist && exists) {
    throw new Error(`Pattern interdit detecte: ${pattern}`);
  }
}

function asArray(value) {
  if (value === undefined) {
    return [];
  }

  return Array.isArray(value) ? value : [value];
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

    if (parsed[key] === undefined) {
      parsed[key] = next;
    } else if (Array.isArray(parsed[key])) {
      parsed[key].push(next);
    } else {
      parsed[key] = [parsed[key], next];
    }

    index += 1;
  }

  return parsed;
}
