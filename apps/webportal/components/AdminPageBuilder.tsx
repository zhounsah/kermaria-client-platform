"use client";

import type { SiteMediaAsset, SitePageArea, SitePageBlock, SitePageBlockType, SitePageFormField, SitePageLayout, SitePageRevision } from "@kermaria/shared";
import { getManagedContentRegistry } from "@kermaria/shared";
import Image from "next/image";
import Link from "next/link";
import { useEffect, useState } from "react";
import { SitePageFrame } from "@/components/SitePageFrame";
import { requestBffJson } from "@/lib/client-api";
import { resolvePortalAreaUrl } from "@/lib/public-route-config";

const types: { value: SitePageBlockType; label: string }[] = [
  { value: "text", label: "Texte" }, { value: "image", label: "Image" },
  { value: "cards", label: "Cartes" }, { value: "faq", label: "Questions fréquentes" },
  { value: "link", label: "Lien / bouton" }, { value: "form", label: "Formulaire" },
  { value: "footer_brand", label: "Présentation du pied de page" },
  { value: "footer_links", label: "Liens du pied de page" },
  { value: "hero", label: "Bandeau d’accueil" },
  { value: "audiences", label: "Publics accompagnés" },
  { value: "steps", label: "Étapes" },
  { value: "services", label: "Besoins couverts" },
  { value: "offer_path", label: "Chemins vers les offres" },
  { value: "final_cta", label: "Appel à contact final" },
  { value: "offers_story", label: "Pourquoi protéger ses fichiers" },
  { value: "diagnostic_intro", label: "Présentation du questionnaire" },
  { value: "contact_intro", label: "Présentation du contact" },
  { value: "contact_steps", label: "Étapes après l'envoi" },
  { value: "data_rights_intro", label: "Présentation des demandes de données" },
  { value: "widget", label: "Module fonctionnel" },
];
const offersWidgets = [
  { value: "offers_intro", label: "Présentation des offres", required: true },
  { value: "offers_configure", label: "Accès à la configuration", required: true },
  { value: "offers_demo", label: "Découverte de l'espace client", required: false },
  { value: "offers_overview", label: "Vue simple des offres", required: true },
  { value: "offers_comparison", label: "Comparatif détaillé", required: true },
  { value: "offers_help", label: "Aide au choix", required: false },
] as const;
const offerSheetWidgets = [
  { value: "offer_sheet_intro", label: "Présentation de l'offre", required: true },
  { value: "offer_sheet_back", label: "Retour au comparatif", required: true },
  { value: "offer_sheet_summary", label: "Résumé et choix de l'offre", required: true },
  { value: "offer_sheet_services", label: "Services inclus", required: true },
  { value: "offer_sheet_details", label: "Détails de l'offre", required: true },
  { value: "offer_sheet_source", label: "Indication des données de démonstration", required: true },
] as const;
const cartWidgets = [
  { value: "cart_intro", label: "Présentation du panier", required: true },
  { value: "cart_items", label: "Services et choix du panier", required: true },
  { value: "cart_summary", label: "Prix et suite de la commande", required: true },
] as const;
const checkoutWidgets = [
  { value: "checkout_intro", label: "Présentation de la vérification", required: true },
  { value: "checkout_details", label: "Services et conditions choisis", required: true },
  { value: "checkout_summary", label: "Prix, confirmation et paiement", required: true },
] as const;
const dataRequestWidgets = [
  { value: "data_request_intro", label: "Présentation de la page", required: true },
  { value: "data_request_form", label: "Formulaire de demande", required: true },
  { value: "data_request_history", label: "Liste des demandes", required: true },
  { value: "data_request_help", label: "Aide complémentaire", required: false },
] as const;
const formulesWidgets = [
  { value: "formules_intro", label: "Présentation des offres", required: true },
  { value: "formules_catalog", label: "Offres et prix du catalogue", required: true },
  { value: "formules_note", label: "Conseils sur le tarif", required: false },
  { value: "formules_help", label: "Aide au choix", required: false },
] as const;
const formuleDetailWidgets = [
  { value: "formule_detail_breadcrumb", label: "Retour aux offres", required: true },
  { value: "formule_detail_intro", label: "Présentation et prix de départ", required: true },
  { value: "formule_detail_configurator", label: "Choix de l'offre et récapitulatif", required: true },
] as const;
const tariffWidgets = [
  { value: "tariffs_intro", label: "Présentation des tarifs", required: true },
  { value: "tariffs_catalog", label: "Tarifs du catalogue", required: true },
  { value: "tariffs_explanations", label: "Explications des tarifs", required: true },
  { value: "tariffs_faq", label: "Questions fréquentes", required: false },
  { value: "tariffs_related", label: "Services associés", required: false },
  { value: "tariffs_contact", label: "Aide et demande de devis", required: true },
] as const;
const publicServicesWidgets = [
  { value: "services_intro", label: "Présentation des services", required: true },
  { value: "services_needs", label: "Choisir selon son besoin", required: true },
  { value: "services_categories", label: "Domaines d'intervention", required: true },
  { value: "services_explanations", label: "Explications complémentaires", required: false },
  { value: "services_faq", label: "Questions fréquentes", required: false },
  { value: "services_contact", label: "Aide au choix et contact", required: true },
] as const;
const diagnosticWidgets = [
  { value: "diagnostic_questionnaire", label: "Questions d'orientation", required: true },
  { value: "diagnostic_result", label: "Résultat et prochaines étapes", required: true },
] as const;
const contactWidgets = [
  { value: "contact_back", label: "Retour vers les offres ou l'accueil", required: true },
  { value: "contact_offer", label: "Offre choisie, le cas échéant", required: true },
  { value: "contact_form", label: "Formulaire de contact", required: true },
] as const;
const dataRightsWidgets = [
  { value: "data_rights_actions", label: "Accès à la demande et au contact", required: true },
] as const;
const signupWidgets = [
  { value: "signup_intro", label: "Présentation de l'inscription", required: true },
  { value: "signup_continuation", label: "Reprise du panier ou du serveur", required: false },
  { value: "signup_selection", label: "Récapitulatif de l'offre", required: true },
  { value: "signup_steps", label: "Étapes après inscription", required: false },
  { value: "signup_form", label: "Formulaire d'inscription", required: true },
  { value: "signup_login", label: "Accès des clients existants", required: true },
] as const;
const subscribeWidgets = [
  { value: "subscribe_intro", label: "Présentation de la souscription", required: true },
  { value: "subscribe_offers", label: "Offres du catalogue", required: true },
  { value: "subscribe_direct", label: "Choix d'un service à la carte", required: true },
  { value: "subscribe_help", label: "Aide au choix", required: false },
] as const;
const clientHomeWidgets = [
  { value: "client_home_intro", label: "Accueil du client", required: true },
  { value: "client_home_status", label: "État du chargement", required: true },
  { value: "client_home_metrics", label: "Indicateurs du compte", required: false },
  { value: "client_home_services", label: "Services et démarches rapides", required: true },
  { value: "client_home_recent", label: "Documents et demandes récents", required: false },
  { value: "client_home_activity", label: "Activité récente", required: false },
  { value: "client_home_source", label: "Indication des données de démonstration", required: true },
] as const;
const profileWidgets = [
  { value: "profile_intro", label: "Présentation du profil", required: true },
  { value: "profile_contact", label: "Coordonnées du client", required: true },
  { value: "profile_security", label: "Sécurité du compte", required: true },
  { value: "profile_source", label: "Indication des données de démonstration", required: true },
] as const;
const adminCatalogWidgets = [
  { value: "admin_catalog_intro", label: "Présentation du catalogue", required: true },
  { value: "admin_catalog_vitrine", label: "Accès à la vitrine des offres", required: true },
  { value: "admin_catalog_editor", label: "Éditeur des services et des prix", required: true },
] as const;
const adminHomeWidgets = [
  { value: "admin_home_intro", label: "Accueil de l'administration", required: true },
  { value: "admin_home_activity", label: "Demandes à traiter", required: true },
  { value: "admin_home_overview", label: "Vue générale", required: true },
  { value: "admin_home_integrations", label: "État des intégrations", required: true },
  { value: "admin_home_shortcuts", label: "Accès aux pages détaillées", required: true },
  { value: "admin_home_source", label: "Indication des données de démonstration", required: true },
] as const;
const adminDataRequestWidgets = [
  { value: "admin_data_request_intro", label: "Présentation de l'administration", required: true },
  { value: "admin_data_request_list", label: "Liste des demandes reçues", required: true },
  { value: "admin_data_request_help", label: "Aide au traitement", required: false },
] as const;
const clientDataDetailWidgets = [
  { value: "client_data_detail_intro", label: "En-tête de la demande", required: true },
  { value: "client_data_detail_summary", label: "Résumé de la demande", required: true },
  { value: "client_data_detail_messages", label: "Échanges", required: true },
  { value: "client_data_detail_file", label: "Document à télécharger", required: true },
  { value: "client_data_detail_reply", label: "Réponse du client", required: true },
  { value: "client_data_detail_help", label: "Aide complémentaire", required: false },
] as const;
const adminDataDetailWidgets = [
  { value: "admin_data_detail_intro", label: "En-tête de la demande", required: true },
  { value: "admin_data_detail_summary", label: "Résumé de la demande", required: true },
  { value: "admin_data_detail_messages", label: "Échanges avec le client", required: true },
  { value: "admin_data_detail_reply", label: "Réponse de l'équipe", required: true },
  { value: "admin_data_detail_file", label: "Remise d'un document", required: true },
  { value: "admin_data_detail_help", label: "Aide au traitement", required: false },
] as const;
const allWidgets = [
  ...offersWidgets, ...offerSheetWidgets, ...cartWidgets, ...checkoutWidgets,
  ...formulesWidgets, ...formuleDetailWidgets, ...tariffWidgets, ...publicServicesWidgets, ...diagnosticWidgets, ...contactWidgets, ...dataRightsWidgets,
  ...signupWidgets, ...subscribeWidgets, ...clientHomeWidgets, ...profileWidgets, ...adminHomeWidgets, ...adminCatalogWidgets,
  ...dataRequestWidgets, ...clientDataDetailWidgets,
  ...adminDataRequestWidgets, ...adminDataDetailWidgets,
];
const suggestedPages: { area: SitePageArea; key: string; label: string }[] = [
  { area: "public", key: "/", label: "Accueil public" },
  { area: "public", key: "/services", label: "Services publics" },
  { area: "public", key: "/offres", label: "Offres" },
  { area: "public", key: "/offres/[slug]", label: "Fiche d'une offre" },
  { area: "public", key: "/panier", label: "Panier" },
  { area: "public", key: "/souscription", label: "Vérifier la souscription" },
  { area: "public", key: "/formules", label: "Offres configurables" },
  { area: "public", key: "/formules/[code]", label: "Fiche d'une offre configurable" },
  { area: "public", key: "/tarifs", label: "Tarifs" },
  { area: "public", key: "/diagnostic", label: "Diagnostic" },
  { area: "public", key: "/demander-mes-donnees", label: "Demander mes données" },
  { area: "public", key: "/signup", label: "Inscription" },
  { area: "public", key: "/footer", label: "Pied de page" },
  { area: "client", key: "/dashboard", label: "Accueil client" },
  { area: "client", key: "/profile", label: "Profil client" },
  { area: "client", key: "/souscrire", label: "Souscrire" },
  { area: "client", key: "/profile/donnees", label: "Données personnelles" },
  { area: "client", key: "/profile/donnees/[id]", label: "Détail d'une demande" },
  { area: "admin", key: "/admin", label: "Accueil administration" },
  { area: "admin", key: "/admin/catalog", label: "Catalogue commercial" },
  { area: "admin", key: "/admin/data-requests", label: "Demandes de données" },
  { area: "admin", key: "/admin/data-requests/[id]", label: "Détail d'une demande" },
];

