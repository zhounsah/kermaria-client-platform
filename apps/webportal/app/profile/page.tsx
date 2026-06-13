import Link from "next/link";

import { EmptyState } from "@/components/EmptyState";
import { LogoutButton } from "@/components/LogoutButton";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { RevokeOtherSessionsButton } from "@/components/RevokeOtherSessionsButton";
import { SectionHeading } from "@/components/SectionHeading";
import { StatusBadge } from "@/components/StatusBadge";
import { requireClientSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import { getClientProfile } from "@/lib/internal-api";

export const metadata = {
  title: "Profil",
};

export const dynamic = "force-dynamic";

export default async function ProfilePage() {
  const session = await requireClientSession();
  const result = await getClientProfile();
  const profile = result.data;

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Session active" tone="success" />}
        description="Consultez les informations rattachées au client de la session active."
        eyebrow="Compte"
        title="Mon profil"
      />

      {profile ? (
        <div className="profile-layout">
          <section className="content-panel">
            <SectionHeading
              description="Informations principales du contact et de l'organisation."
              title="Coordonnées"
            />
            <dl className="profile-details">
              <div>
                <dt>Organisation</dt>
                <dd>{profile.companyName}</dd>
              </div>
              <div>
                <dt>Référence client</dt>
                <dd>{profile.customerReference}</dd>
              </div>
              <div>
                <dt>Contact principal</dt>
                <dd>{profile.contactName}</dd>
              </div>
              <div>
                <dt>Adresse e-mail</dt>
                <dd>{profile.email}</dd>
              </div>
              <div>
                <dt>Téléphone</dt>
                <dd>{profile.phone}</dd>
              </div>
              <div>
                <dt>Adresse</dt>
                <dd>
                  {profile.address}
                  <br />
                  {profile.city}, {profile.country}
                </dd>
              </div>
            </dl>
            <button className="button button-secondary" disabled type="button">
              Modification désactivée
            </button>
          </section>

          <aside className="content-panel security-panel">
            <SectionHeading
              description="Fonctions prévues pour les prochaines phases."
              title="Sécurité du compte"
            />
            <div className="security-item">
              <div>
                <strong>Authentification</strong>
                <span>Session serveur avec cookie HttpOnly</span>
              </div>
              <StatusBadge label="Active" tone="success" />
            </div>
            <div className="security-item">
              <div>
                <strong>Statut du compte</strong>
                <span>{session.user.status}</span>
              </div>
              <StatusBadge label={session.user.role} tone="info" />
            </div>
            <div className="security-item">
              <div>
                <strong>Dernière connexion</strong>
                <span>
                  {session.user.lastLoginAt
                    ? formatDateTime(session.user.lastLoginAt)
                    : "Non disponible"}
                </span>
              </div>
            </div>
            <div className="security-item">
              <div>
                <strong>Expiration de la session</strong>
                <span>{formatDateTime(session.expiresAt)}</span>
              </div>
            </div>
            <div className="security-item">
              <div>
                <strong>Authentification multifacteur</strong>
                <span>Fournisseur à choisir ultérieurement</span>
              </div>
              <StatusBadge label="À venir" tone="warning" />
            </div>
            <div className="security-item">
              <div>
                <strong>Mot de passe</strong>
                <span>Intégration Active Directory désactivée</span>
              </div>
              <Link href="/password">Voir le parcours</Link>
            </div>
            <RevokeOtherSessionsButton />
            <div className="profile-logout">
              <LogoutButton />
            </div>
          </aside>
        </div>
      ) : (
        <EmptyState
          description="Le profil mock n'est pas disponible. Aucun détail technique n'est affiché."
          title="Profil indisponible"
        />
      )}

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
