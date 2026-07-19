"use client";

import type {
  AdminCustomerAdWorkspace,
  AdProvisioningDiagnostic,
  CustomerAdProvisioningMutationPayload,
  CustomerAdProvisioningMutationResponse,
  ManualProvisioningOperation,
  ProvisionableGroupSummary,
  ProvisionableServiceSummary,
  SubscriptionStatus,
} from "@kermaria/shared";
import { useMemo, useState } from "react";
import Link from "next/link";

import { AdminReconcileProvisioningButton } from "@/components/AdminReconcileProvisioningButton";
import { FormMessage } from "@/components/FormMessage";
import { SectionCard } from "@/components/SectionCard";
import { StatusBadge } from "@/components/StatusBadge";
import {
  subscriptionStatus,
} from "@/lib/formatters";
import { requestBffJson } from "@/lib/client-api";

type AdminCustomerActiveDirectoryWorkbenchProps = {
  customerReference: string;
  initialWorkspace: AdminCustomerAdWorkspace;
};

type MessageState = {
  tone: "success" | "error" | "info";
  text: string;
} | null;

export function AdminCustomerActiveDirectoryWorkbench({
  customerReference,
  initialWorkspace,
}: AdminCustomerActiveDirectoryWorkbenchProps) {
  const [workspace, setWorkspace] = useState(initialWorkspace);
  const [selectedUsers, setSelectedUsers] = useState<string[]>(
    initialWorkspace.linkedUsers.map((user) => user.samAccountName),
  );
  const [overrideEnabled, setOverrideEnabled] = useState(false);
  const [pendingKey, setPendingKey] = useState<string | null>(null);
  const [message, setMessage] = useState<MessageState>(null);

  const allUserSamAccountNames = useMemo(
    () => workspace.linkedUsers.map((user) => user.samAccountName),
    [workspace.linkedUsers],
  );
  const selectedUserSet = useMemo(
    () => new Set(selectedUsers),
    [selectedUsers],
  );
  const selectedCount = selectedUsers.length;
  const allSelected =
    workspace.linkedUsers.length > 0
    && selectedCount === workspace.linkedUsers.length;
  const targetedUsers = selectedCount > 0 ? selectedUsers : null;

  function toggleUser(samAccountName: string) {
    setSelectedUsers((current) => (
      current.includes(samAccountName)
        ? current.filter((value) => value !== samAccountName)
        : [...current, samAccountName]
    ));
  }

  function selectOnlyUser(samAccountName: string) {
    setSelectedUsers([samAccountName]);
  }

  function selectAllUsers() {
    setSelectedUsers(allUserSamAccountNames);
  }

  function clearSelection() {
    setSelectedUsers([]);
  }

  async function runMutation(
    kind: "service" | "group",
    reference: string,
    operation: ManualProvisioningOperation,
    summary: ProvisionableServiceSummary | ProvisionableGroupSummary,
  ) {
    if (pendingKey || selectedCount === 0) {
      return;
    }

    const needsOverride = summary.isOverrideRequired;
    if (needsOverride && !overrideEnabled) {
      setMessage({
        tone: "error",
        text:
          "Cette action exige l'override administrateur, car le service n'est pas couvert par une souscription active ou déjà payée.",
      });
      return;
    }

    if (operation === "remove") {
      const scope = selectedCount === 1
        ? "cet utilisateur"
        : `${selectedCount} utilisateur(s)`;
      const confirmed = window.confirm(
        `Confirmer le retrait pour ${scope} ?`,
      );
      if (!confirmed) {
        return;
      }
    }

    setPendingKey(`${kind}:${reference}:${operation}`);
    setMessage(null);

    const payload: CustomerAdProvisioningMutationPayload = {
      operation,
      targetUserSamAccountNames: targetedUsers,
      override: needsOverride ? overrideEnabled : false,
      subscriptionId: workspace.subscriptionContext?.id ?? null,
    };

    const path = kind === "service"
      ? `/api/admin/customers/${encodeURIComponent(customerReference)}/active-directory/services/${encodeURIComponent(reference)}`
      : `/api/admin/customers/${encodeURIComponent(customerReference)}/active-directory/groups/${encodeURIComponent(reference)}`;

    const result = await requestBffJson<CustomerAdProvisioningMutationResponse>(
      path as `/api/${string}`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      },
    );

    if (result.ok) {
      setWorkspace(result.data.workspace);
      setMessage({
        tone: "success",
        text: result.data.message,
      });
    } else {
      setMessage({
        tone: "error",
        text: result.error.message,
      });
    }

    setPendingKey(null);
  }

  return (
    <div className="stack-panels">
      <SectionCard ariaLabel="Synthèse Active Directory client" className="stack-panel">
        <div className="section-heading">
          <div>
            <h2>Workbench Active Directory</h2>
            <p>
              Cette page regroupe le provisionning automatique, la
              réconciliation manuelle et l&apos;activation service par service pour
              <strong> {workspace.customerName}</strong>.
            </p>
          </div>
          <div className="badge-stack">
            <StatusBadge
              label={localizeProvisioningStatus(workspace.provisioningStatus)}
              tone={toneForProvisioningStatus(workspace.provisioningStatus)}
            />
            <StatusBadge
              label={localizeAdStatus(workspace.adStatus?.status)}
              tone={toneForAdStatus(workspace.adStatus?.status)}
            />
          </div>
        </div>

        <div className="ad-status-grid">
          <div className="security-item">
            <div>
              <strong>Client</strong>
              <span>{workspace.customerReference}</span>
            </div>
          </div>
          <div className="security-item">
            <div>
              <strong>Utilisateurs liés</strong>
              <span>{workspace.linkedUsers.length}</span>
            </div>
          </div>
          <div className="security-item">
            <div>
              <strong>Dernier résultat</strong>
              <span>{describeResultCode(workspace.lastResultCode)}</span>
            </div>
          </div>
          <div className="security-item">
            <div>
              <strong>Racines AD autorisées</strong>
              <span>
                {workspace.adStatus?.allowedRoots.length
                  ? workspace.adStatus.allowedRoots.join(" ; ")
                  : "Non renseignées"}
              </span>
            </div>
          </div>
        </div>

        {message ? (
          <FormMessage
            title={message.tone === "success" ? "Action terminée" : message.tone === "info" ? "Information" : "Action refusée"}
            tone={message.tone}
          >
            <p>{message.text}</p>
          </FormMessage>
        ) : null}
      </SectionCard>

      <div className="request-detail-layout">
        <SectionCard ariaLabel="Contexte des souscriptions">
          <div className="section-heading">
            <div>
              <h2>Contexte des souscriptions</h2>
              <p>
                Le workbench AD reste centré sur le client, avec un filtre
                éventuel sur l&apos;abonnement courant.
              </p>
            </div>
            <Link
              className="button button-secondary"
              href={`/admin/customers/${encodeURIComponent(customerReference)}`}
            >
              Retour à la fiche client
            </Link>
          </div>

          {workspace.subscriptionContext ? (
            <div className="security-item">
              <div>
                <strong>Abonnement filtré</strong>
                <span>
                  {workspace.subscriptionContext.offerName} (
                  {localizeSubscriptionStatus(workspace.subscriptionContext.status)})
                </span>
              </div>
              <Link
                className="button button-secondary"
                href={`/admin/subscriptions/${encodeURIComponent(workspace.subscriptionContext.id)}`}
              >
                Ouvrir l&apos;abonnement
              </Link>
            </div>
          ) : (
            <p className="field-hint">
              Aucun filtre d&apos;abonnement actif. Les services affichés couvrent
              toutes les souscriptions du client.
            </p>
          )}

          <div className="stack-list">
            {workspace.subscriptions.map((subscription) => {
              const status = subscriptionStatus[
                subscription.status as SubscriptionStatus
              ];
              return (
                <article className="stack-row" key={subscription.id}>
                  <div className="stack-row-main">
                    <strong>{subscription.offerName}</strong>
                    <span>
                      {subscription.offerExternalReference ?? "Sans référence offre"}
                    </span>
                    <span>
                      Services couverts :{" "}
                      {subscription.coveredServiceTechnicalReferences.length > 0
                        ? subscription.coveredServiceTechnicalReferences.join(", ")
                        : "aucun"}
                    </span>
                  </div>
                  <StatusBadge label={status.label} tone={status.tone} />
                  <Link
                    className="button button-secondary"
                    href={`/admin/customers/${encodeURIComponent(customerReference)}/active-directory?subscriptionId=${encodeURIComponent(subscription.id)}`}
                  >
                    Filtrer
                  </Link>
                  <Link
                    className="button button-secondary"
                    href={`/admin/subscriptions/${encodeURIComponent(subscription.id)}`}
                  >
                    Voir
                  </Link>
                </article>
              );
            })}
          </div>
        </SectionCard>

        <SectionCard ariaLabel="Ciblage des utilisateurs AD">
          <div className="section-heading">
            <div>
              <h2>Ciblage des utilisateurs liés</h2>
              <p>
                Les actions peuvent s&apos;appliquer à tous les utilisateurs liés, à
                une sélection, ou à un seul utilisateur.
              </p>
            </div>
          </div>

          <div className="ad-button-row" style={{ marginBottom: 12 }}>
            <button
              className="button button-secondary"
              onClick={selectAllUsers}
              type="button"
            >
              Tout sélectionner
            </button>
            <button
              className="button button-secondary"
              onClick={clearSelection}
              type="button"
            >
              Vider la sélection
            </button>
          </div>

          <p className="field-hint">
            Portée actuelle : {describeScope(workspace, selectedUsers)}.
          </p>

          {workspace.linkedUsers.length === 0 ? (
            <FormMessage title="Aucun utilisateur lié" tone="error">
              <p>
                Le provisionning AD est bloqué tant qu&apos;aucun utilisateur lié
                n&apos;est rattaché à ce client.
              </p>
            </FormMessage>
          ) : (
            <div className="stack-list">
              {workspace.linkedUsers.map((user) => (
                <article className="stack-row" key={user.samAccountName}>
                  <label className="checkbox-inline">
                    <input
                      checked={selectedUserSet.has(user.samAccountName)}
                      onChange={() => toggleUser(user.samAccountName)}
                      type="checkbox"
                    />
                    {user.samAccountName}
                  </label>
                  <div className="stack-row-main">
                    <strong>{user.displayName}</strong>
                    <span>{user.userPrincipalName ?? "Sans UPN"}</span>
                  </div>
                  <button
                    className="button button-secondary"
                    onClick={() => selectOnlyUser(user.samAccountName)}
                    type="button"
                  >
                    Cibler uniquement
                  </button>
                </article>
              ))}
            </div>
          )}
        </SectionCard>
      </div>

      <div className="request-detail-layout">
        <SectionCard ariaLabel="Provisionning manuel et garde-fous">
          <h2>Maintenance et garde-fous</h2>
          <p className="field-hint">
            La réconciliation globale sert au rattrapage. Les actions
            manuelles par service restent limitées aux services couverts, sauf
            override administrateur explicite.
          </p>
          <div className="stack-list">
            {workspace.subscriptions
              .filter((subscription) => subscription.mappedGroups.length > 0)
              .map((subscription) => (
                <article className="stack-row" key={subscription.id}>
                  <div className="stack-row-main">
                    <strong>{subscription.offerName}</strong>
                    <span>
                      Réconciliation globale de l&apos;abonnement
                    </span>
                  </div>
                  <AdminReconcileProvisioningButton
                    idleLabel="Réconcilier"
                    subscriptionId={subscription.id}
                    submittingLabel="Réconciliation..."
                    targetUserSamAccountNames={
                      allSelected ? null : targetedUsers
                    }
                  />
                </article>
              ))}
          </div>

          <div
            className="content-panel"
            style={{
              border: "1px solid rgba(185, 28, 28, 0.18)",
              marginTop: 16,
              padding: 16,
              background:
                "linear-gradient(135deg, rgba(185, 28, 28, 0.04), rgba(185, 28, 28, 0.01))",
            }}
          >
            <label className="checkbox-inline">
              <input
                checked={overrideEnabled}
                onChange={(event) => setOverrideEnabled(event.target.checked)}
                type="checkbox"
              />
              Activer l&apos;override administrateur pour les actions non couvertes
            </label>
            <p className="field-hint" style={{ marginTop: 8 }}>
              À utiliser uniquement pour les cas d&apos;exception. Les actions en
              override sont journalisées séparément.
            </p>
          </div>
        </SectionCard>

        <SectionCard ariaLabel="Diagnostics AD">
          <h2>Diagnostics AD</h2>
          {workspace.diagnostics.length === 0 ? (
            <p className="field-hint">
              Aucun blocage AD majeur n&apos;est actuellement détecté.
            </p>
          ) : (
            <div className="stack-list">
              {workspace.diagnostics.map((diagnostic, index) => (
                <DiagnosticCard
                  customerReference={customerReference}
                  diagnostic={diagnostic}
                  key={`${diagnostic.code}-${index}`}
                />
              ))}
            </div>
          )}
        </SectionCard>
      </div>

      <SectionCard ariaLabel="Services provisionnables" className="stack-panel">
        <h2>Services provisionnables</h2>
        {workspace.services.length === 0 ? (
          <p className="field-hint">
            Aucun service AD activable n&apos;est exposé dans ce contexte.
          </p>
        ) : (
          <div className="stack-list">
            {workspace.services.map((service) => (
              <ServiceCard
                key={service.technicalServiceReference}
                onRunAction={(operation) =>
                  runMutation(
                    "service",
                    service.technicalServiceReference,
                    operation,
                    service,
                  )}
                pending={pendingKey}
                selectedCount={selectedCount}
                service={service}
                overrideEnabled={overrideEnabled}
              />
            ))}
          </div>
        )}
      </SectionCard>

      <SectionCard ariaLabel="Vue avancée par groupe AD" className="stack-panel">
        <h2>Vue avancée par groupe AD</h2>
        <p className="field-hint">
          Cette vue agit directement sur les groupes de sécurité AD du type
          <code> GG_*</code>, pour garder une administration fine et cohérente
          avec la vue métier par service.
        </p>
        {workspace.groups.length === 0 ? (
          <p className="field-hint">
            Aucun groupe AD manuel n&apos;est visible dans ce contexte.
          </p>
        ) : (
          <div className="stack-list">
            {workspace.groups.map((group) => (
              <GroupCard
                group={group}
                key={group.groupSamAccountName}
                onRunAction={(operation) =>
                  runMutation(
                    "group",
                    group.groupSamAccountName,
                    operation,
                    group,
                  )}
                overrideEnabled={overrideEnabled}
                pending={pendingKey}
                selectedCount={selectedCount}
              />
            ))}
          </div>
        )}
      </SectionCard>
    </div>
  );
}

