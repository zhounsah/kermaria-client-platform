import type { Metadata } from "next";
import Link from "next/link";

import type { PublicClientSolution } from "@kermaria/shared";

import { getPublicClientSolutionPortal } from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";

export const metadata: Metadata = buildPublicMetadata({
  title: "Solutions",
  description:
    "Portail d'accès aux solutions mises à disposition des clients : cliquez sur une tuile pour ouvrir le service correspondant.",
  path: "/solutions",
  // Portail d'acces client, pas une page vitrine : ~115 mots, aucun `h2`,
  // aucun trafic a en attendre, et elle tire vers le bas la qualite moyenne
  // percue du site. `follow` reste actif pour que les liens sortants
  // continuent de transmettre.
  //
  // Volontairement PAS de `Disallow` dans `robots.ts` : une URL bloquee au
  // crawl n'est jamais exploree, donc ce `noindex` ne serait jamais lu. Les
  // deux directives sont contradictoires.
  robots: { index: false, follow: true },
});

export const revalidate = 300;

export default async function SolutionsPage() {
  const { data } = await getPublicClientSolutionPortal();
  const { settings, solutions } = data;

  return (
    <div className="solutions-page">
      <header className="solutions-header">
        {settings.eyebrow ? (
          <p className="eyebrow">{settings.eyebrow}</p>
        ) : null}
        <h1>{settings.title}</h1>
        {settings.description ? (
          <p className="solutions-lead">{settings.description}</p>
        ) : null}
      </header>

      {solutions.length === 0 ? (
        <p className="solutions-empty">
          Aucune solution n&apos;est publiée pour le moment. Contactez-nous pour
          connaître les accès disponibles pour votre compte.
        </p>
      ) : (
        <section
          aria-label="Solutions accessibles"
          className="solutions-grid"
        >
          {solutions.map((solution) => (
            <SolutionTile key={solution.id} solution={solution} />
          ))}
        </section>
      )}

      {settings.footerNote ? (
        <p className="solutions-footnote">{settings.footerNote}</p>
      ) : null}

      <p className="solutions-help">
        Besoin d&apos;un accès qui ne figure pas ici ?{" "}
        <Link className="text-link" href="/contact">
          Contactez-nous
        </Link>
        .
      </p>
    </div>
  );
}

function SolutionTile({ solution }: { solution: PublicClientSolution }) {
  const externalAttributes = solution.opensInNewTab
    ? { target: "_blank" as const, rel: "noopener noreferrer" }
    : {};

  return (
    <a
      className="solution-tile"
      href={solution.targetUrl}
      {...externalAttributes}
    >
      <span className="solution-tile-header">
        <span className="solution-tile-title">{solution.title}</span>
        {solution.opensInNewTab ? (
          <span className="solution-tile-external" aria-hidden="true">
            ↗
          </span>
        ) : null}
      </span>
      <span className="solution-tile-body">
        {solution.hasLogo ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            alt=""
            className="solution-tile-logo"
            src={buildLogoUrl(solution)}
          />
        ) : (
          <span className="solution-tile-monogram" aria-hidden="true">
            {buildMonogram(solution.title)}
          </span>
        )}
      </span>
      {solution.tagline ? (
        <span className="solution-tile-tagline">{solution.tagline}</span>
      ) : null}
      <span className="sr-only">
        {solution.opensInNewTab
          ? "Ouvre le service dans un nouvel onglet"
          : "Ouvre le service"}
      </span>
    </a>
  );
}

function buildLogoUrl(solution: PublicClientSolution) {
  const path = `/api/solutions/${encodeURIComponent(solution.id)}/logo`;
  return solution.logoUpdatedAt
    ? `${path}?v=${encodeURIComponent(solution.logoUpdatedAt)}`
    : path;
}

function buildMonogram(title: string) {
  const initials = title
    .split(/\s+/)
    .filter((word) => word.length > 0)
    .slice(0, 2)
    .map((word) => word[0]?.toUpperCase() ?? "")
    .join("");

  return initials || title.slice(0, 2).toUpperCase();
}
