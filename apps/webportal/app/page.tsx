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
    "Sauvegarde distante, stockage documentaire et accompagnement de proximité à Guichen pour protéger les données importantes des particuliers et petites structures.",
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
    title: "Sélection des documents clés",
    body: "On identifie les fichiers, justificatifs et données qui doivent rester accessibles même si votre matériel devient inutilisable.",
  },
  {
    number: "02",
    title: "Copie distante organisée",
    body: "Vos documents importants sont conservés sur un espace distant distinct de vos équipements et de vos locaux.",
  },
  {
    number: "03",
    title: "Restauration et reprise",
    body: "En cas de besoin, vous retrouvez vos fichiers depuis un autre appareil avec un accompagnement humain pour repartir plus vite.",
  },
];

const SERVICES = [
  {
    title: "Dossier de secours numérique",
    body: "Un espace documentaire distant pour conserver factures, contrats, photos, garanties et autres justificatifs importants.",
  },
  {
    title: "Sauvegarde de données",
    body: "Des copies distantes pour éviter qu'une panne matérielle, une erreur de manipulation ou un rançongiciel ne devienne une perte définitive.",
  },
  {
    title: "Stockage distant accessible",
    body: "Retrouver vos fichiers importants depuis un autre appareil lorsque votre ordinateur principal n'est plus disponible.",
  },
  {
    title: "Continuité d'activité",
    body: "Protéger les documents clients, devis, factures, configurations et fichiers de travail nécessaires à la reprise d'une petite activité.",
  },
  {
    title: "Accès distant sécurisé",
    body: "VPN privé, accès contrôlé et accompagnement simple pour consulter vos ressources sans exposition publique inutile.",
  },
  {
    title: "Maintenance et infrastructure",
    body: "Un cadre cohérent pour relier sauvegarde, réseau, supervision et bonnes pratiques sans reconstruire inutilement votre existant.",
  },
];

const AUDIENCES = [
  {
    title: "Particuliers",
    body: "Pour conserver à distance des photos, documents administratifs, garanties et preuves utiles si un sinistre touche le logement ou le matériel.",
  },
  {
    title: "Associations",
    body: "Pour partager des documents importants, préserver les archives utiles et éviter qu'un seul poste concentre les informations essentielles.",
  },
  {
    title: "Petites entreprises",
    body: "Pour sécuriser devis, factures, contrats, fichiers clients et documents de travail afin de reprendre plus rapidement après un incident.",
  },
];

const TRUST_POINTS = [
  {
    title: "Interlocuteur identifiable",
    body: "Un accompagnement humain et local, assuré par Zachary IT depuis Guichen, sans plateforme impersonnelle à traverser.",
  },
  {
    title: "Promesses mesurées",
    body: "Des offres lisibles, des explications compréhensibles et aucune promesse de sécurité absolue ou de risque nul.",
  },
  {
    title: "Approche transparente",
    body: "La localisation des données, les solutions retenues et les possibilités d'export sont expliquées sans jargon inutile.",
  },
];

const RISK_ITEMS = [
  "Ordinateur ou téléphone endommagé",
  "NAS ou serveur indisponible",
  "Documents papier détruits",
  "Sauvegardes stockées dans le même bâtiment",
  "Erreur humaine ou suppression involontaire",
  "Rançongiciel ou panne matérielle",
];

const DOSSIER_ITEMS = [
  "Factures importantes et garanties",
  "Photographies du logement et des biens de valeur",
  "Numéros de série et inventaires de matériel",
  "Contrats et attestations d'assurance",
  "Diplômes et documents administratifs",
  "Documents utiles à la continuité d'une petite activité",
];

