import type { Metadata } from "next";
import Link from "next/link";

import { PublicConfigurator } from "@/components/PublicConfigurator";
import { resolveCatalogConfiguration } from "@/lib/catalog-configuration-server";
import {
  getPublicCommercialCatalog,
  getPublicPackCatalogContent,
} from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";
import {
  configurationFromSearchParams,
  DEFAULT_CATALOG_CONFIGURATION,
} from "@/lib/public-configurator";
import { resolvePackCatalog } from "@/lib/public-packs";
import { isSignupEnabled } from "@/lib/public-routes";

export const metadata: Metadata = buildPublicMetadata({
  title: "Configurer une offre",
  description:
    "Personnalisez un pack Zachary IT et obtenez une estimation claire avant l'inscription.",
  path: "/configurer",
  robots: { index: false, follow: true },
});

export const dynamic = "force-dynamic";

export default async function ConfigurerPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const rawSearchParams = await searchParams;
  const initialConfiguration =
    configurationFromSearchParams(rawSearchParams)
    ?? DEFAULT_CATALOG_CONFIGURATION;
  const fromDiagnostic = rawSearchParams.source === "diagnostic";
  const [{ data: offers }, { data: content }, configurationResult] =
    await Promise.all([
      getPublicCommercialCatalog(),
      getPublicPackCatalogContent(),
      resolveCatalogConfiguration(initialConfiguration),
    ]);
  const packs = resolvePackCatalog(offers, content);

  return (
    <div className="configurator-page">
      <Link className="back-link" href={fromDiagnostic ? "/diagnostic" : "/offres"}>
        <span aria-hidden="true">←</span>{" "}
        {fromDiagnostic ? "Retour au diagnostic" : "Retour aux offres"}
      </Link>

      <header className="configurator-header">
        <p className="eyebrow">Configurateur</p>
        <h1>Personnaliser une offre Zachary IT</h1>
        <p>
          Personnalisez votre offre selon vos besoins. Le tarif est mis à jour
          automatiquement avant de poursuivre vers l&apos;inscription.
        </p>
      </header>

      {packs.length === 0 ? (
        <section className="offres-empty">
          Les packs ne sont pas encore disponibles en ligne. Contactez-nous pour
          obtenir une proposition adaptée.
        </section>
      ) : (
        <PublicConfigurator
          initialConfiguration={initialConfiguration}
          initialResolution={configurationResult.ok ? configurationResult.data : null}
          packs={packs}
          signupEnabled={isSignupEnabled()}
        />
      )}
    </div>
  );
}
