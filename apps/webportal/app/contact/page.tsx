import type { Metadata } from "next";
import Link from "next/link";

import { ContactForm } from "@/components/ContactForm";
import { SitePageFrame } from "@/components/SitePageFrame";
import { findAssistanceOffer, resolveAssistanceContactContext } from "@/lib/assistance-offers";
import { getBillingV2FormulesCatalog, getPublicSitePageLayout } from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";
import { resolveSystemSnippets } from "@/lib/system-snippets";

export const metadata: Metadata = buildPublicMetadata({
  title: "Contact",
  description:
    "Décrivez votre besoin à Zachary IT à Guichen : maintenance, réseau, sauvegarde, messagerie ou accès à distance pour votre activité.",
  path: "/contact",
});

export const dynamic = "force-dynamic";

type ContactPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

/**
 * Contact, éventuellement pré-rempli depuis une offre ou un abonnement d'assistance.
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
  const requestedAssistance = findAssistanceOffer(resolvedSearchParams.assistance);

  const [catalogResult, snippets, layoutResult] = await Promise.all([
    trimmedFormule || requestedAssistance
      ? getBillingV2FormulesCatalog().catch(() => null)
      : Promise.resolve(null),
    resolveSystemSnippets(),
    getPublicSitePageLayout("/contact"),
  ]);
  const preset =
    catalogResult?.data.presets.find(
      (candidate) => candidate.code === trimmedFormule,
    ) ?? null;
  // Un lien historique vers une offre garde sa priorité si les deux
  // paramètres sont présents. Le contexte assistance exige un service public.
  const assistance = preset ? null : resolveAssistanceContactContext(
    resolvedSearchParams.assistance,
    catalogResult?.data ?? null,
  );
  const defaultSubject = preset
    ? `Demande d'offre — ${preset.name}`
    : assistance?.subject ?? "";
  const formKey = preset
    ? `formule:${preset.code}`
    : assistance ? `assistance:${assistance.code}` : "general";
  // Les liens `?formule=` sont poses par les cartes et le tableau
  // comparatif de `/offres` : c'est bien la page d'ou vient le visiteur.
  // L'intitule annoncait « Retour aux formules », qui designe une autre page.
  const backLink = preset
    ? { href: "/offres", label: "Retour aux offres" }
    : assistance
      ? { href: assistance.href, label: "Retour aux abonnements d’assistance" }
      : { href: "/", label: "Retour à l'accueil" };

  const slots = {
    contact_back: <Link className="back-link" href={backLink.href}>
        <span aria-hidden="true">←</span> {backLink.label}
      </Link>,
    contact_offer: preset ? <p className="contact-offer-banner">
      Demande pré-remplie pour l&apos;offre : <strong>{preset.name}</strong>.
    </p> : assistance ? <p className="contact-offer-banner">
      Demande de devis pour l&apos;abonnement : <strong>{assistance.name}</strong>.
      {" "}Vous pouvez compléter le message avant de l&apos;envoyer.
    </p> : null,
    contact_form: <ContactForm
        key={formKey}
        confirmationText={snippets.contact_form_confirmation}
        defaultMessage={assistance?.message ?? ""}
        defaultSubject={defaultSubject}
        formuleCode={preset ? preset.code : null}
        privacyNotice={snippets.contact_form_privacy_notice}
      />,
  };
  return (
    <div className="contact-page">
      <SitePageFrame area="public" pageKey="/contact" initialLayout={layoutResult.data}
        slots={slots}>{null}</SitePageFrame>
    </div>
  );
}
