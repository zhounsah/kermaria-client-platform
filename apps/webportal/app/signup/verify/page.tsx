import type { Metadata } from "next";
import Link from "next/link";

import { resolveCorrelationId } from "@/lib/correlation";
import { callInternalSignup } from "@/lib/signup-server";

export const metadata: Metadata = {
  title: "Confirmation de l'adresse e-mail",
  robots: { index: false, follow: false },
};

export const dynamic = "force-dynamic";

type VerifyPageProps = {
  searchParams: Promise<{ token?: string }>;
};

export default async function SignupVerifyPage({
  searchParams,
}: VerifyPageProps) {
  const { token } = await searchParams;
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

  return (
    <div className="signup-verify-page">
      <header className="signup-header">
        <p className="eyebrow">Inscription</p>
        <h1>
          {succeeded
            ? "Adresse e-mail confirmée"
            : "Vérification impossible"}
        </h1>
      </header>

      {succeeded ? (
        <section className="signup-verify-result signup-verify-success">
          <p>
            Merci, votre adresse e-mail est confirmée. Votre demande est
            désormais en attente de validation par notre équipe. Vous
            recevrez un e-mail dès qu&apos;une décision sera prise.
          </p>
        </section>
      ) : (
        <section className="signup-verify-result signup-verify-error">
          <p>
            {expired
              ? "Ce lien de vérification a expiré. Vous pouvez soumettre une nouvelle demande d'inscription."
              : "Ce lien est invalide ou a déjà été utilisé."}
          </p>
          <p>
            <Link href="/signup">Retour au formulaire d&apos;inscription</Link>
          </p>
        </section>
      )}
    </div>
  );
}
