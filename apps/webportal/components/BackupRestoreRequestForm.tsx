"use client";

import { FormEvent, useState, useTransition } from "react";

type BackupRestoreRequestFormProps = {
  backupJobId: string;
};

export function BackupRestoreRequestForm({
  backupJobId,
}: BackupRestoreRequestFormProps) {
  const [message, setMessage] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    const itemPath = String(data.get("itemPath") ?? "").trim();
    const description = String(data.get("description") ?? "").trim();
    const desiredRestoreAt = String(data.get("desiredRestoreAt") ?? "").trim();
    const priority = String(data.get("priority") ?? "normal");

    startTransition(async () => {
      setMessage(null);
      const response = await fetch(
        `/api/backups/${encodeURIComponent(backupJobId)}/restore-requests`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            itemPath,
            description,
            desiredRestoreAt: desiredRestoreAt || undefined,
            priority,
          }),
        },
      );

      if (!response.ok) {
        setMessage("La demande n'a pas pu etre enregistree.");
        return;
      }

      const payload = await response.json() as { reference?: string };
      form.reset();
      setMessage(
        payload.reference
          ? `Demande ${payload.reference} enregistree.`
          : "Demande de restauration enregistree.",
      );
    });
  }

  return (
    <form className="backup-restore-form" onSubmit={onSubmit}>
      <label>
        <span>Element ou dossier</span>
        <input
          maxLength={300}
          name="itemPath"
          placeholder="Exemple : dossier Comptabilite"
        />
      </label>
      <label>
        <span>Date souhaitee</span>
        <input name="desiredRestoreAt" type="datetime-local" />
      </label>
      <label>
        <span>Priorite</span>
        <select defaultValue="normal" name="priority">
          <option value="low">Basse</option>
          <option value="normal">Normale</option>
          <option value="high">Haute</option>
        </select>
      </label>
      <label className="backup-restore-form-wide">
        <span>Description</span>
        <textarea
          maxLength={2000}
          name="description"
          placeholder="Precisez les fichiers, le contexte et toute contrainte utile."
          rows={4}
        />
      </label>
      <div className="backup-restore-actions">
        <button className="button" disabled={isPending} type="submit">
          {isPending ? "Envoi..." : "Demander une restauration"}
        </button>
        {message ? <span className="field-hint">{message}</span> : null}
      </div>
    </form>
  );
}
