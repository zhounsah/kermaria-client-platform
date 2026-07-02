"use client";

import { useRouter } from "next/navigation";
import { useRef, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { requestBffJson } from "@/lib/client-api";

type AdminSignupActionsProps = {
  signupId: string;
  status: string;
};

type ActionState =
  | { status: "idle" | "working" }
  | { status: "success"; message: string }
  | { status: "error"; message: string };

type ActionResponse = {
  code: string;
  message: string;
  correlation_id?: string;
};

export function AdminSignupActions({
  signupId,
  status,
}: AdminSignupActionsProps) {
  const router = useRouter();
  const isWorkingRef = useRef(false);
  const [reason, setReason] = useState("");
  const [state, setState] = useState<ActionState>({ status: "idle" });

  const canApprove = status === "email_verified";
  const canReject = status === "email_pending" || status === "email_verified";

  async function run(
    path: `/api/${string}`,
    body: Record<string, unknown> | undefined,
    confirmMessage: string,
  ) {
    if (isWorkingRef.current) {
      return;
    }
    if (!window.confirm(confirmMessage)) {
      return;
    }

    isWorkingRef.current = true;
    setState({ status: "working" });

    try {
      const response = await requestBffJson<ActionResponse>(path, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: body ? JSON.stringify(body) : undefined,
      });

      if (!response.ok) {
        setState({ status: "error", message: response.error.message });
        return;
      }

      setState({ status: "success", message: response.data.message });
      router.refresh();
    } finally {
      isWorkingRef.current = false;
    }
  }

  if (!canApprove && !canReject) {
    return (
      <p className="signup-actions-done">
        Cette demande est clôturée : aucune action supplémentaire n&apos;est
        possible.
      </p>
    );
  }

  const isWorking = state.status === "working";

  return (
    <div className="signup-actions">
      {state.status === "success" ? (
        <FormMessage title="Action effectuée" tone="success">
          <p>{state.message}</p>
        </FormMessage>
      ) : null}
      {state.status === "error" ? (
        <FormMessage title="Action impossible" tone="error">
          <p>{state.message}</p>
        </FormMessage>
      ) : null}

      {canApprove ? (
        <button
          className="button"
          disabled={isWorking}
          onClick={() =>
            run(
              `/api/admin/signups/${encodeURIComponent(signupId)}/approve`,
              undefined,
              "Approuver cette demande ? Un compte client sera créé et un lien de définition de mot de passe sera envoyé.",
            )
          }
          type="button"
        >
          Approuver et créer le compte
        </button>
      ) : (
        <p className="signup-actions-hint">
          L&apos;approbation sera possible une fois l&apos;adresse e-mail
          confirmée par le demandeur.
        </p>
      )}

      {canReject ? (
        <div className="signup-reject">
          <label>
            Motif du refus (facultatif, transmis par e-mail)
            <textarea
              maxLength={500}
              onChange={(event) => setReason(event.target.value)}
              rows={3}
              value={reason}
            />
          </label>
          <button
            className="button button-danger"
            disabled={isWorking}
            onClick={() =>
              run(
                `/api/admin/signups/${encodeURIComponent(signupId)}/reject`,
                { reason: reason.trim() || null },
                "Refuser cette demande ? Le demandeur en sera informé par e-mail.",
              )
            }
            type="button"
          >
            Refuser la demande
          </button>
        </div>
      ) : null}
    </div>
  );
}
