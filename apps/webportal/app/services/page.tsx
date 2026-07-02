import Link from "next/link";

import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionHeading } from "@/components/SectionHeading";
import { ServiceCard } from "@/components/ServiceCard";
import { StatusBadge } from "@/components/StatusBadge";
import { SubscribeButton } from "@/components/SubscribeButton";
import { requireClientSession } from "@/lib/auth";
import {
  commercialOfferBillingCadence,
  formatCurrencyFromCents,
} from "@/lib/formatters";
import {
  getCommercialCatalog,
  getServices,
  resolveDataSource,
} from "@/lib/internal-api";
import { getPayPalMode } from "@/lib/paypal";
import { getStripeMode } from "@/lib/stripe";

export const metadata = {
  title: "Services",
};

export const dynamic = "force-dynamic";

export default async function ServicesPage() {
  await requireClientSession();
  const [servicesResult, catalogResult] = await Promise.all([
    getServices(),
    getCommercialCatalog(),
  ]);
  const source = resolveDataSource([
    servicesResult.source,
    catalogResult.source,
  ]);

  return (
    <>
      <PageHeader
        action={
          <Link className="button" href="/request-service">
            Demander un service
          </Link>
        }
        description="Consultez les services déjà suivis sur votre compte ainsi que les offres commerciales actuellement visibles dans le portail."
        eyebrow="Périmètre client"
        title="Mes services et offres"
      />

      {servicesResult.error ? (
        <ErrorState
          action={
            <Link className="button" href="/services">
              Réessayer
            </Link>
          }
          description="Impossible de charger vos services pour le moment."
          reference={servicesResult.correlationId}
          title="Services indisponibles"
        />
      ) : servicesResult.data.length === 0 ? (
        <EmptyState
          action={
            <Link className="button" href="/request-service">
              Préparer une demande
            </Link>
          }
          description="Aucun service n'est actuellement associé à ce compte. Vous pouvez transmettre une demande qui sera étudiée avant toute activation."
          title="Aucun service"
        />
      ) : (
        <section className="service-grid" aria-label="Services du compte">
          {servicesResult.data.map((service) => (
            <ServiceCard key={service.id} service={service} />
          ))}
        </section>
      )}

      <section className="request-history-section">
        <SectionHeading
          action={<StatusBadge label="Catalogue informatif" tone="info" />}
          description="Prix indicatifs HT et périmètre informatif. Aucun paiement ni engagement automatique n'est possible ici."
          title="Catalogue des offres visibles"
        />
        {catalogResult.error ? (
          <ErrorState
            compact
            description="Impossible de charger le catalogue des offres pour le moment."
            reference={catalogResult.correlationId}
            title="Catalogue indisponible"
          />
        ) : catalogResult.data.length === 0 ? (
          <EmptyState
            description="Aucune offre commerciale n'est actuellement affichée dans le portail."
            title="Catalogue vide"
          />
        ) : (
          <section className="catalog-grid" aria-label="Catalogue commercial">
            {catalogResult.data.map((offer) => {
              const cadence = commercialOfferBillingCadence[offer.billingCadence];
              const activePlanId =
                getPayPalMode() === "live"
                  ? offer.paypalPlanIdLive
                  : offer.paypalPlanIdSandbox;
              const activePriceId =
                getStripeMode() === "live"
                  ? offer.stripePriceIdLive
                  : offer.stripePriceIdTest;
              const canSubscribePaypal =
                offer.billingCadence === "monthly"
                && !!activePlanId
                && offer.status === "active";
              const canSubscribeStripe =
                offer.billingCadence === "monthly"
                && !!activePriceId
                && offer.status === "active";
              const canSubscribe = canSubscribePaypal || canSubscribeStripe;
              return (
                <article className="catalog-card" key={offer.id}>
                  <div className="badge-stack">
                    <StatusBadge label={cadence.label} tone={cadence.tone} />
                  </div>
                  <span className="card-kicker">{offer.category}</span>
                  <h2>{offer.name}</h2>
                  <p>{offer.description}</p>
                  <div className="catalog-scope">
                    <span>
                      {offer.unitLabel} · Prix indicatif {offer.priceKind.toUpperCase()}
                    </span>
                    <strong>{formatCurrencyFromCents(offer.priceAmountCents)}</strong>
                  </div>
                  {canSubscribe ? (
                    <SubscribeButton
                      offerId={offer.id}
                      offerName={offer.name}
                      paypalEnabled={canSubscribePaypal}
                      stripeEnabled={canSubscribeStripe}
                    />
                  ) : null}
                </article>
              );
            })}
          </section>
        )}
      </section>

      {source !== "unavailable" ? (
        <MockNotice
          correlationId={servicesResult.correlationId}
          source={source}
        />
      ) : null}
    </>
  );
}