const PRINCIPLES = [
  {
    title: "Une infrastructure distincte de vos locaux",
    body: "Le principe est simple : vos copies importantes ne doivent pas dépendre du même lieu que votre matériel ou vos documents papier.",
  },
  {
    title: "Accompagnement local depuis Guichen",
    body: "Zachary IT s'adresse aux particuliers, indépendants et petites structures qui veulent un échange direct et compréhensible.",
  },
  {
    title: "Réversibilité et formats exploitables",
    body: "L'objectif est que vos données restent récupérables et réutilisables selon les fonctionnalités réellement disponibles.",
  },
  {
    title: "Solutions ouvertes lorsque c'est pertinent",
    body: "Lorsque cela a du sens, les choix privilégient la transparence, l'interopérabilité et une moindre dépendance à une plateforme unique.",
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
        <p className="eyebrow">Zachary HOUNSA-HOUNKPA</p>
        <h1>
          Un sinistre peut détruire votre matériel. Il ne devrait pas détruire
          vos données.
        </h1>
        <p className="vitrine-hero-lead">
          Zachary IT vous aide à conserver une copie distante de vos documents
          importants, de vos sauvegardes et des fichiers utiles à la continuité
          de votre activité.
        </p>
        <p className="vitrine-hero-note">
          Basé à Guichen, Zachary IT accompagne les particuliers, associations
          et petites structures avec un ton rassurant, des solutions lisibles
          et un interlocuteur identifiable.
        </p>
        <div className="vitrine-hero-actions">
          <Link className="button" href="/offres">
            Découvrir les solutions de sauvegarde
          </Link>
          <Link className="button button-secondary" href="/contact">
            Préparer mon dossier de secours
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

      <section className="vitrine-resilience">
        <header className="vitrine-section-header">
          <p className="eyebrow">Sauvegarde distante</p>
          <h2>Vos données ne devraient pas dépendre d&apos;un seul lieu.</h2>
          <p className="vitrine-section-lead">
            Un incendie, un dégât des eaux, un vol, une panne ou un
            rançongiciel peuvent rendre inutilisables un ordinateur, un NAS,
            un serveur ou des documents conservés au même endroit.
          </p>
        </header>
        <div className="vitrine-resilience-layout">
          <article className="vitrine-resilience-message">
            <p className="vitrine-resilience-statement">
              Une sauvegarde conservée uniquement à domicile ou dans les locaux
              du client ne protège pas contre tous les sinistres physiques.
            </p>
            <p>
              Le rôle d&apos;une copie distante est de conserver l&apos;essentiel
              ailleurs, pour qu&apos;un incident matériel ne se transforme pas en
              perte définitive de documents, de souvenirs ou de repères utiles
              à la reprise.
            </p>
          </article>

          <ul
            className="vitrine-risk-grid"
            aria-label="Exemples de risques couverts"
          >
            {RISK_ITEMS.map((item) => (
              <li className="vitrine-risk-card" key={item}>
                {item}
              </li>
            ))}
          </ul>
        </div>
      </section>

      <section className="vitrine-method">
        <header className="vitrine-section-header">
          <p className="eyebrow">Parcours</p>
          <h2>
            Un chemin simple pour retrouver l&apos;essentiel en cas de besoin.
          </h2>
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
          <h2>
            Des solutions sobres pour sauvegarder, stocker et reprendre plus
            vite.
          </h2>
          <p className="vitrine-section-lead">
            Les prestations se combinent selon vos besoins. Pour un tarif
            indicatif, consultez le <Link href="/offres">catalogue d&apos;offres</Link>
            {" "}; pour des exemples concrets, voyez le{" "}
            <a href={PORTFOLIO_URL}>portfolio</a>.
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

      <section className="vitrine-dossier">
        <header className="vitrine-section-header">
          <p className="eyebrow">Dossier de secours numérique</p>
          <h2>Conservez aussi les preuves de ce que vous possédez.</h2>
          <p className="vitrine-section-lead">
            Les justificatifs sont souvent stockés au même endroit que les
            biens qu&apos;ils permettent d&apos;identifier. Lors d&apos;un sinistre, les
            objets, le matériel informatique et les preuves peuvent disparaître
            ensemble.
          </p>
        </header>

        <div className="vitrine-dossier-layout">
          <article className="vitrine-dossier-card">
            <p>
              Le dossier de secours numérique conserve une copie distante de vos
              documents importants afin qu&apos;ils restent accessibles depuis un
              autre appareil en cas de besoin.
            </p>
            <p>
              L&apos;objectif n&apos;est pas de vendre par la peur, mais de vous aider
              à préparer calmement ce qu&apos;il serait difficile de reconstituer
              dans l&apos;urgence.
            </p>
            <Link className="text-link" href="/contact">
              Évaluer mes besoins
            </Link>
          </article>

          <ul
            className="vitrine-dossier-list"
            aria-label="Exemples de documents à préserver"
          >
            {DOSSIER_ITEMS.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </div>
      </section>

      <section className="vitrine-audiences">
        <header className="vitrine-section-header">
          <p className="eyebrow">Pour qui</p>
          <h2>
            Un accompagnement utile aux particuliers comme aux petites
            structures.
          </h2>
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

      <section className="vitrine-principles">
        <header className="vitrine-section-header">
          <p className="eyebrow">Repères</p>
          <h2>Une approche locale, transparente et réversible.</h2>
          <p className="vitrine-section-lead">
            La localisation des données, la conservation, les solutions
            retenues et l&apos;accompagnement à la restauration doivent pouvoir
            être expliqués clairement, sans promesse absolue.
          </p>
        </header>

        <ul className="vitrine-principles-grid">
          {PRINCIPLES.map((item) => (
            <li className="vitrine-principle-card" key={item.title}>
              <h3>{item.title}</h3>
              <p>{item.body}</p>
            </li>
          ))}
        </ul>
      </section>

      <section className="vitrine-cta">
        <div>
          <h2>
            Besoin de protéger vos documents importants ou votre reprise
            d&apos;activité ?
          </h2>
          <p>
            Décrivez votre contexte en quelques lignes. Réponse personnelle par
            e-mail, sans engagement et sans discours alarmiste.
          </p>
        </div>
        <div className="vitrine-hero-actions">
          <Link className="button" href="/contact">
            Demander un accompagnement
          </Link>
          <Link className="button button-secondary" href="/offres">
            Protéger mes documents importants
          </Link>
        </div>
      </section>
    </>
  );
}
