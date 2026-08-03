import Link from "next/link";

import { AdminDataTable } from "@/components/AdminDataTable";
import { DemoAccountConvertButton } from "@/components/DemoAccountConvertButton";
import { DemoAccountDeleteButton } from "@/components/DemoAccountDeleteButton";
import { DemoAccountCreateForm } from "@/components/DemoAccountCreateForm";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import {
  getAdminDemoAccounts,
  getAdminDemoContentTemplates,
  getAdminDemoProfiles,
} from "@/lib/internal-api";

export const metadata = {
  title: "Comptes démo - Administration",
};

export const dynamic = "force-dynamic";

function kindBadge(kind: string) {
  if (kind === "trial") {
    return <StatusBadge label="Essai réel" tone="warning" />;
  }

  return <StatusBadge label="Vitrine" tone="info" />;
}

function lifecycleBadge(expiresAt: string | null, revokedAt: string | null) {
  if (revokedAt) {
    return <StatusBadge label="Révoqué" tone="neutral" />;
  }

  if (expiresAt && new Date(expiresAt).getTime() < Date.now()) {
    return <StatusBadge label="Expiré" tone="warning" />;
  }

  return <StatusBadge label="Actif" tone="success" />;
}

export default async function AdminDemoAccountsPage() {
  await requireAdminSession();
  const [profilesResult, templatesResult, accountsResult] = await Promise.all([
    getAdminDemoProfiles(),
    getAdminDemoContentTemplates(),
    getAdminDemoAccounts(),
  ]);
  const accounts = accountsResult.data;

  return (
    <>
      <PageHeader
        description="Générez des comptes de démonstration ou d'essai personnalisés à partir d'un profil, avec une durée de vie et un contenu adaptés au prospect."
        eyebrow="Administration interne"
        title="Comptes de démonstration"
      />

      <MockNotice
        correlationId={accountsResult.correlationId}
        source={accountsResult.source}
      />

      <section>
        <h2>Créer un compte de démonstration</h2>
        <p>
          <Link className="text-link" href="/admin/demo/profiles">
            Gérer les profils de démonstration
          </Link>
        </p>
        <DemoAccountCreateForm
          profiles={profilesResult.data}
          templates={templatesResult.data}
        />
      </section>

      <section>
        <h2>Comptes de démonstration existants</h2>
        {accountsResult.error ? (
          <ErrorState
            description="Impossible de charger les comptes de démonstration pour le moment."
            reference={accountsResult.correlationId}
            title="Comptes indisponibles"
          />
        ) : accounts.length === 0 ? (
          <EmptyState
            description="Aucun compte de démonstration n'a encore été créé."
            title="Aucun compte de démonstration"
          />
        ) : (
          <AdminDataTable
            caption="Comptes de démonstration"
            columns={[
              "Référence",
              "Nom",
              "Type",
              "Profil",
              "Services",
              "Créé le",
              "Expire le",
              "Statut",
              "Action",
            ]}
            rows={accounts.map((account) => [
              <code key={`${account.customerReference}-ref`}>
                {account.customerReference}
              </code>,
              <strong key={`${account.customerReference}-name`}>
                {account.displayName}
              </strong>,
              <span key={`${account.customerReference}-kind`}>
                {kindBadge(account.kind)}
              </span>,
              account.profileKey ?? "—",
              account.serviceCount,
              formatDateTime(account.createdAt),
              account.expiresAt ? formatDateTime(account.expiresAt) : "—",
              <span key={`${account.customerReference}-lifecycle`}>
                {lifecycleBadge(account.expiresAt, account.revokedAt)}
              </span>,
              <span
                className="table-actions"
                key={`${account.customerReference}-actions`}
              >
                {/* Seul un essai se convertit : une vitrine n'a aucun accès réel
                    à basculer (l'API refuserait de toute façon). */}
                {account.kind === "trial" ? (
                  <DemoAccountConvertButton
                    customerReference={account.customerReference}
                    displayName={account.displayName}
                  />
                ) : null}
                <DemoAccountDeleteButton
                  customerReference={account.customerReference}
                  displayName={account.displayName}
                  kind={account.kind}
                />
              </span>,
            ])}
          />
        )}
      </section>
    </>
  );
}
