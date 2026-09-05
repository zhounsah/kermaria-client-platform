"use client";

import { FormEvent, useState, useTransition } from "react";

import { requestBffJson } from "@/lib/client-api";

export function AdminBackupIntegrationForm() {
  const [message, setMessage] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);

    startTransition(async () => {
      setMessage(null);
      const result = await requestBffJson<unknown>("/api/admin/backups/integrations", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          provider: "veeam",
          externalJobId: String(data.get("externalJobId") ?? "").trim(),
          customerId: String(data.get("customerId") ?? "").trim(),
          serviceId: String(data.get("serviceId") ?? "").trim(),
          enabled: data.get("enabled") === "on",
          expectedIntervalMinutes: Number(data.get("expectedIntervalMinutes")),
          criticalAfterMinutes: Number(data.get("criticalAfterMinutes")),
          staleAfterMinutes: Number(data.get("staleAfterMinutes")),
        }),
      });

      if (!result.ok) {
        setMessage(result.error.message);
        return;
      }

      form.reset();
      setMessage("Mapping enregistre. Rechargez la page pour verifier la liste.");
    });
  }

  return (
    <form className="backup-restore-form" onSubmit={onSubmit}>
      <label>
        <span>ID job Veeam</span>
        <input name="externalJobId" required />
      </label>
      <label>
        <span>ID client</span>
        <input name="customerId" required />
      </label>
      <label>
        <span>ID service</span>
        <input name="serviceId" required />
      </label>
      <label>
        <span>Intervalle attendu</span>
        <input
          defaultValue={1440}
          min={60}
          name="expectedIntervalMinutes"
          required
          type="number"
        />
      </label>
      <label>
        <span>Critique apres</span>
        <input
          defaultValue={2160}
          min={60}
          name="criticalAfterMinutes"
          required
          type="number"
        />
      </label>
      <label>
        <span>Collecteur silencieux apres</span>
        <input
          defaultValue={180}
          min={15}
          name="staleAfterMinutes"
          required
          type="number"
        />
      </label>
      <label>
        <span>Active</span>
        <input defaultChecked name="enabled" type="checkbox" />
      </label>
      <div className="backup-restore-actions">
        <button className="button" disabled={isPending} type="submit">
          {isPending ? "Enregistrement..." : "Associer le job"}
        </button>
        {message ? <span className="field-hint">{message}</span> : null}
      </div>
    </form>
  );
}
