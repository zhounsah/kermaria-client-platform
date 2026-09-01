import Link from "next/link";
import type {
  BillingV2PublicCatalog,
  BillingV2PublicPriceComponent,
  BillingV2PublicService,
  BillingV2PublicTier,
} from "@kermaria/shared";

import { ManagedMarkdown } from "@/components/ManagedMarkdown";
import { ServiceBreadcrumb } from "@/components/PublicServiceComponents";
import { describeTierAttributes } from "@/lib/billing-v2-formules";
import {
  contextualizeDiagnosticHref,
  diagnosticContextForServiceSlug,
} from "@/lib/diagnostic-context";
import { formatCurrencyFromCents } from "@/lib/formatters";
import { PUBLIC_SITE_URL } from "@/lib/public-route-config";
import { breadcrumbJsonLd, JsonLd } from "@/lib/seo";
import {
  resolveStorefrontPublicCta,
  resolveStorefrontPublicRelatedLinks,
  type StorefrontBreadcrumbItem,
  type StorefrontCommercialActions,
  type StorefrontPageContent,
  type StorefrontServiceSlug,
} from "@/lib/storefront-content";

const VPS_PRIORITY_CODES = ["VPS-LOCAL", "VPS-CLOUD"] as const;

type PublicVpsServicePageProps = {
  breadcrumbItems: readonly StorefrontBreadcrumbItem[];
  catalog: BillingV2PublicCatalog;
  commercialActions?: StorefrontCommercialActions | null;
  content: StorefrontPageContent;
  selfServiceOrderable?: boolean | null;
  serviceSlug: StorefrontServiceSlug;
};

/**
 * Composition specifique de la landing VPS. Le catalogue Billing V2 public est
 * l'unique source des offres, prix et caracteristiques ; le CMS reste l'autorite
 * du contenu explicatif, des FAQ et des liens associes.
 */
export function PublicVpsServicePage({
  breadcrumbItems,
  catalog,
  commercialActions = null,
  content,
  selfServiceOrderable = null,
  serviceSlug,
}: PublicVpsServicePageProps) {
  const fallbackCta = resolveStorefrontPublicCta(content, selfServiceOrderable);
  const diagnosticContext = diagnosticContextForServiceSlug(serviceSlug);
  const rawPrimaryAction = commercialActions?.primaryAction ?? fallbackCta;
  const primaryAction = {
    ...rawPrimaryAction,
    href: contextualizeDiagnosticHref(rawPrimaryAction.href, diagnosticContext),
  };
  const rawSecondaryAction = commercialActions?.secondaryAction ?? null;
  const secondaryAction = rawSecondaryAction
    ? {
        ...rawSecondaryAction,
        href: contextualizeDiagnosticHref(rawSecondaryAction.href, diagnosticContext),
      }
    : null;
  const relatedLinks = resolveStorefrontPublicRelatedLinks(
    content.relatedLinks,
    selfServiceOrderable,
  );
  const services = catalog.services
    .filter((service) => service.publicVisible && service.code.startsWith("VPS-"))
    .sort(compareVpsServices);

  return (
    <>
      <JsonLd data={breadcrumbJsonLd(PUBLIC_SITE_URL, [...breadcrumbItems])} />
      <main className="services-page storefront-page vps-storefront-page">
        <ServiceBreadcrumb items={breadcrumbItems} />

        <section className="service-hero vps-storefront-hero" aria-labelledby="vps-page-title">
          <div>
            <span className="card-kicker">Infrastructure VPS</span>
            <h1 id="vps-page-title">{content.title}</h1>
            <p>{content.lead}</p>
          </div>
          <div className="button-row storefront-action-row">
            <Link className="button" href={primaryAction.href}>{primaryAction.label}</Link>
            {secondaryAction ? (
              <Link className="button button-secondary" href={secondaryAction.href}>
                {secondaryAction.label}
              </Link>
            ) : null}
          </div>
        </section>

        <section className="service-section vps-catalog" aria-labelledby="vps-catalog-title">
          <header className="service-section-heading vps-catalog-intro">
            <span className="card-kicker">Offres VPS</span>
            <h2 id="vps-catalog-title">Choisissez la gamme adaptée à votre projet</h2>
            <p>
              Comparez les caractéristiques et les composantes tarifaires de chaque
              palier avant de préparer votre configuration.
            </p>
          </header>
          {services.length ? (
            services.map((service) => <VpsServiceOffers key={service.code} service={service} />)
          ) : (
            <p className="service-empty-state">
              Les offres VPS publiques sont temporairement indisponibles.
            </p>
          )}
        </section>

        {content.sections.map((section) => (
          <section className="service-section storefront-section" key={section.heading}>
            <header className="service-section-heading"><h2>{section.heading}</h2></header>
            <ManagedMarkdown markdown={section.bodyMarkdown} />
          </section>
        ))}

        <section className="service-section storefront-section vps-choice-help" aria-labelledby="vps-choice-help-title">
          <header className="service-section-heading">
            <h2 id="vps-choice-help-title">Besoin d’aide pour choisir ?</h2>
          </header>
          <p>
            Comparez les caractéristiques de chaque palier et contactez-nous si vous
            souhaitez valider votre besoin technique avant de commander.
          </p>
          {secondaryAction ? (
            <Link className="service-inline-link" href={secondaryAction.href}>
              {secondaryAction.label}
            </Link>
          ) : (
            <Link className="service-inline-link" href={primaryAction.href}>
              {primaryAction.label}
            </Link>
          )}
        </section>

        <section className="service-section storefront-faq" aria-labelledby="vps-faq-title">
          <header className="service-section-heading"><h2 id="vps-faq-title">Questions fréquentes</h2></header>
          <div className="storefront-faq-grid">
            {content.faq.map((item) => (
              <details key={item.question}><summary>{item.question}</summary><p>{item.answer}</p></details>
            ))}
          </div>
        </section>

        <section className="service-category-proof storefront-related" aria-labelledby="vps-related-title">
          <div><h2 id="vps-related-title">Services associés</h2><p>Explorez le service correspondant à votre besoin ou demandez un cadrage.</p></div>
          <nav aria-label="Pages associées" className="storefront-link-list">
            {relatedLinks.map((link) => <Link className="service-inline-link" href={link.href} key={link.href}>{link.label}</Link>)}
          </nav>
        </section>

        <section className="service-cta" aria-labelledby="vps-final-cta-title">
          <div>
            <h2 id="vps-final-cta-title">Prêt à préparer votre VPS ?</h2>
            <p>
              Notre équipe peut vous aider à confirmer la configuration adaptée à votre usage.
            </p>
          </div>
          <div className="button-row storefront-action-row">
            <Link
              className={secondaryAction ? "button" : "button button-secondary"}
              href={primaryAction.href}
            >
              {primaryAction.label}
            </Link>
            {secondaryAction ? (
              <Link className="button button-secondary" href={secondaryAction.href}>
                {secondaryAction.label}
              </Link>
            ) : null}
          </div>
        </section>
      </main>
    </>
  );
}

