import { headers } from "next/headers";
import { notFound, redirect } from "next/navigation";

import { LoginForm } from "@/components/LoginForm";
import { getCurrentPortalSession } from "@/lib/auth";
import {
  getPortalArea,
  isPortalRoleAllowed,
  resolveClientCheckoutContinuationPath,
  resolvePortalAreaUrl,
  resolvePortalRoleUrl,
} from "@/lib/public-route-config";
import { getPortalRequestOriginFromHeaders } from "@/lib/public-routes";

export const metadata = {
  title: "Connexion",
};

export const dynamic = "force-dynamic";

type LoginPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

const LOGIN_ERROR_MESSAGES = {
  INVALID_CREDENTIALS: "Identifiants invalides.",
  LOGIN_REQUEST_TOO_LARGE: "La demande de connexion est trop volumineuse.",
  LOGIN_UNAVAILABLE: "La connexion est momentanément indisponible.",
  PORTAL_ROLE_MISMATCH: "Ce compte doit être connecté depuis l’autre portail.",
} as const;

export default async function LoginPage({ searchParams }: LoginPageProps) {
  const origin = getPortalRequestOriginFromHeaders(await headers());
  const area = getPortalArea(origin);
  const query = await searchParams;
  const continuationPath = resolveClientCheckoutContinuationPath(query.next);

  if (!origin || !area) {
    notFound();
  }

  if (area === "public") {
    const loginPath = continuationPath
      ? `/login?next=${encodeURIComponent(continuationPath)}`
      : "/login";
    const clientLoginUrl = resolvePortalAreaUrl(origin, "client", loginPath);
    if (!clientLoginUrl) {
      notFound();
    }
    redirect(clientLoginUrl);
  }

  const canonicalLoginUrl = resolvePortalAreaUrl(origin, area, "/login");
  if (!canonicalLoginUrl) {
    notFound();
  }
  if (new URL(canonicalLoginUrl).origin !== origin) {
    redirect(canonicalLoginUrl);
  }

  const session = await getCurrentPortalSession();

  if (
    session
    && (area === "local" || isPortalRoleAllowed(area, session.user.role))
  ) {
    const landingUrl =
      session.user.role === "client_user" && continuationPath
        ? resolvePortalAreaUrl(origin, "client", continuationPath)
        : resolvePortalRoleUrl(origin, session.user.role);
    if (!landingUrl) {
      notFound();
    }
    redirect(landingUrl);
  }

  const errorCode = typeof query.error === "string" ? query.error : "";
  const initialError = Object.hasOwn(LOGIN_ERROR_MESSAGES, errorCode)
    ? LOGIN_ERROR_MESSAGES[errorCode as keyof typeof LOGIN_ERROR_MESSAGES]
    : null;

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
          continuationPath={continuationPath}
          initialError={initialError}
          origin={origin}
          portalArea={area}
        />
        <p className="login-help">
          La récupération automatisée du mot de passe n&apos;est pas disponible
          dans cette version.
        </p>
      </div>
    </section>
  );
}
