import { redirect } from "next/navigation";

import { LoginForm } from "@/components/LoginForm";
import { getCurrentPortalSession } from "@/lib/auth";

export const metadata = {
  title: "Connexion",
};

export const dynamic = "force-dynamic";

export default async function LoginPage() {
  const session = await getCurrentPortalSession();

  if (session) {
    redirect("/dashboard");
  }

  return (
    <section className="login-layout">
      <div className="login-copy">
        <p className="eyebrow">Espace client</p>
        <h1>Connexion à votre espace</h1>
        <p className="lead">
          Utilisez les identifiants de démonstration configurés localement.
          Aucun compte Active Directory n&apos;est utilisé.
        </p>
        <ul className="check-list">
          <li>Session conservée dans un cookie HttpOnly.</li>
          <li>Données limitées au client associé au compte.</li>
          <li>Aucun paiement ni changement de mot de passe AD.</li>
        </ul>
      </div>
      <div>
        <LoginForm />
        <p className="login-help">
          La récupération automatisée du mot de passe n&apos;est pas disponible
          dans cette version.
        </p>
      </div>
    </section>
  );
}
