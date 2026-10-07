"use client";

import type { SitePageArea, SitePageBlock, SitePageLayout } from "@kermaria/shared";
import Image from "next/image";
import Link from "next/link";
import { useEffect, useState, type ReactNode } from "react";
import { CmsConfigurableForm } from "@/components/CmsConfigurableForm";
import { requestBffJson } from "@/lib/client-api";

export function SitePageFrame({ area, pageKey, children, initialLayout, slots, className,
  preview = false, previewLabels }: {
  area: SitePageArea | null; pageKey: string; children: ReactNode;
  initialLayout?: SitePageLayout;
  slots?: Record<string, ReactNode>;
  className?: string;
  preview?: boolean;
  previewLabels?: Record<string, string>;
}) {
  const [loadedLayout, setLayout] = useState<SitePageLayout | null>(null);
  const layout = initialLayout ?? loadedLayout;
  useEffect(() => {
    if (initialLayout) return;
    if (!area) return;
    let active = true;
    void requestBffJson<SitePageLayout>(
      `/api/page-layout?area=${area}&pageKey=${encodeURIComponent(pageKey)}`,
      { method: "GET" },
    ).then((result) => { if (active) setLayout(result.ok ? result.data : null); });
    return () => { active = false; };
  }, [area, pageKey, initialLayout]);

  if (!layout || layout.area !== area || layout.pageKey !== pageKey) return <>{children}</>;
  return <div aria-hidden={preview || undefined}
    className={`site-page-frame${preview ? " site-page-draft-preview" : ""}${className ? ` ${className}` : ""}`}
    inert={preview || undefined}>
    {layout.blocks.map((block) => block.type === "route_content"
      ? preview
        ? <div className="page-builder-preview-block page-builder-preview-core" key={block.id}>
            <strong>Contenu et actions actuels</strong>
            <p>Le contenu métier de cette page occupe cet emplacement.</p>
          </div>
        : <div className="site-page-core" key={block.id}>{children}</div>
      : <SitePageBlockView area={area!} block={block} key={block.id}
          preview={preview} previewLabels={previewLabels} slots={slots} />)}
  </div>;
}

