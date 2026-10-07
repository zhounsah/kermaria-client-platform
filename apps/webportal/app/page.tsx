import type { Metadata } from "next";
import Link from "next/link";
import { headers } from "next/headers";
import { notFound, redirect } from "next/navigation";

import { getCurrentPortalSession } from "@/lib/auth";
import { buildPublicMetadata } from "@/lib/public-metadata";
import {
  getPortalAreaForRequest,
  getPortalRequestOriginFromHeaders,
  isVitrinePublicEnabled,
  resolvePortalAreaUrlForRequest,
  resolvePortalRoleUrlForRequest,
} from "@/lib/public-routes";
import {
  isPortalRoleAllowed,
} from "@/lib/public-route-config";
import { JsonLd, localBusinessJsonLd, webSiteJsonLd } from "@/lib/seo";
import { SitePageFrame } from "@/components/SitePageFrame";
import { getPublicSitePageLayout } from "@/lib/internal-api";

/**
 * Le nom commercial ouvre le titre : sur l'accueil, c'est le nom du site qui
 * doit etre lu en premier, l'activite et la localite venant ensuite.
 *
 * Une seule occurrence de la marque, et c'est volontaire : le `title.template`
 * du layout racine (`%s | Zachary IT`) ne s'applique QU'AUX segments enfants,
 * pas a `app/page.tsx`, qui partage le segment racine avec le layout. Le titre
 * ecrit ici est donc servi tel quel. Ne pas ajouter de suffixe de marque en
 * pensant compenser : cela produirait `Zachary IT | … | Zachary IT`.
 */
export const metadata: Metadata = {
  ...buildPublicMetadata({
    title: "Zachary IT | Informatique, réseau et sauvegarde à Guichen",
    description:
      "Zachary IT à Guichen accompagne particuliers, associations et petites "
      + "entreprises pour le réseau et le Wi-Fi, les postes, la sauvegarde, "
      + "l'hébergement, la messagerie et le support informatique.",
    path: "/",
  }),
};

const METHOD_STEPS = [
  {
    number: "01",
    title: "Vous nous expliquez votre besoin",
    body: "Une question, une panne ou un projet : décrivez simplement votre situation, même si vous ne savez pas quelle solution choisir.",
  },
  {
    number: "02",
    title: "Nous proposons une réponse claire",
    body: "Vous savez ce qui est prévu, ce que cela coûte et ce qui se passe ensuite avant de prendre une décision.",
  },
  {
    number: "03",
    title: "Nous restons à vos côtés",
    body: "Une fois la solution mise en place, nous vérifions qu'elle vous convient et restons disponibles si votre besoin évolue.",
  },
];

const SERVICES = [
  {
    title: "Un réseau qui fonctionne",
    body: "Retrouvez une connexion stable à la maison ou au travail, là où vous en avez besoin.",
  },
  {
    title: "Des outils prêts à l'emploi",
    body: "Installation, aide à la prise en main et accès à vos outils, y compris à distance lorsque c'est utile.",
  },
  {
    title: "Vos fichiers protégés",
    body: "Gardez une copie de vos documents importants et préparez leur récupération en cas de problème.",
  },
  {
    title: "Votre activité en ligne",
    body: "Site, adresse e-mail et services en ligne : nous vous aidons à les mettre en place et à les suivre.",
  },
  {
    title: "Une aide quand il faut",
    body: "Obtenez une réponse quand un outil bloque, et un suivi pour éviter que les problèmes s'accumulent.",
  },
];

const AUDIENCES = [
  {
    title: "Particuliers",
    body: "Pour retrouver un ordinateur agréable à utiliser, un Wi-Fi fiable et des fichiers importants protégés.",
  },
  {
    title: "Associations",
    body: "Pour partager les informations plus facilement et continuer à fonctionner quand un bénévole ou un outil manque.",
  },
  {
    title: "Indépendants et petites entreprises",
    body: "Pour travailler sereinement avec des outils suivis, des données protégées et un interlocuteur disponible.",
  },
];

