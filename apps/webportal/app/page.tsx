import type { Metadata } from "next";
import Link from "next/link";
import { headers } from "next/headers";
import { notFound, redirect } from "next/navigation";

import { getCurrentPortalSession } from "@/lib/auth";
import {
  PORTFOLIO_URL,
  getPortalRequestOriginFromHeaders,
  isVitrinePublicEnabled,
} from "@/lib/public-routes";
import {
  getPortalArea,
  isPortalRoleAllowed,
  resolvePortalAreaUrl,
  resolvePortalRoleUrl,
} from "@/lib/public-route-config";

export const metadata: Metadata = {
  title: "Sauvegarde distante et continuité d'activité",
  description:
    "Sauvegarde distante et stockage documentaire à Guichen pour les particuliers, associations et petites entreprises : une copie de vos fichiers importants conservée hors de vos locaux.",
};

function organizationJsonLd(baseUrl: string) {
  return {
    "@context": "https://schema.org",
    "@type": "Organization",
    name: "Zachary HOUNSA-HOUNKPA EI",
    url: baseUrl,
    sameAs: [],
  };
}

const METHOD_STEPS = [
  {
    number: "01",
    title: "Identifier les données à protéger",
    body: "Ensemble, nous repérons les fichiers à conserver : documents administratifs, photos, factures, contrats, fichiers clients, configurations ou archives.",
  },
  {
    number: "02",
    title: "Mettre en place la copie",
    body: "Les données retenues sont copiées vers un stockage distinct de votre ordinateur, de votre serveur et de vos locaux.",
  },
  {
    number: "03",
    title: "Récupérer les fichiers",
    body: "Après un incident, vous récupérez les données sauvegardées selon les conditions et fonctionnalités de l'offre choisie.",
  },
];

const SERVICES = [
  {
    title: "Dossier de secours numérique",
    body: "Conserver à distance vos documents importants : factures et garanties, contrats et attestations, photos de vos biens, numéros de série, diplômes et documents administratifs.",
  },
  {
    title: "Sauvegarde de données",
    body: "Copier les fichiers d'un ordinateur, d'un NAS ou d'un serveur vers un stockage séparé. La fréquence, la conservation et les conditions de restauration dépendent de l'offre choisie.",
  },
  {
    title: "Stockage et accès distant",
    body: "Un espace de stockage distant pour déposer vos fichiers et les retrouver depuis un autre appareil quand votre équipement principal n'est plus disponible.",
  },
  {
    title: "Continuité d'activité",
    body: "Protéger les devis, factures, dossiers clients, fichiers de travail, configurations et archives nécessaires à une petite activité.",
  },
  {
    title: "Assistance et infrastructure",
    body: "Selon le besoin : maintenance, réseau, VPN privé, supervision, mise en place de postes et de serveurs.",
  },
];

const AUDIENCES = [
  {
    title: "Particuliers",
    body: "Pour protéger vos photos, documents administratifs, factures, garanties et autres fichiers personnels importants.",
  },
  {
    title: "Associations",
    body: "Pour centraliser vos documents, les partager entre responsables et éviter qu'ils ne restent sur l'ordinateur d'un seul membre.",
  },
  {
    title: "Indépendants et petites entreprises",
    body: "Pour sauvegarder les documents nécessaires à l'activité et faciliter la reprise après une panne ou une perte de matériel.",
  },
];