function SitePageBlockView({ block, area, slots, preview, previewLabels }: {
  block: SitePageBlock; area: SitePageArea; slots?: Record<string, ReactNode>;
  preview: boolean; previewLabels?: Record<string, string>;
}) {
  const items = block.items ?? [];
  if (block.type === "widget" && preview) return <div className="page-builder-preview-block page-builder-preview-core">
    <strong>{previewLabels?.[block.widgetKey ?? ""] ?? "Module fonctionnel"}</strong>
    <p>Ce module affichera les données autorisées sur la page publiée.</p>
  </div>;
  if (block.type === "widget") return block.widgetKey && slots?.[block.widgetKey]
    ? <div className={`site-page-widget site-page-widget-${block.widgetKey}`}>
        {slots[block.widgetKey]}
      </div> : null;
  if (block.type === "hero") return <section className="vitrine-hero-band">
    <div className="vitrine-hero vitrine-hero-2026" style={block.mediaId ? { backgroundImage: `url(/api/site-media/${encodeURIComponent(block.mediaId)})` } : undefined}>
      <div className="vitrine-hero-copy"><h1>{block.title}</h1><p className="vitrine-hero-lead">{block.body}</p>
        <div className="vitrine-hero-actions">
          {block.href ? <Link className="button" href={block.href}>{block.label ?? "Découvrir"}</Link> : null}
          {items[0]?.href ? <Link className="button button-secondary" href={items[0].href}>{items[0].title}</Link> : null}
        </div></div><div aria-hidden="true" className="vitrine-hero-photo-spacer" />
    </div></section>;
  if (block.type === "audiences") return <section className="vitrine-audiences vitrine-audiences-first">
    <header className="vitrine-section-header"><h2>{block.title}</h2><p>{block.body}</p></header>
    <ul className="vitrine-audiences-grid">{items.map((item, index) => <li className="vitrine-audience-card" key={`${item.title}-${index}`}>
      <h3>{item.title}</h3><p>{item.body}</p>{item.href ? <Link href={item.href}>{item.label ?? "Voir les solutions"}</Link> : null}
    </li>)}</ul></section>;
  if (block.type === "steps") return <section className="vitrine-method">
    <header className="vitrine-section-header"><h2>{block.title}</h2>{block.body ? <p>{block.body}</p> : null}</header>
    <ol className="vitrine-method-grid">{items.map((item, index) => <li className="vitrine-method-step" key={`${item.title}-${index}`}>
      <span className="vitrine-method-number">{String(index + 1).padStart(2, "0")}</span><h3>{item.title}</h3><p>{item.body}</p>
    </li>)}</ol></section>;
  if (block.type === "services") return <section className="vitrine-services">
    <header className="vitrine-section-header"><h2>{block.title}</h2><p className="vitrine-section-lead">{block.body}</p></header>
    <ul className="vitrine-services-grid">{items.map((item, index) => <li className="vitrine-service-card" key={`${item.title}-${index}`}>
      <h3>{item.title}</h3><p>{item.body}</p>{item.href ? <Link href={item.href}>En savoir plus</Link> : null}
    </li>)}</ul></section>;
  if (block.type === "offer_path") return <section className="vitrine-offer-path">
    <header className="vitrine-section-header"><h2>{block.title}</h2><p>{block.body}</p></header>
    <div className="vitrine-offer-path-grid">{items.map((item, index) => <article key={`${item.title}-${index}`}>
      <h3>{item.title}</h3><p>{item.body}</p>{item.href ? <Link href={item.href}>{item.label ?? "En savoir plus"}</Link> : null}
    </article>)}</div></section>;
  if (block.type === "final_cta") return <section className="vitrine-cta"><div><h2>{block.title}</h2><p>{block.body}</p></div>
    <div className="vitrine-hero-actions">{block.href ? <Link className="button" href={block.href}>{block.label ?? "Nous contacter"}</Link> : null}
      {items[0]?.href ? <Link className="button button-secondary" href={items[0].href}>{items[0].title}</Link> : null}</div></section>;
  if (block.type === "offers_story") return <section className="offres-story">
    <div className="offres-story-highlight"><h2>{block.title}</h2><p>{block.body}</p>
      {block.href ? <Link className="text-link" href={block.href}>{block.label}</Link> : null}</div>
    <div className="offres-story-grid">{items.map((item, index) => <article className="offres-story-card" key={`${item.title}-${index}`}>
      <h3>{item.title}</h3><p>{item.body}</p></article>)}</div>
  </section>;
  if (block.type === "diagnostic_intro") return <header className="diagnostic-header">
    <div className="diagnostic-header-copy"><span aria-hidden="true" className="diagnostic-header-icon">✓</span>
      <div><p className="eyebrow">Trouver ma solution</p><h1>{block.title}</h1><p>{block.body}</p></div>
    </div>
    <div aria-label="Fonctionnement" className="diagnostic-benefits">
      {items.map((item, index) => <article key={`${item.title}-${index}`}>
        <span className="diagnostic-benefit-icon">{index + 1}</span>
        <div><h2>{item.title}</h2><p>{item.body}</p></div>
      </article>)}
    </div>
  </header>;
  if (block.type === "contact_intro") return <header className="contact-header">
    <p className="eyebrow">Contact</p><h1>{block.title}</h1>
    <p className="contact-lead">{block.body}</p>
  </header>;
  if (block.type === "data_rights_intro") return <header className="data-request-intro">
    <h1>{block.title}</h1><p>{block.body}</p>
  </header>;
  if (block.type === "contact_steps") return <section aria-labelledby={`contact-steps-${block.id}`} className="signup-steps-card">
    <h2 id={`contact-steps-${block.id}`}>{block.title}</h2>
    <ol>{items.map((item, index) => <li key={`${item.title}-${index}`}>
      <strong>{item.title}</strong><span>{item.body}</span>
    </li>)}</ol>
  </section>;
  if (block.type === "text") return <section className="site-page-block site-page-text">
    {block.title ? <h2>{block.title}</h2> : null}<p>{block.body}</p></section>;
  if (preview && block.type === "image" && !block.mediaId) return <div className="page-builder-preview-block">
    <strong>Image à choisir</strong><p>Ajoutez une image de la médiathèque pour voir son rendu.</p>
  </div>;
  if (block.type === "image" && block.mediaId) return <figure className="site-page-block site-page-image">
    <Image alt={block.label ?? ""} height={600} src={`/api/site-media/${encodeURIComponent(block.mediaId)}`} unoptimized width={1200} />
    {block.body ? <figcaption>{block.body}</figcaption> : null}</figure>;
  if (block.type === "link" && block.href) return <section className="site-page-block site-page-link">
    {block.title ? <h2>{block.title}</h2> : null}{block.body ? <p>{block.body}</p> : null}
    <Link className="button" href={block.href}>{block.label ?? "En savoir plus"}</Link></section>;
  if (preview && block.type === "link") return <div className="page-builder-preview-block">
    <strong>Lien à compléter</strong><p>Indiquez sa destination pour voir le bouton.</p>
  </div>;
  if (block.type === "cards") return <section className="site-page-block site-page-cards">
    {block.title ? <h2>{block.title}</h2> : null}<div>{items.map((item, index) => <article key={`${item.title}-${index}`}>
      <h3>{item.title}</h3>{item.body ? <p>{item.body}</p> : null}
      {item.href ? <Link href={item.href}>{item.label ?? "En savoir plus"}</Link> : null}</article>)}</div></section>;
  if (block.type === "faq") return <section className="site-page-block site-page-faq">
    {block.title ? <h2>{block.title}</h2> : null}{items.map((item, index) => <details key={`${item.title}-${index}`}>
      <summary>{item.title}</summary><p>{item.body}</p></details>)}</section>;
  if (block.type === "form" && block.action === "contact") return <section className="site-page-block site-page-form">
    {block.title ? <h2>{block.title}</h2> : null}<CmsConfigurableForm block={block} preview={preview} /></section>;
  if (block.type === "form" && block.action === "data_request" && area === "client") return <section className="site-page-block site-page-form">
    {block.title ? <h2>{block.title}</h2> : null}<CmsConfigurableForm block={block} preview={preview} /></section>;
  if (block.type === "footer_brand") return <div className="public-footer-brand">
    <strong className="public-footer-cms-brand">{block.title}</strong>
    <p className="public-footer-tagline">{block.body}</p>
    {block.href ? <Link className="public-footer-contact" href={block.href}>{block.label ?? "Nous contacter"}</Link> : null}
  </div>;
  if (block.type === "footer_links") return <section className="public-footer-column">
    <h2>{block.title}</h2><ul className="public-footer-link-list">
      {items.map((item, index) => item.href ? <li key={`${item.title}-${index}`}><Link href={item.href}>{item.title}</Link></li> : null)}
    </ul></section>;
  return null;
}
