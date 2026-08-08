import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const repoRoot = new URL("../../../", import.meta.url);

async function read(path) {
  return readFile(new URL(path, repoRoot), "utf8");
}

const [
  shared,
  internalApi,
  backupsPage,
  backupDetailPage,
  restoreRoute,
  adminRoute,
  collector,
  migration,
] = await Promise.all([
  read("packages/shared/src/index.ts"),
  read("apps/webportal/lib/internal-api.ts"),
  read("apps/webportal/app/backups/page.tsx"),
  read("apps/webportal/app/backups/[id]/page.tsx"),
  read("apps/webportal/app/api/backups/[id]/restore-requests/route.ts"),
  read("apps/webportal/app/api/admin/backups/integrations/route.ts"),
  read("scripts/veeam/Invoke-VeeamBackupCollection.ps1"),
  read("apps/api-internal/Migrations/MariaDb/044_veeam_backup_status.sql"),
]);

assert.match(shared, /BackupProtectionStatus/);
assert.match(shared, /BackupRestoreRequestPayload/);
assert.match(internalApi, /\/internal\/portal\/backups/);
assert.match(internalApi, /\/internal\/admin\/backups\/integrations/);
assert.doesNotMatch(backupsPage, /VEEAM-|repository01|192\.168\.|\\\\/);
assert.doesNotMatch(backupDetailPage, /VEEAM-|repository01|192\.168\.|\\\\/);
assert.match(restoreRoute, /createBackupRestoreRequest/);
assert.match(adminRoute, /handleAdminGet/);
assert.match(adminRoute, /handleAdminMutation/);
assert.match(collector, /X-Service-Auth/);
assert.match(collector, /WhatIfReport/);
assert.match(migration, /UNIQUE KEY uq_backup_runs_job_session/);
assert.match(migration, /stale_after_minutes/);

console.log("Verification backups WEBPORTAL reussie.");
