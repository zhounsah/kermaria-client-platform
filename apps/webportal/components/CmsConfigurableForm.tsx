"use client";

import type { DataSubjectRequestDetail, DataSubjectRequestType, SitePageBlock, SitePageFormField } from "@kermaria/shared";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { requestBffJson } from "@/lib/client-api";

const requestTypes: { value: DataSubjectRequestType; label: string }[] = [
  { value: "access", label: "Consulter mes données" },
  { value: "rectification", label: "Corriger mes données" },
  { value: "erasure", label: "Demander leur suppression" },
  { value: "portability", label: "Récupérer mes données" },
  { value: "objection", label: "M’opposer à une utilisation" },
  { value: "restriction", label: "Limiter leur utilisation" },
];

export function CmsConfigurableForm({ block, preview = false }: {
  block: SitePageBlock; preview?: boolean;
}) {
  const router = useRouter();
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [subject, setSubject] = useState(block.title ?? "");
  const [message, setMessage] = useState("");
  const [requestType, setRequestType] = useState<DataSubjectRequestType>("access");
  const [extras, setExtras] = useState<Record<string, string | boolean>>({});
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState("");
  const fields = block.fields ?? [];

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (preview) return;
    if (busy) return;
    const additional = fields.map((field) => `${field.label} : ${String(extras[field.id] ?? "")}`).join("\n");
    const combined = additional ? `${message.trim()}\n\n${additional}` : message.trim();
    if (combined.length > 5000) { setFeedback("Votre message est trop long. Raccourcissez-le avant l’envoi."); return; }
    setBusy(true); setFeedback("");
    if (block.action === "contact") {
      const result = await requestBffJson<{ code: string }>("/api/contact", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name, email, subject, message: combined, formuleCode: null }),
      });
      setBusy(false);
      if (!result.ok) { setFeedback(result.error.message); return; }
      setFeedback("Votre message a bien été envoyé.");
      setName(""); setEmail(""); setMessage(""); setExtras({});
      return;
    }
    const result = await requestBffJson<DataSubjectRequestDetail>("/api/data-requests", {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ requestType, details: combined }),
    });
    setBusy(false);
    if (!result.ok) { setFeedback(result.error.message); return; }
    router.push(`/profile/donnees/${encodeURIComponent(result.data.id)}`);
  }

  return <form className="data-request-form cms-custom-form" onSubmit={submit}>
    {block.action === "contact" ? <>
      <label>Votre nom <input autoComplete="name" maxLength={120} onChange={(event) => setName(event.target.value)} required value={name} /></label>
      <label>Votre adresse e-mail <input autoComplete="email" maxLength={254} onChange={(event) => setEmail(event.target.value)} required type="email" value={email} /></label>
      <label>Objet <input maxLength={150} onChange={(event) => setSubject(event.target.value)} required value={subject} /></label>
    </> : <label>Votre demande concerne <select onChange={(event) => setRequestType(event.target.value as DataSubjectRequestType)} value={requestType}>
      {requestTypes.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}</select></label>}
    <label>{block.action === "contact" ? "Votre message" : "Quelques précisions"}
      <textarea maxLength={block.action === "contact" ? 3000 : 3500} minLength={10} onChange={(event) => setMessage(event.target.value)} required rows={5} value={message} /></label>
    {fields.map((field) => <ExtraField field={field} key={field.id} onChange={(value) => setExtras((current) => ({ ...current, [field.id]: value }))} value={extras[field.id]} />)}
    {block.action === "data_request" ? <p className="field-hint">Ne transmettez pas de pièce d’identité dans ce formulaire.</p> : null}
    {feedback ? <p role="status">{feedback}</p> : null}
    <button className="button" disabled={busy || preview} type="submit">{busy ? "Envoi en cours…" : block.label || "Envoyer"}</button>
  </form>;
}

function ExtraField({ field, value, onChange }: {
  field: SitePageFormField; value: string | boolean | undefined;
  onChange: (value: string | boolean) => void;
}) {
  if (field.type === "checkbox") return <label className="cms-custom-checkbox">
    <input checked={value === true} onChange={(event) => onChange(event.target.checked)} required={field.required} type="checkbox" />{field.label}
  </label>;
  if (field.type === "select") return <label>{field.label}<select onChange={(event) => onChange(event.target.value)} required={field.required} value={typeof value === "string" ? value : ""}>
    <option value="">Choisir…</option>{field.options?.map((option) => <option key={option} value={option}>{option}</option>)}
  </select></label>;
  return <label>{field.label}<input maxLength={200} onChange={(event) => onChange(event.target.value)} required={field.required}
    type={field.type === "email" ? "email" : field.type === "number" ? "number" : "text"} value={typeof value === "string" ? value : ""} /></label>;
}
