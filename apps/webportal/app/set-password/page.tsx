import type { Metadata } from "next";
import Link from "next/link";

import { SetPasswordForm } from "@/components/SetPasswordForm";
import { resolveCorrelationId } from "@/lib/correlation";
import { validateSetPasswordToken } from "@/lib/signup-server";

export const metadata: Metadata = {
  title: "Définir votre mot de passe",
  robots: { index: false, follow: false },
};

export const dynamic = "force-dynamic";

type SetPasswordPageProps = {
  searchParams: Promise<{ token?: string }>;
};

export default async function SetPasswordPage({
  searchParams,
}: SetPasswordPageProps) {
  const { token } = await searchParams;
  const trimmedToken = token?.trim() || "";

  const validation = trimmedToken
    ? await validateSetPasswordToken(trimmedToken, resolveCorrelationId(null))
    : {
        ok: false,
        status: 400,
        code: "TOKEN_INVALID",
        message: "Lien de définition de mot de passe invalidé.",
      };

  const valid = validation.ok;
  const expiréd = validation.code === "TOKEN_EXPIRED";
  const serviceUnavailable =
    validation.code === "INTERNAL_API_UNAVAILABLE";

  return (
    <div className="set-password-page">
      <header className="signup-header">
        <p className="eyebrow">Activation du compte</p>
        <h1>{valid ? "Définir votre mot de passe" : "Définition impossible"}</h1>
        {valid ? (
          <p className="signup-lead">
            Votre compte a été validé. Choisissez un mot de passe pour activer
            votre accès à l&apos;espace client. Cette définition du mot de passe
            finalise aussi l&apos;identité cible dans clients.home.bzh lorsque
            l&apos;écriture AD est active. Une fois connecté, votre tableau de
            bord vous guidera vers les prochaines étapes, notamment la
            finalisation de votre pack si vous en aviez choisi un.
          </p>
        ) : null}
      </header>

      {valid ? (
        <SetPasswordForm token={trimmedToken} />
      ) : (
        <section className="set-password-invalid">
          {serviceUnavailable ? (
            <p>
              Le service est temporairement indisponible. Rechargez la page
              dans quelques instants, ou{" "}
              <Link href="/contact">contactez-nous</Link> si le problème
              persiste.
            </p>
          ) : expiréd ? (
            <p>
              Ce lien de définition de mot de passe a expiré. Contactez notre
              équipe pour obtenir un nouveau lien, ou{" "}
              <Link href="/contact">contactez-nous</Link> si le problème
              persiste.
            </p>
          ) : (
            <p>
              Ce lien est invalide ou a déjà été utilisé. Utilisez le lien reçu
              par e-mail, ou <Link href="/contact">contactez-nous</Link> si le
              problème persiste.
            </p>
          )}
        </section>
      )}
    </div>
  );
}
