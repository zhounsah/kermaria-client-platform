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
  const decisionDescription = signup.status === "approved"
    ? "Le compte client existe déjà. Tant que le mot de passe initial n'a pas ete défini, vous pouvez l'initialiser vous-même ou renvoyer un nouveau lien. Cette définition de mot de passe finalise aussi l'identité dans clients.home.bzh quand l'écriture AD est active."
    : "L'approbation créé le client et l'utilisateur portail, puis envoie un lien de définition du mot de passe. L'identité Active Directory n'est creee qu'au moment de cette définition dans clients.home.bzh.";
  const decisionTitle = signup.status === "approved"
    ? "Accès en attente"
    : "Decision";

  return (
    <>
      <PageHeader
        action={
          <StatusBadge
            label={localizeSignupStatus(signup.status)}
            tone={signupStatusTone(signup.status)}
          />
        }
        description="Detail de la demande et trajectoire d'ouverture du compte."
        eyebrow="Relation client"
        title={signup.companyName}
      />

      <Link className="back-link" href="/admin/signups">
        Retour aux demandes
      </Link>

      <SectionCard ariaLabel="Informations de la demande">
        <SectionHeading title="Informations transmises" />

        <div className="signup-message-block">
          <h3>Structure cliente</h3>
          <dl className="detail-grid">
            <div>
              <dt>Type</dt>
              <dd>{localizeCustomerType(signup.customer?.customerType)}</dd>
            </div>
            <div>
              <dt>Societe</dt>
              <dd>{signup.customer?.displayName ?? signup.companyName}</dd>
            </div>
            <div>
              <dt>E-mail de facturation</dt>
              <dd>{signup.customer?.billingEmail ?? signup.email}</dd>
            </div>
            <div>
              <dt>Telephone</dt>
              <dd>{signup.customer?.phone ?? signup.phone ?? "-"}</dd>
            </div>
            <div>
              <dt>Adresse</dt>
              <dd>{formatAddress(signup)}</dd>
            </div>
            <div>
              <dt>Adresse IP source</dt>
              <dd>
                <code>{signup.sourceAddress ?? "-"}</code>
              </dd>
            </div>
            <div>
              <dt>Recue le</dt>
              <dd>{formatDateTime(signup.createdAt)}</dd>
            </div>
            <div>
              <dt>Mise à jour</dt>
              <dd>{formatDateTime(signup.updatedAt)}</dd>
            </div>
            {signup.approvedAt ? (
              <div>
                <dt>Approuvee le</dt>
                <dd>{formatDateTime(signup.approvedAt)}</dd>
              </div>
            ) : null}
            {signup.rejectedAt ? (
              <div>
                <dt>Refusee le</dt>
                <dd>{formatDateTime(signup.rejectedAt)}</dd>
              </div>
            ) : null}
          </dl>
        </div>

        <div className="signup-message-block">
          <h3>Utilisateur principal</h3>
          <dl className="detail-grid">
            <div>
              <dt>Nom complet</dt>
              <dd>{signup.primaryUser?.displayName ?? signup.contactName}</dd>
            </div>
            <div>
              <dt>Civilite</dt>
              <dd>{localizePersonalTitle(signup.primaryUser?.personalTitle)}</dd>
            </div>
            <div>
              <dt>Prenom</dt>
              <dd>{signup.primaryUser?.givenName ?? "-"}</dd>
            </div>
            <div>
              <dt>Nom</dt>
              <dd>{signup.primaryUser?.surname ?? "-"}</dd>
            </div>
            <div>
              <dt>Initiales</dt>
              <dd>{signup.primaryUser?.initials ?? "-"}</dd>
            </div>
            <div>
              <dt>E-mail de connexion</dt>
              <dd>{signup.primaryUser?.email ?? signup.email}</dd>
            </div>
            <div>
              <dt>Telephone</dt>
              <dd>{signup.primaryUser?.phone ?? signup.phone ?? "-"}</dd>
            </div>
            <div>
              <dt>Contact principal</dt>
              <dd>{localizeBoolean(signup.primaryUser?.isPrimaryContact)}</dd>
            </div>
          </dl>
        </div>

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
                <dt>Reference</dt>
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
                    : "Mensualise"}
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

      <SectionCard ariaLabel="Accès et identité">
        <SectionHeading
          description="Le portail reste la source du mot de passe. Lors de la définition initiale ou d'un changement depuis l'espace client, le mot de passe est aussi synchronisé vers l'identité Active Directory si un lien AD existe ou doit être créé."
          title="Accès et identité"
        />

        {signup.accountAccess ? (
          <dl className="detail-grid">
            <div>
              <dt>Reference client</dt>
              <dd>{signup.accountAccess.customerReference ?? "-"}</dd>
            </div>
            <div>
              <dt>Mot de passe défini</dt>
              <dd>{signup.accountAccess.passwordDefined ? "Oui" : "Non"}</dd>
            </div>
            <div>
              <dt>Echeance du lien</dt>
              <dd>
                {signup.accountAccess.passwordSetupExpiresAt
                  ? formatDateTime(signup.accountAccess.passwordSetupExpiresAt)
                  : "-"}
              </dd>
            </div>
            <div>
              <dt>Provisioning AD</dt>
              <dd>{localizeAccessStatus(signup.accountAccess.adProvisioningStatus)}</dd>
            </div>
            <div>
              <dt>Derniere synchro mot de passe</dt>
              <dd>{localizeAccessStatus(signup.accountAccess.lastPasswordSyncStatus)}</dd>
            </div>
            <div>
              <dt>Export KoXo</dt>
              <dd>{localizeAccessStatus(signup.accountAccess.koxoExportStatus)}</dd>
            </div>
            <div>
              <dt>sAMAccountName</dt>
              <dd>{signup.accountAccess.samAccountName ?? "-"}</dd>
            </div>
            <div>
              <dt>User principal name</dt>
              <dd>{signup.accountAccess.userPrincipalName ?? "-"}</dd>
            </div>
          </dl>
        ) : (
          <p className="field-hint">
            Aucun accès n'a encore été créé. Cette section se renseignera après
            approbation de la demande.
          </p>
        )}
      </SectionCard>

      <SectionCard ariaLabel="Decision de validation">
        <SectionHeading
          description={decisionDescription}
          title={decisionTitle}
        />
        <AdminSignupActions
          accountAccess={signup.accountAccess}
          signupId={signup.id}
          status={signup.status}
        />
      </SectionCard>

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}

