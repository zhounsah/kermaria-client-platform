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
import { VitrineHero } from "@/components/VitrineHero";
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
    title: "Zachary IT | Maintenance, réseau et sauvegarde à Guichen",
    description:
      "Zachary IT accompagne PME, commerces, indépendants et associations à Guichen pour la maintenance informatique, le réseau, la sauvegarde et les accès à distance.",
    path: "/",
  }),
};

const METHOD_STEPS = [
  {
    number: "01",
    title: "Décrivez la situation",
    body: "Dites-nous ce qui bloque, ce qui doit changer et les outils concernés. Il n'est pas nécessaire de choisir une prestation à l'avance.",
  },
  {
    number: "02",
    title: "Nous cadrons l'intervention",
    body: "Nous précisons le périmètre utile, les dépendances éventuelles et les conditions avant de proposer une intervention ou un devis.",
  },
  {
    number: "03",
    title: "Vous gardez une vue claire",
    body: "L'intervention, les accès et le suivi prévus sont expliqués pour que vous sachiez ce qui relève de Zachary IT et de vos autres prestataires.",
  },
];

const SERVICES = [
  {
    title: "Assistance et maintenance informatique",
    body: "Un poste ralentit, un logiciel bloque, les demandes s'accumulent : faites le point sur les incidents et l'entretien de vos outils.",
    href: "/services/support-it",
  },
  {
    title: "Réseau et Wi-Fi professionnel",
    body: "Des coupures, des zones mal couvertes ou des accès difficiles à gérer : reprenez la maîtrise de votre réseau.",
    href: "/services/reseau-securite",
  },
  {
    title: "Sauvegarde et continuité",
    body: "Identifiez les données à protéger, la fréquence des copies et la façon de les récupérer en cas d'incident.",
    href: "/services/sauvegarde-externalisee",
  },
  {
    title: "Accès à distance et serveurs",
    body: "Organisez l'accès aux outils hors de vos locaux et l'administration des services qui font tourner votre activité.",
    href: "/services/vpn-entreprise",
  },
];

const AUDIENCES = [
  {
    title: "PME et commerces",
    body: "Pour limiter les interruptions sur les postes, le réseau et les outils utilisés chaque jour.",
    href: "/services/support-it",
  },
  {
    title: "Indépendants",
    body: "Pour garder l'accès à vos dossiers et à vos échanges, au bureau comme en déplacement.",
    href: "/services/sauvegarde-externalisee",
  },
  {
    title: "Associations et petites structures",
    body: "Pour clarifier les accès, la sauvegarde des documents et la continuité des outils partagés.",
    href: "/services",
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

      <VitrineHero
        title="Votre informatique doit suivre votre activité."
        body="Maintenance, réseau, sauvegarde et accès à distance pour les professionnels, indépendants et petites structures."
        primary={{ href: "/contact", label: "Demander un devis" }}
        secondary={{ href: "/services", label: "Voir les services" }}
      />

      <section className="vitrine-audiences vitrine-audiences-first">
        <header className="vitrine-section-header">
          <h2>Une aide adaptée à votre organisation</h2>
          <p>La première étape dépend de vos usages, pas d&apos;une formule imposée.</p>
        </header>
        <ul className="vitrine-audiences-grid">
          {AUDIENCES.map((audience) => (
            <li key={audience.title} className="vitrine-audience-card">
              <h3>{audience.title}</h3>
              <p>{audience.body}</p>
              <Link href={audience.href}>Explorer les services</Link>
            </li>
          ))}
        </ul>
      </section>

      <section className="vitrine-method">
        <header className="vitrine-section-header">
          <h2>Une intervention qui commence par le bon diagnostic</h2>
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
          <h2>Des services pour les problèmes du quotidien</h2>
          <p className="vitrine-section-lead">
            Chaque prestation part d&apos;une situation concrète. Consultez son
            périmètre, puis contactez-nous si plusieurs sujets se recoupent.
          </p>
        </header>
        <ul className="vitrine-services-grid">
          {SERVICES.map((service) => (
            <li key={service.title} className="vitrine-service-card">
              <h3>{service.title}</h3>
              <p>{service.body}</p>
              <Link href={service.href}>Voir le service <span aria-hidden="true">→</span></Link>
            </li>
          ))}
        </ul>
      </section>

      <section className="vitrine-offer-path">
        <header className="vitrine-section-header"><h2>Quel est votre prochain pas ?</h2>
          <p>Les offres publiées et leurs prix sont détaillés séparément. Pour un besoin particulier, demandez un devis.</p></header>
        <div className="vitrine-offer-path-grid">
          <article><h3>Comparer les offres</h3><p>Consultez les configurations publiées et ce qu&apos;elles comprennent.</p><Link href="/offres">Voir les offres</Link></article>
          <article><h3>Consulter les tarifs</h3><p>Retrouvez les prix affichés et les prestations étudiées sur devis.</p><Link href="/tarifs">Voir les tarifs</Link></article>
          <article><h3>Faire le point</h3><p>Un questionnaire peut vous aider à décrire vos priorités.</p><Link href="/diagnostic">Ouvrir le diagnostic</Link></article>
        </div>
      </section>

      <section className="vitrine-cta">
        <div>
          <h2>Parlons de ce qui doit fonctionner chez vous.</h2>
          <p>
            Expliquez le contexte et les contraintes de votre activité. Nous
            préciserons ensemble la suite utile avant tout devis.
          </p>
        </div>
        <div className="vitrine-hero-actions">
          <Link className="button" href="/contact">
            Demander un devis
          </Link>
          <Link className="button button-secondary" href="/offres">
            Voir les offres
          </Link>
        </div>
      </section>
      </SitePageFrame>
    </>
  );
}