function ServiceCard({
  service,
  pending,
  selectedCount,
  overrideEnabled,
  onRunAction,
}: {
  service: ProvisionableServiceSummary;
  pending: string | null;
  selectedCount: number;
  overrideEnabled: boolean;
  onRunAction: (operation: ManualProvisioningOperation) => void;
}) {
  const actionPendingPrefix = `service:${service.technicalServiceReference}:`;
  const disabled = selectedCount === 0 || pending?.startsWith(actionPendingPrefix);

  return (
    <article className="stack-row">
      <div className="stack-row-main">
        <strong>{service.label}</strong>
        <span>{service.technicalServiceReference}</span>
        <span>
          Groupes AD :{" "}
          {service.groupSamAccountNames.length > 0
            ? service.groupSamAccountNames.join(", ")
            : "aucun"}
        </span>
        <span>
          Couverture :{" "}
          {service.isCoveredByActiveSubscription
            ? "souscription active ou déjà payée"
            : "override requis"}
        </span>
      </div>
      <StatusBadge
        label={localizeCurrentStatus(service.currentStatus)}
        tone={toneForCurrentStatus(service.currentStatus)}
      />
      <button
        className="button"
        disabled={disabled || (service.isOverrideRequired && !overrideEnabled)}
        onClick={() => onRunAction("activate")}
        type="button"
      >
        Activer le service
      </button>
      <button
        className="button button-secondary"
        disabled={disabled || (service.isOverrideRequired && !overrideEnabled)}
        onClick={() => onRunAction("remove")}
        type="button"
      >
        Retirer le service
      </button>
    </article>
  );
}

