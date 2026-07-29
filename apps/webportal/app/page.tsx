import type { Metadata } from "next";
import Link from "next/link";
import { headers } from "next/headers";
import { redirect } from "next/navigation";

import { getCurrentPortalSession } from "@/lib/auth";
import {
  PORTFOLIO_URL,
  getPortalPublicUrlFromHeaders,
  isVitrinePublicEnabled,
} from "@/lib/public-routes";
import { resolvePortalRoleUrl } from "@/lib/public-route-config";

export const metadata: Metadata = {
  title: "Informatique claire et utile",
  description:
    "Comparez des offres lisibles, faites cadrer votre besoin et avancez vers un parcours simple d'inscription ou de contact.",
  openGraph: {
    title: "Informatique claire et utile",
    description:
      "Hébergement, sauvegarde, accès distant, VPN et maintenance pour petites structures, associations et professionnels.",
  },
};

function organizationJsonLd(baseUrl: string) {
  return {
    "@context": "https://schema.org",
    "@graph": [
      {
        "@type": "ProfessionalService",
        "@id": `${baseUrl}/#organization`,
        name: "Zachary HOUNSA-HOUNKPA EI",
        url: baseUrl,
        description:
          "Accompagnement informatique pour petites structures, associations et professionnels : hébergement, sauvegarde, accès distant, VPN et maintenance.",
        sameAs: [PORTFOLIO_URL],
        serviceType: [
          "Hébergement de dossiers",
          "Sauvegarde de données",
          "Accès distant sécurisé",
          "VPN privé",
          "Maintenance informatique",
          "Réseau et infrastructure",
        ],
      },
      {
        "@type": "WebSite",
        "@id": `${baseUrl}/#website`,
        url: baseUrl,
        name: "Zachary HOUNSA-HOUNKPA EI",
        publisher: {
          "@id": `${baseUrl}/#organization`,
        },
      },
    ],
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

const PROOF_POINTS = [
  {
    title: "Parcours lisible avant engagement",
    body: "Vous voyez d'abord les niveaux de service, les prochaines étapes et ce qui sera finalisé après l'ouverture du compte.",
  },
  {
    title: "Catalogue et cas réels se répondent",
    body: "Le catalogue donne un cadre simple, le portfolio montre des exemples concrets quand vous avez besoin de vous projeter.",
  },
  {
    title: "Mise en service préparée pour durer",
    body: "Le but n'est pas de vendre un écran de plus, mais un cadre stable pour héberger, sauvegarder, connecter et maintenir vos usages.",
  },
];

const ENTRY_PATHS = [
  {
    title: "Comparer les packs",
    body: "Le meilleur point d'entrée si vous voulez une vue immédiate des options, des tarifs et des niveaux d'engagement.",
    ctaLabel: "Voir les offres",
    href: "/offres",
  },
  {
    title: "Faire cadrer un besoin",
    body: "Si votre situation est encore floue, le formulaire de contact permet de décrire vos contraintes avant de choisir un pack.",
    ctaLabel: "Demander un échange",
    href: "/contact",
  },
  {
    title: "Voir des exemples concrets",
    body: "Le portfolio aide à comprendre le type d'accompagnement et de résultats que vous pouvez attendre dans des contextes réels.",
    ctaLabel: "Ouvrir le portfolio",
    href: PORTFOLIO_URL,
  },
];

export default async function HomePage() {
  const session = await getCurrentPortalSession();
  const baseUrl = getPortalPublicUrlFromHeaders(await headers());

  if (session?.user.role === "client_user") {
    redirect(resolvePortalRoleUrl(baseUrl, "client_user"));
  }

  if (session?.user.role === "internal_admin") {
    redirect(resolvePortalRoleUrl(baseUrl, "internal_admin"));
  }

  if (!isVitrinePublicEnabled()) {
    redirect("/login");
  }

  return (
    <>
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{
          __html: JSON.stringify(organizationJsonLd(baseUrl)),
        }}
      />

      <section className="vitrine-hero">
        <p className="eyebrow">Zachary HOUNSA-HOUNKPA</p>
        <h1>Informatique claire et utile.</h1>
        <p className="vitrine-hero-lead">
          Vos outils informatiques, mieux organisés. Comparez des packs
          lisibles, clarifiez votre besoin, puis activez un cadre simple pour
          héberger, sauvegarder, connecter et maintenir ce dont vous avez
          besoin sans jargon inutile.
        </p>
        <p className="vitrine-hero-note">
          Pour les petites structures, associations et professionnels qui
          veulent un cadre simple, un suivi lisible et un interlocuteur
          identifiable, avec un parcours clair entre choix du pack, ouverture
          d&apos;accès et mise en service.
        </p>
        <div className="vitrine-hero-actions">
          <Link className="button" href="/offres">
            Comparer les packs
          </Link>
          <Link className="button button-secondary" href="/contact">
            Parler de votre projet
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

      <section className="vitrine-proof">
        <header className="vitrine-section-header">
          <p className="eyebrow">Réassurance</p>
          <h2>Un site qui vous aide à choisir, pas un tunnel opaque.</h2>
          <p className="vitrine-section-lead">
            L&apos;objectif est de rendre lisibles le cadrage, le choix du pack,
            l&apos;ouverture d&apos;accès et la mise en service, sans vous noyer dans le
            jargon ni vous pousser trop vite vers une connexion.
          </p>
        </header>
        <ul className="vitrine-proof-grid">
          {PROOF_POINTS.map((item) => (
            <li className="vitrine-proof-card" key={item.title}>
              <h3>{item.title}</h3>
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
            Les prestations se combinent selon vos besoins. Commencez par le{" "}
            <Link href="/offres">catalogue d&apos;offres</Link> pour comparer les
            packs et comprendre les prochaines étapes, ou passez par le{" "}
            <Link href="/contact">contact</Link> si vous avez besoin d&apos;un
            cadrage plus libre ; pour des exemples concrets, voyez aussi le{" "}
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

      <section className="vitrine-entry-paths">
        <header className="vitrine-section-header">
          <p className="eyebrow">Parcours</p>
          <h2>Choisissez votre point d&apos;entrée selon votre niveau de clarté.</h2>
        </header>
        <div className="vitrine-entry-grid">
          {ENTRY_PATHS.map((path) => (
            <article className="vitrine-entry-card" key={path.title}>
              <h3>{path.title}</h3>
              <p>{path.body}</p>
              {path.href.startsWith("http") ? (
                <a className="text-link" href={path.href}>
                  {path.ctaLabel}
                </a>
              ) : (
                <Link className="text-link" href={path.href}>
                  {path.ctaLabel}
                </Link>
              )}
            </article>
          ))}
        </div>
      </section>

      <section className="vitrine-cta">
        <div>
          <h2>Comparer un pack ou partir d&apos;un besoin plus ouvert ?</h2>
          <p>
            Le catalogue permet de choisir un parcours d&apos;activation clair. Si
            votre besoin est encore flou, décrivez votre situation en quelques
            lignes pour un retour personnalisé.
          </p>
        </div>
        <div className="vitrine-hero-actions">
          <Link className="button" href="/offres">
            Voir les offres
          </Link>
          <Link className="button button-secondary" href="/contact">
            Demander un échange
          </Link>
        </div>
      </section>
    </>
  );
}