function formatAddress(signup: Awaited<ReturnType<typeof getAdminSignup>>["data"]) {
  if (!signup) {
    return "-";
  }

  const lines = [
    signup.customer?.addressLine1,
    signup.customer?.addressLine2,
    [signup.customer?.postalCode, signup.customer?.city]
      .filter(Boolean)
      .join(" ")
      .trim(),
    signup.customer?.country,
  ].filter((value): value is string => Boolean(value && value.trim()));

  return lines.length > 0 ? lines.join(", ") : "-";
}

function localizeCustomerType(value: string | null | undefined) {
  switch (value) {
    case "professional":
      return "Professionnel";
    case "association":
      return "Association";
    case "individual":
      return "Particulier";
    default:
      return "-";
  }
}

function localizePersonalTitle(value: string | null | undefined) {
  switch (value) {
    case "madame":
      return "Madame";
    case "monsieur":
      return "Monsieur";
    case "autre":
      return "Autre";
    default:
      return "-";
  }
}

function localizeBoolean(value: boolean | null | undefined) {
  if (value === true) {
    return "Oui";
  }
  if (value === false) {
    return "Non";
  }
  return "-";
}

function localizeAccessStatus(value: string | null | undefined) {
  switch (value) {
    case "succeeded":
      return "Succes";
    case "pending":
      return "En attente";
    case "koxo_pending":
      return "En attente KoXo";
    case "failed":
      return "En echec";
    default:
      return value ? humanizeToken(value) : "-";
  }
}

function humanizeToken(value: string) {
  return value
    .split(/[_-]+/)
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(" ");
}
