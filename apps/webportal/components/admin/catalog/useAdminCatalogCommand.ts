"use client";

import { useCallback, useState } from "react";
import { useRouter } from "next/navigation";

import { requestBffJson } from "@/lib/client-api";

type CommandResult = { code: string; message: string; id?: string | null };
type Feedback = { tone: "success" | "error"; message: string } | null;

export function useAdminCatalogCommand() {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>(null);

  const send = useCallback(async (command: Record<string, unknown>) => {
    if (busy) {
      return null;
    }
    setBusy(true);
    setFeedback(null);
    const result = await requestBffJson<CommandResult>(
      "/api/admin/billing-v2/catalog",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(command),
      },
    );
    setBusy(false);

    if (!result.ok) {
      setFeedback({ tone: "error", message: result.error.message });
      return null;
    }

    setFeedback({ tone: "success", message: result.data.message });
    router.refresh();
    return result.data;
  }, [busy, router]);

  return { busy, feedback, setFeedback, send };
}
