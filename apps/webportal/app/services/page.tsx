import type { Metadata } from "next";
import Link from "next/link";
import { headers } from "next/headers";

import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionHeading } from "@/components/SectionHeading";
import { ServiceCard } from "@/components/ServiceCard";
import { StatusBadge } from "@/components/StatusBadge";
import { PublicStorefrontPage } from "@/components/PublicStorefrontPage";
import { requireClientSession } from "@/lib/auth";
import { buildPublicMetadata } from "@/lib/public-metadata";
import { getPortalArea } from "@/lib/public-route-config";
import { getPortalRequestOriginFromHeaders } from "@/lib/public-routes";
import {
  getPendingBillingV2Selection,
  getPublicManagedContent,
  getServices,
  resolveDataSource,
} from "@/lib/internal-api";
import { parseStorefrontPageContent, resolveStorefrontBreadcrumb } from "@/lib/storefront-content";

export const dynamic = "force-dynamic";

export async function generateMetadata(): Promise<Metadata> {
  const content = await getPublicManagedContent("storefront:services");
  const page = content.data
    ? parseStorefrontPageContent(content.data.bodyMarkdown)
    : null;
  return buildPublicMetadata({
    title: page?.seoTitle ?? "Services IT gérés pour indépendants, associations et TPE",
    description: page?.seoDescription ?? "Cloud, hébergement, domaines, messagerie, réseau, sécurité, sauvegarde et support gérés par Zachary IT.",
    path: "/services",
  });
}

export default async function ServicesPage() {
  const requestHeaders = await headers();
  const portalArea = getPortalArea(
    getPortalRequestOriginFromHeaders(requestHeaders),
  );

  if (portalArea === "public" || portalArea === "local") {
    const contentResult = await getPublicManagedContent("storefront:services");
    const content = contentResult.data
      ? parseStorefrontPageContent(contentResult.data.bodyMarkdown)
      : null;
    return content ? <PublicStorefrontPage breadcrumbItems={resolveStorefrontBreadcrumb("/services")!} content={content} /> : (
      <ErrorState
        description="Le catalogue de services est temporairement indisponible."
        reference={contentResult.correlationId}
        title="Services indisponibles"
      />
    );
  }

  await requireClientSession();
  const [servicesResult, pendingSelectionResult] = await Promise.all([
    getServices(),
    getPendingBillingV2Selection(),
  ]);
  const source = resolveDataSource([
    servicesResult.source,
    pendingSelectionResult.source,
  ]);
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
              Découvrir les formules
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
          description="Souscrivez une formule clé en main ou prenez un service à la carte, sans dépendre d'un mapping technique caché."
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
