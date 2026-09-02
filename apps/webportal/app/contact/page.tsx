import type { Metadata } from "next";
import Link from "next/link";

import { ContactForm } from "@/components/ContactForm";
import { getBillingV2FormulesCatalog } from "@/lib/internal-api";
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

  const snippets = await resolveSystemSnippets();
  const defaultSubject = preset ? `Demande de formule — ${preset.name}` : "";
  // Les liens `?formule=` sont poses par les cartes et le tableau
  // comparatif de `/offres` : c'est bien la page d'ou vient le visiteur.
  // L'intitule annoncait « Retour aux formules », qui designe une autre page.
  const backLink = preset
    ? { href: "/offres", label: "Retour aux offres" }
    : { href: "/", label: "Retour à l'accueil" };

  return (
    <div className="contact-page">
      <Link className="back-link" href={backLink.href}>
        <span aria-hidden="true">←</span> {backLink.label}
      </Link>

      <header className="contact-header">
        <p className="eyebrow">Contact</p>
        <h1>Parlons de votre besoin</h1>
        <p className="contact-lead">
          Décrivez votre situation en quelques lignes : ce qui ne fonctionne
          pas, ce que vous voulez mettre en place, ou simplement la question
          que vous vous posez. Sauvegarde, messagerie, réseau, hébergement,
          postes de travail ou assistance au quotidien — si nous ne sommes pas
          les bons interlocuteurs, nous vous le dirons.
        </p>
      </header>

      {preset ? (
        <p className="contact-offer-banner">
          Demande pré-remplie pour l&apos;offre : <strong>{preset.name}</strong>.
        </p>
      ) : null}

      <ContactForm
        confirmationText={snippets.contact_form_confirmation}
        defaultSubject={defaultSubject}
        formuleCode={preset ? preset.code : null}
        privacyNotice={snippets.contact_form_privacy_notice}
      />

      {/* Ce bloc ne decrit que ce que le systeme fait reellement : le message
          part par e-mail et la reponse revient a l'adresse saisie. Aucun
          delai n'est annonce — rien dans le produit ne permet de le tenir. */}
      <section aria-labelledby="contact-next-steps" className="signup-steps-card">
        <h2 id="contact-next-steps">Ce qui se passe ensuite</h2>
        <ol>
          <li>Votre message nous est transmis par e-mail.</li>
          <li>
            Nous répondons à l&apos;adresse que vous indiquez, en reprenant les
            éléments à préciser.
          </li>
          <li>
            S&apos;il faut regarder l&apos;existant avant de chiffrer quoi que
            ce soit, nous vous proposons un cadrage plutôt qu&apos;un devis
            approximatif.
          </li>
        </ol>
      </section>

      <p className="contact-form-note">
        Vous cherchez plutôt à situer votre besoin ?{" "}
        <Link href="/diagnostic">Le diagnostic en ligne</Link> propose une
        orientation en quelques questions, et{" "}
        <Link href="/services">les pages services</Link> détaillent chaque
        prestation.
      </p>
    </div>
  );
}
