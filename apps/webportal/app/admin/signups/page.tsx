import Link from "next/link";

import { AdminDataTable } from "@/components/AdminDataTable";
import { EmptyState } from "@/components/EmptyState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import { formatDateTime } from "@/lib/formatters";
import { getAdminSignups } from "@/lib/internal-api";
import {
  localizeSignupStatus,
  signupStatusTone,
} from "@/lib/signup-status";

export const metadata = { title: "Demandes d'inscription - Administration" };
export const dynamic = "force-dynamic";

const FILTERS: { value: string | null; label: string }[] = [
  { value: null, label: "Toutes" },
  { value: "email_pending", label: "En attente e-mail" },
  { value: "email_verified", label: "Vérifiées" },
  { value: "approved", label: "Approuvées" },
  { value: "rejected", label: "Refusées" },
];

type PageProps = {
  searchParams: Promise<{ status?: string }>;
};

export default async function AdminSignupsPage({ searchParams }: PageProps) {
  await requireAdminSession();
  const { status } = await searchParams;
  const activeStatus =
    status && FILTERS.some((filter) => filter.value === status)
      ? status
      : null;
  const result = await getAdminSignups(activeStatus ?? undefined);

  return (
    <>
      <PageHeader
        description="Demandes de création de compte self-service à examiner."
        eyebrow="Relation client"
        title="Demandes d'inscription"
      />

      <nav aria-label="Filtrer par statut" className="signup-filter-bar">
        {FILTERS.map((filter) => {
          const isActive = (filter.value ?? null) === activeStatus;
          const href = filter.value
            ? `/admin/signups?status=${filter.value}`
            : "/admin/signups";
          return (
            <Link
              aria-current={isActive ? "page" : undefined}
              className={
                isActive
                  ? "signup-filter-link signup-filter-link-active"
                  : "signup-filter-link"
              }
              href={href}
              key={filter.label}
            >
              {filter.label}
            </Link>
          );
        })}
      </nav>

      {result.data.length > 0 ? (
        <AdminDataTable
          caption="Demandes d'inscription"
          columns={[
            "Statut",
            "Société",
            "Contact",
            "E-mail",
            "Reçue le",
            "Détail",
          ]}
          rows={result.data.map((signup) => [
            <StatusBadge
              key={`${signup.id}-status`}
              label={localizeSignupStatus(signup.status)}
              tone={signupStatusTone(signup.status)}
            />,
            <Link
              className="table-action"
              href={`/admin/signups/${encodeURIComponent(signup.id)}`}
              key={`${signup.id}-company`}
            >
              {signup.companyName}
            </Link>,
            signup.contactName,
            signup.email,
            formatDateTime(signup.createdAt),
            <Link
              className="table-action"
              href={`/admin/signups/${encodeURIComponent(signup.id)}`}
              key={`${signup.id}-detail`}
            >
              Consulter
            </Link>,
          ])}
        />
      ) : (
        <EmptyState
          description="Aucune demande d'inscription ne correspond à ce filtre."
          title="Aucune demande"
        />
      )}

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