function GroupCard({
  group,
  pending,
  selectedCount,
  overrideEnabled,
  onRunAction,
}: {
  group: ProvisionableGroupSummary;
  pending: string | null;
  selectedCount: number;
  overrideEnabled: boolean;
  onRunAction: (operation: ManualProvisioningOperation) => void;
}) {
  const actionPendingPrefix = `group:${group.groupSamAccountName}:`;
  const disabled = selectedCount === 0 || pending?.startsWith(actionPendingPrefix);

  return (
    <article className="stack-row">
      <div className="stack-row-main">
        <strong>{group.groupSamAccountName}</strong>
        <span>
          Services liés :{" "}
          {group.technicalServiceReferences.length > 0
            ? group.technicalServiceReferences.join(", ")
            : "aucun"}
        </span>
        <span>
          Couverture :{" "}
          {group.isCoveredByActiveSubscription
            ? "souscription active ou déjà payée"
            : "override requis"}
        </span>
      </div>
      <StatusBadge
        label={localizeCurrentStatus(group.currentStatus)}
        tone={toneForCurrentStatus(group.currentStatus)}
      />
      <button
        className="button"
        disabled={disabled || (group.isOverrideRequired && !overrideEnabled)}
        onClick={() => onRunAction("activate")}
        type="button"
      >
        Activer le groupe
      </button>
      <button
        className="button button-secondary"
        disabled={disabled || (group.isOverrideRequired && !overrideEnabled)}
        onClick={() => onRunAction("remove")}
        type="button"
      >
        Retirer le groupe
      </button>
    </article>
  );
}

