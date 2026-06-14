import { readdir, readFile } from "node:fs/promises";
import path from "node:path";

const root = process.cwd();
const excludedDirectories = new Set([
  ".git",
  ".next",
  ".nuget",
  ".nuget-local",
  ".app-data",
  "bin",
  "node_modules",
  "obj",
  "backups",
]);
const excludedFiles = new Set([
  path.normalize("scripts/check-secrets.mjs"),
  path.normalize("apps/api-internal/Data/Configuration/RuntimeConfigurationValidator.cs"),
  path.normalize("apps/webportal/lib/runtime-config.ts"),
]);
const sensitiveAssignment =
  /\b(SQL_PASSWORD|SERVICE_AUTH_TOKEN|DEMO_PORTAL_PASSWORD|DEMO_INTERNAL_ADMIN_PASSWORD)\s*=\s*(.+)$/gim;
const allowedValue =
  /(^\||\*\*(REPLACE_WITH_SECURE_VALUE|INJECTER_LOCALEMENT)\*\*|<[^>]+>|\$\{[^}]+})/i;
const forbiddenPatterns = [
  { label: "mot de passe de test exposé", pattern: /Test12345!/i },
  { label: "token local faible", pattern: /dev-local-token/i },
  {
    label: "mot de passe de test en clair",
    pattern: /password\s*:\s*["']Test[^"']*["']/i,
  },
  {
    label: "clé privée",
    pattern: /-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----/,
  },
];

const findings = [];
for (const filePath of await listFiles(root)) {
  const relativePath = path.relative(root, filePath);
  if (excludedFiles.has(path.normalize(relativePath))) {
    continue;
  }

  const content = await readFile(filePath, "utf8");

  for (const { label, pattern } of forbiddenPatterns) {
    if (pattern.test(content)) {
      findings.push(`${relativePath}: ${label}`);
    }
  }

  for (const match of content.matchAll(sensitiveAssignment)) {
    const value = match[2].trim();
    if (!allowedValue.test(value)) {
      findings.push(`${relativePath}: valeur sensible potentielle pour ${match[1]}`);
    }
  }
}

if (findings.length > 0) {
  console.error("Le garde-fou secrets a détecté des valeurs à vérifier :");
  for (const finding of findings) {
    console.error(`- ${finding}`);
  }
  process.exit(1);
}

console.log("Garde-fou secrets : aucun motif sensible évident détecté.");

async function listFiles(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    if (entry.isDirectory() && excludedDirectories.has(entry.name)) {
      continue;
    }

    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      files.push(...(await listFiles(fullPath)));
    } else if (entry.isFile() && isTextFile(entry.name)) {
      files.push(fullPath);
    }
  }

  return files;
}

function isTextFile(fileName) {
  return /\.(?:cs|csproj|css|env|example|js|json|md|mjs|props|sql|targets|ts|tsx|txt|xml|ya?ml)$/i.test(
    fileName,
  );
}
