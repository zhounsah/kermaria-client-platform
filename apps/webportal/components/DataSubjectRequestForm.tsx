"use client";

import type { DataSubjectRequestCreatePayload, DataSubjectRequestDetail, DataSubjectRequestType } from "@kermaria/shared";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { requestBffJson } from "@/lib/client-api";

const choices: { value: DataSubjectRequestType; label: string }[] = [
  { value: "access", label: "Consulter mes données" },
  { value: "rectification", label: "Corriger mes données" },
  { value: "erasure", label: "Demander leur suppression" },
  { value: "portability", label: "Récupérer mes données" },
  { value: "objection", label: "M’opposer à une utilisation" },
  { value: "restriction", label: "Limiter leur utilisation" },
];

export function DataSubjectRequestForm() {
  const router = useRouter();
  const [requestType, setRequestType] = useState<DataSubjectRequestType>("access");
  const [details, setDetails] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    setBusy(true);
    setError(null);
    const payload: DataSubjectRequestCreatePayload = { requestType, details: details.trim() };
    const result = await requestBffJson<DataSubjectRequestDetail>("/api/data-requests", {
      method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload),
    });
    setBusy(false);
    if (!result.ok) { setError(result.error.message); return; }
    router.push(`/profile/donnees/${encodeURIComponent(result.data.id)}`);
    router.refresh();
  }

  return <form className="data-request-form content-panel" onSubmit={submit}>
    <h2>Faire une demande</h2>
    <p>Choisissez ce que vous souhaitez faire. Vous pourrez retrouver la réponse ici.</p>
    <label htmlFor="data-request-type">Ma demande concerne</label>
    <select id="data-request-type" onChange={(event) => setRequestType(event.target.value as DataSubjectRequestType)} value={requestType}>
      {choices.map((choice) => <option key={choice.value} value={choice.value}>{choice.label}</option>)}
    </select>
    <label htmlFor="data-request-details">Quelques précisions</label>
    <textarea id="data-request-details" maxLength={5000} minLength={10} onChange={(event) => setDetails(event.target.value)} required rows={5} value={details} />
    <p className="field-hint">Ne transmettez pas de pièce d’identité dans ce formulaire. Nous vous contacterons seulement si une vérification est nécessaire.</p>
    {error ? <p className="data-request-error" role="alert">{error}</p> : null}
    <button className="button" disabled={busy} type="submit">{busy ? "Envoi en cours…" : "Envoyer ma demande"}</button>
  </form>;
}

export { choices as dataRequestChoices };
