import Link from "next/link";

import { AdminDataTable } from "@/components/AdminDataTable";
import { DemoProfileDeleteButton } from "@/components/DemoProfileDeleteButton";
import { DemoProfileForm } from "@/components/DemoProfileForm";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import {
  getAdminDemoContentTemplates,
  getAdminDemoProfiles,
} from "@/lib/internal-api";

export const metadata = {
  title: "Profils démo - Administration",
};

export const dynamic = "force-dynamic";

function capabilitiesSummary(
  kind: string,
  capabilities: {
    adProvisioningMode: string;
    adGroups: string[];
    storageQuotaGo: number | null;
    rdsSessionMode: string;
  },
) {
  if (kind !== "trial") {
    return "Inerte";
  }

  const parts: string[] = [];
  if (capabilities.adProvisioningMode !== "off") {
    parts.push(`AD ${capabilities.adProvisioningMode}`);
  }
  if (capabilities.adGroups.length > 0) {
    parts.push(capabilities.adGroups.join(", "));
  }
  if (capabilities.storageQuotaGo != null) {
    parts.push(`${capabilities.storageQuotaGo} Go`);
  }
  if (capabilities.rdsSessionMode !== "off") {
    parts.push(`RDS ${capabilities.rdsSessionMode}`);
  }

  return parts.length > 0 ? parts.join(" · ") : "Réel";
}

export default async function AdminDemoProfilesPage() {
  await requireAdminSession();
  const [profilesResult, templatesResult] = await Promise.all([
    getAdminDemoProfiles(),
    getAdminDemoContentTemplates(),
  ]);
  const profiles = profilesResult.data;

  return (
    <>
      <PageHeader
        description="Registre administrable des profils de démonstration : contenu, capacités et durée de vie appliqués à chaque compte généré."
        eyebrow="Administration interne"
        title="Profils de démonstration"
      />

      <p>
        <Link className="text-link" href="/admin/demo">
          ← Retour aux comptes de démonstration
        </Link>
      </p>

      <MockNotice
        correlationId={profilesResult.correlationId}
        source={profilesResult.source}
      />

      <section>
        <h2>Créer ou mettre à jour un profil</h2>
        <DemoProfileForm templates={templatesResult.data} />
      </section>

      <section>
        <h2>Profils existants</h2>
        {profilesResult.error ? (
          <ErrorState
            description="Impossible de charger les profils de démonstration."
            reference={profilesResult.correlationId}
            title="Profils indisponibles"
          />
        ) : profiles.length === 0 ? (
          <EmptyState
            description="Aucun profil de démonstration n'est encore défini."
            title="Aucun profil"
          />
        ) : (
          <AdminDataTable
            caption="Profils de démonstration"
            columns={[
              "Clé",
              "Libellé",
              "Type",
              "Template",
              "Durée",
              "Capacités",
              "Statut",
              "Action",
            ]}
            rows={profiles.map((profile) => [
              <code key={`${profile.key}-key`}>{profile.key}</code>,
              profile.label,
              profile.kind === "trial" ? (
                <StatusBadge
                  key={`${profile.key}-kind`}
                  label="Essai réel"
                  tone="warning"
                />
              ) : (
                <StatusBadge
                  key={`${profile.key}-kind`}
                  label="Vitrine"
                  tone="info"
                />
              ),
              profile.contentTemplateKey ?? "—",
              `${profile.lifetimeDays} j`,
              capabilitiesSummary(profile.kind, profile.capabilities),
              <StatusBadge
                key={`${profile.key}-status`}
                label={profile.status === "active" ? "Actif" : "Inactif"}
                tone={profile.status === "active" ? "success" : "neutral"}
              />,
              <DemoProfileDeleteButton
                key={`${profile.key}-delete`}
                profileKey={profile.key}
              />,
            ])}
          />
        )}
      </section>
    </>
  );
}
