import type { Metadata } from "next";
import Link from "next/link";
import { headers } from "next/headers";
import { notFound, redirect } from "next/navigation";

import { getCurrentPortalSession } from "@/lib/auth";
import { buildPublicMetadata } from "@/lib/public-metadata";
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
import { JsonLd, localBusinessJsonLd, webSiteJsonLd } from "@/lib/seo";
import { BrandLogo } from "@/components/BrandLogo";

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
    title: "Comprendre votre besoin",
    body: "Nous partons de votre situation réelle : Wi-Fi instable, nouveaux postes, accès à distance, sauvegarde, messagerie, serveur ou besoin d'assistance.",
  },
  {
    number: "02",
    title: "Définir et mettre en place la solution",
    body: "Les choix sont expliqués avant installation : matériel, services, accès, sécurité, sauvegarde et coût. La solution est ensuite configurée pour votre usage.",
  },
  {
    number: "03",
    title: "Vérifier et accompagner",
    body: "Le fonctionnement est vérifié avec vous. Selon le besoin, Zachary IT peut ensuite assurer le support, la maintenance, la supervision ou les évolutions futures.",
  },
];

const SERVICES = [
  {
    title: "Réseau et Wi-Fi",
    body: "Conception, amélioration et maintenance de réseaux filaires ou Wi-Fi : couverture, équipements, segmentation, accès distant et sécurisation adaptée au contexte.",
  },
  {
    title: "Postes et accès de travail",
    body: "Installation et suivi de postes, préparation d'environnements de travail, accès à distance et bureau Windows distant lorsque l'usage le justifie.",
  },
  {
    title: "Sauvegarde et continuité",
    body: "Sauvegarde de postes, NAS ou serveurs, stockage séparé et préparation de la restauration pour réduire l'impact d'une panne, d'une erreur ou d'un sinistre.",
  },
  {
    title: "Hébergement et services en ligne",
    body: "VPS, hébergement, domaines, DNS et messagerie professionnelle avec une configuration suivie et des responsabilités clairement définies.",
  },
  {
    title: "Maintenance et support",
    body: "Assistance, mises à jour, supervision et accompagnement des utilisateurs pour traiter les incidents et éviter l'accumulation de problèmes techniques.",
  },
];

const AUDIENCES = [
  {
    title: "Particuliers",
    body: "Pour améliorer le Wi-Fi, remettre un poste en état, protéger des fichiers importants ou accéder simplement à ses outils à distance.",
  },
  {
    title: "Associations",
    body: "Pour organiser les postes, les accès, la messagerie, le partage de fichiers et les sauvegardes sans faire reposer toute l'informatique sur une seule personne.",
  },
  {
    title: "Indépendants et petites entreprises",
    body: "Pour fiabiliser réseau, postes et outils de travail, protéger les données, maintenir la messagerie et préparer la continuité de l'activité.",
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
      <JsonLd data={localBusinessJsonLd(baseUrl)} />
      <JsonLd data={webSiteJsonLd(baseUrl)} />

      <section className="vitrine-hero-band">
        <div className="vitrine-hero vitrine-hero-2026">
          <div className="vitrine-hero-copy">
            <p className="eyebrow">Zachary IT — Guichen</p>
            <p className="vitrine-hero-baseline">
              Votre informatique. Gérée, sécurisée, disponible.
            </p>
            <h1>Une informatique fiable, sans avoir à tout gérer vous-même.</h1>
            <p className="vitrine-hero-lead">
              Réseau et Wi-Fi, postes, sauvegarde, accès à distance,
              hébergement, messagerie et support IT pour les particuliers,
              associations, indépendants et petites entreprises.
            </p>
            <p className="vitrine-hero-note">
              Basé à Guichen, j&apos;échange directement avec chaque client et
              j&apos;explique ce qui est installé, protégé et accessible au quotidien.
            </p>
            <div className="vitrine-hero-actions">
              <Link className="button" href="/offres">
                Comparer les offres
              </Link>
              <Link className="button button-secondary" href="/contact">
                Expliquer mon besoin
              </Link>
            </div>
          </div>
          <div aria-hidden="true" className="vitrine-hero-brand-panel">
            <BrandLogo className="vitrine-hero-logo" priority variant="dark" />
            <div className="vitrine-network-motif" />
          </div>
        </div>
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
          <p className="eyebrow">Services</p>
          <h2>Réseau, postes, sauvegarde, services en ligne et accompagnement.</h2>
          <p className="vitrine-section-lead">
            Les prestations se combinent selon votre besoin. Comparez les
            <Link href="/offres">offres configurables</Link>, consultez les <Link href="/tarifs">tarifs</Link>
            ou voyez des exemples concrets sur le <a href={PORTFOLIO_URL}>portfolio</a>.
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
            la mise en service. Avant toute commande, je précise le périmètre,
            les choix techniques, les accès, les responsabilités, le prix et
            les limites du service retenu.
          </p>
        </div>
        <div className="vitrine-hero-actions">
          <Link className="button" href="/contact">
            Expliquer mon besoin
          </Link>
          <Link className="button button-secondary" href="/offres">
            Comparer les offres
          </Link>
        </div>
      </section>
    </>
  );
}
