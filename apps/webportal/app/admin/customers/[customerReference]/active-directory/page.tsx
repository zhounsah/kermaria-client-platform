import Link from "next/link";
import { notFound } from "next/navigation";

import { AdminCustomerActiveDirectoryWorkbench } from "@/components/AdminCustomerActiveDirectoryWorkbench";
import { AdminCustomerAdManager } from "@/components/AdminCustomerAdManager";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { requireAdminSession } from "@/lib/auth";
import { getAdminCustomerAdWorkspace } from "@/lib/internal-api";

export const metadata = {
  title: "Active Directory client - Administration",
};

export const dynamic = "force-dynamic";

export default async function AdminCustomerActiveDirectoryPage({
  params,
  searchParams,
}: {
  params: Promise<{ customerReference: string }>;
  searchParams: Promise<{ subscriptionId?: string }>;
}) {
  await requireAdminSession();
  const { customerReference } = await params;
  const { subscriptionId } = await searchParams;
  const workspaceResult = await getAdminCustomerAdWorkspace(
    customerReference,
    subscriptionId ?? null,
  );

  if (workspaceResult.error) {
    return (
      <ErrorState
        action={
          <Link
            className="button"
            href={`/admin/customers/${encodeURIComponent(customerReference)}`}
          >
            Retour à la fiche client
          </Link>
        }
        description="Impossible de charger le workbench Active Directory de ce client."
        reference={workspaceResult.correlationId}
        title="Page Active Directory indisponible"
      />
    );
  }

  if (!workspaceResult.data) {
    notFound();
  }

  const workspace = workspaceResult.data;

  return (
    <>
      <PageHeader
        description="Administration des services, des groupes de sécurité et des liens AD du client."
        eyebrow={workspace.customerReference}
        title={`Active Directory - ${workspace.customerName}`}
      />

      <AdminCustomerActiveDirectoryWorkbench
        customerReference={workspace.customerReference}
        initialWorkspace={workspace}
      />

      <div id="gestion-avancee-ad">
        <SectionCard ariaLabel="Gestion avancée Active Directory" className="stack-panel">
          <div className="section-heading">
            <div>
              <h2>Gestion avancée des objets et liens AD</h2>
              <p>
                Cette zone reprend les opérations d&apos;administration détaillées
                auparavant affichées dans la fiche client longue.
              </p>
            </div>
          </div>
          <AdminCustomerAdManager
            customerReference={workspace.customerReference}
            initialLinks={workspace.links}
            initialStatus={workspace.adStatus}
          />
        </SectionCard>
      </div>

      <MockNotice
        correlationId={workspaceResult.correlationId}
        source={workspaceResult.source}
      />
    </>
  );
}
