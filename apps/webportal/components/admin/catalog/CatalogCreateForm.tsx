"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";

import { percentToBasisPoints } from "@/lib/admin-catalog-units";
import { CatalogFeedback, CatalogField, CatalogToggle, StickyActions, adminCatalogStyles as styles, useUnsavedChangesGuard } from "./AdminCatalogUi";
import { useAdminCatalogCommand } from "./useAdminCatalogCommand";

type Kind = "service" | "formule" | "engagement";

export function CatalogCreateForm({ kind }: { kind: Kind }) {
  const router = useRouter();
  const command = useAdminCatalogCommand();
  const [form, setForm] = useState<Record<string, string | number | boolean>>(() => initialCreateForm(kind));
  const dirty = JSON.stringify(form) !== JSON.stringify(initialCreateForm(kind));
  const [validation, setValidation] = useState<string | null>(null);
  useUnsavedChangesGuard(dirty);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    let payload: Record<string, unknown>;
    if (kind === "service") payload = { kind: "service.create", ...form };
    else if (kind === "formule") payload = { kind: "preset.create", ...form };
    else {
      const discountBasisPoints = percentToBasisPoints(String(form.discountPercent));
      if (discountBasisPoints === null) { setValidation("La remise doit être comprise entre 0 % et 100 %, avec deux décimales maximum."); return; }
      payload = {
        kind: "commitment.create",
        code: form.code,
        name: form.name,
        commitmentMonths: form.commitmentMonths,
        allowMonthlyPayment: form.allowMonthlyPayment,
        allowUpfrontPayment: form.allowUpfrontPayment,
        status: form.status,
        displayOrder: form.displayOrder,
        discountBasisPoints,
      };
    }
    setValidation(null);
    const result = await command.send(payload);
    if (result?.id) router.push(`/admin/catalog/${kind === "service" ? "services" : kind === "formule" ? "formules" : "engagements"}/${result.id}`);
  }

  return <form onSubmit={submit}><section className={styles.panel}><div className={styles.sectionHeading}><div><h2>{kind === "service" ? "Nouveau service" : kind === "formule" ? "Nouvelle formule" : "Nouvel engagement"}</h2><p>Le code est défini une seule fois et devient immuable après la création.</p></div></div><CatalogFeedback feedback={command.feedback} />{validation ? <p className={`${styles.notice} ${styles.dangerNotice}`}>{validation}</p> : null}<div className={styles.formGrid}>
    <CatalogField htmlFor="create-code" label="Code" hint="Lettres, chiffres, tirets et underscores."><input id="create-code" maxLength={kind === "formule" ? 96 : 64} onChange={(e) => set("code", e.currentTarget.value)} required value={String(form.code)} /></CatalogField>
    <CatalogField htmlFor="create-name" label="Nom"><input id="create-name" maxLength={160} onChange={(e) => set("name", e.currentTarget.value)} required value={String(form.name)} /></CatalogField>
    {kind !== "engagement" ? <CatalogField full htmlFor="create-description" label="Description"><textarea id="create-description" onChange={(e) => set("description", e.currentTarget.value)} value={String(form.description)} /></CatalogField> : null}
    {kind === "service" ? <>
      <CatalogField htmlFor="create-category" label="Catégorie"><input id="create-category" maxLength={80} onChange={(e) => set("category", e.currentTarget.value)} value={String(form.category)} /></CatalogField>
      <CatalogField htmlFor="create-billing-type" label="Type de facturation"><select id="create-billing-type" onChange={(e) => set("billingType", e.currentTarget.value)} value={String(form.billingType)}><option value="recurring">Récurrent</option><option value="one_time">Ponctuel</option><option value="included">Inclus</option></select></CatalogField>
      <CatalogField htmlFor="create-scope" label="Portée par défaut"><select id="create-scope" onChange={(e) => set("defaultScopeType", e.currentTarget.value)} value={String(form.defaultScopeType)}><option value="subscription">Abonnement</option><option value="user">Utilisateur</option></select></CatalogField>
      <CatalogField htmlFor="create-pricing-model" label="Modèle tarifaire"><select id="create-pricing-model" onChange={(e) => set("pricingModel", e.currentTarget.value)} value={String(form.pricingModel)}><option value="fixed">Prix fixe</option><option value="tiered">Par paliers</option></select></CatalogField>
      <CatalogToggle checked={Boolean(form.mandatoryForSubscription)} description="Le service appartient au socle de chaque abonnement." label="Obligatoire" name="mandatory" onChange={(value) => set("mandatoryForSubscription", value)} />
      <CatalogToggle checked={Boolean(form.discountEligible)} description="Les lignes mensuelles peuvent recevoir une remise." label="Éligible aux remises" name="discount" onChange={(value) => set("discountEligible", value)} />
      <p className={`${styles.notice} ${styles.fieldFull}`}>Le service sera créé inactif, masqué et non commandable. Les paliers et tarifs seront configurés depuis sa fiche.</p>
    </> : null}
    {kind === "formule" ? <><CatalogField htmlFor="create-status" label="Statut"><select id="create-status" onChange={(e) => set("status", e.currentTarget.value)} value={String(form.status)}><option value="active">Active</option><option value="inactive">Inactive</option></select></CatalogField><CatalogToggle checked={Boolean(form.isPublic)} description="La formule peut apparaître sur les parcours publics." label="Visible sur la vitrine" name="public" onChange={(value) => set("isPublic", value)} /></> : null}
    {kind === "engagement" ? <><CatalogField htmlFor="create-months" label="Durée (mois)"><input id="create-months" max={120} min={1} onChange={(e) => set("commitmentMonths", Number(e.currentTarget.value))} required type="number" value={Number(form.commitmentMonths)} /></CatalogField><CatalogField htmlFor="create-discount" label="Remise générale (%)" hint="Les options mensuel/comptant seront affinées dans la fiche."><input id="create-discount" inputMode="decimal" onChange={(e) => set("discountPercent", e.currentTarget.value)} value={String(form.discountPercent)} /></CatalogField><CatalogToggle checked={Boolean(form.allowMonthlyPayment)} description="Autorise un règlement mensuel." label="Paiement mensuel" name="monthly" onChange={(value) => set("allowMonthlyPayment", value)} /><CatalogToggle checked={Boolean(form.allowUpfrontPayment)} description="Autorise un règlement comptant." label="Paiement comptant" name="upfront" onChange={(value) => set("allowUpfrontPayment", value)} /></> : null}
    <CatalogField htmlFor="create-order" label="Ordre d’affichage"><input id="create-order" min={0} onChange={(e) => set("displayOrder", Number(e.currentTarget.value))} type="number" value={Number(form.displayOrder)} /></CatalogField>
  </div></section><StickyActions busy={command.busy} dirty={dirty} onCancel={() => router.back()} /></form>;

  function set(name: string, value: string | number | boolean) { setForm((current) => ({ ...current, [name]: value })); }
}

function initialCreateForm(kind: Kind): Record<string, string | number | boolean> {
  if (kind === "service") return { code: "", name: "", description: "", category: "", billingType: "recurring", defaultScopeType: "subscription", pricingModel: "fixed", mandatoryForSubscription: false, discountEligible: true, publicVisible: false, selfServiceOrderable: false, status: "inactive", displayOrder: 0 };
  if (kind === "formule") return { code: "", name: "", description: "", status: "active", isPublic: false, displayOrder: 0 };
  return { code: "", name: "", commitmentMonths: 12, discountPercent: "0", allowMonthlyPayment: true, allowUpfrontPayment: true, status: "active", displayOrder: 0 };
}
