import Link from "next/link";
import { ManagedMarkdown } from "@/components/ManagedMarkdown";
import { ServiceBreadcrumb } from "@/components/PublicServiceComponents";
import {
  contextualizeDiagnosticHref,
  diagnosticContextForServiceSlug,
} from "@/lib/diagnostic-context";
import { PUBLIC_SITE_URL } from "@/lib/public-route-config";
import { breadcrumbJsonLd, faqPageJsonLd, JsonLd } from "@/lib/seo";
import {
  resolveStorefrontPublicCta,
  resolveStorefrontPublicRelatedLinks,
  type StorefrontBreadcrumbItem,
  type StorefrontCommercialActions,
  type StorefrontPageContent,
  type StorefrontServiceSlug,
} from "@/lib/storefront-content";

type PublicStorefrontPageProps = {
  breadcrumbItems: readonly StorefrontBreadcrumbItem[];
  commercialActions?: StorefrontCommercialActions | null;
  content: StorefrontPageContent;
  serviceSlug?: StorefrontServiceSlug | null;
  selfServiceOrderable?: boolean | null;
};

export function PublicStorefrontPage({
  breadcrumbItems,
  commercialActions = null,
  content,
  serviceSlug = null,
  selfServiceOrderable = null,
}: PublicStorefrontPageProps) {
  const fallbackCta = resolveStorefrontPublicCta(content, selfServiceOrderable);
  const diagnosticContext = serviceSlug
    ? diagnosticContextForServiceSlug(serviceSlug)
    : "general";
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
  const hasFormulaPath = commercialActions?.mode === "FORMULA"
    || commercialActions?.mode === "HYBRID";
  const relatedLinks = resolveStorefrontPublicRelatedLinks(
    content.relatedLinks,
    selfServiceOrderable,
  );

  return (
    <>
      <JsonLd data={breadcrumbJsonLd(PUBLIC_SITE_URL, [...breadcrumbItems])} />
      <JsonLd
        data={faqPageJsonLd(
          PUBLIC_SITE_URL,
          breadcrumbItems[breadcrumbItems.length - 1]?.path ?? "/",
          content.faq,
        )}
      />
      <div className="services-page storefront-page">
        <ServiceBreadcrumb items={breadcrumbItems} />

        <section className="service-hero">
          <div>
            <span className="card-kicker">Zachary IT</span>
            <h1>{content.title}</h1>
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

        {content.sections.map((section) => (
          <section className="service-section storefront-section" key={section.heading}>
            <header className="service-section-heading"><h2>{section.heading}</h2></header>
            <ManagedMarkdown markdown={section.bodyMarkdown} />
          </section>
        ))}

        <section className="service-section storefront-faq" aria-labelledby="storefront-faq-title">
          <header className="service-section-heading"><h2 id="storefront-faq-title">Questions fréquentes</h2></header>
          <div className="storefront-faq-grid">
            {content.faq.map((item) => (
              <details key={item.question}><summary>{item.question}</summary><p>{item.answer}</p></details>
            ))}
          </div>
        </section>

        <section className="service-category-proof storefront-related" aria-labelledby="storefront-related-title">
          <div><h2 id="storefront-related-title">Services associés</h2><p>Explorez le service correspondant à votre besoin ou demandez un cadrage.</p></div>
          <nav aria-label="Pages associées" className="storefront-link-list">
            {relatedLinks.map((link) => <Link className="service-inline-link" href={link.href} key={link.href}>{link.label}</Link>)}
          </nav>
        </section>

        <section className="service-cta">
          <div>
            <h2>{hasFormulaPath ? "Choisissez le parcours adapté." : "Parlons de votre besoin."}</h2>
            <p>
              {hasFormulaPath
                ? "Une formule couvre le besoin standard. Pour un environnement existant ou un périmètre particulier, passez par le diagnostic ou le devis."
                : "Un devis ou un audit permet de confirmer le périmètre, les prérequis et les limites avant mise en service."}
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
      </div>
    </>
  );
}