function DiagnosticCard({
  customerReference,
  diagnostic,
}: {
  customerReference: string;
  diagnostic: AdProvisioningDiagnostic;
}) {
  return (
    <article className="stack-row">
      <div className="stack-row-main">
        <strong>{diagnostic.code}</strong>
        <span>{diagnostic.message}</span>
        {diagnostic.allowedRoots.length > 0 ? (
          <span>
            Racines autorisées : {diagnostic.allowedRoots.join(" ; ")}
          </span>
        ) : null}
        {diagnostic.affectedUserDistinguishedNames.length > 0 ? (
          <span>
            Utilisateurs hors périmètre :{" "}
            {diagnostic.affectedUserDistinguishedNames.join(" ; ")}
          </span>
        ) : null}
        {diagnostic.affectedGroupDistinguishedNames.length > 0 ? (
          <span>
            Groupes hors périmètre :{" "}
            {diagnostic.affectedGroupDistinguishedNames.join(" ; ")}
          </span>
        ) : null}
        {diagnostic.linkedUserReferences.length > 0 ? (
          <span>
            Comptes concernés : {diagnostic.linkedUserReferences.join(", ")}
          </span>
        ) : null}
      </div>
      {diagnostic.targetType === "user" || diagnostic.targetType === "user_and_group" ? (
        <Link
          className="button button-secondary"
          href={`/admin/customers/${encodeURIComponent(customerReference)}/active-directory#gestion-avancee-ad`}
        >
          Vérifier les liens AD
        </Link>
      ) : null}
    </article>
  );
}

