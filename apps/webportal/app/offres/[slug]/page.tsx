import type { Metadata } from "next";
import Link from "next/link";
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
  getPublicCommercialCatalog,
  getPublicManagedContent,
  getPublicPackCatalogContent,
} from "@/lib/internal-api";
import { isSignupEnabled } from "@/lib/public-routes";
import { resolvePackCatalog } from "@/lib/public-packs";

type PageProps = {
  params: Promise<{ slug: string }>;
};

export async function generateMetadata({
  params,
}: PageProps): Promise<Metadata> {
  const { slug } = await params;
  const pack = getPublicPackManifestBySlug(slug);

  if (!pack) {
    return {
      title: "Offre introuvable",
    };
  }

  return {
    title: `Fiche technique - ${pack.label}`,
    description: pack.description,
  };
}

export const revalidate = 300;

export default async function PublicPackSheetPage({ params }: PageProps) {
  const { slug } = await params;
  const manifest = getPublicPackManifestBySlug(slug);

  if (!manifest) {
    notFound();
  }

  const contentKey = buildPackSheetContentKey(manifest.key);
  const [catalogResult, catalogContentResult, managedContentResult] =
    await Promise.all([
      getPublicCommercialCatalog(),
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

  const packs = resolvePackCatalog(
    catalogResult.data,
    catalogContentResult.data,
  );
  const pack = packs.find((item) => item.key === manifest.key);
  if (!pack) {
    return (
      <ErrorState
        description="Cette offre n'est pas encore disponible à la publication."
        reference={catalogResult.correlationId}
        title="Offre non publiée"
      />
    );
  }

  const content = managedContentResult.data;
  const signupEnabled = isSignupEnabled();
  const componentOffers = manifest.technicalServiceReferences
    .map((reference) =>
      catalogResult.data.find(
        (offer) => offer.status === "active" && offer.externalReference === reference,
      ) ?? null,
    )
    .filter((offer): offer is (typeof catalogResult.data)[number] => offer !== null);
  const highlightLabel =
    catalogContentResult.data.packs.find((item) => item.packCode === pack.key)
      ?.highlightLabel ?? null;

  return (
    <div className="offres-page managed-pack-sheet-page">
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

        <PublicPackCard
          highlightLabel={highlightLabel}
          mode="signup"
          pack={pack}
          signupEnabled={signupEnabled}
        />
      </section>

      <SectionCard ariaLabel={`Composants techniques liés à ${pack.label}`}>
        <div className="page-header-split">
          <div>
            <span className="card-kicker">Catalogue actif</span>
            <h2>Composants techniques liés</h2>
            <p>
              Ce bloc est calculé automatiquement à partir des références
              techniques du pack et du catalogue commercial actuellement actif.
            </p>
          </div>
        </div>

        {componentOffers.length === 0 ? (
          <p className="field-hint">
            Aucun composant technique lié n&apos;est actuellement publié pour ce
            pack.
          </p>
        ) : (
          <div className="managed-pack-component-grid">
            {componentOffers.map((offer) => (
              <article className="managed-pack-component-card" key={offer.id}>
                <p className="card-kicker">{offer.category}</p>
                <h3>{offer.name}</h3>
                <p>{offer.description}</p>
                <p className="field-hint">
                  Référence : {offer.externalReference ?? "—"} · Unité :{" "}
                  {offer.unitLabel}
                </p>
              </article>
            ))}
          </div>
        )}
      </SectionCard>

      <SectionCard ariaLabel={`Détails éditoriaux de ${pack.label}`}>
        <div className="page-header-split">
          <div>
            <span className="card-kicker">Contenu éditable</span>
            <h2>Détails opérationnels</h2>
            <p>
              Cette partie est administrable en Markdown depuis le back-office
              et complète la synthèse tarifaire du pack.
            </p>
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
