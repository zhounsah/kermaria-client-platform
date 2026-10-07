import Link from "next/link";
import type { ReactNode } from "react";
import type { SitePageLayout } from "@kermaria/shared";
import { ManagedMarkdown } from "@/components/ManagedMarkdown";
import { ServiceBreadcrumb } from "@/components/PublicServiceComponents";
import { SitePageFrame } from "@/components/SitePageFrame";
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
  /** Contenu spécifique à une page, inséré avant les explications CMS. */
  beforeSections?: ReactNode;
  /** Variante courte pour une page dont le contenu utile suit immédiatement. */
  compactHero?: boolean;
  heroLead?: string;
  heroTitle?: string;
  showHeroActions?: boolean;
  pageLayout?: SitePageLayout;
};

export function PublicStorefrontPage({
  breadcrumbItems,
  commercialActions = null,
  content,
  beforeSections = null,
  compactHero = false,
  heroLead,
  heroTitle,
  serviceSlug = null,
  selfServiceOrderable = null,
  showHeroActions = true,
  pageLayout,
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

  const intro = <>
        <ServiceBreadcrumb items={breadcrumbItems} />
        <section className={`service-hero${compactHero ? " service-hero-compact storefront-compact-hero" : ""}`}>
          <div>
            <span className="card-kicker">Zachary IT</span>
            <h1>{heroTitle ?? content.title}</h1>
            <p>{heroLead ?? content.lead}</p>
          </div>
          {showHeroActions ? (
            <div className="button-row storefront-action-row">
              <Link className="button" href={primaryAction.href}>{primaryAction.label}</Link>
              {secondaryAction ? (
                <Link className="button button-secondary" href={secondaryAction.href}>
                  {secondaryAction.label}
                </Link>
              ) : null}
            </div>
          ) : null}
        </section>
  </>;
  const explanations = <>{content.sections.map((section) => (
          <section className="service-section storefront-section" key={section.heading}>
            <header className="service-section-heading"><h2>{section.heading}</h2></header>
            <ManagedMarkdown markdown={section.bodyMarkdown} />
          </section>
        ))}</>;
  const faq = <section className="service-section storefront-faq" aria-labelledby="storefront-faq-title">
          <header className="service-section-heading"><h2 id="storefront-faq-title">Questions fréquentes</h2></header>
          <div className="storefront-faq-grid">
            {content.faq.map((item) => (
              <details key={item.question}><summary>{item.question}</summary><p>{item.answer}</p></details>
            ))}
          </div>
        </section>;
  const related = <section className="service-category-proof storefront-related" aria-labelledby="storefront-related-title">
          <div><h2 id="storefront-related-title">Services associés</h2><p>Découvrez le service qui répond à votre besoin ou parlons-en ensemble.</p></div>
          <nav aria-label="Pages associées" className="storefront-link-list">
            {relatedLinks.map((link) => <Link className="service-inline-link" href={link.href} key={link.href}>{link.label}</Link>)}
          </nav>
        </section>;
  const contact = <section className="service-cta">
          <div>
            <h2>{hasFormulaPath ? "Choisissez le parcours adapté." : "Parlons de votre besoin."}</h2>
            <p>
              {hasFormulaPath
                ? "Une offre peut répondre à votre besoin. Si votre situation est particulière, le questionnaire ou un devis vous aidera à choisir."
                : "Expliquez-nous votre situation : nous préciserons les étapes et vous proposerons un devis clair avant de commencer."}
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
        </section>;
  return (
    <>
      <JsonLd data={breadcrumbJsonLd(PUBLIC_SITE_URL, [...breadcrumbItems])} />
      <JsonLd data={!pageLayout || pageLayout.blocks.some((block) => block.widgetKey === "tariffs_faq")
        ? faqPageJsonLd(
          PUBLIC_SITE_URL,
          breadcrumbItems[breadcrumbItems.length - 1]?.path ?? "/",
          content.faq,
        ) : null} />
      <div className={`services-page storefront-page${compactHero ? " storefront-page-compact" : ""}`}>
        {pageLayout ? <SitePageFrame area="public" pageKey={pageLayout.pageKey}
          initialLayout={pageLayout} slots={{
            tariffs_intro: intro,
            tariffs_catalog: beforeSections,
            tariffs_explanations: explanations,
            tariffs_faq: faq,
            tariffs_related: related,
            tariffs_contact: contact,
          }}>{null}</SitePageFrame>
          : <>{intro}{beforeSections}{explanations}{faq}{related}{contact}</>}
      </div>
    </>
  );
}
