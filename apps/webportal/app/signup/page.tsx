import type { Metadata } from "next";
import Link from "next/link";

import { PublicPackSelectionSummary } from "@/components/PublicPackSelectionSummary";
import { SignupForm } from "@/components/SignupForm";
import {
  getPublicCommercialCatalog,
  getPublicPackCatalogContent,
} from "@/lib/internal-api";
import {
  buildSignupPackSnapshot,
  selectionFromSearchParams,
} from "@/lib/public-packs";
import { isSignupEnabled } from "@/lib/public-routes";

export const metadata: Metadata = {
  title: "Créer un compte",
  description:
    "Demandez l'ouverture de votre accès client et reprenez, si besoin, le pack déjà selectionné sur la vitrine.",
};

export const dynamic = "force-dynamic";

export default async function SignupPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const enabled = isSignupEnabled();
  const hcaptchaSiteKey = process.env.HCAPTCHA_SITE_KEY?.trim() || null;
  const selection = selectionFromSearchParams(await searchParams);
  const [catalogResult, packContentResult] = selection
    ? await Promise.all([
        getPublicCommercialCatalog(),
        getPublicPackCatalogContent(),
      ])
    : [null, null];
  const packSelection = selection && catalogResult
    ? buildSignupPackSnapshot(
        catalogResult.data,
        selection,
        packContentResult?.data ?? null,
      )
    : null;

  return (
    <div className="signup-page">
      <Link className="back-link" href="/">
        <span aria-hidden="true">{"<-"}</span> Retour à l&apos;accueil
      </Link>

      <header className="signup-header">
        <p className="eyebrow">Inscription</p>
        <h1>Créer un compte client</h1>
        <p className="signup-lead">
          Renseignez vos informations pour demander l&apos;ouverture de votre accès
          client. Le parcours reste simple et assume : confirmation de votre
          adresse e-mail, validation de votre demande par notre équipe, puis
          définition du mot de passe avant la finalisation du pack choisi. Avec
          v0.38, cette étape prepare aussi l&apos;identité cible sous
          <code> clients.home.bzh</code>.
        </p>
      </header>

      {packSelection ? (
        <div className="signup-selection-stack">
          <PublicPackSelectionSummary
            commitmentMonths={packSelection.commitmentMonths}
            description="Le pack selectionné reste attaché à cette demande. Le paiement ne se fait pas sur cet écran : vous retrouverez ensuite ce contexte dans l'espace client."
            eyebrow="Pack repris"
            firstChargeAmountCents={packSelection.firstChargeAmountCents}
            monthlyPriceAmountCents={packSelection.monthlyPriceAmountCents}
            packLabel={packSelection.packLabel}
            paymentMode={packSelection.paymentMode}
            setupFeeAmountCents={packSelection.setupFeeAmountCents}
          />
          <section className="signup-steps-card" aria-label="Étapes d'ouverture">
            <h2>Ce qui se passe ensuite</h2>
            <ol>
              <li>Vous confirmez votre adresse e-mail.</li>
              <li>Nous validons l&apos;ouverture de votre accès client.</li>
              <li>Vous définissez votre mot de passe et activez votre accès client.</li>
              <li>Si l'écriture AD est active, l'identité clients.home.bzh est finalisée à ce moment-là.</li>
              <li>Vous finalisez ensuite le pack depuis l&apos;espace client.</li>
            </ol>
          </section>
        </div>
      ) : null}

      {enabled ? (
        <SignupForm
          hcaptchaSiteKey={hcaptchaSiteKey}
          initialPackSelection={packSelection
            ? {
                packKey: packSelection.packKey,
                packLabel: packSelection.packLabel,
                commitmentMonths: packSelection.commitmentMonths,
                paymentMode: packSelection.paymentMode,
                monthlyPriceAmountCents: packSelection.monthlyPriceAmountCents,
                setupFeeAmountCents: packSelection.setupFeeAmountCents,
                firstChargeAmountCents: packSelection.firstChargeAmountCents,
              }
            : null}
        />
      ) : (
        <section className="signup-closed">
          <p>
            Les inscriptions en ligne ne sont pas ouvertes pour le moment. Pour
            toute demande d&apos;accès, contactez-nous via le{" "}
            <Link href="/contact">formulaire de contact</Link>.
          </p>
        </section>
      )}
    </div>
  );
}
