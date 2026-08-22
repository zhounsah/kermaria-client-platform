import Link from "next/link";
import { ManagedMarkdown } from "@/components/ManagedMarkdown";
import { ServiceBreadcrumb } from "@/components/PublicServiceComponents";
import { PUBLIC_SITE_URL } from "@/lib/public-route-config";
import { breadcrumbJsonLd, JsonLd } from "@/lib/seo";
import {
  resolveStorefrontPublicCta,
  resolveStorefrontPublicRelatedLinks,
  type StorefrontBreadcrumbItem,
  type StorefrontPageContent,
} from "@/lib/storefront-content";

type PublicStorefrontPageProps = {
  breadcrumbItems: readonly StorefrontBreadcrumbItem[];
  content: StorefrontPageContent;
  selfServiceOrderable?: boolean | null;
};

export function PublicStorefrontPage({
  breadcrumbItems,
  content,
  selfServiceOrderable = null,
}: PublicStorefrontPageProps) {
  const cta = resolveStorefrontPublicCta(content, selfServiceOrderable);
  const relatedLinks = resolveStorefrontPublicRelatedLinks(
    content.relatedLinks,
    selfServiceOrderable,
  );

  return (
    <>
      <JsonLd data={breadcrumbJsonLd(PUBLIC_SITE_URL, [...breadcrumbItems])} />
      <div className="services-page storefront-page">
        <ServiceBreadcrumb items={breadcrumbItems} />

        <section className="service-hero">
          <div>
            <span className="card-kicker">Zachary IT</span>
            <h1>{content.title}</h1>
            <p>{content.lead}</p>
          </div>
          <Link className="button" href={cta.href}>{cta.label}</Link>
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
            <h2>Parlons de votre besoin.</h2>
            <p>Un devis ou un audit permet de confirmer le périmètre, les prérequis et les limites avant mise en service.</p>
          </div>
          <Link className="button button-secondary" href={cta.href}>{cta.label}</Link>
        </section>
      </div>
    </>
  );
}
