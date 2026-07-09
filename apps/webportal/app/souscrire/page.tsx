import Link from "next/link";

import { AddToCartButton } from "@/components/AddToCartButton";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { PublicPackCard } from "@/components/PublicPackCard";
import { SectionHeading } from "@/components/SectionHeading";
import { StatusBadge } from "@/components/StatusBadge";
import { requireClientSession } from "@/lib/auth";
import { formatCurrencyFromCents } from "@/lib/formatters";
import {
  getCheckoutSummary,
  getCommercialCatalog,
  getPublicCommercialCatalog,
  getPublicPackCatalogContent,
  getServiceCatalog,
  resolveDataSource,
} from "@/lib/internal-api";
import {
  findPackPresentation,
  resolvePackCatalog,
} from "@/lib/public-packs";

export const metadata = {
  title: "Souscrire",
};

export const dynamic = "force-dynamic";

export default async function SubscribePage() {
  await requireClientSession();
  const [
    catalogResult,
    packContentResult,
    serviceCatalogResult,
    commercialCatalogResult,
    checkoutResult,
  ] = await Promise.all([
    getPublicCommercialCatalog(),
    getPublicPackCatalogContent(),
    getServiceCatalog(),
    getCommercialCatalog(),
    getCheckoutSummary(),
  ]);
  const source = resolveDataSource([
    catalogResult.source,
    packContentResult.source,
    serviceCatalogResult.source,
    commercialCatalogResult.source,
    checkoutResult.source,
  ]);
  const packs = resolvePackCatalog(catalogResult.data, packContentResult.data);

  const aLaCarteOffers = commercialCatalogResult.data
    .filter(
      (offer) =>
        offer.status === "active"
        && offer.billingCadence === "one_time"
        && offer.publicPackCode === null
        && offer.priceAmountCents > 0,
    )
    .sort((a, b) => a.displayOrder - b.displayOrder);
  const checkout = checkoutResult.data;

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
            <span className="card-kicker">Panier unifié</span>
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
        {catalogResult.error ? (
          <ErrorState
            compact
            description="Impossible de charger le catalogue packs pour le moment."
            reference={catalogResult.correlationId}
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
                mode="subscribe"
                pack={pack}
                initialSelection={null}
                highlightLabel={findPackPresentation(
                  pack.key,
                  packContentResult.data,
                )?.highlightLabel}
              />
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
        {commercialCatalogResult.error ? (
          <ErrorState
            compact
            description="Impossible de charger les options à la carte pour le moment."
            reference={commercialCatalogResult.correlationId}
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
                    {formatCurrencyFromCents(offer.priceAmountCents)} HT
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
        {serviceCatalogResult.error ? (
          <ErrorState
            compact
            description="Impossible de charger le catalogue des prestations pour le moment."
            reference={serviceCatalogResult.correlationId}
            title="Prestations indisponibles"
          />
        ) : serviceCatalogResult.data.length === 0 ? (
          <EmptyState
            description="Aucune prestation à la carte n'est actuellement proposée."
            title="Catalogue vide"
          />
        ) : (
          <section className="catalog-grid" aria-label="Prestations à la carte">
            {serviceCatalogResult.data.map((service) => (
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
        <MockNotice
          correlationId={catalogResult.correlationId}
          source={source}
        />
      ) : null}
    </>
  );
}
