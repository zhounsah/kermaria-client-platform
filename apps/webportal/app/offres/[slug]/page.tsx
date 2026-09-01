import type { Metadata } from "next";
import Link from "next/link";
import { headers } from "next/headers";
import { notFound } from "next/navigation";
import {
  buildPackSheetContentKey,
  getPublicPackManifestBySlug,
} from "@kermaria/shared";

import { ErrorState } from "@/components/ErrorState";
import { ManagedMarkdown } from "@/components/ManagedMarkdown";
import { MockNotice } from "@/components/MockNotice";
import { PublicPackCard } from "@/components/PublicPackCard";
import { SectionCard } from "@/components/SectionCard";
import { formatDateTime } from "@/lib/formatters";
import {
  getBillingV2FormulesCatalog,
  getPublicManagedContent,
  getPublicPackCatalogContent,
} from "@/lib/internal-api";
import {
  getPortalPublicUrlFromHeaders,
  isSignupEnabled,
} from "@/lib/public-routes";
import { buildPublicMetadata } from "@/lib/public-metadata";
import { buildPublicPackViews } from "@/lib/public-packs";
import { JsonLd, breadcrumbJsonLd, packServiceJsonLd } from "@/lib/seo";

type PageProps = {
  params: Promise<{ slug: string }>;
};

export async function generateMetadata({
  params,
}: PageProps): Promise<Metadata> {
  const { slug } = await params;
  const pack = getPublicPackManifestBySlug(slug);

  if (!pack) {
    // Pas de canonical sur une 404 : la page appelle `notFound()`.
    return {
      title: "Offre introuvable",
    };
  }

  return buildPublicMetadata({
    title: `Fiche technique - ${pack.label}`,
    description: pack.description,
    // `pack.slug` et non le `slug` de l'URL : si un alias de slug est un jour
    // accepte, la canonical continue de pointer la forme unique.
    path: `/offres/${pack.slug}`,
  });
}

export const revalidate = 300;

export default async function PublicPackSheetPage({ params }: PageProps) {
  const { slug } = await params;
  const manifest = getPublicPackManifestBySlug(slug);

  if (!manifest) {
    notFound();
  }

  // Le balisage schema.org exige des URL absolues, donc l'hote reel de la
  // requete (`zachary-it.fr` en production, `www.home.bzh` en
  // recette). Cet appel rend la page dynamique et neutralise le
  // `revalidate` ci-dessus — sans consequence tant que le layout racine
  // impose deja le rendu dynamique a tout l'arbre (cf. le TODO ISR dans
  // `app/layout.tsx`), mais a reprendre en meme temps que ce chantier.
  const baseUrl = getPortalPublicUrlFromHeaders(await headers());

  const contentKey = buildPackSheetContentKey(manifest.key);
  const [catalogResult, catalogContentResult, managedContentResult] =
    await Promise.all([
      getBillingV2FormulesCatalog(),
      getPublicPackCatalogContent(),
      getPublicManagedContent(contentKey),
    ]);

  if (managedContentResult.error || !managedContentResult.data) {
    return (
      <ErrorState
        description="Impossible de charger cette fiche technique pour le moment."
        reference={managedContentResult.correlationId}
        title="Fiche technique indisponible"
      />
    );
  }

  const packs = buildPublicPackViews(
    catalogResult.data,
    catalogContentResult.data,
  );
  const pack = packs.find((item) => item.key === manifest.key);
  if (!pack) {
    return (
      <ErrorState
        description="Cette formule n'est pas encore publiée au catalogue."
        reference={catalogResult.correlationId}
        title="Formule non publiée"
      />
    );
  }

  const content = managedContentResult.data;
  const signupEnabled = isSignupEnabled();
  // Les references techniques du pack sont des codes `billing_v2_services` :
  // le bloc ci-dessous decrit donc les services du catalogue V2 reellement
  // publies, et non une recopie editoriale qui pourrait diverger.
  const componentServices = manifest.technicalServiceReferences
    .map(
      (reference) =>
        catalogResult.data.services.find(
          (service) => service.code === reference,
        ) ?? null,
    )
    .filter(
      (service): service is (typeof catalogResult.data.services)[number] =>
        service !== null,
    );

  return (
    <div className="offres-page managed-pack-sheet-page">
      <JsonLd
        data={packServiceJsonLd(baseUrl, {
          slug: manifest.slug,
          label: pack.label,
          description: pack.description,
        })}
      />
      <JsonLd
        data={breadcrumbJsonLd(baseUrl, [
          { name: "Offres", path: "/offres" },
          { name: pack.label, path: `/offres/${manifest.slug}` },
        ])}
      />

      <header className="offres-header managed-pack-sheet-header">
        <p className="eyebrow">Fiche technique pack</p>
        <h1>{pack.label}</h1>
        <p className="offres-lead">{pack.description}</p>
        <div className="managed-content-meta">
          {content.versionLabel ? (
            <p className="managed-content-version">{content.versionLabel}</p>
          ) : null}
          {content.updatedAt ? (
            <p className="managed-content-updated">
              Mis à jour le {formatDateTime(content.updatedAt)}
            </p>
          ) : null}
        </div>
      </header>

      <p>
        <Link className="text-link" href="/offres">
          ← Retour au comparatif des offres
        </Link>
      </p>

      <section className="managed-pack-sheet-hero">
        <div className="managed-pack-sheet-summary">
          <SectionCard ariaLabel={`Synthèse de ${pack.label}`}>
            <h2>À retenir</h2>
            <p>{pack.headline}</p>
            <ul className="check-list managed-pack-sheet-checklist">
              {pack.highlights.slice(0, 4).map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
          </SectionCard>
        </div>

        <PublicPackCard pack={pack} signupEnabled={signupEnabled} />
      </section>

      <SectionCard ariaLabel={`Composants techniques liés à ${pack.label}`}>
        <div className="page-header-split">
          <div>
            <span className="card-kicker">Catalogue actif</span>
            <h2>Composants techniques liés</h2>
            <p>
              Ce bloc présente les éléments associés à cette formule.
            </p>
          </div>
        </div>

        {componentServices.length === 0 ? (
          <p className="field-hint">
            Aucun composant technique lié n&apos;est actuellement publié pour
            cette formule.
          </p>
        ) : (
          <div className="managed-pack-component-grid">
            {componentServices.map((service) => (
              <article
                className="managed-pack-component-card"
                key={service.code}
              >
                <p className="card-kicker">{service.category}</p>
                <h3>{service.name}</h3>
                <p className="field-hint">
                  Référence : {service.code} · Portée : {service.scopeType}
                </p>
              </article>
            ))}
          </div>
        )}
      </SectionCard>

      <SectionCard ariaLabel={`Détails opérationnels de ${pack.label}`}>
        <div className="page-header-split">
          <div>
            <span className="card-kicker">Détails opérationnels</span>
          </div>
        </div>

        <ManagedMarkdown markdown={content.bodyMarkdown} />
      </SectionCard>

      <MockNotice
        correlationId={managedContentResult.correlationId}
        source={managedContentResult.source}
      />
    </div>
  );
}