export default async function HomePage() {
  const origin = getPortalRequestOriginFromHeaders(await headers());
  const area = getPortalArea(origin);

  if (!origin || !area) {
    notFound();
  }

  const session = await getCurrentPortalSession();
  if (area === "public") {
    if (session) {
      const loginUrl = resolvePortalRoleUrl(origin, session.user.role, "/login");
      if (!loginUrl) {
        notFound();
      }
      redirect(loginUrl);
    }

    if (!isVitrinePublicEnabled()) {
      const loginUrl = resolvePortalAreaUrl(origin, "client", "/login");
      if (!loginUrl) {
        notFound();
      }
      redirect(loginUrl);
    }
  } else if (area === "local") {
    if (session) {
      const landingUrl = resolvePortalRoleUrl(origin, session.user.role);
      if (!landingUrl) {
        notFound();
      }
      redirect(landingUrl);
    }

    if (!isVitrinePublicEnabled()) {
      const loginUrl = resolvePortalAreaUrl(origin, "local", "/login");
      if (!loginUrl) {
        notFound();
      }
      redirect(loginUrl);
    }
  } else if (session && isPortalRoleAllowed(area, session.user.role)) {
    const landingUrl = resolvePortalRoleUrl(origin, session.user.role);
    if (!landingUrl) {
      notFound();
    }
    redirect(landingUrl);
  } else {
    const loginUrl = resolvePortalAreaUrl(origin, area, "/login");
    if (!loginUrl) {
      notFound();
    }
    redirect(loginUrl);
  }

  const baseUrl = resolvePortalAreaUrl(origin, "public");
  if (!baseUrl) {
    notFound();
  }

  return (
    <>
      <script
        type="application/ld+json"
        // Schema.org structured data - safe inlined JSON, generated server-side.
        dangerouslySetInnerHTML={{
          __html: JSON.stringify(organizationJsonLd(baseUrl)),
        }}
      />

      <section className="vitrine-hero">
        <p className="eyebrow">Zachary IT — Guichen</p>
        <h1>
          Un sinistre peut détruire votre matériel. Il ne devrait pas détruire
          vos données.
        </h1>
        <p className="vitrine-hero-lead">
          Zachary IT met en place des solutions de sauvegarde et de stockage
          distant pour les particuliers, associations et petites entreprises.
          Vos fichiers importants sont conservés séparément de votre ordinateur,
          de votre serveur ou de vos locaux. En cas de panne, de vol, d&apos;incendie
          ou de rançongiciel, vous disposez ainsi d&apos;une copie à partir de
          laquelle repartir.
        </p>
        <p className="vitrine-hero-note">
          Basé à Guichen, j&apos;échange directement avec chaque client et
          j&apos;explique ce qui est installé, ce qui est sauvegardé et comment les
          données peuvent être récupérées.
        </p>
        <div className="vitrine-hero-actions">
          <Link className="button" href="/offres">
            Découvrir les offres
          </Link>
          <Link className="button button-secondary" href="/contact">
            Expliquer mon besoin
          </Link>
        </div>
      </section>

      <section className="vitrine-method">
        <header className="vitrine-section-header">
          <p className="eyebrow">Comment ça marche</p>
          <h2>Trois étapes, de la sélection des fichiers à la récupération.</h2>
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
          <p className="eyebrow">Services</p>
          <h2>Sauvegarder, stocker à distance et reprendre après un incident.</h2>
          <p className="vitrine-section-lead">
            Les prestations se combinent selon votre besoin. Tarifs indicatifs
            dans le <Link href="/offres">catalogue d&apos;offres</Link> ; exemples
            de réalisations sur le <a href={PORTFOLIO_URL}>portfolio</a>.
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

      <section className="vitrine-audiences">
        <header className="vitrine-section-header">
          <p className="eyebrow">Pour qui</p>
          <h2>Particuliers, associations, indépendants et petites entreprises.</h2>
        </header>
        <ul className="vitrine-audiences-grid">
          {AUDIENCES.map((audience) => (
            <li key={audience.title} className="vitrine-audience-card">
              <h3>{audience.title}</h3>
              <p>{audience.body}</p>
            </li>
          ))}
        </ul>
      </section>

      <section className="vitrine-cta">
        <div>
          <h2>Un échange direct, du besoin jusqu&apos;à la mise en service.</h2>
          <p>
            Vous échangez directement avec moi, de l&apos;étude du besoin jusqu&apos;à
            la mise en service. Avant toute commande, je précise les données
            concernées, les modalités de stockage, les conditions de sauvegarde
            et de restauration, ainsi que le prix et les limites du service.
          </p>
        </div>
        <div className="vitrine-hero-actions">
          <Link className="button" href="/contact">
            Expliquer mon besoin
          </Link>
          <Link className="button button-secondary" href="/offres">
            Découvrir les offres
          </Link>
        </div>
      </section>
    </>
  );
}
