"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useRef, useState } from "react";

import { FormMessage } from "@/components/FormMessage";
import { requestBffJson } from "@/lib/client-api";
import type { SignupAdminAccountAccess } from "@/lib/internal-api";

type AdminSignupActionsProps = {
  signupId: string;
  status: string;
  accountAccess: SignupAdminAccountAccess | null;
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

const MIN_PASSWORD_LENGTH = 12;

export function AdminSignupActions({
  signupId,
  status,
  accountAccess,
}: AdminSignupActionsProps) {
  const router = useRouter();
  const isWorkingRef = useRef(false);
  const [reason, setReason] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [state, setState] = useState<ActionState>({ status: "idle" });

  const canApprove = status === "email_verified";
  const canReject = status === "email_pending" || status === "email_verified";
  const canRecoverPassword =
    status === "approved"
    && accountAccess !== null
    && !accountAccess.passwordDefined;

  async function run(
    path: `/api/${string}`,
    body: Record<string, unknown> | undefined,
    confirmMessage: string,
    onSuccess?: () => void,
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

      onSuccess?.();
      setState({ status: "success", message: response.data.message });
      router.refresh();
    } finally {
      isWorkingRef.current = false;
    }
  }

  async function handleInitializePassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (password.length < MIN_PASSWORD_LENGTH) {
      setState({
        status: "error",
        message:
          `Le mot de passe doit comporter au moins ${MIN_PASSWORD_LENGTH} caractères.`,
      });
      return;
    }

    if (password !== confirmPassword) {
      setState({
        status: "error",
        message: "Les deux mots de passe ne correspondent pas.",
      });
      return;
    }

    await run(
      `/api/admin/signups/${encodeURIComponent(signupId)}/initialize-password`,
      { password },
      "Initialiser ce mot de passe maintenant ? Le client pourra se connecter avec son e-mail et ce mot de passe. Si l'écriture AD est active, cela finalisera aussi son identité clients.home.bzh.",
      () => {
        setPassword("");
        setConfirmPassword("");
      },
    );
  }

  const isWorking = state.status === "working";

  if (!canApprove && !canReject && !canRecoverPassword) {
    return (
      <div className="signup-actions">
        {status === "approved" && accountAccess?.passwordDefined ? (
          <p className="signup-actions-done">
            Le mot de passe a déjà été défini sur ce compte. Aucune action
            supplementaire n&apos;est necessaire ici.
          </p>
        ) : status === "approved" ? (
          <p className="signup-actions-done">
            Ce compte approuve n&apos;expose pas d&apos;action supplementaire
            depuis cette fiche.
          </p>
        ) : (
          <p className="signup-actions-done">
            Cette demande est cloturee : aucune action supplementaire
            n&apos;est possible.
          </p>
        )}
      </div>
    );
  }

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
              "Approuver cette demande ? Un compte client sera créé et un lien de définition de mot de passe sera envoyé. L'identité clients.home.bzh sera finalisée lors de cette définition du mot de passe.",
            )
          }
          type="button"
        >
          Approuver et creer le compte
        </button>
      ) : null}

      {!canApprove && canReject ? (
        <p className="signup-actions-hint">
          L&apos;approbation sera possible une fois l&apos;adresse e-mail
          confirmée par le demandeur.
        </p>
      ) : null}

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
                "Refuser cette demande ? Le demandeur en sera informe par e-mail.",
              )
            }
            type="button"
          >
            Refuser la demande
          </button>
        </div>
      ) : null}

      {canRecoverPassword ? (
        <div className="signup-password-recovery">
          <div className="section-heading">
            <div>
              <h3>Accès et identité en attente</h3>
              <p>
                Le compte client a bien été créé, mais le mot de passe initial
                n&apos;a pas encore été défini.
              </p>
            </div>
            {accountAccess?.customerReference ? (
              <p className="field-hint">
                Client : <code>{accountAccess.customerReference}</code>
              </p>
            ) : null}
          </div>

          <form
            className="form-card compact-form-card signup-password-form"
            noValidate
            onSubmit={handleInitializePassword}
          >
            <label>
              Initialisation par mes soins
              <input
                autoComplete="new-password"
                disabled={isWorking}
                minLength={MIN_PASSWORD_LENGTH}
                onChange={(event) => setPassword(event.target.value)}
                required
                type="password"
                value={password}
              />
            </label>
            <label>
              Confirmez le mot de passe
              <input
                autoComplete="new-password"
                disabled={isWorking}
                minLength={MIN_PASSWORD_LENGTH}
                onChange={(event) => setConfirmPassword(event.target.value)}
                required
                type="password"
                value={confirmPassword}
              />
            </label>
            <p className="field-hint">
              Le mot de passe n&apos;est jamais stocke en clair. Cette action
              invalide aussi l&apos;ancien lien de définition s&apos;il existait
              encore et declenche la synchronisation AD quand elle est active.
            </p>
            <button className="button" disabled={isWorking} type="submit">
              Initialiser le mot de passe
            </button>
          </form>

          <div className="signup-password-email">
            <p className="field-hint">
              Si vous preferez laisser le client choisir lui-meme son mot de
              passe, vous pouvez renvoyer un nouveau lien à usage unique. La
              création ou la synchronisation vers clients.home.bzh se fera au
              moment ou il définira ce mot de passe.
            </p>
            <button
              className="button button-secondary"
              disabled={isWorking}
              onClick={() =>
                run(
                  `/api/admin/signups/${encodeURIComponent(signupId)}/resend-password-email`,
                  undefined,
                  "Envoyer un nouveau lien de définition du mot de passe a ce client ?",
                )
              }
              type="button"
            >
              Renvoyer le mail de réinitialisation
            </button>
          </div>
        </div>
      ) : null}
    </div>
  );
}
