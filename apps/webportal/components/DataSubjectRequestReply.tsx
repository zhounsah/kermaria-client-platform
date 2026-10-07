"use client";

import type { DataSubjectRequestDetail, DataSubjectRequestStatus } from "@kermaria/shared";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { requestBffJson } from "@/lib/client-api";

export function DataSubjectRequestReply({ id, admin, canExtendDeadline = false }: { id: string; admin: boolean; canExtendDeadline?: boolean }) {
  const router = useRouter();
  const [body, setBody] = useState("");
  const [status, setStatus] = useState<DataSubjectRequestStatus>("in_progress");
  const [extendDeadline, setExtendDeadline] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    setBusy(true);
    setError(null);
    const result = await requestBffJson<DataSubjectRequestDetail>(
      `${admin ? "/api/admin" : "/api"}/data-requests/${encodeURIComponent(id)}/messages`,
      { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(admin ? { body, status, extendDeadline } : { body }) });
    setBusy(false);
    if (!result.ok) { setError(result.error.message); return; }
    setBody("");
    setExtendDeadline(false);
    router.refresh();
  }

  return <form className="data-request-form content-panel" onSubmit={submit}>
    <h2>{admin ? "Répondre au client" : "Ajouter une précision"}</h2>
    {admin ? <><label htmlFor="data-request-status">Étape de traitement</label>
      <select disabled={extendDeadline} id="data-request-status" onChange={(event) => setStatus(event.target.value as DataSubjectRequestStatus)} value={status}>
        <option value="in_progress">En cours</option><option value="waiting_for_customer">Précision attendue</option>
        <option value="response_ready">Réponse disponible</option><option value="closed">Terminée</option>
        <option value="refused">Demande refusée, motif indiqué</option>
      </select></> : null}
    {admin && canExtendDeadline ? <label className="cms-custom-checkbox">
      <input checked={extendDeadline} onChange={(event) => { setExtendDeadline(event.target.checked); if (event.target.checked) setStatus("in_progress"); }} type="checkbox" />
      Prolonger le délai de deux mois pour une demande complexe
    </label> : null}
    <label htmlFor="data-request-reply">Message</label>
    <textarea id="data-request-reply" maxLength={5000} minLength={3} onChange={(event) => setBody(event.target.value)} required rows={6} value={body} />
    {admin ? <p className="field-hint">Ce message est visible dans l’espace client. Ne transmettez aucune donnée d’un autre client.</p> : null}
    {extendDeadline ? <p className="field-hint">Expliquez précisément la raison de la prolongation dans le message. Le client sera notifié.</p> : null}
    {error ? <p className="data-request-error" role="alert">{error}</p> : null}
    <button className="button" disabled={busy} type="submit">{busy ? "Envoi en cours…" : "Publier le message"}</button>
  </form>;
}
