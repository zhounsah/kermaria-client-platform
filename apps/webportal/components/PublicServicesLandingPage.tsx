import Link from "next/link";

import { ManagedMarkdown } from "@/components/ManagedMarkdown";
import {
  ServiceBreadcrumb,
  ServiceCategoryCard,
} from "@/components/PublicServiceComponents";
import {
  SERVICE_CATEGORY_BY_SLUG,
  type ServiceCategory,
} from "@/lib/public-services";
import { PUBLIC_SITE_URL } from "@/lib/public-route-config";
import { breadcrumbJsonLd, JsonLd } from "@/lib/seo";
import {
  resolveStorefrontPublicCta,
  type StorefrontBreadcrumbItem,
  type StorefrontServicesLandingContent,
} from "@/lib/storefront-content";

type PublicServicesLandingPageProps = {
  breadcrumbItems: readonly StorefrontBreadcrumbItem[];
  content: StorefrontServicesLandingContent;
};

export function PublicServicesLandingPage({
  breadcrumbItems,
  content,
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

  return (
    <>
      <JsonLd data={breadcrumbJsonLd(PUBLIC_SITE_URL, [...breadcrumbItems])} />
      <div className="services-page storefront-page services-landing-page">
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

        <section
          aria-labelledby="services-problems-title"
          className="service-section service-problem-routing"
        >
          <header className="service-section-heading">
            <span className="card-kicker">Votre besoin</span>
            <h2 id="services-problems-title">Quel problème cherchez-vous à résoudre ?</h2>
            <p>
              Partez de la situation que vous rencontrez. Chaque entrée vous mène
              vers le service, l&apos;univers ou le guide le plus utile pour avancer.
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
        </section>

        <section
          aria-labelledby="services-categories-title"
          className="service-section service-main-services"
        >
          <header className="service-section-heading">
            <span className="card-kicker">Domaines d&apos;intervention</span>
            <h2 id="services-categories-title">Les services Zachary IT</h2>
            <p>
              Quatre univers regroupent les services selon leur rôle. Ils viennent
              après le besoin pour vous éviter de devoir choisir d&apos;abord une
              technologie.
            </p>
          </header>
          <div className="service-category-grid">
            {categories.map((category) => (
              <ServiceCategoryCard category={category} key={category.slug} />
            ))}
          </div>
        </section>

        {content.sections.map((section) => (
          <section className="service-section storefront-section" key={section.heading}>
            <header className="service-section-heading">
              <h2>{section.heading}</h2>
            </header>
            <ManagedMarkdown markdown={section.bodyMarkdown} />
          </section>
        ))}

        <section
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
        </section>

        <section className="service-cta">
          <div>
            <h2>Votre besoin touche plusieurs sujets ?</h2>
            <p>
              Un audit permet de partir de votre existant, d&apos;identifier les
              priorités et de cadrer les responsabilités avant de proposer une
              solution.
            </p>
          </div>
          <div className="button-row storefront-action-row">
            <Link className="button button-secondary" href={primaryAction.href}>
              {primaryAction.label}
            </Link>
          </div>
        </section>
      </div>
    </>
  );
}
