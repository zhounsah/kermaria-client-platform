import type { Metadata } from "next";
import Link from "next/link";

import { PublicDiagnosticWizard } from "@/components/PublicDiagnosticWizard";
import {
  getPublicCommercialCatalog,
  getPublicPackCatalogContent,
} from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";
import { resolvePackCatalog } from "@/lib/public-packs";

export const metadata: Metadata = buildPublicMetadata({
  title: "Diagnostic sauvegarde et accès distant",
  description:
    "Répondez à quelques questions pour identifier la solution Zachary IT adaptée à vos besoins de sauvegarde, stockage et accès distant.",
  path: "/diagnostic",
});

export const dynamic = "force-dynamic";

const BENEFITS = [
  {
    title: "Résultat immédiat",
    body: "Un niveau de risque clair, sans jargon.",
  },
  {
    title: "Conseils prioritaires",
    body: "Les premières actions à mettre en place.",
  },
  {
    title: "Sans inscription",
    body: "Aucun compte ni achat nécessaire pour obtenir votre résultat.",
  },
];

export default async function DiagnosticPage() {
  const [{ data: offers }, { data: content }] = await Promise.all([
    getPublicCommercialCatalog(),
    getPublicPackCatalogContent(),
  ]);
  const packs = resolvePackCatalog(offers, content);

  return (
    <div className="diagnostic-page">
      <Link className="back-link" href="/offres">
        <span aria-hidden="true">{"<-"}</span> Retour aux offres
      </Link>

      <header className="diagnostic-header">
        <div>
          <p className="eyebrow">Diagnostic</p>
          <h1>Vos données importantes pourraient-elles disparaître demain ?</h1>
          <p>
            Répondez à quelques questions simples. Le diagnostic s&apos;adresse aux
            particuliers, indépendants, associations et petites structures qui
            veulent un premier avis sur leurs sauvegardes, leur stockage et
            leur accès distant.
          </p>
        </div>
        <div className="diagnostic-benefits" aria-label="Benefices du diagnostic">
          {BENEFITS.map((benefit) => (
            <article key={benefit.title}>
              <span aria-hidden="true">✓</span>
              <h2>{benefit.title}</h2>
              <p>{benefit.body}</p>
            </article>
          ))}
        </div>
      </header>

      {packs.length === 0 ? (
        <section className="offres-empty">
          Les packs ne sont pas encore disponibles en ligne. Contactez-nous pour
          obtenir une proposition adaptée.
        </section>
      ) : (
        <PublicDiagnosticWizard packs={packs} />
      )}
    </div>
  );
}
