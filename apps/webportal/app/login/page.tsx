import { headers } from "next/headers";
import { redirect } from "next/navigation";

import { LoginForm } from "@/components/LoginForm";
import { getCurrentPortalSession } from "@/lib/auth";
import { resolvePortalAreaUrl, resolvePortalRoleUrl } from "@/lib/public-route-config";
import { getPortalPublicUrlFromHeaders } from "@/lib/public-routes";

export const metadata = {
  title: "Connexion",
};

export const dynamic = "force-dynamic";

type LoginPageSearchParams = {
  error?: string;
  email?: string;
};

type LoginPageProps = {
  searchParams: Promise<LoginPageSearchParams>;
};

export default async function LoginPage({
  searchParams,
}: LoginPageProps) {
  const baseUrl = getPortalPublicUrlFromHeaders(await headers());
  const session = await getCurrentPortalSession();

  if (session) {
    redirect(resolvePortalRoleUrl(baseUrl, session.user.role));
  }

  const canonicalLoginUrl = resolvePortalAreaUrl(baseUrl, "client", "/login");
  if (canonicalLoginUrl !== `${baseUrl}/login`) {
    redirect(canonicalLoginUrl);
  }

  const { error, email } = await searchParams;

  return (
    <section className="login-layout">
      <div className="login-copy">
        <p className="eyebrow">Espace client</p>
        <h1>Connexion à votre espace</h1>
        <p className="lead">
          Utilisez les identifiants qui vous ont été communiqués pour accéder à
          votre espace. Aucun compte Active Directory n&apos;est utilisé.
        </p>
        <ul className="check-list">
          <li>Session conservée dans un cookie HttpOnly.</li>
          <li>Données client isolées ou vues internes selon le rôle.</li>
          <li>Aucun paiement ni changement de mot de passe AD.</li>
        </ul>
      </div>
      <div>
        <LoginForm
          initialEmail={email?.trim() ?? ""}
          initialError={getInitialErrorMessage(error)}
          baseUrl={baseUrl}
        />
        <p className="login-help">
          La récupération automatisée du mot de passe n&apos;est pas disponible
          dans cette version.
        </p>
      </div>
    </section>
  );
}

function getInitialErrorMessage(errorCode?: string) {
  switch (errorCode) {
    case "INVALID_CREDENTIALS":
    case "LOGIN_FAILED":
      return "Identifiants invalides.";
    case "ACCOUNT_LOCKED":
      return "Identifiants invalides ou connexion temporairement indisponible.";
    case "INTERNAL_API_UNAVAILABLE":
    case "INTERNAL_ERROR":
      return "Le service est temporairement indisponible. Réessayez dans quelques instants.";
    default:
      return null;
  }
}
