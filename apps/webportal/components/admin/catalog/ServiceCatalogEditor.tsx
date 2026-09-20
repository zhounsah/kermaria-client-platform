"use client";

import Link from "next/link";
import { useState } from "react";

import { PageHeader } from "@/components/PageHeader";
import type {
  BillingV2AdminDirectOrderingDiagnostic,
  BillingV2AdminService,
} from "@/lib/internal-api";
import type { PublicCommercialOrderingMode } from "@kermaria/shared";
import { CatalogFeedback, CatalogField, CatalogNavigation, CatalogTabs, CatalogToggle, ImmutableCode, StickyActions, adminCatalogStyles as styles, useUnsavedChangesGuard } from "./AdminCatalogUi";
import { ServicePricingPanel } from "./ServicePricingPanel";
import { ServiceTiersPanel } from "./ServiceTiersPanel";
import { useAdminCatalogCommand } from "./useAdminCatalogCommand";

type ServiceTab = "essential" | "tiers" | "pricing" | "commercialization";
type ServiceForm = {
  name: string; description: string; tierSelectorLabel: string; category: string; status: string; displayOrder: number;
  publicVisible: boolean; selfServiceOrderable: boolean; publicOrderingMode: PublicCommercialOrderingMode; discountEligible: boolean; mandatoryForSubscription: boolean;
};

function toForm(service: BillingV2AdminService): ServiceForm {
  return {
    name: service.name, description: service.description ?? "", tierSelectorLabel: service.tierSelectorLabel ?? "", category: service.category ?? "", status: service.status,
    displayOrder: service.displayOrder, publicVisible: service.publicVisible, selfServiceOrderable: service.selfServiceOrderable,
    publicOrderingMode: service.publicOrderingMode,
    discountEligible: service.discountEligible, mandatoryForSubscription: service.mandatoryForSubscription,
  };
}

