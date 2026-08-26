"use client";

import Link from "next/link";
import { useMemo, useState } from "react";

import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { currentAdminPrices } from "@/lib/admin-catalog-presenters";
import type { BillingV2AdminCatalogProviderCoverage, BillingV2AdminCatalogSnapshot, BillingV2AdminPrice, BillingV2AdminProviderReadiness } from "@/lib/internal-api";
import { CatalogFeedback, CatalogField, CatalogNavigation, adminCatalogStyles as styles, useUnsavedChangesGuard } from "./AdminCatalogUi";
import { useAdminCatalogCommand } from "./useAdminCatalogCommand";

const ENVIRONMENTS: Record<string, readonly string[]> = { stripe: ["test", "live"], paypal: ["sandbox", "live"] };

export function CatalogIntegrations({ asOf, coverage, readiness, snapshot }: { asOf: string; coverage: BillingV2AdminCatalogProviderCoverage[]; readiness: BillingV2AdminProviderReadiness[]; snapshot: BillingV2AdminCatalogSnapshot }) {
  const command = useAdminCatalogCommand();
  const currentPrices = useMemo(() => snapshot.services.flatMap((service) => currentAdminPrices(service, asOf).map((price) => ({ price, service }))), [asOf, snapshot.services]);
  const [priceId, setPriceId] = useState(currentPrices[0]?.price.id ?? ""); const [provider, setProvider] = useState("stripe"); const [environment, setEnvironment] = useState("test");
  const initialMapping = findMapping(currentPrices, priceId, provider, environment);
  const initialDraft = mappingDraft(initialMapping);
  const [productId, setProductId] = useState(initialDraft.productId); const [externalPriceId, setExternalPriceId] = useState(initialDraft.externalPriceId); const [planId, setPlanId] = useState(initialDraft.planId); const [status, setStatus] = useState(initialDraft.status);
  const [savedMapping, setSavedMapping] = useState(initialDraft);
  const currentDraft = { productId, externalPriceId, planId, status };
  const mappingDirty = JSON.stringify(currentDraft) !== JSON.stringify(savedMapping);
  useUnsavedChangesGuard(mappingDirty);
  function confirmDiscardMapping() { return !mappingDirty || window.confirm("Abandonner les modifications non enregistr\u00e9es de ce mapping ?"); }
  function selectMapping(nextPriceId: string, nextProvider: string, nextEnvironment: string) { const next = mappingDraft(findMapping(currentPrices, nextPriceId, nextProvider, nextEnvironment)); setProductId(next.productId); setExternalPriceId(next.externalPriceId); setPlanId(next.planId); setStatus(next.status); setSavedMapping(next); }
  function changePrice(nextPriceId: string) { if (!confirmDiscardMapping()) return; setPriceId(nextPriceId); selectMapping(nextPriceId, provider, environment); }
  function changeProvider(nextProvider: string) { if (!confirmDiscardMapping()) return; const nextEnvironment = ENVIRONMENTS[nextProvider]?.[0] ?? ""; setProvider(nextProvider); setEnvironment(nextEnvironment); selectMapping(priceId, nextProvider, nextEnvironment); }
  function changeEnvironment(nextEnvironment: string) { if (!confirmDiscardMapping()) return; setEnvironment(nextEnvironment); selectMapping(priceId, provider, nextEnvironment); }
  async function save(event: React.FormEvent) { event.preventDefault(); const result = await command.send({ kind: "provider.mapping", priceId, provider, environment, externalProductId: productId, externalPriceId, externalPlanId: planId, status }); if (result) setSavedMapping(currentDraft); }
  return <div className={styles.shell}><PageHeader eyebrow="Catalogue · Intégrations" title="Intégrations" description="État des rails commerciaux et identifiants externes strictement nécessaires." action={<Link className="button button-secondary" href="/admin/billing-v2">Exploitation Billing V2</Link>} /><CatalogNavigation active="integrations" /><CatalogFeedback feedback={command.feedback} />
    <div className={styles.integrationGrid}>{["stripe", "paypal"].map((name) => { const state = readiness.find((item) => item.provider === name); const row = coverage.find((item) => item.provider === name && item.environment === state?.environment) ?? coverage.find((item) => item.provider === name); const required = name === "paypal"; const mapped = row?.mappedPriceCount ?? 0; const total = row?.currentPriceCount ?? currentPrices.length; const missing = Math.max(0, total - mapped); return <article className={styles.integrationCard} key={name}><StatusBadge label={state?.providerConfigured ? "Configuré" : "Non configuré"} tone={state?.providerConfigured ? "success" : "warning"} /><h2>{name === "stripe" ? "Stripe" : "PayPal"}</h2><p className={styles.hint}>{name === "stripe" ? "Billing V2 construit ses lignes avec price_data inline : aucun price_id préexistant n’est requis." : "Ce rail exige un plan externe préexistant pour vendre les tarifs concernés."}</p><div className={styles.integrationFacts}><div><span>Mode</span><strong>{state?.environment ?? "Indisponible"}</strong></div><div><span>Mapping externe</span><strong>{required ? "Requis" : "Facultatif"}</strong></div><div><span>Couverture</span><strong>{mapped} / {total}</strong></div><div><span>Action</span><strong>{required && missing > 0 ? `${missing} mapping(s) à compléter` : "Aucune action bloquante"}</strong></div></div></article>; })}</div>
    <section className={styles.panel}><div className={styles.sectionHeading}><div><h2>Mappings avancés</h2><p>Ces identifiants restent techniques. Un mapping Stripe facultatif ne devient jamais une condition de checkout.</p></div></div>{currentPrices.length === 0 ? <p className={styles.empty}>Aucune version tarifaire en vigueur.</p> : <form className={styles.formGrid} onSubmit={save}><CatalogField full htmlFor="mapping-price" label="Version tarifaire en vigueur"><select id="mapping-price" onChange={(e) => changePrice(e.currentTarget.value)} value={priceId}>{currentPrices.map((entry) => <option key={entry.price.id} value={entry.price.id}>{entry.service.name} · {entry.price.priceCode}</option>)}</select></CatalogField><CatalogField htmlFor="mapping-provider" label="Intégration"><select id="mapping-provider" onChange={(e) => changeProvider(e.currentTarget.value)} value={provider}><option value="stripe">Stripe</option><option value="paypal">PayPal</option></select></CatalogField><CatalogField htmlFor="mapping-environment" label="Environnement"><select id="mapping-environment" onChange={(e) => changeEnvironment(e.currentTarget.value)} value={environment}>{(ENVIRONMENTS[provider] ?? []).map((value) => <option key={value} value={value}>{value}</option>)}</select></CatalogField><CatalogField htmlFor="mapping-product" label="Identifiant produit externe"><input id="mapping-product" maxLength={255} onChange={(e) => setProductId(e.currentTarget.value)} value={productId} /></CatalogField><CatalogField htmlFor="mapping-price-id" label="Identifiant prix externe"><input id="mapping-price-id" maxLength={255} onChange={(e) => setExternalPriceId(e.currentTarget.value)} value={externalPriceId} /></CatalogField><CatalogField htmlFor="mapping-plan" label="Identifiant plan externe"><input id="mapping-plan" maxLength={255} onChange={(e) => setPlanId(e.currentTarget.value)} value={planId} /></CatalogField><CatalogField htmlFor="mapping-status" label="Statut"><select id="mapping-status" onChange={(e) => setStatus(e.currentTarget.value)} value={status}><option value="active">Actif</option><option value="inactive">Inactif</option></select></CatalogField><div className={styles.fieldFull}><button className="button" disabled={command.busy || !priceId} type="submit">Enregistrer le mapping</button></div></form>}</section>
  </div>;
}

function mappingDraft(mapping: ReturnType<typeof findMapping>) {
  return {
    productId: mapping?.externalProductId ?? "",
    externalPriceId: mapping?.externalPriceId ?? "",
    planId: mapping?.externalPlanId ?? "",
    status: mapping?.status ?? "active",
  };
}

function findMapping(currentPrices: Array<{ price: BillingV2AdminPrice }>, priceId: string, provider: string, environment: string) {
  return currentPrices.find((entry) => entry.price.id === priceId)?.price.providerMappings.find((entry) => entry.provider === provider && entry.environment === environment);
}
