import type { Metadata } from "next";
import Link from "next/link";
import { headers } from "next/headers";

import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { PublicPackCard } from "@/components/PublicPackCard";
import { SectionHeading } from "@/components/SectionHeading";
import { ServiceCard } from "@/components/ServiceCard";
import { StatusBadge } from "@/components/StatusBadge";
import { PublicServicesLandingPage } from "@/components/PublicServicesPages";
import { requireClientSession } from "@/lib/auth";
import { buildPublicMetadata } from "@/lib/public-metadata";
import { getPortalArea } from "@/lib/public-route-config";
import { getPortalRequestOriginFromHeaders } from "@/lib/public-routes";
import {
  getPendingPackSelection,
  getPublicCommercialCatalog,
  getPublicPackCatalogContent,
  getServices,
  resolveDataSource,
} from "@/lib/internal-api";
import {
  findPendingPackSelectionForPack,
  findPackPresentation,
  resolvePackCatalog,
} from "@/lib/public-packs";

export const metadata: Metadata = buildPublicMetadata({
  title: "Services informatiques pour indépendants, associations et PME",
  description:
    "Cloud, hébergement, domaines, messagerie, réseau, sécurité et accompagnement IT : Zachary IT vous aide à garder une informatique fiable au quotidien.",
  path: "/services",
});

export const dynamic = "force-dynamic";

export default async function ServicesPage() {
  const requestHeaders = await headers();
  const portalArea = getPortalArea(
    getPortalRequestOriginFromHeaders(requestHeaders),
  );

  if (portalArea === "public" || portalArea === "local") {
    return <PublicServicesLandingPage />;
  }

  await requireClientSession();
  const [
    servicesResult,
    catalogResult,
    packContentResult,
    pendingSelectionResult,
  ] = await Promise.all([
    getServices(),
    getPublicCommercialCatalog(),
    getPublicPackCatalogContent(),
    getPendingPackSelection(),
  ]);
  const source = resolveDataSource([
    servicesResult.source,
    catalogResult.source,
    packContentResult.source,
    pendingSelectionResult.source,
  ]);
  const packs = resolvePackCatalog(catalogResult.data, packContentResult.data);
  const pendingSelection = pendingSelectionResult.data;

  return (
    <>
      <PageHeader
        action={
          <div className="button-row">
            <Link className="button button-secondary" href="/backups">
              Voir mes sauvegardes
            </Link>
            <Link className="button" href="/souscrire">
              Ajouter un service
            </Link>
          </div>
        }
        description="Retrouvez ici les services réellement déduits de vos packs, options et souscriptions. Pour ajouter un service, ouvrez l'espace « Souscrire »."
        eyebrow="Périmètre client"
        title="Mes services"
      />

      {servicesResult.error ? (
        <ErrorState
          action={
            <Link className="button" href="/services">
              Réessayer
            </Link>
          }
          description="Impossible de charger vos services pour le moment."
          reference={servicesResult.correlationId}
          title="Services indisponibles"
        />
      ) : servicesResult.data.length === 0 ? (
        <EmptyState
          action={
            <Link className="button" href="/souscrire">
              Découvrir les offres
            </Link>
          }
          description="Aucun service n'est actuellement associé à ce compte."
          title="Aucun service"
        />
      ) : (
        <section className="service-grid" aria-label="Services du compte">
          {servicesResult.data.map((service) => (
            <ServiceCard key={service.id} service={service} />
          ))}
        </section>
      )}

      {pendingSelection ? (
        <section className="request-history-section">
          <SectionHeading
            action={<StatusBadge label="À finaliser" tone="warning" />}
            description="Votre compte a bien été créé. Il ne reste qu'à finaliser le paiement du pack choisi lors de votre demande d'inscription."
            title="Finaliser mon pack"
          />
          <div className="public-pack-grid">
            {packs
              .filter((pack) => pack.key === pendingSelection.snapshot.packKey)
              .map((pack) => (
                <PublicPackCard
                  key={`pending-${pack.key}`}
                  mode="subscribe"
                  pack={pack}
                  initialSelection={findPendingPackSelectionForPack(
                    pendingSelection,
                    pack.key,
                  )}
                  highlightLabel={
                    findPackPresentation(
                      pack.key,
                      packContentResult.data,
                    )?.highlightLabel ?? "Sélection reprise"
                  }
                />
              ))}
          </div>
        </section>
      ) : null}

      <section className="request-history-section">
        <SectionHeading
          action={<StatusBadge label="Ajouter un service" tone="info" />}
          description="Souscrivez un pack clé en main ou prenez une option à la carte, sans dépendre d'un mapping technique caché."
          title="Étendre mon périmètre"
        />
        <div className="cta-panel">
          <p>
            L&apos;espace « Souscrire » regroupe les packs grand public et les
            options individuelles. Chaque option à la carte se prend
            séparément, sans obligation de pack.
          </p>
          <Link className="button" href="/souscrire">
            Ouvrir l&apos;espace Souscrire
          </Link>
        </div>
      </section>

      {source !== "unavailable" ? (
        <MockNotice
          correlationId={servicesResult.correlationId}
          source={source}
        />
      ) : null}
    </>
  );
}
