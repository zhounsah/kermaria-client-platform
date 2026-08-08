import Link from "next/link";

import type {
  CheckoutSummary,
  CommercialOfferSummary,
  CorrelationId,
  DataSource,
  PublicPackCatalogContent,
  ResolvedPublicPackManifest,
  ServiceCatalogItem,
} from "@kermaria/shared";

import { AddRecurringCheckoutButton } from "@/components/AddRecurringCheckoutButton";
import { AddToCartButton } from "@/components/AddToCartButton";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { PublicPackCard } from "@/components/PublicPackCard";
import { SectionHeading } from "@/components/SectionHeading";
import { StatusBadge } from "@/components/StatusBadge";
import { formatCommercialAmountFromCents } from "@/lib/fiscal-formatters";
import {
  formatBillingIntervalMonths,
  formatCommitmentMonths,
  formatCurrencyFromCents,
  formatPaymentModeLabel,
} from "@/lib/formatters";
import { findPackPresentation } from "@/lib/public-packs";

type SubscribeCatalogSectionsProps = {
  aLaCarteOffers: CommercialOfferSummary[];
  catalogCorrelationId?: CorrelationId;
  catalogError: boolean;
  checkout: CheckoutSummary;
  commercialCatalogCorrelationId?: CorrelationId;
  commercialCatalogError: boolean;
  packContent: PublicPackCatalogContent | null;
  packs: ResolvedPublicPackManifest[];
  serviceCatalog: ServiceCatalogItem[];
  serviceCatalogCorrelationId?: CorrelationId;
  serviceCatalogError: boolean;
  source: DataSource;
  standaloneRecurringOffers: CommercialOfferSummary[];
};