function blank(type: SitePageBlockType, widgetKey = "data_request_help"): SitePageBlock {
  return { id: crypto.randomUUID(), type, title: "", body: "", href: null,
    label: null, mediaId: null, action: type === "form" ? "contact" : null,
    items: ["cards", "faq", "footer_links", "hero", "audiences", "steps", "services", "offer_path", "final_cta", "offers_story", "diagnostic_intro", "contact_steps"].includes(type)
      ? [{ title: "", body: "", href: null, label: null }] : null,
    fields: type === "form" ? [] : null,
    widgetKey: type === "widget" ? widgetKey : null };
}

function contentEditorHref(area: SitePageArea, path: string): string | null {
  if (area !== "public") return null;
  if (path === "/offres" || path === "/offres/[slug]") return "/admin/public-pack-catalog";
  if (path === "/formules" || path === "/formules/[code]") return "/admin/catalog";
  if (path === "/diagnostic") return "/admin/settings/diagnostic";
  const key = getManagedContentRegistry().find((entry) => entry.publicPath === path)?.key;
  return key ? `/admin/content/${encodeURIComponent(key)}` : null;
}

export function AdminPageBuilder() {
  const [area, setArea] = useState<SitePageArea>("public");
  const [pageKey, setPageKey] = useState("/");
  const [pathDraft, setPathDraft] = useState("/");
  const [customSelected, setCustomSelected] = useState(false);
  const [layout, setLayout] = useState<SitePageLayout | null>(null);
  const [savedSnapshot, setSavedSnapshot] = useState<string | null>(null);
  const [revisions, setRevisions] = useState<SitePageRevision[]>([]);
  const [media, setMedia] = useState<SiteMediaAsset[]>([]);
  const [addType, setAddType] = useState<SitePageBlockType>("text");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const [altText, setAltText] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [mediaQuery, setMediaQuery] = useState("");
  const widgetChoices = area === "public" && pageKey === "/offres"
    ? offersWidgets : area === "public" && pageKey === "/offres/[slug]"
      ? offerSheetWidgets : area === "public" && pageKey === "/panier"
      ? cartWidgets : area === "public" && pageKey === "/souscription"
      ? checkoutWidgets : area === "public" && pageKey === "/formules"
      ? formulesWidgets : area === "public" && pageKey === "/formules/[code]"
      ? formuleDetailWidgets : area === "public" && pageKey === "/tarifs"
      ? tariffWidgets : area === "public" && pageKey === "/services"
      ? publicServicesWidgets : area === "public" && pageKey === "/diagnostic"
      ? diagnosticWidgets : area === "public" && pageKey === "/contact"
      ? contactWidgets : area === "public" && pageKey === "/demander-mes-donnees"
      ? dataRightsWidgets : area === "public" && pageKey === "/signup"
      ? signupWidgets : area === "client" && pageKey === "/souscrire"
      ? subscribeWidgets : area === "client" && pageKey === "/dashboard"
      ? clientHomeWidgets : area === "admin" && pageKey === "/admin"
      ? adminHomeWidgets : area === "admin" && pageKey === "/admin/catalog"
      ? adminCatalogWidgets : area === "client" && pageKey === "/profile"
      ? profileWidgets : area === "client" && pageKey === "/profile/donnees"
      ? dataRequestWidgets : area === "client" && pageKey === "/profile/donnees/[id]"
      ? clientDataDetailWidgets : area === "admin" && pageKey === "/admin/data-requests"
        ? adminDataRequestWidgets : area === "admin" && pageKey === "/admin/data-requests/[id]"
          ? adminDataDetailWidgets : [];
  const availableOptionalWidget = widgetChoices.find((item) => !item.required
    && !layout?.blocks.some((block) => block.type === "widget" && block.widgetKey === item.value));
  const homeTypes = new Set(["hero", "audiences", "steps", "services", "offer_path", "final_cta", "text", "image", "cards", "faq", "link", "form"]);
  const availableTypes = types.filter((item) => pageKey === "/footer"
    ? item.value.startsWith("footer_") : pageKey === "/"
      ? homeTypes.has(item.value) : !item.value.startsWith("footer_")
        && !["hero", "audiences", "steps", "services", "offer_path", "final_cta"].includes(item.value)
        && (item.value !== "widget" || availableOptionalWidget !== undefined)
        && (item.value !== "offers_story" || area === "public" && pageKey === "/offres")
        && (item.value !== "diagnostic_intro" || area === "public" && pageKey === "/diagnostic"
          && !layout?.blocks.some((block) => block.type === "diagnostic_intro"))
        && (item.value !== "contact_intro" || area === "public" && pageKey === "/contact"
          && !layout?.blocks.some((block) => block.type === "contact_intro"))
        && (item.value !== "data_rights_intro" || area === "public" && pageKey === "/demander-mes-donnees"
          && !layout?.blocks.some((block) => block.type === "data_rights_intro"))
        && (item.value !== "contact_steps" || area === "public" && pageKey === "/contact"));
  const effectiveAddType = availableTypes.some((item) => item.value === addType)
    ? addType : availableTypes[0].value;
  const dirty = layout !== null && savedSnapshot !== null
    && JSON.stringify(layout.blocks) !== savedSnapshot;
  const matchingMedia = media.filter((asset) =>
    `${asset.fileName} ${asset.altText}`.toLocaleLowerCase("fr")
      .includes(mediaQuery.trim().toLocaleLowerCase("fr")));
  const visibleMedia = matchingMedia.slice(0, 36);

  useEffect(() => {
    if (!/^\/[a-z0-9/_\[\].-]*$/.test(pageKey)) return;
    let active = true;
    const query = `area=${area}&pageKey=${encodeURIComponent(pageKey)}`;
    void Promise.all([
      requestBffJson<SitePageLayout>(`/api/admin/page-layout?${query}`, { method: "GET" }),
      requestBffJson<SitePageRevision[]>(`/api/admin/page-layout/revisions?${query}`, { method: "GET" }),
      requestBffJson<SiteMediaAsset[]>("/api/admin/site-media", { method: "GET" }),
    ]).then(([page, history, assets]) => {
      if (!active) return;
      setLayout(page.ok ? page.data : null);
      setSavedSnapshot(page.ok ? JSON.stringify(page.data.blocks) : null);
      setRevisions(history.ok ? history.data : []);
      setMedia(assets.ok ? assets.data : []);
      if (!page.ok) setMessage(page.error.message);
    });
    return () => { active = false; };
  }, [area, pageKey]);

  useEffect(() => {
    if (!dirty) return;
    const warn = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = ""; };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [dirty]);

  function openPage(nextArea: SitePageArea, nextPath: string) {
    if (dirty && !window.confirm("Abandonner les modifications non enregistrées ?")) {
      setMessage("Vos modifications restent sur la page actuelle.");
      return;
    }
    setArea(nextArea);
    setPageKey(nextPath);
    setPathDraft(nextPath);
    setLayout(null);
    setSavedSnapshot(null);
    setRevisions([]);
    setCustomSelected(!suggestedPages.some((item) => item.area === nextArea && item.key === nextPath));
    setMessage("");
  }

  function changeBlock(id: string, patch: Partial<SitePageBlock>) {
    setLayout((current) => current ? { ...current,
      blocks: current.blocks.map((block) => block.id === id ? { ...block, ...patch } : block) } : current);
  }
  function move(index: number, direction: -1 | 1) {
    setLayout((current) => {
      if (!current || index + direction < 0 || index + direction >= current.blocks.length) return current;
      const blocks = [...current.blocks];
      [blocks[index], blocks[index + direction]] = [blocks[index + direction], blocks[index]];
      return { ...current, blocks };
    });
  }
  function add() {
    if (layout && layout.blocks.length >= 40) return;
    const optionalWidget = availableOptionalWidget?.value;
    setLayout((current) => current ? { ...current,
      blocks: [...current.blocks, blank(effectiveAddType, optionalWidget)] } : current);
  }
  function insertMedia(asset: SiteMediaAsset) {
    if (pageKey === "/footer") return;
    setLayout((current) => current && current.blocks.length < 40
      ? { ...current, blocks: [...current.blocks, {
          ...blank("image"), mediaId: asset.id, label: asset.altText,
        }] } : current);
    setMessage("Image placée en fin de page. Enregistrez pour la publier.");
  }
  async function save() {
    if (!layout || busy) return;
    setBusy(true); setMessage("");
    const result = await requestBffJson<SitePageLayout>("/api/admin/page-layout", {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ pageKey, area, expectedVersion: layout.version, blocks: layout.blocks }),
    });
    setBusy(false);
    if (!result.ok) { setMessage(result.error.message); return; }
    setLayout(result.data); setSavedSnapshot(JSON.stringify(result.data.blocks));
    setMessage("La page a été enregistrée et publiée.");
    await reloadRevisions();
  }
  async function reloadRevisions() {
    const result = await requestBffJson<SitePageRevision[]>(
      `/api/admin/page-layout/revisions?area=${area}&pageKey=${encodeURIComponent(pageKey)}`, { method: "GET" });
    if (result.ok) setRevisions(result.data);
  }
  async function restore(version: number) {
    if (!layout || busy || !window.confirm(`Restaurer la version ${version} et la publier immédiatement ?`)) return;
    setBusy(true); setMessage("");
    const result = await requestBffJson<SitePageLayout>("/api/admin/page-layout/restore", {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ area, pageKey, version, expectedVersion: layout.version }),
    });
    setBusy(false);
    if (!result.ok) { setMessage(result.error.message); return; }
    setLayout(result.data); setSavedSnapshot(JSON.stringify(result.data.blocks));
    setMessage("Version restaurée et publiée.");
    await reloadRevisions();
  }
  async function upload() {
    if (!file || busy) return;
    setBusy(true); setMessage("");
    const data = new FormData(); data.set("file", file); data.set("altText", altText);
    const result = await requestBffJson<SiteMediaAsset>("/api/admin/site-media", { method: "POST", body: data });
    setBusy(false);
    if (!result.ok) { setMessage(result.error.message); return; }
    setMedia((current) => [result.data, ...current]);
    setFile(null); setAltText(""); setMessage("Image ajoutée à la médiathèque.");
  }

  return <div className="page-builder">
    <div className="page-builder-toolbar content-panel">
      <label>Espace <select onChange={(event) => {
        const nextArea = event.target.value as SitePageArea;
        openPage(nextArea, nextArea === "admin" ? "/admin" : nextArea === "client" ? "/dashboard" : "/");
      }} value={area}>
        <option value="public">Vitrine</option><option value="client">Espace client</option><option value="admin">Administration</option>
      </select></label>
      <label>Page <select onChange={(event) => {
        if (event.target.value === "custom") { setCustomSelected(true); setPathDraft(""); }
        else openPage(area, event.target.value);
      }} value={customSelected ? "custom" : pageKey}>
        {suggestedPages.filter((item) => item.area === area).map((item) => <option key={item.key} value={item.key}>{item.label}</option>)}
        <option value="custom">Autre page…</option>
      </select></label>
      <label>Chemin de la page <input onChange={(event) => setPathDraft(event.target.value)} value={pathDraft} /></label>
      <button className="button button-secondary" disabled={busy || !/^\/[a-z0-9/_\[\].-]*$/.test(pathDraft)} onClick={() => openPage(area, pathDraft)} type="button">Ouvrir</button>
      <button className="button" disabled={!layout || busy || pathDraft !== pageKey} onClick={() => void save()} type="button">{busy ? "Enregistrement…" : "Enregistrer et publier"}</button>
    </div>
    <p className="field-hint">Page affichée : <strong>{area} · {pageKey}</strong>{dirty ? " · Modifications non enregistrées" : ""}</p>
    <p className="field-hint">Chaque sauvegarde est visible immédiatement. Les blocs requis de cette page doivent rester présents ; une publication invalide est refusée.</p>
    {contentEditorHref(area, pageKey) ? <p className="field-hint">
      Les textes et données du catalogue gardent leur éditeur : <Link href={contentEditorHref(area, pageKey)!}>modifier le contenu d&apos;origine</Link>.
    </p> : null}
    {message ? <p className="page-builder-message" role="status">{message}</p> : null}
    {layout ? <div className="page-builder-grid">
      <div className="page-builder-editor">
        {layout.blocks.map((block, index) => <section className="content-panel page-builder-block" key={block.id}>
          <header><strong>{block.type === "route_content" ? "Contenu et actions actuels" : block.type === "widget"
            ? allWidgets.find((item) => item.value === block.widgetKey)?.label ?? "Module fonctionnel"
            : types.find((item) => item.value === block.type)?.label}</strong>
            <span><button disabled={index === 0} onClick={() => move(index, -1)} type="button">Monter</button>
              <button disabled={index === layout.blocks.length - 1} onClick={() => move(index, 1)} type="button">Descendre</button>
              {block.type !== "route_content" && !(pageKey === "/" && block.type === "hero") && !(pageKey === "/footer" && block.type === "footer_brand")
                && !(pageKey === "/diagnostic" && block.type === "diagnostic_intro")
                && !(pageKey === "/contact" && block.type === "contact_intro")
                && !(pageKey === "/demander-mes-donnees" && block.type === "data_rights_intro")
                && !(block.type === "widget" && allWidgets.some((item) => item.value === block.widgetKey && item.required))
                ? <button onClick={() => setLayout({ ...layout, blocks: layout.blocks.filter((item) => item.id !== block.id) })} type="button">Retirer</button> : null}</span>
          </header>
          {block.type === "route_content" ? <div><p>Ce bloc conserve les fonctions existantes de la page. Vous pouvez le déplacer, sans le supprimer.</p>
            {contentEditorHref(area, pageKey) ? <Link href={contentEditorHref(area, pageKey)!}>Modifier les textes de cette page dans leur éditeur actuel</Link> : null}</div>
            : block.type === "widget" ? <div className="page-builder-fields"><p>Ce module affiche les données autorisées pour cette page. Les accès restent vérifiés par le serveur.</p>
              <label>Module <select onChange={(event) => changeBlock(block.id, { widgetKey: event.target.value })} value={block.widgetKey ?? ""}>
                {widgetChoices.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
              </select></label></div>
            : <div className="page-builder-fields">
              <label>Titre <input maxLength={200} onChange={(event) => changeBlock(block.id, { title: event.target.value })} value={block.title ?? ""} /></label>
              {block.type !== "image" && block.type !== "form" && block.type !== "footer_links" ? <label>Texte <textarea maxLength={5000} onChange={(event) => changeBlock(block.id, { body: event.target.value })} rows={3} value={block.body ?? ""} /></label> : null}
              {(["link", "footer_brand", "hero", "final_cta", "offers_story"].includes(block.type)) ? <><label>Libellé du bouton <input onChange={(event) => changeBlock(block.id, { label: event.target.value })} value={block.label ?? ""} /></label>
                <label>Chemin de destination <input onChange={(event) => changeBlock(block.id, { href: event.target.value })} placeholder="/contact" value={block.href ?? ""} /></label></> : null}
              {block.type === "image" || block.type === "hero" ? <><label>{block.type === "hero" ? "Image de fond (facultatif)" : "Image"} <select onChange={(event) => changeBlock(block.id, { mediaId: event.target.value || null })} value={block.mediaId ?? ""}>
                <option value="">Choisir une image</option>{media.map((asset) => <option key={asset.id} value={asset.id}>{asset.fileName}</option>)}</select></label>
                {block.type === "image" ? <label>Description de l’image <input onChange={(event) => changeBlock(block.id, { label: event.target.value })} value={block.label ?? ""} /></label> : null}</> : null}
              {block.type === "form" ? <><label>Action du formulaire <select onChange={(event) => changeBlock(block.id, { action: event.target.value as SitePageBlock["action"] })} value={block.action ?? "contact"}>
                <option value="contact">Contact</option>{area === "client" ? <option value="data_request">Demande de données</option> : null}</select></label>
                <label>Libellé du bouton d’envoi <input maxLength={100} onChange={(event) => changeBlock(block.id, { label: event.target.value })} value={block.label ?? ""} /></label></> : null}
              {block.type === "form" ? <div className="page-builder-items"><h3>Champs complémentaires</h3>
                <p className="field-hint">Les champs essentiels du contact ou de la demande sont déjà présents. Ces champs seront ajoutés au message transmis.</p>
                {(block.fields ?? []).map((field, index) => <div className="page-builder-item" key={field.id}>
                  <label>Nom du champ <input maxLength={100} onChange={(event) => changeBlock(block.id, { fields: (block.fields ?? []).map((item, i) => i === index ? { ...item, label: event.target.value } : item) })} value={field.label} /></label>
                  <label>Type <select onChange={(event) => changeBlock(block.id, { fields: (block.fields ?? []).map((item, i) => i === index ? { ...item, type: event.target.value as SitePageFormField["type"] } : item) })} value={field.type}>
                    <option value="text">Texte</option><option value="email">E-mail</option><option value="number">Nombre</option><option value="select">Choix</option><option value="checkbox">Case à cocher</option></select></label>
                  {field.type === "select" ? <label>Choix, séparés par une virgule <input onChange={(event) => changeBlock(block.id, { fields: (block.fields ?? []).map((item, i) => i === index ? { ...item, options: event.target.value.split(",").map((value) => value.trim()).filter(Boolean) } : item) })} value={(field.options ?? []).join(", ")} /></label> : null}
                  <label className="page-builder-check"><input checked={field.required} onChange={(event) => changeBlock(block.id, { fields: (block.fields ?? []).map((item, i) => i === index ? { ...item, required: event.target.checked } : item) })} type="checkbox" /> Obligatoire</label>
                  <button onClick={() => changeBlock(block.id, { fields: (block.fields ?? []).filter((_, i) => i !== index) })} type="button">Retirer ce champ</button>
                </div>)}
                <button disabled={(block.fields ?? []).length >= 8} onClick={() => changeBlock(block.id, { fields: [...(block.fields ?? []), { id: `champ${crypto.randomUUID().replaceAll("-", "").slice(0, 12)}`, label: "Nouveau champ", type: "text", required: false, options: null }] })} type="button">Ajouter un champ</button>
              </div> : null}
              {block.items ? <div className="page-builder-items"><h3>{block.type === "faq" ? "Questions" : block.type === "footer_links" ? "Liens" : "Éléments"}</h3>
                {(block.items ?? []).map((item, itemIndex) => <div className="page-builder-item" key={itemIndex}>
                  <label>Titre <input onChange={(event) => changeBlock(block.id, { items: (block.items ?? []).map((value, i) => i === itemIndex ? { ...value, title: event.target.value } : value) })} value={item.title} /></label>
                  <label>Texte <textarea onChange={(event) => changeBlock(block.id, { items: (block.items ?? []).map((value, i) => i === itemIndex ? { ...value, body: event.target.value } : value) })} value={item.body ?? ""} /></label>
                  {!["faq", "steps", "services"].includes(block.type) ? <label>Lien <input onChange={(event) => changeBlock(block.id, { items: (block.items ?? []).map((value, i) => i === itemIndex ? { ...value, href: event.target.value } : value) })} value={item.href ?? ""} /></label> : null}
                  {!["faq", "steps", "services", "footer_links"].includes(block.type) ? <label>Texte du lien <input onChange={(event) => changeBlock(block.id, { items: (block.items ?? []).map((value, i) => i === itemIndex ? { ...value, label: event.target.value } : value) })} value={item.label ?? ""} /></label> : null}
                  <button onClick={() => changeBlock(block.id, { items: (block.items ?? []).filter((_, i) => i !== itemIndex) })} type="button">Retirer cet élément</button>
                </div>)}
                <button onClick={() => changeBlock(block.id, { items: [...(block.items ?? []), { title: "", body: "", href: null, label: null }] })} type="button">Ajouter un élément</button>
              </div> : null}
            </div>}
        </section>)}
        <div className="content-panel page-builder-add"><select onChange={(event) => setAddType(event.target.value as SitePageBlockType)} value={effectiveAddType}>{availableTypes.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}</select>
          <button className="button button-secondary" disabled={layout.blocks.length >= 40} onClick={add} type="button">Ajouter un bloc</button></div>
      </div>
      <aside className="page-builder-side">
        <section className="content-panel"><h2>Aperçu du brouillon</h2><p>Les blocs éditoriaux utilisent le rendu de la page dans la largeur de ce panneau. Les fonctions existantes sont représentées par leur emplacement. Les liens et formulaires de cet aperçu sont désactivés.</p>
          <p className="field-hint">{dirty ? "Modifications non publiées" : layout.version === 0 ? "Mise en page initiale" : "Version publiée"}</p>
          {!pageKey.includes("[") && pageKey !== "/footer" ? <button onClick={() => {
            const target = resolvePortalAreaUrl(window.location.origin, area, pageKey);
            if (target) window.open(target, "_blank", "noopener,noreferrer");
          }} type="button">Voir la page actuelle</button> : null}
          <div className={pageKey === "/footer" ? "page-builder-live-preview public-footer" : "page-builder-live-preview"}
            data-testid="page-builder-draft-preview">
            <SitePageFrame area={area} pageKey={pageKey} initialLayout={layout} preview
              previewLabels={Object.fromEntries(allWidgets.map((item) => [item.value, item.label]))}>
              {null}
            </SitePageFrame>
          </div>
        </section>
        <section className="content-panel"><h2>Médiathèque</h2><p>Images PNG, JPEG ou WebP de 5 Mo maximum.</p>
          <input accept="image/png,image/jpeg,image/webp" onChange={(event) => setFile(event.target.files?.[0] ?? null)} type="file" />
          <label>Description de l’image<input onChange={(event) => setAltText(event.target.value)} value={altText} /></label>
          <button disabled={!file || altText.trim().length < 3 || busy} onClick={() => void upload()} type="button">Ajouter l’image</button>
          <label>Rechercher une image<input onChange={(event) => setMediaQuery(event.target.value)}
            placeholder="Nom ou description" type="search" value={mediaQuery} /></label>
          {matchingMedia.length === 0 ? <p className="field-hint">{media.length === 0
            ? "Aucune image ajoutée pour le moment." : "Aucune image ne correspond à cette recherche."}</p>
            : <div className="page-builder-media-grid">{visibleMedia.map((asset) => <figure className="page-builder-media-card" key={asset.id}>
              <Image alt={asset.altText} height={180} src={`/api/site-media/${asset.id}`}
                unoptimized width={240} />
              <figcaption><strong>{asset.fileName}</strong><span>{asset.altText}</span>
                <small>{(asset.byteLength / 1024).toLocaleString("fr-FR", { maximumFractionDigits: 0 })} Ko</small></figcaption>
              {pageKey !== "/footer" ? <button disabled={layout.blocks.length >= 40}
                onClick={() => insertMedia(asset)} type="button">Placer sur cette page</button> : null}
            </figure>)}</div>}
          {matchingMedia.length > visibleMedia.length ? <p className="field-hint">
            {matchingMedia.length - visibleMedia.length} autre(s) image(s) : précisez la recherche pour les afficher.
          </p> : null}
        </section>
        <section className="content-panel"><h2>Historique</h2>{revisions.length === 0 ? <p>Aucune version enregistrée.</p> : <ul>{revisions.map((revision) => <li key={revision.version}>
          Version {revision.version} <button disabled={busy || revision.version === layout.version} onClick={() => void restore(revision.version)} type="button">Restaurer</button>
        </li>)}</ul>}</section>
      </aside>
    </div> : <p>Chargement de la page…</p>}
  </div>;
}
