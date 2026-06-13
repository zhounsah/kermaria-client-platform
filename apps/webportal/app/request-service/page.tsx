import { EmptyState } from "@/components/EmptyState";
import { FormSection } from "@/components/FormSection";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { ServiceRequestForm } from "@/components/ServiceRequestForm";
import { StatusBadge } from "@/components/StatusBadge";
import { requirePortalSession } from "@/lib/auth";
import { getServiceCatalog } from "@/lib/internal-api";

export const metadata = {
  title: "Demander un service",
};

export const dynamic = "force-dynamic";

export default async function RequestServicePage() {
  await requirePortalSession();
  const result = await getServiceCatalog();

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Sans engagement" tone="info" />}
        description="Présentez votre besoin pour préparer un échange et une proposition adaptée, selon le périmètre convenu."
        eyebrow="Nouveau besoin"
        title="Demander un service"
      />

      {result.data.length === 0 ? (
        <EmptyState
          description="Le catalogue mock est temporairement indisponible."
          title="Aucun service proposé"
        />
      ) : (
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
      )}

      <div className="request-layout">
        <FormSection
          description="Le formulaire appelle uniquement le BFF du portail. API-INTERNAL indique si la demande est persistée ou traitée en fallback mock."
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

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