function describeScope(
  workspace: AdminCustomerAdWorkspace,
  selectedUsers: string[],
) {
  if (workspace.linkedUsers.length === 0 || selectedUsers.length === 0) {
    return "aucun utilisateur sélectionné";
  }

  if (selectedUsers.length === workspace.linkedUsers.length) {
    return "tous les utilisateurs liés";
  }

  if (selectedUsers.length === 1) {
    return `un utilisateur (${selectedUsers[0]})`;
  }

  return `${selectedUsers.length} utilisateurs sélectionnés`;
}

function describeResultCodeLabel(code: string) {
  switch (code) {
    case "PROVISIONING_SYNCHRONIZED":
      return "Les groupes AD sont maintenant synchronises avec la souscription.";
    case "PROVISIONING_APPLIED":
      return "Le provisionning AD a été applique avec succes.";
    case "PROVISIONING_UNCHANGED":
      return "Aucune modification AD n'etait necessaire.";
    case "AD_GROUP_SCOPE_INCOMPATIBLE":
      return "Le groupe AD cible n'accepte pas l'appartenance demandee avec la portee actuelle.";
    case "PROVISIONING_GROUP_NOT_CONFIGURED":
      return "Au moins un groupe AD requis n'est pas configure.";
    case "AD_UNAVAILABLE":
      return "La lecture ou l'écriture Active Directory est actuellement indisponible.";
    default:
      return null;
  }
}

