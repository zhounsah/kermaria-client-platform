"use client";

import { useMemo, useState } from "react";

import { StatusBadge } from "@/components/StatusBadge";
import { formatCurrencyFromCents } from "@/lib/formatters";
import { basisPointsToPercent, centsToEuros, eurosToCents, percentToBasisPoints } from "@/lib/admin-catalog-units";
import { classifyAdminPrice, currentPriceForSelection, formatAdminDateTime } from "@/lib/admin-catalog-presenters";
import type { BillingV2AdminPrice, BillingV2AdminService } from "@/lib/internal-api";
import { CatalogFeedback, CatalogField, adminCatalogStyles as styles, useUnsavedChangesGuard } from "./AdminCatalogUi";
import { useAdminCatalogCommand } from "./useAdminCatalogCommand";

type Target = { key: string; label: string; tierId: string | null; prices: BillingV2AdminPrice[] };
type Revision = { amount: string; currency: string; cadence: string; trigger: string; taxPercent: string; effectiveAt: string };
const EMPTY_REVISION: Revision = { amount: "", currency: "EUR", cadence: "monthly", trigger: "initial_subscription", taxPercent: "", effectiveAt: "" };

export function ServicePricingPanel({ asOf, service }: { asOf: string; service: BillingV2AdminService }) {
  const targets = useMemo<Target[]>(() => [
    { key: "flat", label: "Service sans palier", tierId: null, prices: service.flatPrices },
    ...service.tiers.map((tier) => ({ key: tier.id, label: `${tier.name} (${tier.code})`, tierId: tier.id, prices: tier.prices })),
  ], [service.flatPrices, service.tiers]);
  const [editing, setEditing] = useState<string | null>(null);
  const [revision, setRevision] = useState<Revision>(EMPTY_REVISION);
  const [savedRevision, setSavedRevision] = useState<Revision | null>(null);
  const [taxTouched, setTaxTouched] = useState(false);
  const [validation, setValidation] = useState<string | null>(null);
  const command = useAdminCatalogCommand();
  const revisionDirty = editing !== null && savedRevision !== null && JSON.stringify(revision) !== JSON.stringify(savedRevision);
  useUnsavedChangesGuard(revisionDirty);

  async function publish(event: React.FormEvent, target: Target) {
    event.preventDefault();
    const amountCents = eurosToCents(revision.amount);
    const taxRateBasisPoints = revision.taxPercent.trim() ? percentToBasisPoints(revision.taxPercent) : undefined;
    if (amountCents === null || taxRateBasisPoints === null) {
      setValidation("Le montant accepte deux décimales et la TVA doit être comprise entre 0 % et 100 %."); return;
    }
    setValidation(null);
    const result = await command.send({ kind: "price.publish", serviceId: service.id, tierId: target.tierId, amountCents, currency: revision.currency, billingCadence: revision.cadence, chargeTrigger: revision.trigger, taxRateBasisPoints, effectiveAt: revision.effectiveAt ? new Date(revision.effectiveAt).toISOString() : undefined });
    if (result) cancelRevision();
  }

  function suggestedTaxPercent(target: Target, cadence: string, trigger: string) {
    return basisPointsToPercent(currentPriceForSelection(target.prices, asOf, cadence, trigger)?.taxRateBasisPoints);
  }

  function beginRevision(target: Target) {
    if (revisionDirty && !window.confirm("Abandonner la nouvelle version tarifaire non enregistr\u00e9e ?")) return;
    const next = { ...EMPTY_REVISION, taxPercent: suggestedTaxPercent(target, EMPTY_REVISION.cadence, EMPTY_REVISION.trigger) };
    setEditing(target.key); setRevision(next); setSavedRevision(next); setTaxTouched(false); setValidation(null);
  }

  function updateRevisionSelection(target: Target, patch: Partial<Pick<Revision, "cadence" | "trigger">>) {
    const next = { ...revision, ...patch };
    if (!taxTouched) next.taxPercent = suggestedTaxPercent(target, next.cadence, next.trigger);
    setRevision(next);
  }

  function cancelRevision() {
    setEditing(null); setRevision(EMPTY_REVISION); setSavedRevision(null); setTaxTouched(false); setValidation(null);
  }

  return <section className={styles.panel}>
    <div className={styles.sectionHeading}><div><h2>Tarification</h2><p>Un tarif publié est immuable. Toute évolution crée une nouvelle version et conserve les fenêtres passées.</p></div></div>
    <CatalogFeedback feedback={command.feedback} />
    {validation ? <p className={`${styles.notice} ${styles.dangerNotice}`}>{validation}</p> : null}
    <div className={styles.stack}>{targets.map((target) => {
      const current = target.prices.filter((price) => classifyAdminPrice(price, asOf) === "current");
      const scheduled = target.prices.filter((price) => classifyAdminPrice(price, asOf) === "scheduled");
      const history = target.prices.filter((price) => classifyAdminPrice(price, asOf) === "historical").sort((a, b) => new Date(b.validFrom).getTime() - new Date(a.validFrom).getTime());
      const comparisonPrice = currentPriceForSelection(target.prices, asOf, revision.cadence, revision.trigger);
      return <article className={styles.stack} key={target.key}>
        <div className={styles.sectionHeading}><div><h3>{target.label}</h3><p>{current.length} tarif(s) en vigueur · {scheduled.length} planifié(s) · {history.length} historique(s)</p></div><button className="button button-secondary button-small" onClick={() => beginRevision(target)} type="button">Créer une nouvelle version</button></div>
        {current.length === 0 ? <p className={styles.notice}>Aucun tarif actuellement en vigueur pour cette cible.</p> : current.map((price) => <PriceHero commandBusy={command.busy} key={price.id} onClose={() => closePrice(price)} price={price} />)}
        {editing === target.key ? <form className={styles.inlineForm} onSubmit={(event) => void publish(event, target)}>
          <div className={styles.fieldFull}><h3>Nouvelle version</h3><div className={styles.comparison}><div><span className={styles.hint}>Prix en vigueur correspondant</span><strong className={styles.priceAmount}>{comparisonPrice ? formatCurrencyFromCents(comparisonPrice.amountCents) : "Aucun"}</strong></div><div><span className={styles.hint}>Nouveau prix</span><strong className={styles.priceAmount}>{eurosToCents(revision.amount) === null ? "—" : formatCurrencyFromCents(eurosToCents(revision.amount) ?? 0)}</strong></div></div></div>
          <CatalogField htmlFor={`amount-${target.key}`} label="Montant HT (€)"><input id={`amount-${target.key}`} inputMode="decimal" onChange={(e) => setRevision({ ...revision, amount: e.currentTarget.value })} placeholder="0,00" required value={revision.amount} /></CatalogField>
          <CatalogField htmlFor={`currency-${target.key}`} label="Devise"><input id={`currency-${target.key}`} maxLength={3} onChange={(e) => setRevision({ ...revision, currency: e.currentTarget.value.toUpperCase() })} required value={revision.currency} /></CatalogField>
          <CatalogField htmlFor={`cadence-${target.key}`} label="Cadence"><select id={`cadence-${target.key}`} onChange={(e) => updateRevisionSelection(target, { cadence: e.currentTarget.value })} value={revision.cadence}><option value="monthly">Mensuel</option><option value="one_time">Ponctuel</option></select></CatalogField>
          <CatalogField htmlFor={`trigger-${target.key}`} label="Déclencheur"><select id={`trigger-${target.key}`} onChange={(e) => updateRevisionSelection(target, { trigger: e.currentTarget.value })} value={revision.trigger}><option value="initial_subscription">Souscription initiale</option><option value="subscription_change">Changement de configuration</option></select></CatalogField>
          <CatalogField htmlFor={`tax-${target.key}`} label="TVA (%)" hint="Affichée en pourcentage, transmise en points de base."><input id={`tax-${target.key}`} inputMode="decimal" onChange={(e) => { setTaxTouched(true); setRevision({ ...revision, taxPercent: e.currentTarget.value }); }} placeholder="20" value={revision.taxPercent} /></CatalogField>
          <CatalogField htmlFor={`effective-${target.key}`} label="Date d’effet" hint="Vide : prise d’effet immédiate."><input id={`effective-${target.key}`} onChange={(e) => setRevision({ ...revision, effectiveAt: e.currentTarget.value })} type="datetime-local" value={revision.effectiveAt} /></CatalogField>
          <div className={`${styles.fieldFull} ${styles.actionGroup}`}><button className="button button-secondary" onClick={cancelRevision} type="button">Annuler</button><button className="button" disabled={command.busy} type="submit">Publier la nouvelle version</button></div>
        </form> : null}
        {scheduled.length + history.length > 0 ? <div className={styles.tableWrap}><table className={styles.table}><caption className="sr-only">Versions planifiées et historiques de {target.label}</caption><thead><tr><th>Version</th><th>Montant</th><th>Cadence</th><th>TVA</th><th>Du</th><th>Au</th><th>État</th><th>Action</th></tr></thead><tbody>{[...scheduled, ...history].map((price) => { const window = classifyAdminPrice(price, asOf); const taxPercent = basisPointsToPercent(price.taxRateBasisPoints); return <tr key={price.id}><td>v{price.priceVersion}<div className="cell-secondary">{price.priceCode}</div></td><td>{formatCurrencyFromCents(price.amountCents)}</td><td>{price.billingCadence === "monthly" ? "Mensuel" : "Ponctuel"}</td><td>{taxPercent ? `${taxPercent} %` : "—"}</td><td>{formatAdminDateTime(price.validFrom)}</td><td>{formatAdminDateTime(price.validUntil)}</td><td><StatusBadge label={window === "scheduled" ? "Planifié" : "Historique"} tone={window === "scheduled" ? "info" : "neutral"} /></td><td>{window === "scheduled" ? <button className="table-action" disabled={command.busy} onClick={() => closePrice(price)} type="button">Retirer</button> : "—"}</td></tr>; })}</tbody></table></div> : null}
      </article>;
    })}</div>
  </section>;

  async function closePrice(price: BillingV2AdminPrice) {
    if (!window.confirm(`Retirer ${price.priceCode} ? Sa fenêtre sera fermée sans modifier son historique.`)) return;
    await command.send({ kind: "price.close", id: price.id });
  }
}

function PriceHero({ commandBusy, onClose, price }: { commandBusy: boolean; onClose: () => void; price: BillingV2AdminPrice }) {
  const taxPercent = basisPointsToPercent(price.taxRateBasisPoints);
  return <div className={styles.priceHero}><div><StatusBadge label="En vigueur" tone="success" /><strong className={styles.priceAmount}>{formatCurrencyFromCents(price.amountCents)}</strong><div className={styles.priceMeta}><span>{price.billingCadence === "monthly" ? "Mensuel" : "Ponctuel"} · {price.chargeTrigger === "initial_subscription" ? "Souscription" : "Changement"}</span><span>TVA : {taxPercent ? `${taxPercent} %` : "—"}</span></div></div><div className={styles.priceMeta}><strong>v{price.priceVersion} · {price.priceCode}</strong><span>Depuis le {formatAdminDateTime(price.validFrom)}</span><span>Valeur de saisie : {centsToEuros(price.amountCents)} €</span><button className="button button-secondary button-small" disabled={commandBusy} onClick={onClose} type="button">Retirer ce tarif</button></div></div>;
}
