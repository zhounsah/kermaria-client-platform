import Link from "next/link";

import { EmptyState } from "@/components/EmptyState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { ServiceCard } from "@/components/ServiceCard";
import { requireClientSession } from "@/lib/auth";
import { getServices } from "@/lib/internal-api";

export const metadata = {
  title: "Services",
};

export const dynamic = "force-dynamic";

export default async function ServicesPage() {
  await requireClientSession();
  const result = await getServices();

  return (
    <>
      <PageHeader
        action={
          <Link className="button" href="/request-service">
            Demander un service
          </Link>
        }
        description="Consultez les services mock actifs, en attente ou suspendus associés au compte de démonstration."
        eyebrow="Périmètre client"
        title="Mes services"
      />

      {result.data.length === 0 ? (
        <EmptyState
          action={
            <Link className="button" href="/request-service">
              Préparer une demande
            </Link>
          }
          description="Aucun service mock n'est actuellement associé à ce compte."
          title="Aucun service"
        />
      ) : (
        <section className="service-grid" aria-label="Services du compte">
          {result.data.map((service) => (
            <ServiceCard key={service.id} service={service} />
          ))}
        </section>
      )}

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
