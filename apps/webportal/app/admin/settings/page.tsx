import Link from "next/link";

import { ErrorState } from "@/components/ErrorState";
import { PageHeader } from "@/components/PageHeader";
import { AdminSettingsCenter } from "@/components/AdminSettingsCenter";
import { requireAdminSession } from "@/lib/auth";
import { getAdminApplicationSettings } from "@/lib/internal-api";
import { getAdminConfigurationStatus } from "@/lib/internal-api";

export const dynamic = "force-dynamic";
export const metadata = { title: "Centre de configuration - Administration" };

export default async function AdminSettingsPage() {
  await requireAdminSession();
  const [result, statusResult] = await Promise.all([getAdminApplicationSettings(), getAdminConfigurationStatus()]);
  return <>
    <PageHeader eyebrow="Administration interne" title="Centre de configuration" description="Paramètres métier centralisés, contrôlés et audités. Les secrets et réglages d'infrastructure restent protégés ou en lecture seule." />
    <nav aria-label="Sections du centre de configuration" className="admin-settings-subnav">
      <Link className="button button-secondary" href="/admin/settings/messages">Messages & communications</Link>
      <Link className="button button-secondary" href="/admin/settings/diagnostic">Diagnostic</Link>
      <Link className="button button-secondary" href="/admin/settings/billing">Facturation & fiscalité</Link>
      <Link className="button button-secondary" href="/admin/settings/demonstrations">Démonstrations</Link>
      <Link className="button button-secondary" href="/admin/settings/integrations">Intégrations</Link>
    </nav>
    {result.error ? <ErrorState title="Configuration indisponible" description="Le centre de configuration ne peut pas être chargé pour le moment." reference={result.correlationId} /> : <AdminSettingsCenter initialSnapshot={result.data} statusDomains={statusResult.error ? [] : statusResult.data.domains} />}
  </>;
}
