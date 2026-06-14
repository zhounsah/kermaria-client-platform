import { spawn } from "node:child_process";

const requiredVariables = [
  "SQL_PROVIDER",
  "SQL_HOST",
  "SQL_PORT",
  "SQL_DATABASE",
  "SQL_USERNAME",
  "SQL_PASSWORD",
  "SERVICE_AUTH_TOKEN",
  "DEMO_PORTAL_EMAIL",
  "DEMO_PORTAL_PASSWORD",
  "DEMO_INTERNAL_ADMIN_EMAIL",
  "DEMO_INTERNAL_ADMIN_PASSWORD",
];

const missing = requiredVariables.filter((name) => !process.env[name]?.trim());

if (missing.length > 0) {
  console.error(
    `Validation MariaDB non lancée. Variables manquantes : ${missing.join(", ")}.`,
  );
  process.exit(2);
}

if (process.env.SQL_PROVIDER?.toLowerCase() !== "mariadb") {
  console.error("Validation MariaDB non lancée. SQL_PROVIDER doit valoir mariadb.");
  process.exit(2);
}

const isWindows = process.platform === "win32";

const command = isWindows ? "cmd.exe" : "npm";
const args = isWindows
  ? ["/d", "/s", "/c", "npm run test:api"]
  : ["run", "test:api"];

const child = spawn(command, args, {
  env: {
    ...process.env,
    RUN_MARIADB_TESTS: "true",
  },
  stdio: "inherit",
  windowsHide: true,
});

child.on("exit", (code) => process.exit(code ?? 1));

child.on("error", (error) => {
  console.error(`Impossible de lancer la validation MariaDB : ${error.message}`);
  process.exit(1);
});