function describeResultCode(code: string | null) {
  if (!code) {
    return "Aucun résultat récent";
  }

  if (code === "AD_TARGET_OUTSIDE_ALLOWED_ROOTS") {
    return "Un utilisateur lié ou un groupe cible est hors du périmètre AD autorisé.";
  }

  if (code === "PROVISIONING_NO_TARGET_USERS") {
    return "Aucun utilisateur lié n'est disponible pour le provisionning.";
  }

  return describeResultCodeLabel(code) ?? code;
}

function localizeAdStatus(status: string | undefined) {
  switch (status) {
    case "ready":
      return "AD prête";
    case "mock":
      return "Mode simulé";
    case "disabled":
      return "AD désactivée";
    case "configuration_invalid":
      return "Configuration invalide";
    case "unreachable":
      return "AD indisponible";
    default:
      return "Statut inconnu";
  }
}

function toneForAdStatus(status: string | undefined) {
  switch (status) {
    case "ready":
      return "success" as const;
    case "mock":
      return "warning" as const;
    case "disabled":
      return "neutral" as const;
    default:
      return "danger" as const;
  }
}

function localizeProvisioningStatus(status: string) {
  switch (status) {
    case "ready":
      return "Prêt";
    case "succeeded":
      return "Synchronisé";
    case "failed":
      return "En échec";
    case "not_required":
      return "Non requis";
    case "not_configured":
      return "À configurer";
    case "mixed":
      return "État mixte";
    default:
      return status;
  }
}

function toneForProvisioningStatus(status: string) {
  switch (status) {
    case "succeeded":
      return "success" as const;
    case "failed":
      return "danger" as const;
    case "mixed":
      return "warning" as const;
    case "not_required":
      return "info" as const;
    case "not_configured":
      return "warning" as const;
    default:
      return "neutral" as const;
  }
}

function localizeCurrentStatus(status: string) {
  switch (status) {
    case "active":
      return "Actif";
    case "partial":
      return "Partiel";
    case "inactive":
      return "Inactif";
    case "blocked":
      return "Bloqué";
    default:
      return status;
  }
}

function toneForCurrentStatus(status: string) {
  switch (status) {
    case "active":
      return "success" as const;
    case "partial":
      return "warning" as const;
    case "inactive":
      return "neutral" as const;
    default:
      return "danger" as const;
  }
}

function localizeSubscriptionStatus(status: SubscriptionStatus) {
  return subscriptionStatus[status]?.label ?? status;
}
