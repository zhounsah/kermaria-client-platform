import type { Metadata } from "next";
import Link from "next/link";

import { resolveCorrelationId } from "@/lib/correlation";
import {
  resolveSelfServiceCartSignupContinuation,
  resolveSelfServiceVpsSignupContinuation,
} from "@/lib/public-route-config";
import { callInternalSignup } from "@/lib/signup-server";

export const metadata: Metadata = {
  title: "Confirmation de l'adresse e-mail",
  robots: { index: false, follow: false },
};

export const dynamic = "force-dynamic";

type VerifyPageProps = {
  searchParams: Promise<{ token?: string; next?: string }>;
};

export default async function SignupVerifyPage({
  searchParams,
}: VerifyPageProps) {
  const { token, next } = await searchParams;
  const trimmedToken = token?.trim() || "";
  const correlationId = resolveCorrelationId(null);

  const result = trimmedToken
    ? await callInternalSignup(
        "/internal/signup/verify",
        { token: trimmedToken },
        correlationId,
      )
    : {
        ok: false,
        status: 400,
        code: "TOKEN_INVALID",
        message: "Lien de vérification invalide.",
      };

  const succeeded = result.ok;
  const expired = result.code === "TOKEN_EXPIRED";
  const cartContinuation = result.selfServiceFlow === "cart"
    ? resolveSelfServiceCartSignupContinuation(next)
    : null;
  const vpsContinuation = result.selfServiceFlow === "vps"
    ? resolveSelfServiceVpsSignupContinuation(next)
    : null;
  const continuation = cartContinuation?.continuationPath
    ?? vpsContinuation?.continuationPath
    ?? null;
  const selfService = result.selfServiceFlow === "cart" || result.selfServiceFlow === "vps";
  const title = succeeded
    ? "Adresse e-mail confirmée"
    : "Vérification impossible";

  return (
    <div className="signup-verify-page">
      <section className={succeeded ? "signup-verify-card signup-verify-success" : "signup-verify-card signup-verify-error"} aria-labelledby="signup-verify-title">
        <span aria-hidden="true" className="signup-verify-icon">{succeeded ? "✓" : "!"}</span>
        <p className="eyebrow">Inscription</p>
        <h1 id="signup-verify-title">{title}</h1>
        {succeeded ? (
          selfService ? (
            <>
              <p>
                {result.selfServiceFlow === "cart"
                  ? "Votre compte est prêt. Vous pouvez maintenant reprendre votre commande."
                  : "Votre compte est prêt. Vous pouvez maintenant reprendre la configuration de votre VPS."}
              </p>
              {continuation ? (
                <Link className="button signup-verify-action" href={continuation}>
                  {result.selfServiceFlow === "cart" ? "Continuer ma souscription" : "Reprendre mon VPS"}
                </Link>
              ) : (
                <Link className="button button-secondary signup-verify-action" href="/dashboard">Retour à mon espace client</Link>
              )}
            </>
          ) : (
            <p>
              Merci, votre adresse e-mail est confirmée. Votre demande est
              désormais en attente de validation par notre équipe. Vous
              recevrez un e-mail dès qu&apos;une décision sera prise.
            </p>
          )
        ) : (
          <>
            <p>
              {expired
                ? "Ce lien de vérification a expiré. Vous pouvez soumettre une nouvelle demande d'inscription."
                : "Ce lien est invalide ou a déjà été utilisé."}
            </p>
            <Link className="button button-secondary signup-verify-action" href="/signup">Retour au formulaire d&apos;inscription</Link>
          </>
        )}
      </section>
    </div>
  );
}
