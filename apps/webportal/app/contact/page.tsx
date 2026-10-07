import type { Metadata } from "next";
import Link from "next/link";

import { ContactForm } from "@/components/ContactForm";
import { SitePageFrame } from "@/components/SitePageFrame";
import { getBillingV2FormulesCatalog, getPublicSitePageLayout } from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";
import { resolveSystemSnippets } from "@/lib/system-snippets";

export const metadata: Metadata = buildPublicMetadata({
  title: "Contact",
  description:
    "Contactez Zachary IT à Guichen (35) : sauvegarde, messagerie, réseau, "
    + "hébergement, postes de travail et assistance pour indépendants, "
    + "associations et petites entreprises.",
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

  const [snippets, layoutResult] = await Promise.all([
    resolveSystemSnippets(),
    getPublicSitePageLayout("/contact"),
  ]);
  const defaultSubject = preset ? `Demande d'offre — ${preset.name}` : "";
  // Les liens `?formule=` sont poses par les cartes et le tableau
  // comparatif de `/offres` : c'est bien la page d'ou vient le visiteur.
  // L'intitule annoncait « Retour aux formules », qui designe une autre page.
  const backLink = preset
    ? { href: "/offres", label: "Retour aux offres" }
    : { href: "/", label: "Retour à l'accueil" };

  const slots = {
    contact_back: <Link className="back-link" href={backLink.href}>
        <span aria-hidden="true">←</span> {backLink.label}
      </Link>,
    contact_offer: preset ? <p className="contact-offer-banner">
      Demande pré-remplie pour l&apos;offre : <strong>{preset.name}</strong>.
    </p> : null,
    contact_form: <ContactForm
        confirmationText={snippets.contact_form_confirmation}
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
