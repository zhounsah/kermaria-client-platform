"use client";

import { useState } from "react";
import Link from "next/link";
import type {
  PublicCommercialCatalog,
  PublicCommercialMoney,
  PublicCommercialService,
} from "@kermaria/shared";

import { BillingV2DirectCartAdd } from "@/components/BillingV2DirectCartAdd";

type PublicCommercialTariffCatalogProps = {
  catalog: PublicCommercialCatalog;
  currency: string;
};

export function PublicCommercialTariffCatalog({
  catalog,
  currency,
}: PublicCommercialTariffCatalogProps) {
  const categories = [...new Set(catalog.services.map((service) => service.category))];
  const [selectedCategory, setSelectedCategory] = useState("all");
  const visibleServices = selectedCategory === "all"
    ? catalog.services
    : catalog.services.filter((service) => service.category === selectedCategory);

  if (catalog.services.length === 0) {
    return (
      <section className="service-section commercial-tariffs-empty" aria-labelledby="commercial-tariffs-title">
        <header className="service-section-heading">
          <h2 id="commercial-tariffs-title">Tarifs temporairement indisponibles</h2>
          <p>Nous ne pouvons pas afficher le catalogue actuel sans une source tarifaire fiable.</p>
        </header>
        <Link className="button button-secondary" href="/contact">Nous contacter</Link>
      </section>
    );
  }

  return (
    <section className="service-section commercial-tariffs" aria-labelledby="commercial-tariffs-title">
      <header className="service-section-heading commercial-tariffs-heading">
        <span className="card-kicker">Catalogue des services</span>
        <h2 id="commercial-tariffs-title">Catalogue tarifaire</h2>
      </header>

      {categories.length > 1 ? (
        <div className="commercial-tariffs-filters" aria-label="Filtrer les services par besoin">
          <button
            aria-pressed={selectedCategory === "all"}
            className={selectedCategory === "all" ? "is-selected" : undefined}
            onClick={() => setSelectedCategory("all")}
            type="button"
          >
            Tous les services
          </button>
          {categories.map((category) => (
            <button
              aria-pressed={selectedCategory === category}
              className={selectedCategory === category ? "is-selected" : undefined}
              key={category}
              onClick={() => setSelectedCategory(category)}
              type="button"
            >
              {category}
            </button>
          ))}
        </div>
      ) : null}

      <p className="commercial-tariffs-tax-notice">{catalog.taxNotice}</p>

      <div className="commercial-tariffs-groups">
        {groupServicesByCategory(visibleServices).map(([category, services]) => (
          <section className="commercial-tariffs-group" key={category} aria-labelledby={`commercial-category-${slug(category)}`}>
            <h3 id={`commercial-category-${slug(category)}`}>{category}</h3>
            <div className="commercial-tariffs-grid">
              {services.map((service) => (
                <CommercialTariffCard
                  currency={currency}
                  key={service.id}
                  service={service}
                  taxNotice={catalog.taxNotice}
                />
              ))}
            </div>
          </section>
        ))}
      </div>
    </section>
  );
}

function CommercialTariffCard({
  currency,
  service,
  taxNotice,
}: {
  currency: string;
  service: PublicCommercialService;
  taxNotice: string;
}) {
  return (
    <article className="commercial-tariff-card">
      <div className="commercial-tariff-card-heading">
        <h4>{service.name}</h4>
        {service.description ? <p>{service.description}</p> : null}
      </div>

      <PriceSummary service={service} taxNotice={taxNotice} />

      {service.tiers.length > 0 ? (
        <details className="commercial-tariff-tiers">
          <summary>Voir les options disponibles</summary>
          <ul>
            {service.tiers.map((tier) => (
              <li key={tier.id}>
                <span>
                  <strong>{tier.label}</strong>
                  {tier.description ? ` — ${tier.description}` : ""}
                  {tier.details.length > 0 ? ` (${tier.details.join(", ")})` : ""}
                </span>
                <span>{tier.recurringPrice ? `${formatMoney(tier.recurringPrice)} / mois` : "Sur devis"}</span>
                {tier.initialFees.map((fee, index) => (
                  <span className="commercial-tariff-tier-fee" key={`${tier.id}-fee-${index}`}>
                    + {formatMoney(fee)} de mise en service
                  </span>
                ))}
              </li>
            ))}
          </ul>
        </details>
      ) : null}

      {service.initialFees.length === 0
        && service.tiers.some((tier) => tier.initialFees.length > 0) ? (
          <p className="commercial-tariff-tier-fee-notice">
            Des frais de mise en service s’appliquent selon le palier choisi.
          </p>
        ) : null}

      <div className="commercial-tariff-status">
        {orderingStatusLabel(service)}
      </div>

      <div className="commercial-tariff-actions">
        {service.directlyOrderable ? (
          <div id={`ajouter-${slug(service.id)}`}>
            <BillingV2DirectCartAdd currency={currency} service={service} />
          </div>
        ) : (
          <Link className="button" href={service.primaryCta.href}>{service.primaryCta.label}</Link>
        )}
        {service.secondaryCta ? (
          <Link className="service-inline-link" href={service.secondaryCta.href}>
            {service.secondaryCta.label}
          </Link>
        ) : null}
      </div>
    </article>
  );
}

function PriceSummary({
  service,
  taxNotice,
}: {
  service: PublicCommercialService;
  taxNotice: string;
}) {
  if (service.priceType === "quote" || !service.startingPrice) {
    return <p className="commercial-tariff-price commercial-tariff-price-quote">Sur devis</p>;
  }

  const prefix = service.priceType === "from" ? "À partir de " : "";
  return (
    <div className="commercial-tariff-price-block">
      <p className="commercial-tariff-price">
        {prefix}{formatMoney(service.startingPrice)} <span>{priceUnit(service)}</span>
      </p>
      {service.initialFees.map((fee, index) => (
        <p className="commercial-tariff-fee" key={`fee-${index}`}>
          + {formatMoney(fee)} de frais de mise en service
        </p>
      ))}
      <p className="commercial-tariff-tax">{taxNotice}</p>
    </div>
  );
}

function orderingStatusLabel(service: PublicCommercialService) {
  if (service.orderingMode === "direct") return "Commande en ligne disponible";
  if (service.orderingMode === "offer_component") {
    return service.offerCount > 1
      ? "Disponible dans plusieurs offres"
      : "Disponible dans une offre";
  }
  return "Périmètre confirmé avant mise en service";
}

function groupServicesByCategory(
  services: readonly PublicCommercialService[],
): Array<[string, PublicCommercialService[]]> {
  const grouped = new Map<string, PublicCommercialService[]>();
  for (const service of services) {
    const entries = grouped.get(service.category) ?? [];
    entries.push(service);
    grouped.set(service.category, entries);
  }

  return [...grouped.entries()].map(([category, entries]) => [
    category,
    [...entries].sort((left, right) => left.displayOrder - right.displayOrder),
  ]);
}

function formatMoney(money: PublicCommercialMoney) {
  return new Intl.NumberFormat("fr-FR", {
    style: "currency",
    currency: money.currency,
  }).format(money.amountCents / 100);
}

function priceUnit(service: PublicCommercialService) {
  return service.billingUnitLabel === "par utilisateur et par mois"
    ? "/ utilisateur / mois"
    : "/ mois";
}

function slug(value: string) {
  return value.toLocaleLowerCase("fr-FR").replaceAll(/[^a-z0-9]+/gi, "-").replaceAll(/^-|-$/g, "");
}
