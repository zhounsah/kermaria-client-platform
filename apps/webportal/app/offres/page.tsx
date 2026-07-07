import type { Metadata } from "next";

import { PublicPackComparisonTable } from "@/components/PublicPackComparisonTable";
import {
  getPublicCommercialCatalog,
  getPublicPackCatalogContent,
} from "@/lib/internal-api";
import { resolvePackCatalog } from "@/lib/public-packs";
import { isSignupEnabled } from "@/lib/public-routes";

export const metadata: Metadata = {
  title: "Offres",
  description:
    "Quatre packs simples à comprendre, avec engagement au choix et tarification lisible.",
};

export const revalidate = 300;

export default async function OffresPage() {
  const [{ data: offers }, { data: content }] = await Promise.all([
    getPublicCommercialCatalog(),
    getPublicPackCatalogContent(),
  ]);
  const signupEnabled = isSignupEnabled();
  const packs = resolvePackCatalog(offers, content);

  return (
    <div className="offres-page">
      <header className="offres-header">
        {content.pageEyebrow.trim() ? (
          <p className="eyebrow">{content.pageEyebrow}</p>
        ) : null}
        <h1>{content.pageTitle}</h1>
        <p className="offres-lead">{content.pageDescription}</p>
      </header>

      {packs.length === 0 ? (
        <p className="offres-empty">
          Les packs ne sont pas encore disponibles en ligne. Contactez-nous pour
          obtenir une proposition adaptée.
        </p>
      ) : (
        <PublicPackComparisonTable
          content={content}
          packs={packs}
          signupEnabled={signupEnabled}
        />
      )}
    </div>
  );
}