export function SubscribeCatalogSections({
  aLaCarteOffers,
  catalogCorrelationId,
  catalogError,
  checkout,
  commercialCatalogCorrelationId,
  commercialCatalogError,
  packContent,
  packs,
  serviceCatalog,
  serviceCatalogCorrelationId,
  serviceCatalogError,
  source,
  standaloneRecurringOffers,
}: SubscribeCatalogSectionsProps) {
  return (
    <>
      <PageHeader
        action={
          <Link className="button button-ghost" href="/services">
            Retour à mes services
          </Link>
        }
        description="Ajoutez vos achats ponctuels et vos packs récurrents dans un panier unifié, puis confirmez ensuite le tunnel adapté à chaque type d'achat."
        eyebrow="Ajouter un service"
        title="Souscrire à une offre"
      />

      {checkout.totalItemCount > 0 ? (
        <section className="checkout-access-banner">
          <div>
            <span className="card-kicker">Panier unifie</span>
            
            <h2>
              {checkout.cart.itemCount} achat(s) ponctuel(s) et{" "}
              {checkout.recurring.itemCount} abonnement(s) en cours
            </h2>
            <p>
              Validation immédiate estimée à{" "}
              <strong>
                {formatCurrencyFromCents(
                  checkout.cart.subtotalCents + checkout.recurring.subtotalCents,
                )}
              </strong>
              . Les deux tunnels restent distincts au moment de confirmer.
            </p>
          </div>
          <Link className="button" href="/panier">
            Voir mon panier
          </Link>
        </section>
      ) : null}

      <section className="request-history-section">
        <SectionHeading
          action={<StatusBadge label="Abonnements facturés" tone="info" />}
          description="Choisissez votre pack, ajoutez-le au panier, puis confirmez une facture de premier terme avant de régler par Stripe, PayPal ou virement bancaire."
          title="Packs récurrents"
        />
        {catalogError ? (
          <ErrorState
            compact
            description="Impossible de charger le catalogue packs pour le moment."
            reference={catalogCorrelationId}
            title="Catalogue indisponible"
          />
        ) : packs.length === 0 ? (
          <EmptyState
            description="Aucun pack grand public n'est actuellement affiché dans le portail."
            title="Catalogue vide"
          />
        ) : (
          <section className="public-pack-grid" aria-label="Packs grand public">
            {packs.map((pack) => (
              <PublicPackCard
                key={pack.key}
                highlightLabel={findPackPresentation(
                  pack.key,
                  packContent,
                )?.highlightLabel}
                initialSelection={null}
                mode="subscribe"
                pack={pack}
              />
            ))}
          </section>
        )}
      </section>

      <section className="request-history-section">
        <SectionHeading
          action={<StatusBadge label="Abonnements directs" tone="info" />}
          description="Ces offres récurrentes se souscrivent à l'unité, sans passer par un pack grand public."
          title="Abonnements à l'unité"
        />
        {commercialCatalogError ? (
          <ErrorState
            compact
            description="Impossible de charger les abonnements à l'unité pour le moment."
            reference={commercialCatalogCorrelationId}
            title="Abonnements indisponibles"
          />
        ) : standaloneRecurringOffers.length === 0 ? (
          <EmptyState
            description="Aucun abonnement récurrent hors pack n'est actuellement proposé."
            title="Catalogue vide"
          />
        ) : (
          <section
            className="catalog-grid"
            aria-label="Abonnements récurrents à l'unité"
          >
            {standaloneRecurringOffers.map((offer) => (
              <article className="catalog-card" key={offer.id}>
                <span className="card-kicker">{offer.category}</span>
                <h2>{offer.name}</h2>
                <p className="multiline-text">{offer.description}</p>
                <div className="catalog-scope">
                  <strong>
                    {formatCommercialAmountFromCents(offer.priceAmountCents, {
                      fiscalRegime: offer.fiscalRegime,
                    })}
                  </strong>
                  <span>
                    {formatBillingIntervalMonths(offer.billingIntervalMonths)}
                  </span>
                </div>
                <p className="field-hint">
                  {formatPaymentModeLabel(offer.paymentMode)} · engagement{" "}
                  {formatCommitmentMonths(offer.commitmentMonths)}
                </p>
                <AddRecurringCheckoutButton offerId={offer.id} />
              </article>
            ))}
          </section>
        )}
      </section>

      <section className="request-history-section">
        <SectionHeading
          action={<StatusBadge label="Paiement immédiat" tone="success" />}
          description="Ajoutez une ou plusieurs prestations ponctuelles à votre panier, puis réglez le tout en une seule commande."
          title="Achats ponctuels"
        />
        {commercialCatalogError ? (
          <ErrorState
            compact
            description="Impossible de charger les options à la carte pour le moment."
            reference={commercialCatalogCorrelationId}
            title="Options indisponibles"
          />
        ) : aLaCarteOffers.length === 0 ? (
          <EmptyState
            description="Aucune option à la carte payable n'est actuellement proposée."
            title="Catalogue vide"
          />
        ) : (
          <section
            className="catalog-grid"
            aria-label="Options à la carte payables"
          >
            {aLaCarteOffers.map((offer) => (
              <article className="catalog-card" key={offer.id}>
                <span className="card-kicker">{offer.category}</span>
                <h2>{offer.name}</h2>
                <p className="multiline-text">{offer.description}</p>
                <div className="catalog-scope">
                  <strong>
                    {formatCommercialAmountFromCents(offer.priceAmountCents, {
                      fiscalRegime: offer.fiscalRegime,
                    })}
                  </strong>
                  <span>{offer.unitLabel}</span>
                </div>
                <AddToCartButton offerId={offer.id} />
              </article>
            ))}
          </section>
        )}
      </section>

      <section className="request-history-section">
        <SectionHeading
          action={<StatusBadge label="Sur devis" tone="neutral" />}
          description="Besoin d'une prestation sur mesure non listée ci-dessus ? Faites une demande : elle reste étudiée avant toute activation."
          title="Prestations sur devis"
        />
        {serviceCatalogError ? (
          <ErrorState
            compact
            description="Impossible de charger le catalogue des prestations pour le moment."
            reference={serviceCatalogCorrelationId}
            title="Prestations indisponibles"
          />
        ) : serviceCatalog.length === 0 ? (
          <EmptyState
            description="Aucune prestation à la carte n'est actuellement proposée."
            title="Catalogue vide"
          />
        ) : (
          <section className="catalog-grid" aria-label="Prestations à la carte">
            {serviceCatalog.map((service) => (
              <article className="catalog-card" key={service.id}>
                <span className="card-kicker">{service.category}</span>
                <h2>{service.name}</h2>
                <p className="multiline-text">{service.description}</p>
                <div className="catalog-scope">
                  <span>{service.scope}</span>
                  <strong>{service.commercialTerms}</strong>
                </div>
                <Link
                  className="button"
                  href={`/request-service?service=${encodeURIComponent(service.id)}`}
                >
                  Prendre cette option
                </Link>
              </article>
            ))}
          </section>
        )}
      </section>

      {source !== "unavailable" ? (
        <MockNotice correlationId={catalogCorrelationId} source={source} />
      ) : null}
    </>
  );
}
