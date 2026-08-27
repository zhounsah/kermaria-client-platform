"use client";
import type { ManagedContentDetail, ManagedContentMutationResponse, ManagedContentPayload } from "@kermaria/shared";
import { useRouter } from "next/navigation";
import { type FormEvent, useRef, useState } from "react";
import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";
import {
  isStorefrontSelfServiceCta,
  parseStorefrontPageContent,
  parseStorefrontServicesLandingContent,
  STOREFRONT_SERVICES_CATEGORY_DESTINATIONS,
  STOREFRONT_SERVICES_PROBLEM_DESTINATIONS,
  type StorefrontPageContent,
  type StorefrontServicesLandingContent,
} from "@/lib/storefront-content";
const ALLOWED_DESTINATIONS = [
  "/contact", "/diagnostic", "/tarifs", "/services", "/formules",
  "/services/cloud-hebergement", "/services/domaines-messagerie", "/services/reseau-securite", "/services/support-it",
  "/services/vps", "/services/infogerance-vps", "/services/hebergement-web", "/services/maintenance-linux", "/services/maintenance-wordpress",
  "/services/sauvegarde-externalisee", "/services/supervision-informatique", "/services/supervision-nas", "/services/vpn-entreprise",
  "/services/bureau-windows-distance", "/services/unifi", "/services/firewall", "/services/cloudflare-waf", "/services/gestion-dns-domaines", "/services/messagerie-professionnelle",
] as const;
type AdminStorefrontContentFormProps = {
  content: ManagedContentDetail;
  selfServiceOrderable: boolean | null;
};
export function AdminStorefrontContentForm({
  content,
  selfServiceOrderable,
}: AdminStorefrontContentFormProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const isServicesLanding = content.key === "storefront:services";
  const parsed = isServicesLanding
    ? parseStorefrontServicesLandingContent(content.bodyMarkdown, true)
    : parseStorefrontPageContent(content.bodyMarkdown);
  const [page, setPage] = useState<
    StorefrontPageContent | StorefrontServicesLandingContent | null
  >(parsed);
  const [message, setMessage] = useState<{ tone: "success" | "error"; text: string } | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const allowedDestinations = selfServiceOrderable === false
    ? ALLOWED_DESTINATIONS.filter((href) => href !== "/formules")
    : [...ALLOWED_DESTINATIONS];
  const relatedDestinations = isServicesLanding
    ? [...STOREFRONT_SERVICES_CATEGORY_DESTINATIONS]
    : allowedDestinations;
  if (!page) {
    return <FormMessage title="Contenu invalide" tone="error"><p>Ce contenu structuré ne peut pas être affiché dans le formulaire. Il n’a pas été modifié.</p></FormMessage>;
  }
  const change = <K extends keyof StorefrontPageContent>(
    key: K,
    value: StorefrontPageContent[K],
  ) => setPage((current) => current ? { ...current, [key]: value } : current);

  const changeProblemEntries = (
    updater: (entries: StorefrontServicesLandingContent["problemEntries"]) =>
      StorefrontServicesLandingContent["problemEntries"],
  ) => setPage((current) => {
    if (!current || !("problemEntries" in current)) return current;
    return { ...current, problemEntries: updater(current.problemEntries) };
  });
  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isSubmittingRef.current) return;
    if (!page) return;
    if (selfServiceOrderable === false && (
      isStorefrontSelfServiceCta(page.ctaLabel, page.ctaHref)
      || page.relatedLinks.some((link) => isStorefrontSelfServiceCta(link.label, link.href))
    )) {
      setMessage({
        tone: "error",
        text: "Ce service Billing n’est pas commandable en libre-service. Utilisez un CTA de devis, d’audit ou de contact.",
      });
      return;
    }
    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);
    const payload: ManagedContentPayload = { bodyMarkdown: JSON.stringify(page), versionLabel: null };
    const result = await requestBffJson<ManagedContentMutationResponse>(`/api/admin/content/${encodeURIComponent(content.key)}`, { method: "PATCH", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload) });
    setMessage(result.ok
      ? { tone: "success", text: result.data.changed ? "La page a été enregistrée." : "Aucune modification supplémentaire n’a été détectée." }
      : { tone: "error", text: result.error.message });
    if (result.ok) router.refresh();
    isSubmittingRef.current = false;
    setIsSubmitting(false);
  }
  return <form className="form-card managed-content-form storefront-admin-form" onSubmit={handleSubmit}>
    <div className="managed-content-editor-grid">
      <div className="managed-content-editor-column">
        <label>Titre commercial / H1<input maxLength={200} onChange={(event) => change("title", event.target.value)} value={page.title} /></label>
        <label>Introduction<textarea maxLength={1200} onChange={(event) => change("lead", event.target.value)} rows={4} value={page.lead} /></label>
        <label>Title SEO<input maxLength={200} onChange={(event) => change("seoTitle", event.target.value)} value={page.seoTitle} /></label>
        <label>Meta description<input maxLength={400} onChange={(event) => change("seoDescription", event.target.value)} value={page.seoDescription} /></label>
        <label>Libellé du CTA<input maxLength={80} onChange={(event) => change("ctaLabel", event.target.value)} value={page.ctaLabel} /></label>
        <label>Destination du CTA<select onChange={(event) => change("ctaHref", event.target.value)} value={page.ctaHref}>{!allowedDestinations.includes(page.ctaHref as typeof allowedDestinations[number]) ? <option value={page.ctaHref}>{page.ctaHref} — non autorisé actuellement</option> : null}{allowedDestinations.map((href) => <option key={href} value={href}>{href}</option>)}</select></label>
        {selfServiceOrderable === false ? <p className="form-hint">Billing indique que ce service n’est pas commandable en libre-service : les CTA de commande/configuration et `/formules` sont interdits.</p> : null}
      </div>
      <div className="managed-content-preview-card"><span className="card-kicker">Aperçu du haut de page</span><h3>{page.title}</h3><p>{page.lead}</p><p className="managed-content-preview-meta">SEO : {page.seoTitle}</p></div>
    </div>
    {isServicesLanding && "problemEntries" in page ? (
      <fieldset>
        <legend>Problèmes / besoins</legend>
        <p className="form-hint">
          Ces six entrées orientent vers une Ressource, une catégorie ou une page service.
          Elles ne doivent jamais pointer directement vers un configurateur Billing.
        </p>
        {page.problemEntries.map((entry, index) => (
          <div className="storefront-admin-item" key={`${entry.href}-${index}`}>
            <label>
              Titre du besoin
              <input
                maxLength={120}
                onChange={(event) => changeProblemEntries((entries) =>
                  entries.map((item, current) =>
                    current === index ? { ...item, title: event.target.value } : item
                  )
                )}
                value={entry.title}
              />
            </label>
            <label>
              Description
              <textarea
                maxLength={400}
                onChange={(event) => changeProblemEntries((entries) =>
                  entries.map((item, current) =>
                    current === index ? { ...item, description: event.target.value } : item
                  )
                )}
                rows={3}
                value={entry.description}
              />
            </label>
            <label>
              Destination
              <select
                onChange={(event) => changeProblemEntries((entries) =>
                  entries.map((item, current) =>
                    current === index
                      ? {
                          ...item,
                          href: event.target.value as StorefrontServicesLandingContent["problemEntries"][number]["href"],
                        }
                      : item
                  )
                )}
                value={entry.href}
              >
                {STOREFRONT_SERVICES_PROBLEM_DESTINATIONS.map((href) => (
                  <option key={href} value={href}>{href}</option>
                ))}
              </select>
            </label>
          </div>
        ))}
      </fieldset>
    ) : null}
    <fieldset><legend>Sections pédagogiques</legend>{page.sections.map((section, index) => <div className="storefront-admin-item" key={`${section.heading}-${index}`}><label>Titre de section<input maxLength={4000} onChange={(event) => change("sections", page.sections.map((entry, current) => current === index ? { ...entry, heading: event.target.value } : entry))} value={section.heading} /></label><label>Contenu Markdown<textarea maxLength={12000} onChange={(event) => change("sections", page.sections.map((entry, current) => current === index ? { ...entry, bodyMarkdown: event.target.value } : entry))} rows={5} value={section.bodyMarkdown} /></label></div>)}</fieldset>
    <fieldset><legend>Questions fréquentes</legend>{page.faq.map((item, index) => <div className="storefront-admin-item" key={`${item.question}-${index}`}><label>Question<input maxLength={4000} onChange={(event) => change("faq", page.faq.map((entry, current) => current === index ? { ...entry, question: event.target.value } : entry))} value={item.question} /></label><label>Réponse<textarea maxLength={12000} onChange={(event) => change("faq", page.faq.map((entry, current) => current === index ? { ...entry, answer: event.target.value } : entry))} rows={3} value={item.answer} /></label></div>)}</fieldset>
    <fieldset><legend>{isServicesLanding ? "Domaines d'intervention" : "Pages associées"}</legend>{page.relatedLinks.map((link, index) => <div className="storefront-admin-item storefront-admin-link" key={`${link.href}-${index}`}><label>Libellé<input maxLength={4000} onChange={(event) => change("relatedLinks", page.relatedLinks.map((entry, current) => current === index ? { ...entry, label: event.target.value } : entry))} value={link.label} /></label><label>Destination<select onChange={(event) => change("relatedLinks", page.relatedLinks.map((entry, current) => current === index ? { ...entry, href: event.target.value } : entry))} value={link.href}>{!relatedDestinations.some((href) => href === link.href) ? <option value={link.href}>{link.href}</option> : null}{relatedDestinations.map((href) => <option key={href} value={href}>{href}</option>)}</select></label></div>)}</fieldset>
    {message ? <FormMessage title={message.tone === "success" ? "Enregistrement" : "Erreur"} tone={message.tone}><p>{message.text}</p></FormMessage> : null}
    <div className="stack-row"><SubmitButton idleLabel="Enregistrer la page" isSubmitting={isSubmitting} submittingLabel="Enregistrement..." /></div>
  </form>;
}