export function ServiceCatalogEditor({ asOf, service, tab }: { asOf: string; service: BillingV2AdminService; tab: ServiceTab }) {
  const initialValue = toForm(service);
  const [form, setForm] = useState<ServiceForm>(initialValue);
  const [saved, setSaved] = useState<ServiceForm>(initialValue);
  const dirty = JSON.stringify(form) !== JSON.stringify(saved);
  const command = useAdminCatalogCommand();
  const base = `/admin/catalog/services/${service.id}`;
  const tabs = [
    { key: "essential", label: "Essentiel", href: `${base}?tab=essential` },
    { key: "tiers", label: "Paliers", href: `${base}?tab=tiers` },
    { key: "pricing", label: "Tarification", href: `${base}?tab=pricing` },
    { key: "commercialization", label: "Commercialisation", href: `${base}?tab=commercialization` },
  ];

  useUnsavedChangesGuard(dirty);

  async function save(event: React.FormEvent) {
    event.preventDefault();
    const result = await command.send({ kind: "service.update", id: service.id, ...form });
    if (result) setSaved(form);
  }

  return <div className={styles.shell}>
    <PageHeader eyebrow="Catalogue · Service" title={service.name} description="Structure commerciale, paliers et versions tarifaires de ce service Billing V2." action={<Link className="button button-secondary" href="/admin/catalog">Retour au catalogue</Link>} />
    <CatalogNavigation active="services" />
    <CatalogTabs active={tab} tabs={tabs} />
    <CatalogFeedback feedback={command.feedback} />
    {tab === "tiers" ? <ServiceTiersPanel service={service} /> : null}
    {tab === "pricing" ? <ServicePricingPanel asOf={asOf} service={service} /> : null}
    {tab === "essential" || tab === "commercialization" ? <form onSubmit={save}>
      <section className={styles.panel}>
        <div className={styles.sectionHeading}><div><h2>{tab === "essential" ? "Informations essentielles" : "Commercialisation"}</h2><p>{tab === "essential" ? "Le code identifie durablement le service dans Billing V2." : "Sépare la visibilité, la vente directe et les règles commerciales."}</p></div></div>
        {tab === "essential" ? <>
          <ImmutableCode value={service.code} />
          <div className={styles.formGrid} style={{ marginTop: 16 }}>
            <CatalogField htmlFor="service-name" label="Nom"><input id="service-name" maxLength={160} onChange={(e) => setForm({ ...form, name: e.currentTarget.value })} required value={form.name} /></CatalogField>
            <CatalogField htmlFor="service-category" label="Catégorie"><input id="service-category" maxLength={80} onChange={(e) => setForm({ ...form, category: e.currentTarget.value })} value={form.category} /></CatalogField>
            <CatalogField full htmlFor="service-description" label="Description"><textarea id="service-description" onChange={(e) => setForm({ ...form, description: e.currentTarget.value })} value={form.description} /></CatalogField>
            <CatalogField htmlFor="service-tier-selector-label" label="Libellé du choix" hint="Texte affiché au client pour choisir une option ou une capacité. Laisser vide pour utiliser « Option »."><input id="service-tier-selector-label" maxLength={160} onChange={(e) => setForm({ ...form, tierSelectorLabel: e.currentTarget.value })} value={form.tierSelectorLabel} /></CatalogField>
            <CatalogField htmlFor="service-status" label="Statut"><select id="service-status" onChange={(e) => setForm({ ...form, status: e.currentTarget.value })} value={form.status}><option value="active">Actif</option><option value="inactive">Inactif</option></select></CatalogField>
          </div>
          <div className={styles.advanced}><h3>Paramètres techniques</h3><dl className={styles.technicalGrid}>
            <div><dt>Type de facturation</dt><dd>{service.billingType}</dd></div><div><dt>Portée par défaut</dt><dd>{service.defaultScopeType}</dd></div><div><dt>Modèle tarifaire</dt><dd>{service.pricingModel}</dd></div><div><dt>Dernière modification par</dt><dd>{service.updatedByReference ?? "Non renseigné"}</dd></div>
          </dl><p className={styles.hint}>Ces propriétés structurantes sont immuables après la création du service.</p></div>
        </> : <div className={styles.formGrid}>
          <CatalogToggle checked={form.publicVisible} description="Le service peut apparaître sur les surfaces publiques." label="Visible publiquement" name="publicVisible" onChange={(value) => setForm({ ...form, publicVisible: value })} />
          <CatalogField full htmlFor="service-public-ordering-mode" label="Mode de commercialisation" hint="Ce réglage décide du parcours présenté sur la vitrine ; il ne modifie ni le prix ni les règles de facturation.">
            <select id="service-public-ordering-mode" onChange={(event) => setForm({ ...form, publicOrderingMode: event.currentTarget.value as PublicCommercialOrderingMode })} value={form.publicOrderingMode}>
              <option value="quote">Sur devis</option>
              <option value="offer_component">Disponible dans une offre</option>
              <option value="direct">Commande directe</option>
            </select>
          </CatalogField>
          {form.publicOrderingMode === "direct" ? <DirectOrderingDiagnosticPanel diagnostic={service.directOrderingDiagnostic} hasUnsavedCommercialChanges={form.publicVisible !== saved.publicVisible || form.selfServiceOrderable !== saved.selfServiceOrderable || form.publicOrderingMode !== saved.publicOrderingMode} configurationPolicy={service.configurationPolicy} /> : null}
          <CatalogToggle checked={form.selfServiceOrderable} description="Drapeau technique Billing V2 requis par les parcours libre-service existants. Il ne décide plus seul du mode commercial public." label="Autoriser les parcours libre-service Billing V2" name="selfServiceOrderable" onChange={(value) => setForm({ ...form, selfServiceOrderable: value })} />
          <CatalogToggle checked={form.discountEligible} description="Les remises d’engagement peuvent s’appliquer aux lignes mensuelles." label="Éligible aux remises" name="discountEligible" onChange={(value) => setForm({ ...form, discountEligible: value })} />
          <CatalogToggle checked={form.mandatoryForSubscription} description="Le service fait partie du socle obligatoire de l’abonnement." label="Obligatoire pour l’abonnement" name="mandatoryForSubscription" onChange={(value) => setForm({ ...form, mandatoryForSubscription: value })} />
          <CatalogField htmlFor="service-order" label="Ordre d’affichage"><input id="service-order" min={0} onChange={(e) => setForm({ ...form, displayOrder: Number(e.currentTarget.value) })} type="number" value={form.displayOrder} /></CatalogField>
        </div>}
      </section>
      <StickyActions busy={command.busy} dirty={dirty} onCancel={() => setForm(saved)} />
    </form> : null}
  </div>;
}

