import Link from "next/link";
import { ServiceBreadcrumb } from "@/components/PublicServiceComponents";
import { PUBLIC_SITE_URL } from "@/lib/public-route-config";
import { breadcrumbJsonLd, faqPageJsonLd, JsonLd } from "@/lib/seo";
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
    description: "Nous vérifions ce qui peut empêcher vos messages d'arriver correctement. Les réglages de protection et d'authentification de la messagerie sont ensuite corrigés si nécessaire.",
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
    description: "Les accès, contacts et réglages liés à votre nom de domaine sont remis au clair pour que votre activité ne dépende pas d'un ancien prestataire ou d'un compte personnel.",
    href: "/services/gestion-dns-domaines",
    action: "Gérer mon nom de domaine",
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
      <JsonLd
        data={faqPageJsonLd(
          PUBLIC_SITE_URL,
          breadcrumbItems[breadcrumbItems.length - 1]?.path ?? "/",
          content.faq,
        )}
      />
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
            <p>{"Choisissez simplement la situation qui ressemble le plus à la vôtre. Les détails techniques seront expliqués s'ils sont utiles."}</p>
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
            <span className="card-kicker">{"Une identité professionnelle cohérente"}</span>
            <h2 id="messaging-pillars-title">{"Le domaine d'un c\u00f4t\u00e9, la messagerie de l'autre - mais une seule identit\u00e9 professionnelle."}</h2>
          </header>
          <div className="storefront-priority-section-grid messaging-pillar-grid">
            <article className="storefront-priority-card">
              <h3>{"Nom de domaine"}</h3>
              <p>{"Nous vérifions qui contrôle le nom de domaine, les accès nécessaires et les services qui y sont reliés. Les réglages DNS sont pris en charge lorsque votre site ou votre messagerie en ont besoin."}</p>
              <Link className="service-inline-link" href="/services/gestion-dns-domaines">
                {"Voir la gestion du nom de domaine"}
              </Link>
            </article>
            <article className="storefront-priority-card">
              <h3>{"Messagerie & d\u00e9livrabilit\u00e9"}</h3>
              <p>{"Boîtes, alias, Microsoft 365 et migrations sont organisés autour de vos usages. La protection des messages est configurée en arrière-plan ; les licences fournisseur restent distinguées de l'accompagnement Zachary IT."}</p>
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
            <h2>{"Vous ne savez pas si le problème vient du domaine ou de la messagerie ?"}</h2>
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
