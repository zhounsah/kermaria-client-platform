"use client";

import { useState } from "react";

import { StatusBadge } from "@/components/StatusBadge";
import type { BillingV2AdminService, BillingV2AdminTier, BillingV2AdminTierAttribute } from "@/lib/internal-api";
import { CatalogFeedback, CatalogField, CatalogToggle, ImmutableCode, adminCatalogStyles as styles, useUnsavedChangesGuard } from "./AdminCatalogUi";
import { useAdminCatalogCommand } from "./useAdminCatalogCommand";

type AttributeDraft = { attributeCode: string; value: string; unit: string; numeric: boolean };
type TierDraft = { code: string; label: string; publicLabel: string; description: string; numericValue: string; unit: string; publicSelectable: boolean; status: string; displayOrder: number; attributes: AttributeDraft[] };

function attributeDraft(attribute: BillingV2AdminTierAttribute): AttributeDraft {
  return { attributeCode: attribute.attributeCode, value: String(attribute.valueNumeric ?? attribute.valueText ?? ""), unit: attribute.unit ?? "", numeric: attribute.valueNumeric !== null };
}
function tierDraft(tier: BillingV2AdminTier): TierDraft {
  return { code: tier.code, label: tier.name, publicLabel: tier.publicLabel ?? "", description: tier.description ?? "", numericValue: tier.numericValue === null ? "" : String(tier.numericValue), unit: tier.unit ?? "", publicSelectable: tier.publicSelectable, status: tier.status, displayOrder: tier.displayOrder, attributes: tier.attributes.map(attributeDraft) };
}
const EMPTY_TIER: TierDraft = { code: "", label: "", publicLabel: "", description: "", numericValue: "", unit: "", publicSelectable: false, status: "inactive", displayOrder: 0, attributes: [] };

