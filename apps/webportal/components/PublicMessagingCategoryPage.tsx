import Link from "next/link";
import { ServiceBreadcrumb } from "@/components/PublicServiceComponents";
import { PUBLIC_SITE_URL } from "@/lib/public-route-config";
import { breadcrumbJsonLd, JsonLd } from "@/lib/seo";
import {
  resolveStorefrontPublicCta,
  type StorefrontBreadcrumbItem,
  type StorefrontPageContent,
} from "@/lib/storefront-content";
type PublicMessagingCategoryPageProps = {
  breadcrumbItems: readonly StorefrontBreadcrumbItem[];
  content: StorefrontPageContent;
};
const PROBLEMS = [
  {
    title: "Je veux une adresse professionnelle claire",
    description: "Nom de domaine, adresses e-mail, comptes et usages : partez sur une base qui reste sous votre contr\u00f4le et qui peut \u00e9voluer avec votre activit\u00e9.",
    href: "/services/messagerie-professionnelle",
    action: "Organiser ma messagerie",
  },
  {
    title: "Mes e-mails arrivent en spam",
    description: "SPF, DKIM, DMARC, r\u00e9putation et services \u00e9metteurs doivent \u00eatre coh\u00e9rents. Le but est d'identifier ce qui peut r\u00e9ellement \u00eatre corrig\u00e9.",
    href: "/services/messagerie-professionnelle",
    action: "Faire v\u00e9rifier ma messagerie",
    learnMoreHref: "/pourquoi-emails-professionnels-arrivent-spam",
  },
  {
    title: "Je dois migrer des bo\u00eetes ou Microsoft 365",
    description: "Comptes, anciennes donn\u00e9es, alias, appareils et licences sont v\u00e9rifi\u00e9s avant la migration pour limiter les coupures et les mauvaises surprises.",
    href: "/services/messagerie-professionnelle",
    action: "Pr\u00e9parer ma migration",
  },
  {
    title: "Je veux reprendre la main sur mon domaine",
    description: "Registrar, acc\u00e8s, contacts, DNS et services reli\u00e9s sont remis au clair pour que votre identit\u00e9 num\u00e9rique ne d\u00e9pende pas d'un ancien prestataire ou d'un compte personnel.",
    href: "/services/gestion-dns-domaines",
    action: "Voir la gestion domaine et DNS",
  },
] as const;
export function PublicMessagingCategoryPage({
  breadcrumbItems,
  content,
}: PublicMessagingCategoryPageProps) {
  const action = resolveStorefrontPublicCta(content, null);
  return (
    <>
      <JsonLd data={breadcrumbJsonLd(PUBLIC_SITE_URL, [...breadcrumbItems])} />
      <div className="services-page storefront-page messaging-category-page">
        <ServiceBreadcrumb items={breadcrumbItems} />
        <section className="service-hero messaging-category-hero">
          <div>
            <span className="card-kicker">{"Domaines & messagerie"}</span>
            <h1>{"Votre domaine et vos e-mails doivent vous aider \u00e0 travailler, pas vous compliquer la vie."}</h1>
            <p>
              {"Adresse professionnelle, e-mails qui arrivent en spam, migration de bo\u00eetes, Microsoft 365 ou domaine difficile \u00e0 reprendre : partez de votre probl\u00e8me et choisissez la bonne porte d'entr\u00e9e."}
            </p>
          </div>
          <div className="button-row storefront-action-row">
            <Link className="button" href={action.href}>{action.label}</Link>
          </div>
        </section>
        <section className="service-section messaging-problems" aria-labelledby="messaging-problems-title">
          <header className="service-section-heading">
            <span className="card-kicker">{"Votre situation"}</span>
            <h2 id="messaging-problems-title">{"Qu'est-ce qui vous am\u00e8ne ici ?"}</h2>
            <p>{"Pas besoin de conna\u00eetre le vocabulaire DNS ou Microsoft 365 pour commencer. Choisissez simplement le probl\u00e8me le plus proche du v\u00f4tre."}</p>
          </header>
          <div className="service-overview-grid messaging-problem-grid">
            {PROBLEMS.map((problem) => (
              <article className="messaging-problem-card" key={problem.title}>
                <h3>{problem.title}</h3>
                <p>{problem.description}</p>
                <div className="messaging-problem-actions">
                  <Link className="service-inline-link" href={problem.href}>{problem.action}</Link>
                  {"learnMoreHref" in problem ? (
                    <Link className="messaging-secondary-link" href={problem.learnMoreHref}>
                      {"Comprendre les causes"}
                    </Link>
                  ) : null}
                </div>
              </article>
            ))}
          </div>
        </section>
        <section className="service-section messaging-pillars" aria-labelledby="messaging-pillars-title">
          <header className="service-section-heading">
            <span className="card-kicker">{"Deux briques \u00e0 garder coh\u00e9rentes"}</span>
            <h2 id="messaging-pillars-title">{"Le domaine d'un c\u00f4t\u00e9, la messagerie de l'autre - mais une seule identit\u00e9 professionnelle."}</h2>
          </header>
          <div className="storefront-priority-section-grid messaging-pillar-grid">
            <article className="storefront-priority-card">
              <h3>{"Domaine & DNS"}</h3>
              <p>{"Qui poss\u00e8de le domaine ? Qui a acc\u00e8s au registrar ? Quels enregistrements servent le site, la messagerie ou d'autres services ? Ces responsabilit\u00e9s sont clarifi\u00e9es avant de modifier quoi que ce soit."}</p>
              <Link className="service-inline-link" href="/services/gestion-dns-domaines">
                {"Voir la gestion domaine et DNS"}
              </Link>
            </article>
            <article className="storefront-priority-card">
              <h3>{"Messagerie & d\u00e9livrabilit\u00e9"}</h3>
              <p>{"Bo\u00eetes, alias, Microsoft 365, migration et authentification des messages sont organis\u00e9s autour de vos usages. Les licences fournisseur restent distingu\u00e9es de l'accompagnement Zachary IT."}</p>
              <Link className="service-inline-link" href="/services/messagerie-professionnelle">
                {"Voir la messagerie professionnelle"}
              </Link>
            </article>
          </div>
        </section>
        <section className="messaging-checklist" aria-labelledby="messaging-checklist-title">
          <div>
            <span className="card-kicker">{"Avant toute intervention"}</span>
            <h2 id="messaging-checklist-title">{"Ce que nous clarifions avec vous."}</h2>
          </div>
          <ul>
            <li>{"Qui contr\u00f4le aujourd'hui le domaine et les comptes administrateurs."}</li>
            <li>{"Quelles adresses, bo\u00eetes, alias et appareils sont r\u00e9ellement utilis\u00e9s."}</li>
            <li>{"Quels services envoient des e-mails avec votre domaine."}</li>
            <li>{"Quelles donn\u00e9es doivent \u00eatre migr\u00e9es et quelles licences sont n\u00e9cessaires."}</li>
          </ul>
        </section>
        <section className="service-section storefront-faq" aria-labelledby="messaging-faq-title">
          <header className="service-section-heading">
            <span className="card-kicker">{"Questions fr\u00e9quentes"}</span>
            <h2 id="messaging-faq-title">{"Ce que vous pouvez vouloir v\u00e9rifier avant de commencer."}</h2>
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
        <section className="service-cta messaging-category-cta">
          <div>
            <h2>{"Vous ne savez pas si le probl\u00e8me vient du domaine, du DNS ou de la messagerie ?"}</h2>
            <p>{"Expliquez ce que vous observez et ce que vous souhaitez obtenir. Nous vous orientons vers la bonne intervention sans vous demander de diagnostiquer la technique vous-m\u00eame."}</p>
          </div>
          <div className="button-row storefront-action-row">
            <Link className="button" href={action.href}>{action.label}</Link>
          </div>
        </section>
      </div>
    </>
  );
}