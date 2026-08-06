import type { Metadata } from "next";
import Link from "next/link";

import { ContactForm } from "@/components/ContactForm";
import { PublicPackSelectionSummary } from "@/components/PublicPackSelectionSummary";
import {
  getPublicCommercialCatalog,
  getPublicPackCatalogContent,
} from "@/lib/internal-api";
import {
  buildSignupPackSnapshot,
  selectionFromSearchParams,
} from "@/lib/public-packs";

export const metadata: Metadata = {
  title: "Contact",
  description:
    "Formulaire de contact pour échanger sur la sauvegarde distante, le dossier de secours numérique et les services Zachary IT.",
  alternates: { canonical: "/contact" },
};

type ContactPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default async function ContactPage({ searchParams }: ContactPageProps) {
  const resolvedSearchParams = await searchParams;
  const offerParam = resolvedSearchParams.offer;
  const trimmedOffer =
    typeof offerParam === "string" ? offerParam.trim() : "";
  const selection = selectionFromSearchParams(resolvedSearchParams);

  const [catalogResult, packContentResult] = selection || trimmedOffer
    ? await Promise.all([
        getPublicCommercialCatalog(),
        selection ? getPublicPackCatalogContent() : Promise.resolve(null),
      ])
    : [null, null];

  let offerReference: string | null = null;
  let offerName: string | null = null;

  const activeOffer = catalogResult?.data.find(
    (entry) => entry.id === trimmedOffer && entry.status === "active",
  ) ?? null;
  const candidatePackSelection = selection && catalogResult
    ? buildSignupPackSnapshot(
        catalogResult.data,
        selection,
        packContentResult?.data ?? null,
      )
    : null;
  const packSelection =
    candidatePackSelection
    && activeOffer?.id === candidatePackSelection.offerId
      ? candidatePackSelection
      : null;

  if (activeOffer) {
    offerReference = activeOffer.id;
    offerName = activeOffer.name;
  }

  const defaultSubject = packSelection
    ? `Demande de pack — ${packSelection.packLabel}`
    : offerName
      ? `Demande de devis — ${offerName}`
      : "";

  const backLink = packSelection || offerReference
    ? { href: "/offres", label: "Retour aux offres" }
    : { href: "/", label: "Retour à l'accueil" };

  return (
    <div className="contact-page">
      <Link className="back-link" href={backLink.href}>
        <span aria-hidden="true">←</span> {backLink.label}
      </Link>

      <header className="contact-header">
        <p className="eyebrow">Contact</p>
        <h1>Nous écrire</h1>
        <p className="contact-lead">
          Utilisez ce formulaire pour toute demande autour de la sauvegarde
          distante, du stockage documentaire, de la continuité d&apos;activité ou
          de toute autre question générale. Vous recevrez une réponse par
          e-mail sous un délai raisonnable.
        </p>
      </header>

      {packSelection ? (
        <div className="contact-selection-stack">
          <PublicPackSelectionSummary
            commitmentMonths={packSelection.commitmentMonths}
            description="Votre choix est repris dans le formulaire ci-dessous. Précisez votre contexte ou vos questions dans le message libre."
            eyebrow="Pack repris"
            firstChargeAmountCents={packSelection.firstChargeAmountCents}
            monthlyPriceAmountCents={packSelection.monthlyPriceAmountCents}
            packLabel={packSelection.packLabel}
            paymentMode={packSelection.paymentMode}
            setupFeeAmountCents={packSelection.setupFeeAmountCents}
            title={`Demande autour de ${packSelection.packLabel}`}
          />
        </div>
      ) : offerName ? (
        <p className="contact-offer-banner">
          Demande pré-remplie pour l&apos;offre :{" "}
          <strong>{offerName}</strong>.
        </p>
      ) : null}

      <ContactForm
        defaultSubject={defaultSubject}
        offerReference={offerReference}
      />
    </div>
  );
}
