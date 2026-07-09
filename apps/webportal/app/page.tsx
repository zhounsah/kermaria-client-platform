import Link from "next/link";
import { headers } from "next/headers";
import { redirect } from "next/navigation";

import { getCurrentPortalSession } from "@/lib/auth";
import {
  PORTFOLIO_URL,
  getPortalPublicUrlFromHeaders,
  isVitrinePublicEnabled,
} from "@/lib/public-routes";

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
    title: "Échange et diagnostic",
    body: "On commence par comprendre vos usages, vos contraintes et ce qui vous fait perdre du temps aujourd'hui.",
  },
  {
    number: "02",
    title: "Proposition adaptée",
    body: "Vous recevez un périmètre clair, des choix techniques justifiés et un devis sans mauvaise surprise.",
  },
  {
    number: "03",
    title: "Mise en place et transmission",
    body: "On configure, on documente et on vous donne les repères pour rester autonome au quotidien.",
  },
];

const SERVICES = [
  {
    title: "Hébergement de dossiers",
    body: "Un espace centralisé pour ranger, partager et retrouver vos documents sans dépendre du grand public.",
  },
  {
    title: "Sauvegarde de données",
    body: "Vos fichiers protégés contre la perte de matériel, l'erreur de manipulation ou le rançongiciel.",
  },
  {
    title: "Accès distant sécurisé",
    body: "Travailler depuis chez vous, en déplacement ou depuis un site distant comme si vous étiez au bureau.",
  },
  {
    title: "VPN privé",
    body: "Un tunnel chiffré pour relier vos sites, vos collaborateurs ou vos appareils sans exposition publique.",
  },
  {
    title: "Maintenance informatique",
    body: "Mises à jour, surveillance, intervention rapide en cas de souci : votre outil reste en bon état.",
  },
  {
    title: "Réseau et infrastructure",
    body: "Câblage, équipements actifs, segmentation : poser des bases solides ou reprendre une installation existante.",
  },
];

const AUDIENCES = [
  {
    title: "Particuliers",
    body: "Pour ranger une photothèque familiale, sécuriser ses sauvegardes ou retrouver l'accès à ses fichiers depuis n'importe où.",
  },
  {
    title: "Associations",
    body: "Pour mutualiser les outils des bénévoles sans dépendre d'une plateforme publicitaire ni installer du logiciel sur chaque poste.",
  },
  {
    title: "Petites structures",
    body: "Pour disposer d'une infrastructure professionnelle sans embaucher un service informatique en interne.",
  },
];

const TRUST_POINTS = [
  {
    title: "Interlocuteur unique",
    body: "Un seul point de contact pour cadrer, mettre en place et suivre vos besoins informatiques.",
  },
  {
    title: "Offres lisibles",
    body: "Des prestations claires, un catalogue compréhensible et un échange direct avant toute décision.",
  },
  {
    title: "Accompagnement de proximité",
    body: "Pensé pour les petites structures, associations et clients qui veulent des outils fiables sans jargon.",
  },
];

export default async function HomePage() {
  const session = await getCurrentPortalSession();
  const baseUrl = getPortalPublicUrlFromHeaders(await headers());

  if (session?.user.role === "client_user") {
    redirect("/dashboard");
  }

  if (session?.user.role === "internal_admin") {
    redirect("/admin");
  }

  if (!isVitrinePublicEnabled()) {
    redirect("/login");
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
        <p className="eyebrow">Zachary HOUNSA-HOUNKPA</p>
        <h1>Informatique claire et utile.</h1>
        <p className="vitrine-hero-lead">
          Vos outils informatiques, mieux organisés. Un accompagnement pour
          héberger, sauvegarder, connecter et maintenir ce dont vous avez
          besoin sans jargon inutile.
        </p>
        <p className="vitrine-hero-note">
          Pour les petites structures, associations et professionnels qui
          veulent un cadre simple, un suivi lisible et un interlocuteur
          identifiable.
        </p>
        <div className="vitrine-hero-actions">
          <Link className="button" href="#services">
            Découvrir les services
          </Link>
          <Link className="button button-secondary" href="/contact">
            Échanger sur un projet
          </Link>
        </div>
      </section>

      <section className="vitrine-trust" aria-label="Repères de confiance">
        <ul className="vitrine-trust-grid">
          {TRUST_POINTS.map((item) => (
            <li className="vitrine-trust-card" key={item.title}>
              <strong>{item.title}</strong>
              <p>{item.body}</p>
            </li>
          ))}
        </ul>
      </section>

      <section className="vitrine-method">
        <header className="vitrine-section-header">
          <p className="eyebrow">Méthode</p>
          <h2>Une démarche simple, en trois temps.</h2>
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
          <h2>Ce que je propose, de l&apos;atelier à la mise en service.</h2>
          <p className="vitrine-section-lead">
            Les prestations se combinent selon vos besoins. Pour un tarif
            indicatif, consultez le{" "}
            <Link href="/offres">catalogue d&apos;offres</Link> ; pour des exemples
            concrets, voyez le <a href={PORTFOLIO_URL}>portfolio</a>.
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
          <h2>Un accompagnement de proximité pour les structures à taille humaine.</h2>
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
          <h2>Un projet, une question, un besoin d&apos;avis ?</h2>
          <p>
            Décrivez votre situation en quelques lignes. Réponse personnelle par
            e-mail, sans engagement.
          </p>
        </div>
        <Link className="button" href="/contact">
          Demander un échange
        </Link>
      </section>
    </>
  );
}
