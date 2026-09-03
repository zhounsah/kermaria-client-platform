import type { Metadata } from "next";
import Link from "next/link";

import { SetPasswordForm } from "@/components/SetPasswordForm";
import { resolveCorrelationId } from "@/lib/correlation";
import {
  validateAdditionalUserSetPasswordToken,
  validateSetPasswordToken,
} from "@/lib/signup-server";

export const metadata: Metadata = {
  title: "Définir votre mot de passe",
  robots: { index: false, follow: false },
};

export const dynamic = "force-dynamic";

type SetPasswordPageProps = {
  searchParams: Promise<{ result?: string; token?: string; flow?: string }>;
};

// Parcours reconnu pour un utilisateur supplémentaire Billing V2. Le `flow`
// ne fait que choisir le texte et l'endpoint de validation : il n'ouvre aucun
// droit, l'autorisation restant entièrement portée par le jeton et par son
// `purpose`, vérifiés côté API. Un `flow` inconnu est refusé plutôt que
// ramené au parcours d'inscription.
const ADDITIONAL_USER_FLOW = "billing-v2-additional-user";

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
  const { result, token, flow } = await searchParams;
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
              <Link href={flow?.trim() === ADDITIONAL_USER_FLOW ? "/login" : "/login?next=%2Fformules%2Freprendre"}>
                {flow?.trim() === ADDITIONAL_USER_FLOW
                  ? "Se connecter ├á votre espace client"
                  : "Se connecter et finaliser mon offre"}
              </Link>
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
  const trimmedFlow = flow?.trim() || "";
  const isAdditionalUserFlow = trimmedFlow === ADDITIONAL_USER_FLOW;
  const isKnownFlow = trimmedFlow === "" || isAdditionalUserFlow;

  const invalidLink = {
    ok: false,
    status: 400,
    code: "TOKEN_INVALID",
    message: "Lien de définition de mot de passe invalidé.",
  };

  const validation = !isKnownFlow || !trimmedToken
    ? invalidLink
    : isAdditionalUserFlow
      ? await validateAdditionalUserSetPasswordToken(
          trimmedToken,
          resolveCorrelationId(null),
        )
      : await validateSetPasswordToken(trimmedToken, resolveCorrelationId(null));

  const valid = validation.ok;
  const expired = validation.code === "TOKEN_EXPIRED";
  const serviceUnavailable =
    validation.code === "INTERNAL_API_UNAVAILABLE";

  return (
    <div className="set-password-page">
      <header className="signup-header">
        <p className="eyebrow">Activation du compte</p>
        <h1>{valid ? "Définir votre mot de passe" : "Définition impossible"}</h1>
        {valid ? (
          isAdditionalUserFlow ? (
            <p className="signup-lead">
              Votre organisation vous a ouvert un accès à son espace client.
              Choisissez un mot de passe pour activer votre compte. Vos accès
              associés sont préparés automatiquement ensuite : ils peuvent
              mettre quelques minutes à devenir disponibles.
            </p>
          ) : (
            <p className="signup-lead">
              Votre compte a été validé. Choisissez un mot de passe pour activer
              votre accès à l&apos;espace client. Une fois connecté, votre tableau
              de bord vous guidera vers les prochaines étapes, notamment la
              finalisation de votre offre si vous en aviez choisi un.
            </p>
          )
        ) : null}
      </header>

      {valid ? (
        <SetPasswordForm
          flow={isAdditionalUserFlow ? ADDITIONAL_USER_FLOW : undefined}
          token={trimmedToken}
        />
      ) : (
        <section className="set-password-invalid">
          {serviceUnavailable ? (
            <p>
              Le service est temporairement indisponible. Rechargez la page
              dans quelques instants, ou{" "}
              <Link href="/contact">contactez-nous</Link> si le problème
              persiste.
            </p>
          ) : expired ? (
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