function DirectOrderingDiagnosticPanel({
  diagnostic,
  configurationPolicy,
  hasUnsavedCommercialChanges,
}: {
  diagnostic: BillingV2AdminDirectOrderingDiagnostic;
  configurationPolicy: string;
  hasUnsavedCommercialChanges: boolean;
}) {
  return <div className={`${styles.fieldFull} ${styles.advanced}`}>
    <h3>Commande en ligne</h3>
    <p>{diagnostic.eligible ? "✓ Éligible à la commande directe." : "⚠ Commande directe impossible actuellement."}</p>
    {hasUnsavedCommercialChanges ? <p className={styles.hint}>Le diagnostic correspond à la version enregistrée. Enregistrez les modifications pour le recalculer côté serveur.</p> : null}
    <dl className={styles.technicalGrid}>
      <div><dt>Configuration avant commande</dt><dd>{configurationPolicyLabel(configurationPolicy)}</dd></div>
      <div><dt>Éligibilité effective</dt><dd>{diagnostic.eligible ? "Commande directe disponible" : "Bloquée"}</dd></div>
    </dl>
    <ul>
      {diagnostic.checks.filter((check) => !check.satisfied).map((check) => <li key={check.code}>⚠ {check.label}</li>)}
      {diagnostic.eligible ? <li>✓ Toutes les conditions de commande directe sont réunies.</li> : null}
    </ul>
    <DirectOrderingPricing diagnostic={diagnostic} />
  </div>;
}

function DirectOrderingPricing({ diagnostic }: { diagnostic: BillingV2AdminDirectOrderingDiagnostic }) {
  if (diagnostic.tiers.length === 0) {
    return <p className={styles.hint}>Tarif du service : {presentInitialPrices(diagnostic.flatInitialPrices)}</p>;
  }
  return <div>
    <h4>Paliers et prix vérifiés</h4>
    <div className={styles.tableWrap}>
      <table className={styles.table}>
        <caption className="sr-only">Paliers évalués pour la commande directe</caption>
        <thead><tr><th>Palier</th><th>Statut</th><th>Public</th><th>Prix initial actif</th></tr></thead>
        <tbody>{diagnostic.tiers.map((tier) => <tr key={tier.id}>
          <td>{tier.name} <code>{tier.code}</code>{tier.unit ? ` · ${tier.unit}` : ""}</td>
          <td>{tier.active ? "Actif" : "Inactif"}</td>
          <td>{tier.publicSelectable ? "Sélectionnable" : "Non sélectionnable"}</td>
          <td>{presentInitialPrices(tier.currentInitialPrices)}</td>
        </tr>)}</tbody>
      </table>
    </div>
  </div>;
}

function presentInitialPrices(prices: BillingV2AdminDirectOrderingDiagnostic["flatInitialPrices"]) {
  if (prices.length === 0) return "Aucun dans la devise du catalogue";
  return prices.map((price) => `${formatMoney(price.amountCents, price.currency)}${price.billingCadence === "monthly" ? " / mois" : " · frais initiaux"}`).join(" ; ");
}

function configurationPolicyLabel(value: string) {
  if (value === "not_required") return "Aucune requise";
  if (value === "required_before_checkout") return "Configuration nécessaire avant commande";
  if (value === "deferrable_in_mixed_cart") return "Configuration différable dans un panier mixte";
  return "Policy non reconnue — commande directe bloquée";
}

function formatMoney(amountCents: number, currency: string) {
  return new Intl.NumberFormat("fr-FR", { style: "currency", currency }).format(amountCents / 100);
}
