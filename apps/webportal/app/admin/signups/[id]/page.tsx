import Link from "next/link";
import { notFound } from "next/navigation";

import { AdminSignupActions } from "@/components/AdminSignupActions";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { SectionHeading } from "@/components/SectionHeading";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import { formatCurrencyFromCents, formatDateTime } from "@/lib/formatters";
import { getAdminSignup } from "@/lib/internal-api";
import {
  localizeSignupStatus,
  signupStatusTone,
} from "@/lib/signup-status";

export const metadata = { title: "Demande d'inscription - Administration" };
export const dynamic = "force-dynamic";

type PageProps = { params: Promise<{ id: string }> };

export default async function AdminSignupDetailPage({ params }: PageProps) {
  await requireAdminSession();
  const { id } = await params;
  const result = await getAdminSignup(id);

  if (result.error) {
    return (
      <ErrorState
        action={
          <Link className="button" href="/admin/signups">
            Retour
          </Link>
        }
        description="Impossible de charger cette demande d'inscription."
        reference={result.correlationId}
        title="Demande indisponible"
      />
    );
  }

  if (!result.data) {
    notFound();
  }

  const signup = result.data;

  return (
    <>
      <PageHeader
        action={
          <StatusBadge
            label={localizeSignupStatus(signup.status)}
            tone={signupStatusTone(signup.status)}
          />
        }
        description="Détail de la demande et décision de validation."
        eyebrow="Relation client"
        title={signup.companyName}
      />

      <Link className="back-link" href="/admin/signups">
        Retour aux demandes
      </Link>

      <SectionCard ariaLabel="Informations de la demande">
        <SectionHeading title="Informations transmises" />
        <dl className="detail-grid">
          <div>
            <dt>Société</dt>
            <dd>{signup.companyName}</dd>
          </div>
          <div>
            <dt>Contact</dt>
            <dd>{signup.contactName}</dd>
          </div>
          <div>
            <dt>E-mail</dt>
            <dd>{signup.email}</dd>
          </div>
          <div>
            <dt>Téléphone</dt>
            <dd>{signup.phone ?? "—"}</dd>
          </div>
          <div>
            <dt>Adresse IP source</dt>
            <dd>
              <code>{signup.sourceAddress ?? "—"}</code>
            </dd>
          </div>
          <div>
            <dt>Reçue le</dt>
            <dd>{formatDateTime(signup.createdAt)}</dd>
          </div>
          {signup.approvedAt ? (
            <div>
              <dt>Approuvée le</dt>
              <dd>{formatDateTime(signup.approvedAt)}</dd>
            </div>
          ) : null}
          {signup.rejectedAt ? (
            <div>
              <dt>Refusée le</dt>
              <dd>{formatDateTime(signup.rejectedAt)}</dd>
            </div>
          ) : null}
        </dl>

        {signup.message ? (
          <div className="signup-message-block">
            <h3>Message du demandeur</h3>
            <p>{signup.message}</p>
          </div>
        ) : null}

        {signup.packSelection ? (
          <div className="signup-message-block">
            <h3>Pack choisi</h3>
            <dl className="profile-details">
              <div>
                <dt>Pack</dt>
                <dd>{signup.packSelection.packLabel}</dd>
              </div>
              <div>
                <dt>Référence</dt>
                <dd>{signup.packSelection.offerExternalReference}</dd>
              </div>
              <div>
                <dt>Engagement</dt>
                <dd>{signup.packSelection.commitmentMonths} mois</dd>
              </div>
              <div>
                <dt>Paiement</dt>
                <dd>
                  {signup.packSelection.paymentMode === "upfront"
                    ? "Comptant"
                    : "Mensualisé"}
                </dd>
              </div>
              <div>
                <dt>Mensuel affiché</dt>
                <dd>
                  {formatCurrencyFromCents(
                    signup.packSelection.monthlyPriceAmountCents,
                  )}{" "}
                  HT
                </dd>
              </div>
              <div>
                <dt>Première échéance</dt>
                <dd>
                  {formatCurrencyFromCents(
                    signup.packSelection.firstChargeAmountCents,
                  )}{" "}
                  HT
                </dd>
              </div>
            </dl>
          </div>
        ) : null}

        {signup.rejectedReason ? (
          <div className="signup-message-block">
            <h3>Motif du refus</h3>
            <p>{signup.rejectedReason}</p>
          </div>
        ) : null}
      </SectionCard>

      <SectionCard ariaLabel="Décision de validation">
        <SectionHeading
          description="L'approbation crée un compte client (sans Active Directory) et envoie un lien de définition de mot de passe. Aucune création AD automatique."
          title="Décision"
        />
        <AdminSignupActions signupId={signup.id} status={signup.status} />
      </SectionCard>

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
