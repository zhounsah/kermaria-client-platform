import { AdminSettingsAuditCenter } from "@/components/AdminSettingsAuditCenter";
import { ErrorState } from "@/components/ErrorState";
import { PageHeader } from "@/components/PageHeader";
import { requireAdminSession } from "@/lib/auth";
import {
  getAdminSettingsAudit,
  getAdminSettingsPermissions,
  type AdminSettingsAuditFilters,
} from "@/lib/internal-api";

export const dynamic = "force-dynamic";
export const metadata = {
  title: "Audit de la configuration - Administration",
};

type PageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function first(value: string | string[] | undefined): string | undefined {
  return Array.isArray(value) ? value[0] : value;
}

export default async function AdminSettingsAuditPage({
  searchParams,
}: PageProps) {
  await requireAdminSession();
  const raw = await searchParams;

  // Les filtres ne sont pas interpretes ici : c'est API-INTERNAL qui decide ce
  // qu'un filtre inconnu selectionne. Normaliser cote portail pourrait diverger
  // de la regle serveur et laisser croire a une recherche exhaustive.
  const filters: AdminSettingsAuditFilters = {
    from: first(raw.from),
    to: first(raw.to),
    actor: first(raw.actor),
    category: first(raw.category),
    risk: first(raw.risk),
    outcome: first(raw.outcome),
    correlationId: first(raw.correlationId),
    target: first(raw.target),
    limit: first(raw.limit),
  };

  const [auditResult, permissionsResult] = await Promise.all([
    getAdminSettingsAudit(filters),
    getAdminSettingsPermissions(),
  ]);

  return (
    <>
      <PageHeader
        eyebrow="Centre de configuration"
        title="Audit de la configuration"
        description="Qui a changé quoi, quand, avec quel résultat et sous quelle référence. Le journal du portail est lu tel quel, restreint aux actions du Centre."
      />
      {auditResult.error || permissionsResult.error ? (
        <ErrorState
          title="Audit indisponible"
          description="Le journal de configuration ne peut pas être chargé pour le moment."
          reference={auditResult.correlationId}
        />
      ) : (
        <AdminSettingsAuditCenter
          audit={auditResult.data}
          permissions={permissionsResult.data}
        />
      )}
    </>
  );
}
