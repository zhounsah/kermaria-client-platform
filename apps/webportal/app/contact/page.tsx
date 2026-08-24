import type { Metadata } from "next";
import Link from "next/link";

import { ContactForm } from "@/components/ContactForm";
import { getBillingV2FormulesCatalog } from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";

export const metadata: Metadata = buildPublicMetadata({
  title: "Contact",
  description:
    "Formulaire de contact pour échanger sur la sauvegarde distante, le dossier de secours numérique et les services Zachary IT.",
  path: "/contact",
});

export const dynamic = "force-dynamic";

type ContactPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

/**
 * Contact, éventuellement pré-rempli depuis une formule.
 *
 * Le seul contexte transporté est le code de la formule. Aucun montant n'est
 * repris : la page contact ne propose rien de chiffré, et recopier ici un
 * tarif calculé ailleurs créerait une deuxième version du prix, susceptible de
 * ne plus correspondre à celle que le moteur tarifaire produira.
 */
export default async function ContactPage({ searchParams }: ContactPageProps) {
  const resolvedSearchParams = await searchParams;
  const requestedFormule = resolvedSearchParams.formule;
  const trimmedFormule =
    typeof requestedFormule === "string"
      ? requestedFormule.trim().toLowerCase()
      : "";

  const catalogResult = trimmedFormule
    ? await getBillingV2FormulesCatalog().catch(() => null)
    : null;
  const preset =
    catalogResult?.data.presets.find(
      (candidate) => candidate.code === trimmedFormule,
    ) ?? null;

  const defaultSubject = preset ? `Demande de formule — ${preset.name}` : "";
  const backLink = preset
    ? { href: "/offres", label: "Retour aux formules" }
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

      {preset ? (
        <p className="contact-offer-banner">
          Demande pré-remplie pour la formule : <strong>{preset.name}</strong>.
        </p>
      ) : null}

      <ContactForm
        defaultSubject={defaultSubject}
        formuleCode={preset ? preset.code : null}
      />
    </div>
  );
}