function VpsServiceOffers({ service }: { service: BillingV2PublicService }) {
  const publicTiers = service.tiers.filter((tier) => tier.publicSelectable);

  return (
    <section className="vps-service" aria-labelledby={`vps-service-${service.code}`}>
      <header>
        <h2 id={`vps-service-${service.code}`}>{service.name}</h2>
        {service.description ? <p>{service.description}</p> : null}
      </header>
      {publicTiers.length ? (
        <div className="service-offer-grid">
          {publicTiers.map((tier) => <VpsTierCard key={tier.code} service={service} tier={tier} />)}
        </div>
      ) : (
        <p className="vps-service-empty-state">
          Aucun palier de cette gamme n’est actuellement disponible publiquement.
        </p>
      )}
    </section>
  );
}

function VpsTierCard({
  service,
  tier,
}: {
  service: BillingV2PublicService;
  tier: BillingV2PublicTier;
}) {
  const specifications = describeTierAttributes(tier);
  const setupFees = (tier.priceComponents ?? []).filter(isInitialSetupFee);
  const href = `/services/vps/choisir?serviceCode=${encodeURIComponent(service.code)}&tierCode=${encodeURIComponent(tier.code)}`;
  const description = tier.description ?? service.description;

  return (
    <article className="service-offer-card vps-tier-card">
      <p className="vps-tier-availability">Disponible</p>
      <h3>{service.name} — {tier.label}</h3>
      {description ? <p className="vps-tier-description">{description}</p> : null}
      <p className="vps-tier-monthly-price">
        {formatCurrencyFromCents(tier.monthlyAmountCents)} <span>/ mois</span>
      </p>
      {setupFees.map((fee, index) => (
        <p className="vps-tier-setup" key={`${fee.priceCode ?? index}-${fee.amountCents}`}>
          + {formatCurrencyFromCents(fee.amountCents)} de frais de mise en service
        </p>
      ))}
      {specifications.length ? (
        <ul className="vps-tier-specifications" aria-label={`Caractéristiques ${tier.label}`}>
          {specifications.map((specification) => <li key={specification}>{specification}</li>)}
        </ul>
      ) : null}
      <Link className="button vps-tier-cta" href={href}>Configurer et commander</Link>
    </article>
  );
}

function compareVpsServices(a: BillingV2PublicService, b: BillingV2PublicService) {
  return vpsRank(a) - vpsRank(b) || a.name.localeCompare(b.name, "fr");
}

function vpsRank(service: BillingV2PublicService) {
  const rank = VPS_PRIORITY_CODES.indexOf(service.code as (typeof VPS_PRIORITY_CODES)[number]);
  return rank === -1 ? VPS_PRIORITY_CODES.length : rank;
}

function isInitialSetupFee(component: BillingV2PublicPriceComponent) {
  return component.billingCadence === "one_time"
    && component.chargeTrigger === "initial_subscription";
}
