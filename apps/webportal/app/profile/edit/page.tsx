import Link from "next/link";

import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { ProfileEditForm } from "@/components/ProfileEditForm";
import { SectionHeading } from "@/components/SectionHeading";
import { requireClientSession } from "@/lib/auth";
import { getClientProfile } from "@/lib/internal-api";

export const metadata = {
  title: "Modifier mon profil",
};

export const dynamic = "force-dynamic";

function displayValue(value: string | null | undefined) {
  return value?.trim() || "Non renseigné";
}

export default async function ProfileEditPage() {
  await requireClientSession();
  const result = await getClientProfile();
  const profile = result.data;

  return (
    <>
      <PageHeader
        description="Corrigez les coordonnées du contact principal rattaché à votre dossier."
        eyebrow="Compte"
        title="Modifier mon profil"
      />

      {result.error ? (
        <ErrorState
          description="Impossible de charger les informations du profil pour le moment."
          reference={result.correlationId}
          title="Profil indisponible"
        />
      ) : profile ? (
        <div className="password-layout">
          <section className="content-panel">
            <SectionHeading
              description="Ces coordonnées sont utilisées pour vous joindre au sujet de vos services."
              title="Mes coordonnées"
            />
            <ProfileEditForm profile={profile} />
            <div className="form-footer">
              <Link className="text-link" href="/profile">
                Retour au profil
              </Link>
            </div>
          </section>

          <aside className="content-panel">
            <h2>Informations non modifiables</h2>
            <p className="page-description">
              L&apos;organisation, la référence client, l&apos;adresse e-mail de
              connexion et le statut du dossier sont gérés par nos services.
            </p>
            <div className="security-item">
              <div>
                <strong>Organisation</strong>
                <span>{displayValue(profile.companyName)}</span>
              </div>
            </div>
            <div className="security-item">
              <div>
                <strong>Référence client</strong>
                <span>{displayValue(profile.customerReference)}</span>
              </div>
            </div>
            <div className="security-item">
              <div>
                <strong>Adresse e-mail</strong>
                <span>{displayValue(profile.email)}</span>
              </div>
            </div>
            <p className="field-hint">
              Pour toute correction sur ces éléments,{" "}
              <Link className="text-link" href="/support">
                ouvrez une demande de support
              </Link>
              .
            </p>
          </aside>
        </div>
      ) : (
        <EmptyState
          description="Le profil n'est pas disponible. Aucun détail technique n'est affiché."
          title="Profil indisponible"
        />
      )}

      {result.source !== "unavailable" ? (
        <MockNotice
          correlationId={result.correlationId}
          source={result.source}
        />
      ) : null}
    </>
  );
}
