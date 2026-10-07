import Link from "next/link";
import type { SitePageLayout } from "@kermaria/shared";

import { ManagedMarkdown } from "@/components/ManagedMarkdown";
import { SitePageFrame } from "@/components/SitePageFrame";
import {
  ServiceBreadcrumb,
  ServiceCategoryCard,
} from "@/components/PublicServiceComponents";
import {
  SERVICE_CATEGORY_BY_SLUG,
  type ServiceCategory,
} from "@/lib/public-services";
import { PUBLIC_SITE_URL } from "@/lib/public-route-config";
import { breadcrumbJsonLd, faqPageJsonLd, JsonLd } from "@/lib/seo";
import {
  resolveStorefrontPublicCta,
  type StorefrontBreadcrumbItem,
  type StorefrontServicesLandingContent,
} from "@/lib/storefront-content";

type PublicServicesLandingPageProps = {
  breadcrumbItems: readonly StorefrontBreadcrumbItem[];
  content: StorefrontServicesLandingContent;
  pageLayout?: SitePageLayout;
};

export function PublicServicesLandingPage({
  breadcrumbItems,
  content,
  pageLayout,
}: PublicServicesLandingPageProps) {
  const primaryAction = resolveStorefrontPublicCta(content, false);
  const categories = content.relatedLinks.map((link) => {
    const slug = link.href.slice("/services/".length) as ServiceCategory["slug"];
    const category = SERVICE_CATEGORY_BY_SLUG[slug];
    return {
      ...category,
      shortTitle: link.label,
    };
  });

  const intro = <>
        <ServiceBreadcrumb items={breadcrumbItems} />
        <section className="service-hero">
          <div>
            <span className="card-kicker">Zachary IT</span>
            <h1>{content.title}</h1>
            <p>{content.lead}</p>
          </div>
          <div className="button-row storefront-action-row">
            <Link className="button" href={primaryAction.href}>
              {primaryAction.label}
            </Link>
          </div>
        </section>
  </>;
  const needs = <section
          aria-labelledby="services-problems-title"
          className="service-section service-problem-routing"
        >
          <header className="service-section-heading">
            <span className="card-kicker">Votre besoin</span>
            <h2 id="services-problems-title">Quel problème cherchez-vous à résoudre ?</h2>
            <p>
              Partez de la situation que vous rencontrez. Chaque entrée vous mène
              vers le service ou le conseil le plus utile pour avancer.
            </p>
          </header>
          <div className="service-overview-grid services-problem-grid">
            {content.problemEntries.map((entry) => (
              <article key={entry.href}>
                <h3>{entry.title}</h3>
                <p>{entry.description}</p>
                <Link
                  aria-label={`Voir le bon point de départ : ${entry.title}`}
                  className="service-inline-link"
                  href={entry.href}
                >
                  Voir le bon point de départ
                </Link>
              </article>
            ))}
          </div>
        </section>;
  const categoriesSection = <section
          aria-labelledby="services-categories-title"
          className="service-section service-main-services"
        >
          <header className="service-section-heading">
            <span className="card-kicker">Domaines d&apos;intervention</span>
            <h2 id="services-categories-title">Les services Zachary IT</h2>
            <p>
              Ces quatre familles regroupent les services selon ce qu&apos;ils
              apportent. Choisissez d&apos;abord votre besoin, puis découvrez
              les solutions possibles.
            </p>
          </header>
          <div className="service-category-grid">
            {categories.map((category) => (
              <ServiceCategoryCard category={category} key={category.slug} />
            ))}
          </div>
        </section>;
  const explanations = <>{content.sections.map((section) => (
          <section className="service-section storefront-section" key={section.heading}>
            <header className="service-section-heading">
              <h2>{section.heading}</h2>
            </header>
            <ManagedMarkdown markdown={section.bodyMarkdown} />
          </section>
        ))}</>;
  const faq = <section
          aria-labelledby="services-faq-title"
          className="service-section storefront-faq"
        >
          <header className="service-section-heading">
            <h2 id="services-faq-title">Questions fréquentes</h2>
          </header>
          <div className="storefront-faq-grid">
            {content.faq.map((item) => (
              <details key={item.question}>
                <summary>{item.question}</summary>
                <p>{item.answer}</p>
              </details>
            ))}
          </div>
        </section>;
  const contact = <section className="service-cta">
          <div>
            <h2>Vous ne savez pas quel service choisir ?</h2>
            <p>
              Répondez à quelques questions pour trouver un premier point de
              départ. Vous pouvez aussi nous décrire votre besoin directement.
            </p>
          </div>
          <div className="button-row storefront-action-row">
            <Link className="button" href="/diagnostic">Faire le diagnostic</Link>
            <Link className="button button-secondary" href="/contact">Nous contacter</Link>
          </div>
        </section>;
  return (
    <>
      <JsonLd data={breadcrumbJsonLd(PUBLIC_SITE_URL, [...breadcrumbItems])} />
      <JsonLd data={!pageLayout || pageLayout.blocks.some((block) => block.widgetKey === "services_faq")
        ? faqPageJsonLd(
          PUBLIC_SITE_URL,
          breadcrumbItems[breadcrumbItems.length - 1]?.path ?? "/",
          content.faq,
        ) : null} />
      <div className="services-page storefront-page services-landing-page">
        {pageLayout ? <SitePageFrame area="public" pageKey="/services"
          initialLayout={pageLayout} slots={{
            services_intro: intro,
            services_needs: needs,
            services_categories: categoriesSection,
            services_explanations: explanations,
            services_faq: faq,
            services_contact: contact,
          }}>{null}</SitePageFrame>
          : <>{intro}{needs}{categoriesSection}{explanations}{faq}{contact}</>}
      </div>
    </>
  );
}
