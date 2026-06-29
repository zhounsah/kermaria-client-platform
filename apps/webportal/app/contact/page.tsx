import type { Metadata } from "next";
import Link from "next/link";

import { ContactForm } from "@/components/ContactForm";
import { getPublicCommercialCatalog } from "@/lib/internal-api";

export const metadata: Metadata = {
  title: "Contact",
  description:
    "Formulaire de contact pour échanger avec Zachary HOUNSA-HOUNKPA EI.",
};

type ContactPageProps = {
  searchParams: Promise<{ offer?: string }>;
};

export default async function ContactPage({ searchParams }: ContactPageProps) {
  const { offer } = await searchParams;
  const trimmedOffer = offer?.trim() || null;

  let offerReference: string | null = null;
  let offerName: string | null = null;

  if (trimmedOffer) {
    const { data: offers } = await getPublicCommercialCatalog();
    const match = offers.find((entry) => entry.id === trimmedOffer);
    if (match) {
      offerReference = match.id;
      offerName = match.name;
    }
  }

  const defaultSubject = offerName
    ? `Demande de devis — ${offerName}`
    : "";

  const backLink = offerReference
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
          Utilisez ce formulaire pour toute demande commerciale ou question
          générale. Vous recevrez une réponse par e-mail sous un délai
          raisonnable.
        </p>
      </header>

      {offerName ? (
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
