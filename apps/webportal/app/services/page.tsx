import Link from "next/link";

import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { PublicPackCard } from "@/components/PublicPackCard";
import { SectionHeading } from "@/components/SectionHeading";
import { ServiceCard } from "@/components/ServiceCard";
import { StatusBadge } from "@/components/StatusBadge";
import { requireClientSession } from "@/lib/auth";
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
import { getPayPalMode } from "@/lib/paypal";
import { getStripeMode } from "@/lib/stripe";

export const metadata = {
  title: "Services",
};

export const dynamic = "force-dynamic";

export default async function ServicesPage() {
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
  const stripeMode = getStripeMode();
  const paypalMode = getPayPalMode();

  return (
    <>
      <PageHeader
        action={
          <Link className="button" href="/profile/subscriptions">
            GÃ©rer mes souscriptions
          </Link>
        }
        description="Consultez les services dÃ©jÃ  suivis sur votre compte et finalisez un pack grand public sans repasser par les anciennes offres techniques."
        eyebrow="PÃ©rimÃ¨tre client"
        title="Mes services et packs"
      />

      {servicesResult.error ? (
        <ErrorState
          action={
            <Link className="button" href="/services">
              RÃ©essayer
            </Link>
          }
          description="Impossible de charger vos services pour le moment."
          reference={servicesResult.correlationId}
          title="Services indisponibles"
        />
      ) : servicesResult.data.length === 0 ? (
        <EmptyState
          action={
            <Link className="button" href="/offres">
              DÃ©couvrir les packs
            </Link>
          }
          description="Aucun service n'est actuellement associÃ© Ã  ce compte."
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
            action={<StatusBadge label="Ã€ finaliser" tone="warning" />}
            description="Votre compte a bien Ã©tÃ© crÃ©Ã©. Il ne reste qu'Ã  finaliser le paiement du pack choisi lors de votre demande d'inscription."
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
                  stripeMode={stripeMode}
                  paypalMode={paypalMode}
                  highlightLabel={
                    findPackPresentation(
                      pack.key,
                      packContentResult.data,
                    )?.highlightLabel ?? "SÃ©lection reprise"
                  }
                />
              ))}
          </div>
        </section>
      ) : null}

      <section className="request-history-section">
        <SectionHeading
          action={<StatusBadge label="Catalogue packs" tone="info" />}
          description="Les nouvelles souscriptions passent dÃ©sormais uniquement par les packs grand public. Les anciennes offres techniques restent gÃ©rÃ©es en historique et en administration."
          title="Souscrire Ã  un pack"
        />
        {catalogResult.error ? (
          <ErrorState
            compact
            description="Impossible de charger le catalogue packs pour le moment."
            reference={catalogResult.correlationId}
            title="Catalogue indisponible"
          />
        ) : packs.length === 0 ? (
          <EmptyState
            description="Aucun pack grand public n'est actuellement affichÃ© dans le portail."
            title="Catalogue vide"
          />
        ) : (
          <section className="public-pack-grid" aria-label="Packs grand public">
            {packs.map((pack) => (
              <PublicPackCard
                key={pack.key}
                mode="subscribe"
                pack={pack}
                initialSelection={findPendingPackSelectionForPack(
                  pendingSelection,
                  pack.key,
                )}
                stripeMode={stripeMode}
                paypalMode={paypalMode}
                highlightLabel={findPackPresentation(
                  pack.key,
                  packContentResult.data,
                )?.highlightLabel}
              />
            ))}
          </section>
        )}
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