export function ServiceTiersPanel({ service }: { service: BillingV2AdminService }) {
  const [selectedId, setSelectedId] = useState<string | "new" | null>(null);
  const selected = selectedId && selectedId !== "new" ? service.tiers.find((tier) => tier.id === selectedId) ?? null : null;
  const [draft, setDraft] = useState<TierDraft>(EMPTY_TIER);
  const [saved, setSaved] = useState<TierDraft>(EMPTY_TIER);
  const command = useAdminCatalogCommand();
  const dirty = selectedId !== null && JSON.stringify(draft) !== JSON.stringify(saved);

  useUnsavedChangesGuard(dirty);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const attributes = draft.attributes.filter((item) => item.attributeCode.trim()).map((item) => ({ attributeCode: item.attributeCode, valueNumeric: item.numeric && item.value !== "" ? Number(item.value) : undefined, valueText: item.numeric ? undefined : item.value, unit: item.unit }));
    if (selectedId !== "new" && selected) {
      const retainedCodes = new Set(draft.attributes.map((item) => item.attributeCode.trim()).filter(Boolean));
      for (const original of selected.attributes) {
        if (!retainedCodes.has(original.attributeCode)) {
          // UpdateTierAsync interpr├¿te une valeur num├®rique + texte vides comme
          // une suppression. Une omission simple laisserait l'attribut en base.
          attributes.push({ attributeCode: original.attributeCode, valueNumeric: undefined, valueText: undefined, unit: "" });
        }
      }
    }
    const common = { label: draft.label, publicLabel: draft.publicLabel, description: draft.description, numericValue: draft.numericValue === "" ? undefined : Number(draft.numericValue), unit: draft.unit, publicSelectable: draft.publicSelectable, status: draft.status, displayOrder: draft.displayOrder, attributes };
    const result = selectedId === "new"
      ? await command.send({ kind: "tier.create", serviceId: service.id, code: draft.code, ...common })
      : await command.send({ kind: "tier.update", id: selectedId, ...common });
    if (result) { setSaved(draft); if (selectedId === "new") setSelectedId(null); }
  }

  return <section className={styles.panel}>
    <div className={styles.sectionHeading}><div><h2>Paliers</h2><p>Capacités et variantes du service, administrées indépendamment du formulaire principal.</p></div><button className="button" onClick={() => beginCreate()} type="button">Créer un palier</button></div>
    <CatalogFeedback feedback={command.feedback} />
    <div className={styles.stack}>
      {service.tiers.length === 0 ? <p className={styles.empty}>Aucun palier.</p> : service.tiers.map((tier) => <div className={styles.rowCard} key={tier.id}><div><strong>{tier.name}</strong><div className={styles.rowMeta}><code>{tier.code}</code><StatusBadge label={tier.status === "active" ? "Actif" : "Inactif"} tone={tier.status === "active" ? "success" : "neutral"} /><span>{tier.numericValue ?? "—"} {tier.unit ?? ""}</span><span>{tier.publicSelectable ? "Public" : "Interne"}</span></div></div><button className="button button-secondary button-small" onClick={() => beginEdit(tier)} type="button">Modifier</button></div>)}
    </div>
    {selectedId ? <form className={styles.inlineForm} onSubmit={submit}>
      <div className={styles.fieldFull}><h3>{selectedId === "new" ? "Nouveau palier" : `Modifier ${selected?.name ?? "le palier"}`}</h3>{selectedId === "new" ? null : <ImmutableCode value={draft.code} />}</div>
      {selectedId === "new" ? <CatalogField htmlFor="tier-code" label="Code" hint="Immuable après création."><input id="tier-code" maxLength={64} onChange={(e) => setDraft({ ...draft, code: e.currentTarget.value })} required value={draft.code} /></CatalogField> : null}
      <CatalogField htmlFor="tier-label" label="Libellé"><input id="tier-label" maxLength={160} onChange={(e) => setDraft({ ...draft, label: e.currentTarget.value })} required value={draft.label} /></CatalogField>
      <CatalogField htmlFor="tier-public-label" label="Libellé public"><input id="tier-public-label" maxLength={160} onChange={(e) => setDraft({ ...draft, publicLabel: e.currentTarget.value })} value={draft.publicLabel} /></CatalogField>
      <CatalogField full htmlFor="tier-description" label="Description"><textarea id="tier-description" onChange={(e) => setDraft({ ...draft, description: e.currentTarget.value })} value={draft.description} /></CatalogField>
      <CatalogField htmlFor="tier-value" label="Valeur"><input id="tier-value" onChange={(e) => setDraft({ ...draft, numericValue: e.currentTarget.value })} type="number" value={draft.numericValue} /></CatalogField>
      <CatalogField htmlFor="tier-unit" label="Unité"><input id="tier-unit" maxLength={32} onChange={(e) => setDraft({ ...draft, unit: e.currentTarget.value })} value={draft.unit} /></CatalogField>
      <CatalogField htmlFor="tier-status" label="Statut"><select id="tier-status" onChange={(e) => setDraft({ ...draft, status: e.currentTarget.value })} value={draft.status}><option value="active">Actif</option><option value="inactive">Inactif</option></select></CatalogField>
      <CatalogField htmlFor="tier-order" label="Ordre"><input id="tier-order" min={0} onChange={(e) => setDraft({ ...draft, displayOrder: Number(e.currentTarget.value) })} type="number" value={draft.displayOrder} /></CatalogField>
      <CatalogToggle checked={draft.publicSelectable} description="Ce palier peut être sélectionné sur les parcours publics." label="Sélectionnable publiquement" name="tierPublic" onChange={(value) => setDraft({ ...draft, publicSelectable: value })} />
      <div className={styles.fieldFull}><div className={styles.sectionHeading}><div><h3>Attributs commerciaux</h3><p>Une ligne structurée par attribut, sans syntaxe technique à mémoriser.</p></div><button className="button button-secondary button-small" onClick={() => setDraft({ ...draft, attributes: [...draft.attributes, { attributeCode: "", value: "", unit: "", numeric: false }] })} type="button">Ajouter un attribut</button></div>
        <div className={styles.stack}>{draft.attributes.map((attribute, index) => <div className={styles.rowCard} key={`${index}-${attribute.attributeCode}`}><div className={styles.formGrid} style={{ width: "100%" }}><CatalogField htmlFor={`attribute-code-${index}`} label="Code"><input id={`attribute-code-${index}`} onChange={(e) => updateAttribute(index, { attributeCode: e.currentTarget.value })} value={attribute.attributeCode} /></CatalogField><CatalogField htmlFor={`attribute-value-${index}`} label="Valeur"><input id={`attribute-value-${index}`} onChange={(e) => updateAttribute(index, { value: e.currentTarget.value })} value={attribute.value} /></CatalogField><CatalogField htmlFor={`attribute-unit-${index}`} label="Unité"><input id={`attribute-unit-${index}`} onChange={(e) => updateAttribute(index, { unit: e.currentTarget.value })} value={attribute.unit} /></CatalogField><CatalogToggle checked={attribute.numeric} description="La valeur sera transmise comme nombre." label="Valeur numérique" name={`attribute-numeric-${index}`} onChange={(value) => updateAttribute(index, { numeric: value })} /></div><button className="table-action" onClick={() => setDraft({ ...draft, attributes: draft.attributes.filter((_, itemIndex) => itemIndex !== index) })} type="button">Retirer</button></div>)}</div>
      </div>
      <div className={`${styles.fieldFull} ${styles.actionGroup}`}><button className="button button-secondary" disabled={command.busy} onClick={() => { setDraft(saved); setSelectedId(null); }} type="button">Annuler</button><button className="button" disabled={command.busy || !dirty} type="submit">{command.busy ? "Enregistrement…" : "Enregistrer le palier"}</button></div>
    </form> : null}
  </section>;

  function updateAttribute(index: number, values: Partial<AttributeDraft>) {
    setDraft({ ...draft, attributes: draft.attributes.map((item, itemIndex) => itemIndex === index ? { ...item, ...values } : item) });
  }
  function confirmDiscardDraft() { return !dirty || window.confirm("Abandonner les modifications non enregistr├®es de ce palier ?"); }
  function beginCreate() { if (!confirmDiscardDraft()) return; setDraft(EMPTY_TIER); setSaved(EMPTY_TIER); setSelectedId("new"); }
  function beginEdit(tier: BillingV2AdminTier) { if (!confirmDiscardDraft()) return; const next = tierDraft(tier); setDraft(next); setSaved(next); setSelectedId(tier.id); }
}
