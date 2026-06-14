import Link from "next/link";

import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
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
        description="Consultez les services actifs, en attente ou suspendus associés à votre compte."
        eyebrow="Périmètre client"
        title="Mes services"
      />

      {result.error ? (
        <ErrorState
          action={
            <Link className="button" href="/services">
              Réessayer
            </Link>
          }
          description="Impossible de charger vos services pour le moment."
          reference={result.correlationId}
          title="Services indisponibles"
        />
      ) : result.data.length === 0 ? (
        <EmptyState
          action={
            <Link className="button" href="/request-service">
              Préparer une demande
            </Link>
          }
          description="Aucun service n’est actuellement associé à ce compte. Vous pouvez transmettre une demande qui sera étudiée avant toute activation."
          title="Aucun service"
        />
      ) : (
        <section className="service-grid" aria-label="Services du compte">
          {result.data.map((service) => (
            <ServiceCard key={service.id} service={service} />
          ))}
        </section>
      )}

      {result.source !== "unavailable" ? (
        <MockNotice
          correlationId={result.correlationId}
          source={result.source}
        />
      ) : null}
    </>
  );
}
