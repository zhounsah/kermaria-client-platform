import type { Metadata } from "next";
import Link from "next/link";

import { ContactForm } from "@/components/ContactForm";
import { PublicPackSelectionSummary } from "@/components/PublicPackSelectionSummary";
import { getPublicCommercialCatalog, getPublicPackCatalogContent } from "@/lib/internal-api";
import {
  buildSignupPackSnapshot,
  selectionFromSearchParams,
} from "@/lib/public-packs";

export const metadata: Metadata = {
  title: "Contact",
  description:
    "Présentez votre besoin ou reprenez un pack déjà selectionné pour obtenir un retour personnalisé.",
};

type ContactPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default async function ContactPage({ searchParams }: ContactPageProps) {
  const resolvedSearchParams = await searchParams;
  const offerParam = resolvedSearchParams.offer;
  const trimmedOffer = Array.isArray(offerParam)
    ? (offerParam[0]?.trim() ?? "")
    : (offerParam?.trim() ?? "");
  const selection = selectionFromSearchParams(resolvedSearchParams);

  const [catalogResult, packContentResult] = selection || trimmedOffer
    ? await Promise.all([
        getPublicCommercialCatalog(),
        selection ? getPublicPackCatalogContent() : Promise.resolve(null),
      ])
    : [null, null];

  let offerReference: string | null = null;
  let offerName: string | null = null;

  if (trimmedOffer && catalogResult) {
    const match = catalogResult.data.find((entry) => entry.id === trimmedOffer);
    if (match) {
      offerReference = match.id;
      offerName = match.name;
    }
  }

  const packSelection = selection && catalogResult
    ? buildSignupPackSnapshot(
        catalogResult.data,
        selection,
        packContentResult?.data ?? null,
      )
    : null;

  const defaultSubject = packSelection
    ? `Demande de pack - ${packSelection.packLabel}`
    : offerName
      ? `Demande de devis - ${offerName}`
      : "";

  const backLink = packSelection || offerReference
    ? { href: "/offres", label: "Retour aux offres" }
    : { href: "/", label: "Retour à l'accueil" };

  return (
    <div className="contact-page">
      <Link className="back-link" href={backLink.href}>
        <span aria-hidden="true">{"<-"}</span> {backLink.label}
      </Link>

      <header className="contact-header">
        <p className="eyebrow">Contact</p>
        <h1>Nous écrire</h1>
        <p className="contact-lead">
          Utilisez ce formulaire pour cadrer un besoin, demander un devis ou
          reprendre un pack déjà selectionné. Vous recevrez une réponse
          personnelle par e-mail sous un délai raisonnable.
        </p>
      </header>

      {packSelection ? (
        <div className="contact-selection-stack">
          <PublicPackSelectionSummary
            commitmentMonths={packSelection.commitmentMonths}
            description="Votre choix est bien repris dans le formulaire ci-dessous. Utilisez le message libre pour préciser votre contexte, vos contraintes ou vos questions avant ouverture du compte."
            eyebrow="Pack repris"
            firstChargeAmountCents={packSelection.firstChargeAmountCents}
            monthlyPriceAmountCents={packSelection.monthlyPriceAmountCents}
            packLabel={packSelection.packLabel}
            paymentMode={packSelection.paymentMode}
            setupFeeAmountCents={packSelection.setupFeeAmountCents}
            title={`Demande autour de ${packSelection.packLabel}`}
          />
          <section className="contact-next-step-card">
            <h2>Ce qui se passe ensuite</h2>
            <ol>
              <li>Vous nous donnez le contexte de votre demande.</li>
              <li>Nous revenons vers vous avec un cadrage ou des précisions.</li>
              <li>Si le pack vous convient, vous poursuivez ensuite vers l&apos;ouverture d&apos;accès.</li>
            </ol>
          </section>
        </div>
      ) : offerName ? (
        <p className="contact-offer-banner">
          Demande pré-remplie pour l&apos;offre : <strong>{offerName}</strong>.
        </p>
      ) : null}

      <ContactForm
        defaultSubject={defaultSubject}
        offerReference={offerReference}
        selectedPackLabel={packSelection?.packLabel ?? null}
      />
    </div>
  );
}