export default async function HomePage() {
  const origin = getPortalRequestOriginFromHeaders(await headers());
  const area = getPortalAreaForRequest(origin);

  if (!origin || !area) {
    notFound();
  }

  const session = await getCurrentPortalSession();
  if (area === "public") {
    if (session) {
      const loginUrl = resolvePortalRoleUrlForRequest(
        origin,
        session.user.role,
        "/login",
      );
      if (!loginUrl) {
        notFound();
      }
      redirect(loginUrl);
    }

    if (!isVitrinePublicEnabled()) {
      const loginUrl = resolvePortalAreaUrlForRequest(origin, "client", "/login");
      if (!loginUrl) {
        notFound();
      }
      redirect(loginUrl);
    }
  } else if (area === "local") {
    if (session) {
      const landingUrl = resolvePortalRoleUrlForRequest(origin, session.user.role);
      if (!landingUrl) {
        notFound();
      }
      redirect(landingUrl);
    }

    if (!isVitrinePublicEnabled()) {
      const loginUrl = resolvePortalAreaUrlForRequest(origin, "local", "/login");
      if (!loginUrl) {
        notFound();
      }
      redirect(loginUrl);
    }
  } else if (session && isPortalRoleAllowed(area, session.user.role)) {
    const landingUrl = resolvePortalRoleUrlForRequest(origin, session.user.role);
    if (!landingUrl) {
      notFound();
    }
    redirect(landingUrl);
  } else {
    const loginUrl = resolvePortalAreaUrlForRequest(origin, area, "/login");
    if (!loginUrl) {
      notFound();
    }
    redirect(loginUrl);
  }

  const baseUrl = resolvePortalAreaUrlForRequest(origin, "public");
  if (!baseUrl) {
    notFound();
  }
  const homeLayout = await getPublicSitePageLayout("/");

  return (
    <>
      <JsonLd data={localBusinessJsonLd(baseUrl)} />
      <JsonLd data={webSiteJsonLd(baseUrl)} />

      <SitePageFrame area="public" pageKey="/" initialLayout={homeLayout.data}>

      <section className="vitrine-hero-band">
        <div className="vitrine-hero vitrine-hero-2026">
          <div className="vitrine-hero-copy">
            <h1>Une informatique fiable, simplement.</h1>
            <p className="vitrine-hero-lead">
              Dépannage, installation, protection de vos fichiers et conseils :
              Zachary IT vous accompagne à la maison comme au travail.
            </p>
            <p className="vitrine-hero-note">
              À Guichen, vous échangez directement avec la personne qui suit
              votre demande et vous explique chaque étape.
            </p>
            <div className="vitrine-hero-actions">
              <Link className="button" href="/services">
                Trouver une solution
              </Link>
              <Link className="button button-secondary" href="/contact">
                Demander un conseil
              </Link>
            </div>
          </div>
          <div aria-hidden="true" className="vitrine-hero-photo-spacer" />
        </div>
      </section>

      <section className="vitrine-audiences vitrine-audiences-first">
        <header className="vitrine-section-header">
          <h2>Quelle est votre situation ?</h2>
          <p>Choisissez le point de départ qui vous ressemble.</p>
        </header>
        <ul className="vitrine-audiences-grid">
          {AUDIENCES.map((audience) => (
            <li key={audience.title} className="vitrine-audience-card">
              <h3>{audience.title}</h3>
              <p>{audience.body}</p>
              <Link href="/services">Voir les solutions</Link>
            </li>
          ))}
        </ul>
      </section>

      <section className="vitrine-method">
        <header className="vitrine-section-header">
          <p className="eyebrow">Comment ça marche</p>
          <h2>Trois étapes pour définir et mettre en place votre solution.</h2>
        </header>
        <ol className="vitrine-method-grid">
          {METHOD_STEPS.map((step) => (
            <li key={step.number} className="vitrine-method-step">
              <span className="vitrine-method-number">{step.number}</span>
              <h3>{step.title}</h3>
              <p>{step.body}</p>
            </li>
          ))}
        </ol>
      </section>

      <section className="vitrine-services" id="services">
        <header className="vitrine-section-header">
          <h2>De quoi avez-vous besoin ?</h2>
          <p className="vitrine-section-lead">
            Partez de votre problème ou de votre projet. Nous vous aiderons à
            trouver l&apos;offre adaptée, sans avoir à connaître les termes techniques.
          </p>
        </header>
        <ul className="vitrine-services-grid">
          {SERVICES.map((service) => (
            <li key={service.title} className="vitrine-service-card">
              <h3>{service.title}</h3>
              <p>{service.body}</p>
            </li>
          ))}
        </ul>
      </section>

      <section className="vitrine-offer-path">
        <header className="vitrine-section-header"><h2>Choisissez votre prochaine étape</h2>
          <p>Comparez les solutions proposées ou posez votre question directement.</p></header>
        <div className="vitrine-offer-path-grid">
          <article><h3>Je veux comparer les offres</h3><p>Découvrez à quoi elles servent et ce qu&apos;elles comprennent.</p><Link href="/offres">Voir les offres</Link></article>
          <article><h3>Je veux connaître les tarifs</h3><p>Consultez les prix affichés et les prestations proposées sur devis.</p><Link href="/tarifs">Voir les tarifs</Link></article>
          <article><h3>Je ne sais pas encore</h3><p>Quelques questions simples vous aideront à situer votre besoin.</p><Link href="/diagnostic">M&apos;orienter</Link></article>
        </div>
      </section>

      <section className="vitrine-cta">
        <div>
          <h2>Un projet ou une question ? Parlons-en.</h2>
          <p>
            Expliquez votre besoin en quelques mots. Nous vous répondrons avec
            une prochaine étape claire, sans vous demander de choisir seul une solution.
          </p>
        </div>
        <div className="vitrine-hero-actions">
          <Link className="button" href="/contact">
            Parler de mon besoin
          </Link>
          <Link className="button button-secondary" href="/offres">
            Comparer les offres
          </Link>
        </div>
      </section>
      </SitePageFrame>
    </>
  );
}
