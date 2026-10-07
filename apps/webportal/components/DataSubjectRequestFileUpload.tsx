"use client";

import type { DataSubjectRequestDetail } from "@kermaria/shared";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { requestBffJson } from "@/lib/client-api";

export function DataSubjectRequestFileUpload({ id }: { id: string }) {
  const router = useRouter();
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");

  async function upload() {
    if (!file || busy) return;
    setBusy(true); setMessage("");
    const form = new FormData(); form.set("file", file);
    const result = await requestBffJson<DataSubjectRequestDetail>(
      `/api/admin/data-requests/${encodeURIComponent(id)}/file`,
      { method: "POST", body: form }, 35000);
    setBusy(false);
    if (!result.ok) { setMessage(result.error.message); return; }
    setFile(null);
    setMessage("Le document est disponible dans l’espace client.");
    router.refresh();
  }

  return <section className="content-panel data-request-form">
    <h2>Remettre un document</h2>
    <p>Le fichier sera réservé à la personne qui a fait la demande. Son dépôt la notifiera et indiquera qu’une réponse est disponible.</p>
    <label htmlFor="data-request-response-file">Document PDF, CSV, JSON ou ZIP (10 Mo maximum)</label>
    <input accept=".pdf,.csv,.json,.zip,application/pdf,text/csv,application/json,application/zip" id="data-request-response-file" onChange={(event) => setFile(event.target.files?.[0] ?? null)} type="file" />
    {message ? <p role="status">{message}</p> : null}
    <button className="button" disabled={!file || busy} onClick={() => void upload()} type="button">{busy ? "Envoi en cours…" : "Rendre le document disponible"}</button>
  </section>;
}
