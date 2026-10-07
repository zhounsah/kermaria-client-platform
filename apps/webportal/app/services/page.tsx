import type { Metadata } from "next";
import Link from "next/link";
import { headers } from "next/headers";
import { redirect } from "next/navigation";

import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionHeading } from "@/components/SectionHeading";
import { ServiceCard } from "@/components/ServiceCard";
import { StatusBadge } from "@/components/StatusBadge";
import { PublicServicesLandingPage } from "@/components/PublicServicesLandingPage";
import { getCurrentPortalSession, requireClientSession } from "@/lib/auth";
import {
  buildPublicMetadata,
  CONTENT_UNAVAILABLE_ROBOTS,
} from "@/lib/public-metadata";
import {
  getPortalAreaForRequest,
  getPortalRequestOriginFromHeaders,
} from "@/lib/public-routes";
import { resolveServicesPortalMode } from "@/lib/services-portal-mode";
import {
  getPendingBillingV2Selection,
  getClientVps,
  getPublicManagedContent,
  getPublicSitePageLayout,
  getServices,
  resolveDataSource,
} from "@/lib/internal-api";
import {
  parseStorefrontServicesLandingContent,
  presentPublicServicesLandingContent,
  resolveStorefrontBreadcrumb,
} from "@/lib/storefront-content";

export const dynamic = "force-dynamic";

export async function generateMetadata(): Promise<Metadata> {
  const content = await getPublicManagedContent("storefront:services");
  const parsedPage = content.data
    ? parseStorefrontServicesLandingContent(content.data.bodyMarkdown, true)
    : null;
  const page = parsedPage ? presentPublicServicesLandingContent(parsedPage) : null;
  return buildPublicMetadata({
    title: page?.seoTitle ?? "Services informatiques pour particuliers et petites structures",
    description: page?.seoDescription ?? "Sauvegarde, messagerie, réseau, site web et assistance pour avancer selon votre besoin avec Zachary IT.",
    path: "/services",
    // Sans contenu, le corps rend un `ErrorState` : ne pas laisser cet
    // instantane entrer dans l'index a la place de la page.
    ...(page ? {} : { robots: CONTENT_UNAVAILABLE_ROBOTS }),
  });
}

export default async function ServicesPage() {
  const requestHeaders = await headers();
  const portalArea = getPortalAreaForRequest(
    getPortalRequestOriginFromHeaders(requestHeaders),
  );
  const localSession = portalArea === "local"
    ? await getCurrentPortalSession()
    : null;
  const portalMode = resolveServicesPortalMode(
    portalArea,
    localSession?.user.role,
  );

  if (portalMode === "public") {
    const [contentResult, layoutResult] = await Promise.all([
      getPublicManagedContent("storefront:services"),
      getPublicSitePageLayout("/services"),
    ]);
    const parsedContent = contentResult.data
      ? parseStorefrontServicesLandingContent(contentResult.data.bodyMarkdown, true)
      : null;
    const content = parsedContent ? presentPublicServicesLandingContent(parsedContent) : null;
    return content ? (
      <PublicServicesLandingPage
        breadcrumbItems={resolveStorefrontBreadcrumb("/services")!}
        content={content}
        pageLayout={layoutResult.data}
      />
    ) : (
      <ErrorState
        description="Le catalogue de services est temporairement indisponible."
        reference={contentResult.correlationId}
        title="Services indisponibles"
      />
    );
  }

  if (portalMode === "admin") {
    redirect("/admin");
  }

  await requireClientSession();
  const [servicesResult, pendingSelectionResult, vpsResult] = await Promise.all([
    getServices(),
    getPendingBillingV2Selection(),
    getClientVps(),
  ]);
  const source = resolveDataSource([
    servicesResult.source,
    pendingSelectionResult.source,
    vpsResult.source,
  ]);
  const pendingSelection = pendingSelectionResult.data;
  const vpsByServiceCode = Map.groupBy(
    vpsResult.data,
    (vps) => vps.serviceCode,
  );

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
        description="Retrouvez les services inclus dans vos offres et suivez leur disponibilité."
        eyebrow="Espace client"
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
          {servicesResult.data.map((service) => {
            const vps = vpsByServiceCode.get(service.reference) ?? [];
            return (
              <ServiceCard
                key={service.id}
                service={service}
                vpsLinks={vps.map((item) => ({
                  href: `/services/vps/${encodeURIComponent(item.id)}`,
                  label: vps.length === 1 ? "Voir mon serveur" : `Voir le serveur ${item.hostname}`,
                }))}
              />
            );
          })}
        </section>
      )}

      {pendingSelection ? (
        <section className="request-history-section">
          <SectionHeading
            action={<StatusBadge label="À finaliser" tone="warning" />}
            description="Votre compte a bien été créé. Il ne reste qu'à reprendre l'offre choisie lors de votre demande d'inscription, puis à finaliser le paiement."
            title="Finaliser mon offre"
          />
          <div className="cta-panel">
            <p>
              Votre choix effectué à l&apos;inscription est conservé. Le prix
              sera recalculé lorsque vous reprendrez votre offre, avant le
              paiement.
            </p>
            <Link className="button" href="/formules/reprendre">
              Reprendre mon offre
            </Link>
          </div>
        </section>
      ) : null}

      <section className="request-history-section">
        <SectionHeading
          action={<StatusBadge label="Ajouter un service" tone="info" />}
          description="Choisissez une offre complète ou demandez un service précis."
          title="Ajouter un service"
        />
        <div className="cta-panel">
          <p>
            Découvrez les offres recommandées et les services disponibles
            séparément. Vous pourrez choisir ce qui correspond à votre besoin.
          </p>
          <Link className="button" href="/souscrire">
            Voir les offres et services
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
