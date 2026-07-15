"use client";

import type { SubscriptionProvisioningTargetUserSummary } from "@kermaria/shared";
import { useMemo, useState } from "react";

import { AdminReconcileProvisioningButton } from "@/components/AdminReconcileProvisioningButton";

type AdminSubscriptionProvisioningManagerProps = {
  subscriptionId: string;
  canRetry: boolean;
  targetUsers: SubscriptionProvisioningTargetUserSummary[];
};

export function AdminSubscriptionProvisioningManager({
  subscriptionId,
  canRetry,
  targetUsers,
}: AdminSubscriptionProvisioningManagerProps) {
  const [selectedUserSamAccountNames, setSelectedUserSamAccountNames] = useState<
    string[]
  >([]);

  const targetUserSet = useMemo(
    () => new Set(targetUsers.map((user) => user.samAccountName)),
    [targetUsers],
  );

  const selectedUsers = selectedUserSamAccountNames.filter((samAccountName) =>
    targetUserSet.has(samAccountName),
  );

  function toggleSelection(samAccountName: string) {
    setSelectedUserSamAccountNames((current) =>
      current.includes(samAccountName)
        ? current.filter((value) => value !== samAccountName)
        : [...current, samAccountName],
    );
  }

  function selectAll() {
    setSelectedUserSamAccountNames(targetUsers.map((user) => user.samAccountName));
  }

  function clearSelection() {
    setSelectedUserSamAccountNames([]);
  }

  return (
    <div>
      {canRetry ? (
        <div className="stack-list" style={{ marginBottom: 18 }}>
          <div className="stack-row">
            <div className="stack-row-main">
              <strong>Provisioning global</strong>
              <span>
                Relance la reconciliation sur tous les utilisateurs AD cibles par
                cette souscription.
              </span>
            </div>
            <AdminReconcileProvisioningButton
              subscriptionId={subscriptionId}
              idleLabel="Provisionner l'ensemble"
            />
          </div>

          <div className="stack-row">
            <div className="stack-row-main">
              <strong>Provisioning cible</strong>
              <span>
                {selectedUsers.length > 0
                  ? `${selectedUsers.length} utilisateur(s) selectionne(s).`
                  : "Selectionnez un ou plusieurs utilisateurs ci-dessous."}
              </span>
            </div>
            <button
              className="button button-secondary"
              disabled={targetUsers.length === 0}
              onClick={selectAll}
              type="button"
            >
              Tout selectionner
            </button>
            <button
              className="button button-secondary"
              disabled={selectedUsers.length === 0}
              onClick={clearSelection}
              type="button"
            >
              Vider la selection
            </button>
            <AdminReconcileProvisioningButton
              disabled={selectedUsers.length === 0}
              idleLabel="Provisionner la selection"
              subscriptionId={subscriptionId}
              targetUserSamAccountNames={selectedUsers}
            />
          </div>
        </div>
      ) : (
        <p className="field-hint">
          Relance indisponible tant que le mapping ou la configuration AD
          n&apos;est pas exploitable pour cette offre.
        </p>
      )}

      <div style={{ marginTop: 18 }}>
        <h3>Utilisateurs vises</h3>
        {targetUsers.length === 0 ? (
          <p className="field-hint">
            Aucun lien AD utilisateur n&apos;est actuellement rattache a ce client.
          </p>
        ) : (
          <ul className="stack-list">
            {targetUsers.map((user) => {
              const isSelected = selectedUsers.includes(user.samAccountName);
              return (
                <li className="stack-row" key={user.samAccountName}>
                  <label
                    className="checkbox-inline"
                    style={{ alignItems: "flex-start", gap: 10 }}
                  >
                    <input
                      checked={isSelected}
                      disabled={!canRetry}
                      onChange={() => toggleSelection(user.samAccountName)}
                      type="checkbox"
                    />
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <strong>{user.samAccountName}</strong>
                      <p className="field-hint">
                        {user.displayName}
                        {user.userPrincipalName
                          ? ` - ${user.userPrincipalName}`
                          : ""}
                      </p>
                    </div>
                  </label>
                  <AdminReconcileProvisioningButton
                    disabled={!canRetry}
                    idleLabel="Provisionner"
                    subscriptionId={subscriptionId}
                    targetUserSamAccountNames={[user.samAccountName]}
                  />
                </li>
              );
            })}
          </ul>
        )}
      </div>
    </div>
  );
}
