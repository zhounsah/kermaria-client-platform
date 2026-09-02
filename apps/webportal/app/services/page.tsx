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
import { getPortalArea } from "@/lib/public-route-config";
import { getPortalRequestOriginFromHeaders } from "@/lib/public-routes";
import { resolveServicesPortalMode } from "@/lib/services-portal-mode";
import {
  getPendingBillingV2Selection,
  getClientVps,
  getPublicManagedContent,
  getServices,
  resolveDataSource,
} from "@/lib/internal-api";
import {
  parseStorefrontServicesLandingContent,
  resolveStorefrontBreadcrumb,
} from "@/lib/storefront-content";

export const dynamic = "force-dynamic";

export async function generateMetadata(): Promise<Metadata> {
  const content = await getPublicManagedContent("storefront:services");
  const page = content.data
    ? parseStorefrontServicesLandingContent(content.data.bodyMarkdown, true)
    : null;
  return buildPublicMetadata({
    title: page?.seoTitle ?? "Services IT gérés pour indépendants, associations et TPE",
    description: page?.seoDescription ?? "Cloud, hébergement, domaines, messagerie, réseau, sécurité, sauvegarde et support gérés par Zachary IT.",
    path: "/services",
    // Sans contenu, le corps rend un `ErrorState` : ne pas laisser cet
    // instantane entrer dans l'index a la place de la page.
    ...(page ? {} : { robots: CONTENT_UNAVAILABLE_ROBOTS }),
  });
}

export default async function ServicesPage() {
  const requestHeaders = await headers();
  const portalArea = getPortalArea(
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
    const contentResult = await getPublicManagedContent("storefront:services");
    const content = contentResult.data
      ? parseStorefrontServicesLandingContent(contentResult.data.bodyMarkdown, true)
      : null;
    return content ? (
      <PublicServicesLandingPage
        breadcrumbItems={resolveStorefrontBreadcrumb("/services")!}
        content={content}
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
              Découvrir les formules
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
                  label: vps.length === 1 ? "Voir mon VPS" : `Voir ${item.hostname}`,
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
            description="Votre compte a bien été créé. Il ne reste qu'à reprendre la formule choisie lors de votre demande d'inscription, puis à finaliser le paiement."
            title="Finaliser ma formule"
          />
          <div className="cta-panel">
            <p>
              La configuration retenue à l&apos;inscription est conservée telle
              quelle. Elle est retarifée par nos serveurs au moment de la
              reprise : aucun montant n&apos;a été figé entre-temps.
            </p>
            <Link className="button" href="/formules/reprendre">
              Reprendre ma formule
            </Link>
          </div>
        </section>
      ) : null}

      <section className="request-history-section">
        <SectionHeading
          action={<StatusBadge label="Ajouter un service" tone="info" />}
          description="Souscrivez une formule clé en main ou ajoutez un service à la carte selon vos besoins."
          title="Étendre mon périmètre"
        />
        <div className="cta-panel">
          <p>
            L&apos;espace « Souscrire » regroupe les formules recommandées et
            les services individuels. Chaque service à la carte se prend
            séparément, sans obligation de formule.
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
