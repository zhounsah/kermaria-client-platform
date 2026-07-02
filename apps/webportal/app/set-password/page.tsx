import type { Metadata } from "next";
import Link from "next/link";

import { SetPasswordForm } from "@/components/SetPasswordForm";

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

  return (
    <div className="set-password-page">
      <header className="signup-header">
        <p className="eyebrow">Activation du compte</p>
        <h1>Définir votre mot de passe</h1>
        <p className="signup-lead">
          Votre compte a été validé. Choisissez un mot de passe pour
          activer votre accès à l&apos;espace client.
        </p>
      </header>

      {trimmedToken ? (
        <SetPasswordForm token={trimmedToken} />
      ) : (
        <section className="set-password-invalid">
          <p>
            Ce lien est incomplet ou invalide. Utilisez le lien reçu par
            e-mail, ou{" "}
            <Link href="/contact">contactez-nous</Link> si le problème
            persiste.
          </p>
        </section>
      )}
    </div>
  );
}
