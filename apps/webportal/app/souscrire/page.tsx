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
  getCart,
  getCommercialCatalog,
  getPendingPackSelection,
  getPublicCommercialCatalog,
  getPublicPackCatalogContent,
  getServiceCatalog,
  resolveDataSource,
} from "@/lib/internal-api";
import {
  findPendingPackSelectionForPack,
  findPackPresentation,
  resolvePackCatalog,
} from "@/lib/public-packs";
import { getPayPalMode } from "@/lib/paypal";
import { getStripeMode } from "@/lib/stripe";

export const metadata = {
  title: "Souscrire",
};

export const dynamic = "force-dynamic";

export default async function SubscribePage() {
  await requireClientSession();
  const [
    catalogResult,
    packContentResult,
    pendingSelectionResult,
    serviceCatalogResult,
    commercialCatalogResult,
    cartResult,
  ] = await Promise.all([
    getPublicCommercialCatalog(),
    getPublicPackCatalogContent(),
    getPendingPackSelection(),
    getServiceCatalog(),
    getCommercialCatalog(),
    getCart(),
  ]);
  const source = resolveDataSource([
    catalogResult.source,
    packContentResult.source,
    pendingSelectionResult.source,
    serviceCatalogResult.source,
    commercialCatalogResult.source,
    cartResult.source,
  ]);
  const packs = resolvePackCatalog(catalogResult.data, packContentResult.data);
  const pendingSelection = pendingSelectionResult.data;
  const stripeMode = getStripeMode();
  const paypalMode = getPayPalMode();

  // V0.35 : options a la carte payables = offres one-shot actives, hors packs.
  const aLaCarteOffers = commercialCatalogResult.data
    .filter(
      (offer) =>
        offer.status === "active"
        && offer.billingCadence === "one_time"
        && offer.publicPackCode === null
        && offer.priceAmountCents > 0,
    )
    .sort((a, b) => a.displayOrder - b.displayOrder);
  const cart = cartResult.data;

  return (
    <>
      <PageHeader
        action={
          <Link className="button button-ghost" href="/services">
            Retour à mes services
          </Link>
        }
        description="Choisissez un pack grand public clé en main, ou prenez une prestation à la carte sans engagement de pack."
        eyebrow="Ajouter un service"
        title="Souscrire à une offre"
      />

      {cart.itemCount > 0 ? (
        <div className="cart-access-bar">
          <span className="cart-access-count">
            {cart.itemCount} option{cart.itemCount > 1 ? "s" : ""} dans votre
            panier
          </span>
          <Link className="button" href="/panier">
            Voir mon panier
          </Link>
        </div>
      ) : null}

      <section className="request-history-section">
        <SectionHeading
          action={<StatusBadge label="Packs grand public" tone="info" />}
          description="Une offre complète, à tarification lisible, avec l'engagement et le mode de paiement de votre choix."
          title="Souscrire à un pack"
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
                initialSelection={findPendingPackSelectionForPack(
                  pendingSelection,
                  pack.key,
                )}
                stripeMode={stripeMode}
                paypalMode={paypalMode}
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
          description="Ajoutez une ou plusieurs prestations ponctuelles à votre panier, puis réglez le tout en une seule commande (carte bancaire, PayPal ou virement). Aucune validation préalable n'est requise."
          title="Options à la carte"
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
                <p>{offer.description}</p>
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
          description="Besoin d'une prestation sur mesure, non listée ci-dessus ? Faites une demande : elle est étudiée avant toute activation et fait l'objet d'un devis."
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
                <p>{service.description}</p>
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
