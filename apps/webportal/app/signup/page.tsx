import type { Metadata } from "next";
import Link from "next/link";

import { SignupForm } from "@/components/SignupForm";
import {
  getPublicCommercialCatalog,
  getPublicPackCatalogContent,
} from "@/lib/internal-api";
import {
  findPackPresentation,
  resolvePackSelection,
  selectionFromSearchParams,
} from "@/lib/public-packs";
import { isSignupEnabled } from "@/lib/public-routes";

export const metadata: Metadata = {
  title: "Créer un compte",
  description:
    "Demandez la création d'un accès à l'espace client de Zachary HOUNSA-HOUNKPA EI.",
};

export const dynamic = "force-dynamic";

export default async function SignupPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const enabled = isSignupEnabled();
  const hcaptchaSiteKey =
    process.env.HCAPTCHA_SITE_KEY?.trim() || null;
  const selection = selectionFromSearchParams(await searchParams);
  const [catalogResult, packContentResult] = selection
    ? await Promise.all([
        getPublicCommercialCatalog(),
        getPublicPackCatalogContent(),
      ])
    : [null, null];
  const variant = selection && catalogResult
    ? resolvePackSelection(catalogResult.data, selection)
    : null;
  const packPresentation = selection
    ? findPackPresentation(
        selection.packKey,
        packContentResult?.data ?? null,
      )
    : null;

  return (
    <div className="signup-page">
      <Link className="back-link" href="/">
        <span aria-hidden="true">←</span> Retour à l&apos;accueil
      </Link>

      <header className="signup-header">
        <p className="eyebrow">Inscription</p>
        <h1>Créer un compte client</h1>
        <p className="signup-lead">
          Renseignez vos informations pour demander l&apos;ouverture d&apos;un
          accès. Après confirmation de votre adresse e-mail, notre équipe
          examine chaque demande avant d&apos;activer le compte.
        </p>
      </header>

      {enabled ? (
        <SignupForm
          hcaptchaSiteKey={hcaptchaSiteKey}
          initialPackSelection={
            selection && variant
              ? {
                  packKey: selection.packKey,
                  packLabel: packPresentation?.label ?? variant.offer.name,
                  commitmentMonths: selection.commitmentMonths,
                  paymentMode: selection.paymentMode,
                  monthlyPriceAmountCents: variant.monthlyPriceAmountCents,
                  setupFeeAmountCents: variant.setupFeeAmountCents,
                  firstChargeAmountCents: variant.firstChargeAmountCents,
                }
              : null
          }
        />
      ) : (
        <section className="signup-closed">
          <p>
            Les inscriptions en ligne ne sont pas ouvertes pour le moment.
            Pour toute demande d&apos;accès, contactez-nous via le{" "}
            <Link href="/contact">formulaire de contact</Link>.
          </p>
        </section>
      )}
    </div>
  );
}
