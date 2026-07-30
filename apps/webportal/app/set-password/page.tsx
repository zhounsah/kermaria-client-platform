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
  searchParams: Promise<{ result?: string; token?: string }>;
};

const SET_PASSWORD_RESULTS = {
  PASSWORD_SET: {
    message:
      "Votre mot de passe a été défini. Vous pouvez maintenant vous connecter à votre espace client.",
    title: "Mot de passe défini",
    tone: "success",
  },
  TOKEN_INVALID: {
    message:
      "Ce lien est invalide ou a déjà été utilisé. Utilisez le lien reçu par e-mail ou contactez-nous.",
    title: "Lien invalide",
    tone: "error",
  },
  TOKEN_EXPIRED: {
    message:
      "Ce lien de définition de mot de passe a expiré. Contactez notre équipe pour obtenir un nouveau lien.",
    title: "Lien expiré",
    tone: "error",
  },
  INVALID_PASSWORD: {
    message:
      "Le mot de passe proposé n'a pas pu être accepté. Reprenez le lien reçu par e-mail et choisissez un mot de passe conforme.",
    title: "Mot de passe refusé",
    tone: "error",
  },
  INVALID_REQUEST: {
    message:
      "La demande n'a pas pu être traitée. Reprenez le lien reçu par e-mail et réessayez.",
    title: "Demande invalide",
    tone: "error",
  },
  RATE_LIMITED: {
    message:
      "Trop de tentatives ont été effectuées. Réessayez dans quelques minutes.",
    title: "Tentatives temporairement limitées",
    tone: "error",
  },
  SET_PASSWORD_REQUEST_TOO_LARGE: {
    message:
      "La demande est trop volumineuse. Reprenez le lien reçu par e-mail et réessayez.",
    title: "Demande trop volumineuse",
    tone: "error",
  },
  SET_PASSWORD_UNAVAILABLE: {
    message:
      "Le service est temporairement indisponible. Réessayez dans quelques instants.",
    title: "Service indisponible",
    tone: "error",
  },
} as const;

export default async function SetPasswordPage({
  searchParams,
}: SetPasswordPageProps) {
  const { result, token } = await searchParams;
  const resultCode = result?.trim() ?? "";
  const presentation = Object.hasOwn(SET_PASSWORD_RESULTS, resultCode)
    ? SET_PASSWORD_RESULTS[resultCode as keyof typeof SET_PASSWORD_RESULTS]
    : null;

  if (presentation) {
    return (
      <div className="set-password-page">
        <header className="signup-header">
          <p className="eyebrow">Activation du compte</p>
          <h1>{presentation.title}</h1>
        </header>

        <section className="set-password-invalid">
          <p>{presentation.message}</p>
          {presentation.tone === "success" ? (
            <p>
              <Link href="/login">Se connecter à votre espace client</Link>
            </p>
          ) : (
            <p>
              <Link href="/contact">Contacter notre équipe</Link>
            </p>
          )}
        </section>
      </div>
    );
  }

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
