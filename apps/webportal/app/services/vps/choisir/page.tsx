import type { Metadata } from "next";
import { notFound } from "next/navigation";

import { ErrorState } from "@/components/ErrorState";
import {
  PublicVpsConfigurator,
  type PublicVpsConfiguratorSelection,
} from "@/components/PublicVpsConfigurator";
import { describeTierAttributes } from "@/lib/billing-v2-formules";
import { formatCurrencyFromCents } from "@/lib/formatters";
import { getBillingV2FormulesCatalog } from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";

export const dynamic = "force-dynamic";

type PageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

/**
 * Cette route correspond au comparatif VPS public : les codes ne sont donc
 * pas un catalogue concurrent, mais une borne de navigation. Les prix,
 * caractéristiques et autorisations viennent toujours du catalogue renvoyé
 * par API-INTERNAL.
 */
const CONFIGURABLE_VPS_SERVICE_CODES = new Set(["VPS-LOCAL", "VPS-CLOUD"]);

export function generateMetadata(): Metadata {
  return buildPublicMetadata({
    title: "Configurer votre VPS",
    description:
      "Préparez les informations techniques de votre VPS avant la prochaine étape de commande.",
    path: "/services/vps/choisir",
    robots: { index: false, follow: false },
  });
}

export default async function VpsConfigurationPage({ searchParams }: PageProps) {
  const query = await searchParams;
  const serviceCode = singleSearchParam(query.serviceCode);
  const tierCode = singleSearchParam(query.tierCode);

  if (!serviceCode || !tierCode) {
    notFound();
  }

  const result = await getBillingV2FormulesCatalog();
  if (result.error) {
    return (
      <ErrorState
        description="La sélection VPS ne peut pas être vérifiée pour le moment."
        reference={result.correlationId}
        title="Configuration VPS indisponible"
      />
    );
  }

  const service = result.data.services.find(
    (candidate) => candidate.code === serviceCode,
  );
  const tier = service?.tiers.find((candidate) => candidate.code === tierCode);

  // Les paramètres URL ne sont qu'une intention. Une sélection qui n'est plus
  // publique, selectable ou commandable est refusée avant d'atteindre le
  // configurateur, sans jamais fabriquer de prix côté navigateur.
  if (
    !service
    || !tier
    || !CONFIGURABLE_VPS_SERVICE_CODES.has(service.code)
    || !service.publicVisible
    || !service.selfServiceOrderable
    || !tier.publicSelectable
  ) {
    notFound();
  }

  const selection: PublicVpsConfiguratorSelection = {
    serviceCode: service.code,
    serviceName: service.name,
    serviceDescription: service.description,
    tierCode: tier.code,
    tierLabel: tier.label,
    tierDescription: tier.description,
    specifications: describeTierAttributes(tier),
    pricing: {
      monthlyLabel: formatCurrencyFromCents(tier.monthlyAmountCents),
      setupFees: (tier.priceComponents ?? [])
        .filter(
          (component) => component.billingCadence === "one_time"
            && component.chargeTrigger === "initial_subscription",
        )
        .map((component) => ({
          amountLabel: formatCurrencyFromCents(component.amountCents),
        })),
    },
  };

  return <PublicVpsConfigurator selection={selection} />;
}

function singleSearchParam(value: string | string[] | undefined) {
  return typeof value === "string" && value.trim() ? value.trim() : null;
}
