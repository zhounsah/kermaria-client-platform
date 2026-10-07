import Link from "next/link";
import { ManagedMarkdown } from "@/components/ManagedMarkdown";
import { ServiceBreadcrumb } from "@/components/PublicServiceComponents";
import {
  buildDiagnosticHref,
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
  type StorefrontPriorityServiceSlug,
} from "@/lib/storefront-content";
type PublicPriorityServicePageProps = {
  breadcrumbItems: readonly StorefrontBreadcrumbItem[];
  commercialActions: StorefrontCommercialActions;
  content: StorefrontPageContent;
  selfServiceOrderable: boolean;
  serviceSlug: StorefrontPriorityServiceSlug;
};
const COMMERCIAL_MODE_LABELS: Record<StorefrontCommercialActions["mode"], string> = {
  FORMULA: "Offre disponible",
  HYBRID: "Offre + accompagnement",
  QUOTE: "Sur devis",
};
function isTariffSection(heading: string): boolean {
  return /^tarif\b/i.test(heading.trim());
}
function commercialGuidance(
  serviceSlug: StorefrontPriorityServiceSlug,
  mode: StorefrontCommercialActions["mode"],
): { title: string; body: string } {
  if (serviceSlug === "vpn-entreprise") {
    return {
      title: "Vous souhaitez travailler à distance ?",
      body: "Pour un acc\u00e8s distant classique, vous pouvez partir directement de l'offre. Si votre r\u00e9seau existe d\u00e9j\u00e0, si plusieurs utilisateurs ou sites sont concern\u00e9s, ou si vous h\u00e9sitez avec un bureau Windows distant, demandez conseil avant de commencer.",
    };
  }
  if (serviceSlug === "sauvegarde-externalisee") {
    return {
      title: "Fichiers simples ou environnement plus complexe ?",
      body: "Pour protéger des fichiers dans un cas standard, l'offre vous guide. Pour un serveur, un boîtier de stockage, plusieurs postes ou un besoin de récupération particulier, décrivez votre situation pour choisir la bonne solution.",
    };
  }
  if (mode === "QUOTE") {
    return {
      title: "Parlons de votre environnement.",
      body: "Expliquez simplement ce que vous utilisez aujourd'hui et ce que vous souhaitez am\u00e9liorer. Le p\u00e9rim\u00e8tre, les pr\u00e9requis et les \u00e9ventuelles licences sont clarifi\u00e9s avant de vous proposer la suite.",
    };
  }
  return {
    title: "Choisissez la suite qui vous correspond.",
    body: "Le parcours standard peut \u00eatre configur\u00e9 directement. Si votre environnement sort du cas courant, un \u00e9change permet de v\u00e9rifier les pr\u00e9requis avant de continuer.",
  };
}
function VpnComparisonDetails() {
  return (
    <details className="storefront-inline-disclosure">
      <summary>{"En savoir plus sur la diff\u00e9rence"}</summary>
      <div className="storefront-inline-disclosure-body">
        <p>
          <strong>Accès aux fichiers :</strong>{" vous gardez votre ordinateur et retrouvez seulement les fichiers et outils autorisés, sans les rendre accessibles à tous."}
        </p>
        <p>
          <strong>{"Bureau Windows à distance :"}</strong>{" vous ouvrez un bureau complet depuis un autre appareil, avec vos applications et vos fichiers au même endroit."}
        </p>
        <Link className="service-inline-link" href="/vpn-ou-bureau-a-distance-que-choisir">
          {"Voir le comparatif d\u00e9taill\u00e9"}
        </Link>
      </div>
    </details>
  );
}
export function PublicPriorityServicePage({
  breadcrumbItems,
  commercialActions,
  content,
  selfServiceOrderable,
  serviceSlug,
}: PublicPriorityServicePageProps) {
  const fallbackCta = resolveStorefrontPublicCta(content, selfServiceOrderable);
  const diagnosticContext = diagnosticContextForServiceSlug(serviceSlug);
  const rawPrimaryAction = commercialActions.mode === "QUOTE"
    ? fallbackCta
    : commercialActions.primaryAction;
  const primaryAction = {
    ...rawPrimaryAction,
    href: contextualizeDiagnosticHref(rawPrimaryAction.href, diagnosticContext),
  };
  const rawSecondaryAction = commercialActions.secondaryAction ?? null;
  const contextualSecondaryAction = rawSecondaryAction
    ? {
        ...rawSecondaryAction,
        href: contextualizeDiagnosticHref(rawSecondaryAction.href, diagnosticContext),
      }
    : null;
  const diagnosticAction = {
    label: "Faire le diagnostic",
    href: buildDiagnosticHref(diagnosticContext),
  };
  const secondaryAction = contextualSecondaryAction
    ?? (commercialActions.mode === "QUOTE" && primaryAction.href !== diagnosticAction.href
      ? diagnosticAction
      : null);
  const relatedLinks = resolveStorefrontPublicRelatedLinks(
    content.relatedLinks,
    selfServiceOrderable,
  );
  const tariffSection = content.sections.find((section) => isTariffSection(section.heading)) ?? null;
  const detailSections = content.sections.filter((section) => section !== tariffSection);
  const hasFormulaPath = commercialActions.mode === "FORMULA"
    || commercialActions.mode === "HYBRID";
  const guidance = commercialGuidance(serviceSlug, commercialActions.mode);
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
      <div className="services-page storefront-page storefront-priority-page">
        <ServiceBreadcrumb items={breadcrumbItems} />
        <section className="service-hero storefront-priority-hero">
          <div>
            <div className="storefront-priority-meta">
              <span className="card-kicker">{"Service g\u00e9r\u00e9"}</span>
              <span className="storefront-commercial-badge">
                {COMMERCIAL_MODE_LABELS[commercialActions.mode]}
              </span>
            </div>
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
        <section className="storefront-priority-details" aria-label={"D\u00e9tails du service"}>
          <div className="storefront-priority-section-grid">
            {detailSections.map((section) => (
              <article className="storefront-priority-card" key={section.heading}>
                <h2>{section.heading}</h2>
                <ManagedMarkdown markdown={section.bodyMarkdown} />
                {serviceSlug === "vpn-entreprise" && (/^VPN ou bureau Windows distant/i.test(section.heading) || section.heading === "Accéder à ses fichiers ou à un bureau complet ?")
                  ? <VpnComparisonDetails />
                  : null}
              </article>
            ))}
          </div>
        </section>
        {tariffSection ? (
          <section className="storefront-priority-commercial" aria-labelledby="storefront-priority-commercial-title">
            <div>
              <span className="card-kicker">{"Prochaine \u00e9tape"}</span>
              <h2 id="storefront-priority-commercial-title">{guidance.title}</h2>
              <p className="storefront-priority-commercial-copy">{guidance.body}</p>
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
        ) : null}
        <section className="service-section storefront-faq" aria-labelledby="storefront-faq-title">
          <header className="service-section-heading">
            <span className="card-kicker">{"Avant de vous d\u00e9cider"}</span>
            <h2 id="storefront-faq-title">{"Questions fr\u00e9quentes"}</h2>
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
        <section className="service-category-proof storefront-related" aria-labelledby="storefront-related-title">
          <div>
            <span className="card-kicker">Pour aller plus loin</span>
            <h2 id="storefront-related-title">{"Services associ\u00e9s"}</h2>
            <p>{"Découvrez les services qui peuvent compléter cette solution selon votre besoin."}</p>
          </div>
          <nav aria-label={"Pages associ\u00e9es"} className="storefront-link-list">
            {relatedLinks.map((link) => (
              <Link className="service-inline-link" href={link.href} key={link.href}>
                {link.label}
              </Link>
            ))}
          </nav>
        </section>
        <section className="service-cta">
          <div>
            <h2>{hasFormulaPath ? "Choisissez le parcours adapt\u00e9." : "Parlons de votre besoin."}</h2>
            <p>
              {hasFormulaPath
                ? "Une offre peut répondre à votre besoin. Si votre situation est particulière, demandez-nous conseil avant de continuer."
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
        </section>
      </div>
    </>
  );
}
