import Link from "next/link";

import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { LogoutButton } from "@/components/LogoutButton";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { RevokeOtherSessionsButton } from "@/components/RevokeOtherSessionsButton";
import { SectionHeading } from "@/components/SectionHeading";
import { SitePageFrame } from "@/components/SitePageFrame";
import { StatusBadge } from "@/components/StatusBadge";
import { requireClientSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import { getClientProfile, getClientProfilePageLayout } from "@/lib/internal-api";
import { isPasswordChangeEnabled } from "@/lib/runtime-config";

export const metadata = { title: "Profil" };
export const dynamic = "force-dynamic";

function displayValue(value: string | null | undefined) {
  return value?.trim() || "Non renseigné";
}

export default async function ProfilePage() {
  const session = await requireClientSession();
  const [result, layout] = await Promise.all([
    getClientProfile(), getClientProfilePageLayout(),
  ]);
  const profile = result.data;
  const passwordChangeEnabled = isPasswordChangeEnabled();

  const intro = <PageHeader
    action={<StatusBadge label="Session active" tone="success" />}
    description="Retrouvez et mettez à jour les informations de votre compte."
    eyebrow="Compte"
    title="Mon profil"
  />;
  const contact = result.error ? <ErrorState
    description="Impossible de charger les informations du profil pour le moment."
    reference={result.correlationId}
    title="Profil indisponible"
  /> : profile ? <section className="content-panel">
    <SectionHeading
      action={<Link href="/profile/edit">Modifier mon profil</Link>}
      description="Vos coordonnées et celles de votre organisation."
      title="Coordonnées"
    />
    <dl className="profile-details">
      <div><dt>Organisation</dt><dd>{displayValue(profile.companyName)}</dd></div>
      <div><dt>Référence client</dt><dd>{displayValue(profile.customerReference)}</dd></div>
      <div><dt>Contact principal</dt><dd>{displayValue(profile.contactName)}</dd></div>
      <div><dt>Adresse e-mail</dt><dd>{displayValue(profile.email)}</dd></div>
      <div><dt>Téléphone</dt><dd>{displayValue(profile.phone)}</dd></div>
      <div><dt>Adresse</dt><dd>{displayValue(profile.address)}
        {profile.city || profile.country ? <><br />{[profile.city, profile.country].filter(Boolean).join(", ")}</> : null}
      </dd></div>
      <div><dt>Statut client</dt><dd><StatusBadge
        label={profile.accountStatus === "active" ? "Actif" : "En attente"}
        tone={profile.accountStatus === "active" ? "success" : "warning"}
      /></dd></div>
    </dl>
  </section> : <EmptyState
    description="Vos informations ne sont pas disponibles pour le moment."
    title="Profil indisponible"
  />;
  const security = profile ? <aside className="content-panel">
    <SectionHeading description="Gérez votre connexion et l'accès à votre compte."
      title="Sécurité du compte" />
    <div className="security-item"><div>
      <strong>Connexion</strong><span>Votre session est protégée.</span>
    </div><StatusBadge label="Active" tone="success" /></div>
    <div className="security-item"><div>
      <strong>Statut du compte</strong>
      <span>{session.user.status === "active" ? "Compte actif" : "Compte non actif"}</span>
    </div><StatusBadge label="Client" tone="info" /></div>
    <div className="security-item"><div>
      <strong>Dernière connexion</strong>
      <span>{session.user.lastLoginAt ? formatDateTime(session.user.lastLoginAt) : "Non disponible"}</span>
    </div></div>
    <div className="security-item"><div>
      <strong>Fin de la session</strong><span>{formatDateTime(session.expiresAt)}</span>
    </div></div>
    <div className="security-item"><div>
      <strong>Vérification supplémentaire</strong><span>Pas encore disponible</span>
    </div><StatusBadge label="À venir" tone="warning" /></div>
    <div className="security-item"><div>
      <strong>Mot de passe</strong>
      <span>{passwordChangeEnabled ? "Modifiable depuis votre espace" : "Changement indisponible pour le moment"}</span>
    </div><Link href="/password">{passwordChangeEnabled ? "Changer mon mot de passe" : "Voir le parcours"}</Link></div>
    <RevokeOtherSessionsButton />
    <div className="profile-logout"><LogoutButton /></div>
  </aside> : null;
  const source = result.source !== "unavailable" ? <MockNotice
    correlationId={result.correlationId} source={result.source}
  /> : null;

  return <SitePageFrame area="client" pageKey="/profile" initialLayout={layout.data}
    className="profile-layout" slots={{
      profile_intro: intro, profile_contact: contact,
      profile_security: security, profile_source: source,
    }}>
    <>{intro}<div className="profile-layout">{contact}{security}</div>{source}</>
  </SitePageFrame>;
}
