"use client";

import Link from "next/link";
import { useState } from "react";

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
import { SectionHeading } from "@/components/SectionHeading";
import { StatusBadge } from "@/components/StatusBadge";
import { formatCurrencyFromCents } from "@/lib/formatters";
import { findPackPresentation } from "@/lib/public-packs";

type SubscribeCatalogSectionsProps = {
  aLaCarteOffers: CommercialOfferSummary[];
  catalogCorrelationId?: CorrelationId;
  catalogError: boolean;
  checkout: CheckoutSummary;
  commercialCatalogError: boolean;
  packContent: PublicPackCatalogContent | null;
  packs: ResolvedPublicPackManifest[];
  serviceCatalog: ServiceCatalogItem[];
  serviceCatalogError: boolean;
  source: DataSource;
};

export function SubscribeCatalogSections({
  aLaCarteOffers,
  catalogCorrelationId,
  catalogError,
  checkout,
  commercialCatalogError,
  packContent,
  packs,
  serviceCatalog,
  serviceCatalogError,
  source,
}: SubscribeCatalogSectionsProps) {
  const [selectedPackOffers, setSelectedPackOffers] = useState<
    Record<string, string>
  >(() =>
    Object.fromEntries(
      packs.map((pack) => [pack.key, getDefaultPackOfferId(pack)]),
    ),
  );

  return (
    <>
      <PageHeader
        action={
          <Link className="button button-ghost" href="/services">
            Retour a mes services
          </Link>
        }
        description="Composez ici vos achats ponctuels et vos packs recurrents. Le recapitulatif reste commun, mais chaque famille suit ensuite sa validation propre."
        eyebrow="Ajouter un service"
        title="Souscrire a une offre"
      />
      {checkout.totalItemCount > 0 ? (
        <section className="checkout-access-banner">
          <div>
            <span className="card-kicker">Recapitulatif commun</span>
            <h2>
              {checkout.cart.itemCount} achat(s) ponctuel(s) et{" "}
              {checkout.recurring.itemCount} abonnement(s) en cours
            </h2>
            <p>
              Montant immediat estime a{" "}
              <strong>
                {formatCurrencyFromCents(
                  checkout.cart.subtotalCents + checkout.recurring.subtotalCents,
                )}
              </strong>
              . Vous gardez un seul recapitulatif visuel, puis une confirmation
              separee pour chaque type d&apos;achat.
            </p>
          </div>
          <Link className="button" href="/panier">
            Voir mon panier
          </Link>
        </section>
      ) : null}

      <section className="request-history-section">
        <SectionHeading
          action={<StatusBadge label="Abonnements factures" tone="info" />}
          description="Choisissez votre pack, ajoutez-le a la reprise recurrente, puis confirmez une facture de premier terme avant le reglement et l'activation."
          title="Packs recurrents"
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
            description="Aucun pack grand public n'est actuellement affiche dans le portail."
            title="Catalogue vide"
          />
        ) : (
          <section className="public-pack-grid" aria-label="Packs grand public">
            {packs.map((pack) => (
              <article className="public-pack-card" key={pack.key}>
                {(() => {
                  const defaultOfferId = getDefaultPackOfferId(pack);
                  const selectedOfferId =
                    selectedPackOffers[pack.key] ?? defaultOfferId;

                  return (
                    <>
                <header className="public-pack-header">
                  <div className="public-pack-header-copy">
                    <p className="card-kicker">Pack grand public</p>
                    <h2>{pack.label}</h2>
                    <p className="public-pack-audience">{pack.audience}</p>
                  </div>
                  {findPackPresentation(pack.key, packContent)?.highlightLabel ? (
                    <span className="status-badge status-badge-info">
                      {findPackPresentation(pack.key, packContent)?.highlightLabel}
                    </span>
                  ) : null}
                </header>

                <p className="public-pack-headline">{pack.headline}</p>
                <p className="public-pack-description">{pack.description}</p>

                <div className="public-pack-pricing">
                  <div className="public-pack-price-main">
                    <strong>
                      {formatCurrencyFromCents(
                        pack.variantsByCommitment[1].monthly.monthlyPriceAmountCents,
                      )}
                    </strong>
                    <span>HT / mois</span>
                  </div>
                  <span className="public-pack-discount">
                    Formules 1, 6 et 12 mois disponibles
                  </span>
                </div>

                <div className="public-pack-columns">
                  <div>
                    <h3>Inclus</h3>
                    <ul>
                      {pack.included.map((item) => (
                        <li key={item}>{item}</li>
                      ))}
                    </ul>
                  </div>
                  <div>
                    <h3>Differences cles</h3>
                    <ul>
                      {pack.highlights.map((item) => (
                        <li key={item}>{item}</li>
                      ))}
                    </ul>
                  </div>
                </div>

                <div className="public-pack-cta">
                  <div>
                    <label
                      className="public-pack-control-label"
                      htmlFor={`offer-${pack.key}`}
                    >
                      Formule
                    </label>
                    <select
                      id={`offer-${pack.key}`}
                      name="offerId"
                      onChange={(event) =>
                        setSelectedPackOffers((current) => ({
                          ...current,
                          [pack.key]: event.target.value,
                        }))
                      }
                      value={selectedOfferId}
                    >
                      <option value={pack.variantsByCommitment[1].monthly.offer.id}>
                        1 mois - Mensuel -{" "}
                        {formatCurrencyFromCents(
                          pack.variantsByCommitment[1].monthly.billingPriceAmountCents,
                        )}{" "}
                        HT/mois
                      </option>
                      <option value={pack.variantsByCommitment[6].monthly.offer.id}>
                        6 mois - Mensuel -{" "}
                        {formatCurrencyFromCents(
                          pack.variantsByCommitment[6].monthly.billingPriceAmountCents,
                        )}{" "}
                        HT/mois
                      </option>
                      {pack.variantsByCommitment[6].upfront ? (
                        <option value={pack.variantsByCommitment[6].upfront.offer.id}>
                          6 mois - Comptant -{" "}
                          {formatCurrencyFromCents(
                            pack.variantsByCommitment[6].upfront.billingPriceAmountCents,
                          )}{" "}
                          HT / 6 mois
                        </option>
                      ) : null}
                      <option value={pack.variantsByCommitment[12].monthly.offer.id}>
                        12 mois - Mensuel -{" "}
                        {formatCurrencyFromCents(
                          pack.variantsByCommitment[12].monthly.billingPriceAmountCents,
                        )}{" "}
                        HT/mois
                      </option>
                      {pack.variantsByCommitment[12].upfront ? (
                        <option value={pack.variantsByCommitment[12].upfront.offer.id}>
                          12 mois - Comptant -{" "}
                          {formatCurrencyFromCents(
                            pack.variantsByCommitment[12].upfront.billingPriceAmountCents,
                          )}{" "}
                          HT / 12 mois
                        </option>
                      ) : null}
                    </select>
                  </div>
                  <AddRecurringCheckoutButton offerId={selectedOfferId} />
                  <Link className="text-link" href={`/offres/${pack.slug}`}>
                    Voir la fiche technique
                  </Link>
                </div>
                    </>
                  );
                })()}
              </article>
            ))}
          </section>
        )}
      </section>

      <section className="request-history-section">
        <SectionHeading
          action={<StatusBadge label="Paiement immediat" tone="success" />}
          description="Ajoutez une ou plusieurs prestations ponctuelles, puis validez une commande unique reglable en une fois."
          title="Achats ponctuels"
        />
        {commercialCatalogError ? (
          <ErrorState
            compact
            description="Impossible de charger les options a la carte pour le moment."
            reference={catalogCorrelationId}
            title="Options indisponibles"
          />
        ) : aLaCarteOffers.length === 0 ? (
          <EmptyState
            description="Aucune option a la carte payable n'est actuellement proposee."
            title="Catalogue vide"
          />
        ) : (
          <section
            className="catalog-grid"
            aria-label="Options a la carte payables"
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
          description="Besoin d'une prestation sur mesure non listee ci-dessus ? Faites une demande : elle reste etudiee avant toute activation."
          title="Prestations sur devis"
        />
        {serviceCatalogError ? (
          <ErrorState
            compact
            description="Impossible de charger le catalogue des prestations pour le moment."
            reference={catalogCorrelationId}
            title="Prestations indisponibles"
          />
        ) : serviceCatalog.length === 0 ? (
          <EmptyState
            description="Aucune prestation a la carte n'est actuellement proposee."
            title="Catalogue vide"
          />
        ) : (
          <section className="catalog-grid" aria-label="Prestations a la carte">
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

function getDefaultPackOfferId(pack: ResolvedPublicPackManifest) {
  return pack.variantsByCommitment[1].monthly.offer.id;
}
