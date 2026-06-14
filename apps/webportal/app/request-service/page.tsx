import Link from "next/link";

import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { FormSection } from "@/components/FormSection";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { ServiceRequestForm } from "@/components/ServiceRequestForm";
import { StatusBadge } from "@/components/StatusBadge";
import { requireClientSession } from "@/lib/auth";
import { getServiceCatalog } from "@/lib/internal-api";

export const metadata = {
  title: "Demander un service",
};

export const dynamic = "force-dynamic";

export default async function RequestServicePage() {
  await requireClientSession();
  const result = await getServiceCatalog();

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Sans engagement" tone="info" />}
        description="Présentez votre besoin pour préparer un échange et une proposition adaptée, selon le périmètre convenu."
        eyebrow="Nouveau besoin"
        title="Demander un service"
      />

      {result.error ? (
        <ErrorState
          action={
            <Link className="button" href="/request-service">
              Réessayer
            </Link>
          }
          description="Le catalogue et le formulaire de demande ne peuvent pas être chargés pour le moment."
          reference={result.correlationId}
          title="Service temporairement indisponible"
        />
      ) : result.data.length === 0 ? (
        <EmptyState
          description="Aucune prestation n’est actuellement proposée dans cet espace."
          title="Catalogue vide"
        />
      ) : (
        <>
          <section className="catalog-grid" aria-label="Catalogue de services">
            {result.data.map((service) => (
              <article className="catalog-card" key={service.id}>
                <span className="card-kicker">{service.category}</span>
                <h2>{service.name}</h2>
                <p>{service.description}</p>
                <div className="catalog-scope">
                  <span>{service.scope}</span>
                  <strong>{service.commercialTerms}</strong>
                </div>
              </article>
            ))}
          </section>

          <div className="request-layout">
            <FormSection
              description="Présentez le contexte sans identifiant, mot de passe ni donnée confidentielle."
              title="Parlez-nous de votre besoin"
            >
              <ServiceRequestForm services={result.data} />
            </FormSection>
            <aside className="process-card">
              <p className="eyebrow">Parcours prévu</p>
              <h2>Une demande étudiée avant toute action</h2>
              <ol className="process-list">
                <li>
                  <span>1</span>
                  <div>
                    <strong>Qualification</strong>
                    <p>Le besoin et le contexte sont vérifiés.</p>
                  </div>
                </li>
                <li>
                  <span>2</span>
                  <div>
                    <strong>Proposition</strong>
                    <p>Une solution et un devis sont préparés séparément.</p>
                  </div>
                </li>
                <li>
                  <span>3</span>
                  <div>
                    <strong>Validation</strong>
                    <p>Aucune action n&apos;intervient sans accord explicite.</p>
                  </div>
                </li>
              </ol>
            </aside>
          </div>
        </>
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
