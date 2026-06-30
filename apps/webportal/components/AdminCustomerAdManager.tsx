"use client";

import type {
  AdminAdStatus,
  AdDirectoryObjectSummary,
  AdGroupCreatePayload,
  AdGroupMemberPayload,
  AdLinkMutationResponse,
  AdMutationResponse,
  AdUserCreatePayload,
  CustomerAdLinkPayload,
  CustomerAdLinkSummary,
} from "@kermaria/shared";
import { FormEvent, startTransition, useRef, useState } from "react";
import { useRouter } from "next/navigation";

import { FormMessage } from "@/components/FormMessage";
import { SectionCard } from "@/components/SectionCard";
import { StatusBadge } from "@/components/StatusBadge";
import { SubmitButton } from "@/components/SubmitButton";
import { formatDateTime } from "@/lib/formatters";
import { requestBffJson } from "@/lib/client-api";

type AdminCustomerAdManagerProps = {
  customerReference: string;
  initialStatus: AdminAdStatus | null;
  initialLinks: CustomerAdLinkSummary[];
  statusError?: string | null;
  linksError?: string | null;
};

type AdDirectorySearchPayload =
  | AdDirectoryObjectSummary[]
  | {
      users?: AdDirectoryObjectSummary[];
      groups?: AdDirectoryObjectSummary[];
      results?: AdDirectoryObjectSummary[];
    };

function upsertDirectoryObjectResult(
  current: AdDirectoryObjectSummary[],
  next: AdDirectoryObjectSummary,
) {
  const remaining = current.filter((item) => item.objectGuid !== next.objectGuid);
  return [next, ...remaining];
}

function upsertCustomerAdLink(
  current: CustomerAdLinkSummary[],
  next: CustomerAdLinkSummary,
) {
  const remaining = current.filter((item) => item.id !== next.id);
  return [next, ...remaining];
}

function mergeCustomerAdLinks(
  serverLinks: CustomerAdLinkSummary[],
  localLinks: CustomerAdLinkSummary[],
  removedLinkIds: string[],
) {
  return localLinks.reduce(
    (current, item) => upsertCustomerAdLink(current, item),
    serverLinks.filter((item) => !removedLinkIds.includes(item.id)),
  );
}

function isDirectoryObjectSummary(value: unknown): value is AdDirectoryObjectSummary {
  if (!value || typeof value !== "object") {
    return false;
  }

  const candidate = value as Partial<AdDirectoryObjectSummary>;
  return (
    typeof candidate.objectGuid === "string"
    && typeof candidate.objectSid === "string"
    && typeof candidate.objectType === "string"
    && typeof candidate.samAccountName === "string"
    && typeof candidate.displayName === "string"
    && typeof candidate.distinguishedName === "string"
    && typeof candidate.customerReference === "string"
    && typeof candidate.isDisabled === "boolean"
  );
}

function extractDirectorySearchResults(
  payload: AdDirectorySearchPayload,
  kind: "users" | "groups",
) {
  const direct = Array.isArray(payload) ? payload : null;
  if (direct && direct.every(isDirectoryObjectSummary)) {
    return direct;
  }

  if (!payload || typeof payload !== "object" || Array.isArray(payload)) {
    return null;
  }

  const scopedResults = kind === "users" ? payload.users : payload.groups;
  if (Array.isArray(scopedResults) && scopedResults.every(isDirectoryObjectSummary)) {
    return scopedResults;
  }

  if (Array.isArray(payload.results) && payload.results.every(isDirectoryObjectSummary)) {
    return payload.results;
  }

  return null;
}

function toDirectoryObjectSummary(
  link: CustomerAdLinkSummary,
): AdDirectoryObjectSummary {
  return {
    objectGuid: link.objectGuid,
    objectSid: link.objectSid,
    objectType: link.objectType,
    samAccountName: link.samAccountName,
    userPrincipalName: link.userPrincipalName,
    displayName: link.displayName,
    distinguishedName: link.distinguishedName,
    customerReference: link.customerReference,
    isDisabled: false,
  };
}

function mergeDirectoryObjects(...groups: AdDirectoryObjectSummary[][]) {
  return groups.flat().reduce<AdDirectoryObjectSummary[]>((current, item) => (
    upsertDirectoryObjectResult(current, item)
  ), []);
}

function statusTone(status: AdminAdStatus["status"] | "unknown") {
  switch (status) {
    case "ready":
      return "success" as const;
    case "mock":
      return "warning" as const;
    case "disabled":
      return "neutral" as const;
    case "configuration_invalid":
    case "unreachable":
      return "danger" as const;
    default:
      return "warning" as const;
  }
}

function describeAdMode(status: AdminAdStatus | null) {
  if (!status) {
    return "Le statut Active Directory n'est pas disponible.";
  }

  switch (status.mode) {
    case "disabled":
      return "Aucune connexion Active Directory ni action AD n'est autorisee.";
    case "mock":
      return "Les actions AD restent simulees pour les tests et n'ecrivent rien dans l'annuaire.";
    case "read_only":
      return "Les recherches AD sont autorisees mais toutes les ecritures sont refusees.";
    case "controlled_write":
      return status.writesEnabled
        ? "Les ecritures AD reelles sont strictement bornees a l'OU de test OU=TEST_SITE_WEB,DC=home,DC=bzh."
        : "Le mode controlled_write est configure sans disponibilite d'ecriture.";
    default:
      return "Le mode AD n'est pas reconnu par l'interface.";
  }
}

export function AdminCustomerAdManager({
  customerReference,
  initialStatus,
  initialLinks,
  statusError,
  linksError,
}: AdminCustomerAdManagerProps) {
  const router = useRouter();
  const busyRef = useRef(false);
  const status = initialStatus;
  const [localLinks, setLocalLinks] = useState<CustomerAdLinkSummary[]>([]);
  const [removedLinkIds, setRemovedLinkIds] = useState<string[]>([]);
  const [usersQuery, setUsersQuery] = useState("");
  const [groupsQuery, setGroupsQuery] = useState("");
  const [userResults, setUserResults] = useState<AdDirectoryObjectSummary[]>([]);
  const [groupResults, setGroupResults] = useState<AdDirectoryObjectSummary[]>([]);
  const [recentObjects, setRecentObjects] = useState<AdDirectoryObjectSummary[]>([]);
  const [isSearchingUsers, setIsSearchingUsers] = useState(false);
  const [isSearchingGroups, setIsSearchingGroups] = useState(false);
  const [usersSearchCount, setUsersSearchCount] = useState<number | null>(null);
  const [groupsSearchCount, setGroupsSearchCount] = useState<number | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<{
    tone: "success" | "error" | "info";
    text: string;
  } | null>(null);
  const [linkDn, setLinkDn] = useState("");
  const [selectedLinkObjectGuid, setSelectedLinkObjectGuid] = useState("");
  const [showAdvancedLinkMode, setShowAdvancedLinkMode] = useState(false);
  const [newUser, setNewUser] = useState<AdUserCreatePayload>({
    samAccountName: "",
    displayName: "",
    givenName: null,
    surname: null,
    userPrincipalName: null,
    description: null,
  });
  const [newGroup, setNewGroup] = useState<AdGroupCreatePayload>({
    samAccountName: "",
    displayName: "",
    description: null,
  });
  const [membership, setMembership] = useState<AdGroupMemberPayload>({
    userSamAccountName: "",
  });
  const [membershipGroupSam, setMembershipGroupSam] = useState("");
  const [lifecycleSam, setLifecycleSam] = useState("");
  const [selectedUser, setSelectedUser] = useState<AdDirectoryObjectSummary | null>(
    null,
  );
  const [selectedGroup, setSelectedGroup] = useState<AdDirectoryObjectSummary | null>(
    null,
  );
  const [effectiveGroups, setEffectiveGroups] = useState<AdDirectoryObjectSummary[]>([]);
  const [effectiveGroupsForUserGuid, setEffectiveGroupsForUserGuid] = useState<string | null>(null);
  const [isLoadingEffectiveGroups, setIsLoadingEffectiveGroups] = useState(false);

  function rememberDirectoryObject(next: AdDirectoryObjectSummary) {
    setRecentObjects((current) => upsertDirectoryObjectResult(current, next));
    setSelectedLinkObjectGuid(next.objectGuid);
  }

  function selectUser(user: AdDirectoryObjectSummary) {
    rememberDirectoryObject(user);
    setSelectedUser(user);
    setUserResults((current) => upsertDirectoryObjectResult(current, user));
    setMembership((current) => ({
      ...current,
      userSamAccountName: user.samAccountName,
    }));
    setLifecycleSam(user.samAccountName);
    setLinkDn(user.distinguishedName);
  }

  function selectGroup(group: AdDirectoryObjectSummary) {
    rememberDirectoryObject(group);
    setSelectedGroup(group);
    setGroupResults((current) => upsertDirectoryObjectResult(current, group));
    setMembershipGroupSam(group.samAccountName);
    setLinkDn(group.distinguishedName);
  }

  async function searchObjects(kind: "users" | "groups") {
    const query = kind === "users" ? usersQuery.trim() : groupsQuery.trim();
    const params = new URLSearchParams();
    params.set("query", query);
    params.set("customerReference", customerReference);
    const path = kind === "users"
      ? `/api/admin/ad/users?${params.toString()}`
      : `/api/admin/ad/groups?${params.toString()}`;

    if (kind === "users") {
      setIsSearchingUsers(true);
    } else {
      setIsSearchingGroups(true);
    }
    setMessage(null);

    const result = await requestBffJson<AdDirectorySearchPayload>(
      path as `/api/${string}`,
      { method: "GET" },
    );

    if (result.ok) {
      const objects = extractDirectorySearchResults(result.data, kind);
      if (objects === null) {
        if (kind === "users") {
          setUserResults([]);
          setUsersSearchCount(null);
        } else {
          setGroupResults([]);
          setGroupsSearchCount(null);
        }
        setMessage({
          tone: "error",
          text: "Le format de reponse de la recherche Active Directory est invalide.",
        });
      } else if (kind === "users") {
        setUserResults(objects);
        setUsersSearchCount(objects.length);
      } else {
        setGroupResults(objects);
        setGroupsSearchCount(objects.length);
      }

    } else {
      setMessage({ tone: "error", text: result.error.message });
    }

    if (kind === "users") {
      setIsSearchingUsers(false);
    } else {
      setIsSearchingGroups(false);
    }
  }

  function findDirectoryObjectByGuid(
    objects: AdDirectoryObjectSummary[],
    objectGuid: string,
  ) {
    return objects.find((item) => item.objectGuid === objectGuid) ?? null;
  }

  const links = mergeCustomerAdLinks(initialLinks, localLinks, removedLinkIds);
  const linkedObjects = links.map(toDirectoryObjectSummary);
  const selectableUsers = mergeDirectoryObjects(
    recentObjects.filter((item) => item.objectType === "user"),
    userResults,
    linkedObjects.filter((item) => item.objectType === "user"),
  );
  const selectableGroups = mergeDirectoryObjects(
    recentObjects.filter((item) => item.objectType === "group"),
    groupResults,
    linkedObjects.filter((item) => item.objectType === "group"),
  );
  const selectableLinkObjects = mergeDirectoryObjects(
    recentObjects,
    userResults,
    groupResults,
    linkedObjects,
  );

async function submitMutation<TPayload>(
    path: `/api/${string}`,
    method: "POST" | "DELETE",
    payload: TPayload | undefined,
    successText:
      | string
      | ((data: AdMutationResponse | AdLinkMutationResponse) => string),
    onSuccess?: (data: AdMutationResponse | AdLinkMutationResponse) => void,
  ) {
    if (busyRef.current) {
      return;
    }

    busyRef.current = true;
    setIsSubmitting(true);
    setMessage(null);

    const result = await requestBffJson<AdMutationResponse | AdLinkMutationResponse>(
      path,
      {
        method,
        ...(payload === undefined
          ? {}
          : {
              headers: { "Content-Type": "application/json" },
              body: JSON.stringify(payload),
            }),
      },
    );

    if (result.ok) {
      onSuccess?.(result.data);
      setMessage({
        tone: "success",
        text: typeof successText === "function"
          ? successText(result.data)
          : successText,
      });
      if ("id" in result.data && typeof result.data.id === "string" && method === "DELETE") {
        const deletedId = result.data.id;
        setRemovedLinkIds((current) => (
          current.includes(deletedId) ? current : [...current, deletedId]
        ));
        setLocalLinks((current) =>
          current.filter((link) => link.id !== deletedId),
        );
      }
      startTransition(() => router.refresh());
    } else {
      setMessage({ tone: "error", text: result.error.message });
    }

    busyRef.current = false;
    setIsSubmitting(false);
  }

  async function handleLinkSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const payload: CustomerAdLinkPayload = {
      distinguishedName: linkDn.trim(),
    };
    if (!payload.distinguishedName) {
      setMessage({
        tone: "error",
        text: "Saisissez un DistinguishedName avant de lier l'objet.",
      });
      return;
    }

    await submitMutation(
      `/api/admin/customers/${encodeURIComponent(customerReference)}/ad-links`,
      "POST",
      payload,
      "Objet Active Directory lie au client.",
      (data) => {
        const linkedObject = data.object;
        if ("id" in data && typeof data.id === "string" && linkedObject) {
          rememberDirectoryObject(linkedObject);
          setLinkDn(linkedObject.distinguishedName);
          setRemovedLinkIds((current) =>
            current.filter((item) => item !== data.id),
          );
          setLocalLinks((current) => upsertCustomerAdLink(current, {
            id: data.id,
            customerReference: linkedObject.customerReference,
            objectGuid: linkedObject.objectGuid,
            objectSid: linkedObject.objectSid,
            objectType: linkedObject.objectType,
            samAccountName: linkedObject.samAccountName,
            userPrincipalName: linkedObject.userPrincipalName,
            displayName: linkedObject.displayName,
            distinguishedName: linkedObject.distinguishedName,
            linkedAt: new Date().toISOString(),
            linkedBy: null,
          }));
        }
      },
    );
  }

  async function handleCreateUser(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!newUser.samAccountName.trim() || !newUser.displayName.trim()) {
      setMessage({
        tone: "error",
        text: "Le compte et le nom d'affichage sont obligatoires.",
      });
      return;
    }

    await submitMutation(
      `/api/admin/customers/${encodeURIComponent(customerReference)}/ad/users`,
      "POST",
      {
        ...newUser,
        samAccountName: newUser.samAccountName.trim(),
        displayName: newUser.displayName.trim(),
        givenName: newUser.givenName?.trim() || null,
        surname: newUser.surname?.trim() || null,
        userPrincipalName: newUser.userPrincipalName?.trim() || null,
        description: newUser.description?.trim() || null,
      } satisfies AdUserCreatePayload,
      "Utilisateur Active Directory cree et selectionne.",
      (data) => {
        if (data.object?.objectType === "user") {
          selectUser(data.object);
        }
      },
    );
    setNewUser({
      samAccountName: "",
      displayName: "",
      givenName: null,
      surname: null,
      userPrincipalName: null,
      description: null,
    });
  }

  async function handleCreateGroup(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!newGroup.samAccountName.trim() || !newGroup.displayName.trim()) {
      setMessage({
        tone: "error",
        text: "Le groupe et le nom d'affichage sont obligatoires.",
      });
      return;
    }

    await submitMutation(
      `/api/admin/customers/${encodeURIComponent(customerReference)}/ad/groups`,
      "POST",
      {
        ...newGroup,
        samAccountName: newGroup.samAccountName.trim(),
        displayName: newGroup.displayName.trim(),
        description: newGroup.description?.trim() || null,
      } satisfies AdGroupCreatePayload,
      "Groupe Active Directory cree et selectionne.",
      (data) => {
        if (data.object?.objectType === "group") {
          selectGroup(data.object);
        }
      },
    );
    setNewGroup({
      samAccountName: "",
      displayName: "",
      description: null,
    });
  }

  async function handleAddMembership(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!membershipGroupSam.trim() || !membership.userSamAccountName.trim()) {
      setMessage({
        tone: "error",
        text: "Le groupe et l'utilisateur sont obligatoires.",
      });
      return;
    }

    await submitMutation(
      `/api/admin/customers/${encodeURIComponent(customerReference)}/ad/groups/${encodeURIComponent(membershipGroupSam.trim())}/members`,
      "POST",
      {
        userSamAccountName: membership.userSamAccountName.trim(),
      } satisfies AdGroupMemberPayload,
      (data) => data.code === "AD_GROUP_MEMBER_ALREADY_PRESENT"
        ? "L'utilisateur etait deja membre du groupe."
        : "Membre ajoute au groupe.",
    );
  }

  async function handleRemoveMembership() {
    if (!membershipGroupSam.trim() || !membership.userSamAccountName.trim()) {
      setMessage({
        tone: "error",
        text: "Le groupe et l'utilisateur sont obligatoires.",
      });
      return;
    }

    await submitMutation(
      `/api/admin/customers/${encodeURIComponent(customerReference)}/ad/groups/${encodeURIComponent(membershipGroupSam.trim())}/members/${encodeURIComponent(membership.userSamAccountName.trim())}`,
      "DELETE",
      undefined,
      (data) => data.code === "AD_GROUP_MEMBER_ALREADY_ABSENT"
        ? "L'utilisateur n'etait pas membre du groupe."
        : "Membre retire du groupe.",
    );
  }

  async function handleDisableUser(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!lifecycleSam.trim()) {
      setMessage({
        tone: "error",
        text: "Le compte utilisateur est obligatoire.",
      });
      return;
    }

    await submitMutation(
      `/api/admin/customers/${encodeURIComponent(customerReference)}/ad/users/${encodeURIComponent(lifecycleSam.trim())}/disable`,
      "POST",
      {},
      (data) => data.code === "AD_USER_ALREADY_DISABLED"
        ? "L'utilisateur etait deja desactive."
        : "Utilisateur desactive.",
    );
  }

  async function handleMoveToDisabled() {
    if (!lifecycleSam.trim()) {
      setMessage({
        tone: "error",
        text: "Le compte utilisateur est obligatoire.",
      });
      return;
    }

    await submitMutation(
      `/api/admin/customers/${encodeURIComponent(customerReference)}/ad/users/${encodeURIComponent(lifecycleSam.trim())}/move-to-disabled`,
      "POST",
      {},
      (data) => data.code === "AD_USER_ALREADY_IN_DISABLED_OU"
        ? "L'utilisateur etait deja dans l'OU Disabled."
        : "Utilisateur deplace vers l'OU Disabled.",
    );
  }

  async function handleLoadEffectiveGroups() {
    if (!selectedUser) {
      setMessage({
        tone: "error",
        text: "Selectionnez un utilisateur avant de lister ses groupes.",
      });
      return;
    }

    setIsLoadingEffectiveGroups(true);
    setMessage(null);

    const result = await requestBffJson<AdDirectoryObjectSummary[]>(
      `/api/admin/customers/${encodeURIComponent(customerReference)}/ad/users/${encodeURIComponent(selectedUser.samAccountName)}/groups`,
      { method: "GET" },
    );

    if (result.ok) {
      setEffectiveGroups(result.data);
      setEffectiveGroupsForUserGuid(selectedUser.objectGuid);
      setMessage({
        tone: "info",
        text: `${result.data.length} groupe(s) effectif(s) trouve(s).`,
      });
    } else {
      setEffectiveGroups([]);
      setEffectiveGroupsForUserGuid(null);
      setMessage({ tone: "error", text: result.error.message });
    }

    setIsLoadingEffectiveGroups(false);
  }

  async function handleUnlink(linkId: string) {
    await submitMutation(
      `/api/admin/customers/${encodeURIComponent(customerReference)}/ad-links/${encodeURIComponent(linkId)}`,
      "DELETE",
      undefined,
      "Lien Active Directory supprime de la table de liaison.",
    );
  }

  return (
    <div className="stack-panels">
      <SectionCard ariaLabel="Etat Active Directory du client" className="stack-panel">
        <div className="section-heading">
          <div>
            <h2>Administration Active Directory</h2>
            <p>
              {describeAdMode(status)} Client cible : {customerReference}. Aucun
              hard delete AD n&apos;est expose.
            </p>
          </div>
          <StatusBadge
            label={status ? status.status : "indisponible"}
            tone={status ? statusTone(status.status) : "warning"}
          />
        </div>
        <div className="ad-status-grid">
          <div className="security-item">
            <div>
              <strong>Mode</strong>
              <span>{status?.mode ?? "indisponible"}</span>
            </div>
            <StatusBadge
              label={status?.writesEnabled
                ? "Ecriture bornee"
                : status?.readsEnabled
                  ? "Lecture sans ecriture"
                  : "AD desactivee"}
              tone={status?.writesEnabled
                ? "warning"
                : status?.readsEnabled
                  ? "info"
                  : "neutral"}
            />
          </div>
          <div className="security-item">
            <div>
              <strong>OU racine autorisee</strong>
              <span>{status?.clientsOuDn ?? "Non resolue"}</span>
            </div>
          </div>
          <div className="security-item">
            <div>
              <strong>Domaine</strong>
              <span>{status?.domain ?? "Non resolu"}</span>
            </div>
          </div>
          <div className="security-item">
            <div>
              <strong>Limites</strong>
              <span>
                Connect {status?.connectTimeoutMs ?? "-"} ms · Query {status?.queryTimeoutMs ?? "-"} ms · Max {status?.maxResults ?? "-"}
              </span>
            </div>
          </div>
        </div>
        {statusError ? (
          <FormMessage title="Etat AD indisponible" tone="error">
            <p>{statusError}</p>
          </FormMessage>
        ) : null}
        {linksError ? (
          <FormMessage title="Liens AD indisponibles" tone="error">
            <p>{linksError}</p>
          </FormMessage>
        ) : null}
        {message ? (
          <FormMessage
            title={message.tone === "success" ? "Action terminee" : message.tone === "info" ? "Information" : "Echec"}
            tone={message.tone}
          >
            <p>{message.text}</p>
          </FormMessage>
        ) : null}
      </SectionCard>

      <div className="request-detail-layout">
        <SectionCard ariaLabel="Objets AD lies">
          <div className="section-heading">
            <div>
              <h2>Objets AD lies</h2>
              <p>Table MariaDB `customer_ad_links` sans suppression AD definitive.</p>
            </div>
          </div>
          {links.length === 0 ? (
            <p className="field-hint">Aucun objet AD n&apos;est encore lie a ce client.</p>
          ) : (
            <div className="stack-list">
              {links.map((link) => (
                <div className="stack-row" key={link.id}>
                  <div className="stack-row-main">
                    <strong>{link.samAccountName}</strong>
                    <span>
                      {link.objectType} · {link.displayName}
                    </span>
                    <span>{link.distinguishedName}</span>
                    <span>
                      Lie le {formatDateTime(link.linkedAt)}
                      {link.linkedBy ? ` par ${link.linkedBy}` : ""}
                    </span>
                  </div>
                  <button
                    className="button button-secondary"
                    disabled={isSubmitting}
                    onClick={() => void handleUnlink(link.id)}
                    type="button"
                  >
                    Delier
                  </button>
                </div>
              ))}
            </div>
          )}
        </SectionCard>

        <SectionCard ariaLabel="Lier un objet AD existant">
          <h2>Lier un objet existant</h2>
          <form className="form-card compact-form-card" onSubmit={handleLinkSubmit}>
            <label>
              Objet AD existant
              <select
                onChange={(event) => {
                  const nextObject = findDirectoryObjectByGuid(
                    selectableLinkObjects,
                    event.target.value,
                  );
                  setSelectedLinkObjectGuid(event.target.value);
                  setLinkDn(nextObject?.distinguishedName ?? "");
                }}
                value={selectedLinkObjectGuid}
              >
                <option value="">Selectionner un utilisateur ou un groupe</option>
                {selectableLinkObjects.map((item) => (
                  <option key={item.objectGuid} value={item.objectGuid}>
                    {item.objectType} · {item.samAccountName} · {item.displayName}
                  </option>
                ))}
              </select>
            </label>
            <p className="field-hint">
              Options alimentees depuis les recherches, les objets deja lies et les
              creations recentes.
            </p>
            <label className="checkbox-inline">
              <input
                checked={showAdvancedLinkMode}
                onChange={(event) => setShowAdvancedLinkMode(event.target.checked)}
                type="checkbox"
              />
              Afficher le DistinguishedName manuel en mode avance/debug
            </label>
            {!showAdvancedLinkMode ? (
              <p className="field-hint">
                {linkDn
                  ? `DistinguishedName selectionne : ${linkDn}`
                  : "Le DistinguishedName sera rempli automatiquement apres selection d'un objet."}
              </p>
            ) : null}
            {showAdvancedLinkMode ? (
            <label>
              DistinguishedName
              <textarea
                onChange={(event) => setLinkDn(event.target.value)}
                rows={4}
                value={linkDn}
              />
            </label>
            ) : null}
            <SubmitButton
              idleLabel="Lier l'objet"
              isSubmitting={isSubmitting}
              submittingLabel="Liaison..."
            />
          </form>
        </SectionCard>
      </div>

      <div className="request-detail-layout">
        <SectionCard ariaLabel="Recherche utilisateurs AD">
          <h2>Rechercher des utilisateurs</h2>
          <form
            className="form-card compact-form-card"
            onSubmit={(event) => {
              event.preventDefault();
              void searchObjects("users");
            }}
          >
            <label>
              Recherche
              <input
                onChange={(event) => setUsersQuery(event.target.value)}
                placeholder="samAccountName, displayName, UPN"
                value={usersQuery}
              />
            </label>
            <SubmitButton
              idleLabel="Rechercher"
              isSubmitting={isSearchingUsers}
              submittingLabel="Recherche..."
            />
          </form>
          {usersSearchCount !== null ? (
            <p className="field-hint">
              {usersSearchCount} resultat(s) trouve(s).
            </p>
          ) : null}
          {selectedUser ? (
            <div className="ad-selected-summary">
              <div className="ad-selection-summary-header">
                <div className="stack-row-main">
                  <strong>Utilisateur selectionne</strong>
                  <span>{selectedUser.samAccountName}</span>
                  <span className="ad-selection-meta">{selectedUser.displayName}</span>
                  <span className="ad-selection-meta">{selectedUser.distinguishedName}</span>
                </div>
                <StatusBadge
                  label={selectedUser.isDisabled ? "Desactive" : "Actif"}
                  tone={selectedUser.isDisabled ? "warning" : "success"}
                />
              </div>
            </div>
          ) : (
            <p className="field-hint">
              Selectionnez un utilisateur pour pre-remplir les actions de groupe,
              de desactivation et de deplacement.
            </p>
          )}
          {usersSearchCount === 0 ? (
            <p className="field-hint">
              Aucun resultat utilisateur ne correspond a cette recherche.
            </p>
          ) : null}
          <div className="stack-list">
            {userResults.map((user) => (
              <div
                className={`stack-row ${selectedUser?.objectGuid === user.objectGuid ? "ad-selection-card-selected" : ""}`}
                key={user.objectGuid}
              >
                <div className="stack-row-main">
                  <strong>{user.samAccountName}</strong>
                  <span>{user.displayName}</span>
                  <span>{user.distinguishedName}</span>
                </div>
                <button
                  className="button button-secondary"
                  disabled={isSubmitting}
                  onClick={() => selectUser(user)}
                  type="button"
                >
                  {selectedUser?.objectGuid === user.objectGuid
                    ? "Selectionne"
                    : "Selectionner"}
                </button>
                <StatusBadge
                  label={user.isDisabled ? "Desactive" : "Actif"}
                  tone={user.isDisabled ? "warning" : "success"}
                />
              </div>
            ))}
          </div>
        </SectionCard>

        <SectionCard ariaLabel="Recherche groupes AD">
          <h2>Rechercher des groupes</h2>
          <form
            className="form-card compact-form-card"
            onSubmit={(event) => {
              event.preventDefault();
              void searchObjects("groups");
            }}
          >
            <label>
              Recherche
              <input
                onChange={(event) => setGroupsQuery(event.target.value)}
                placeholder="samAccountName ou displayName"
                value={groupsQuery}
              />
            </label>
            <SubmitButton
              idleLabel="Rechercher"
              isSubmitting={isSearchingGroups}
              submittingLabel="Recherche..."
            />
          </form>
          {groupsSearchCount !== null ? (
            <p className="field-hint">
              {groupsSearchCount} resultat(s) trouve(s).
            </p>
          ) : null}
          {selectedGroup ? (
            <div className="ad-selected-summary">
              <div className="ad-selection-summary-header">
                <div className="stack-row-main">
                  <strong>Groupe selectionne</strong>
                  <span>{selectedGroup.samAccountName}</span>
                  <span className="ad-selection-meta">{selectedGroup.displayName}</span>
                  <span className="ad-selection-meta">{selectedGroup.distinguishedName}</span>
                </div>
                <StatusBadge label="Groupe" tone="info" />
              </div>
            </div>
          ) : (
            <p className="field-hint">
              Selectionnez un groupe pour pre-remplir les actions d&apos;ajout et de
              retrait de membre.
            </p>
          )}
          {groupsSearchCount === 0 ? (
            <p className="field-hint">
              Aucun resultat groupe ne correspond a cette recherche.
            </p>
          ) : null}
          <div className="stack-list">
            {groupResults.map((group) => (
              <div
                className={`stack-row ${selectedGroup?.objectGuid === group.objectGuid ? "ad-selection-card-selected" : ""}`}
                key={group.objectGuid}
              >
                <div className="stack-row-main">
                  <strong>{group.samAccountName}</strong>
                  <span>{group.displayName}</span>
                  <span>{group.distinguishedName}</span>
                </div>
                <button
                  className="button button-secondary"
                  disabled={isSubmitting}
                  onClick={() => selectGroup(group)}
                  type="button"
                >
                  {selectedGroup?.objectGuid === group.objectGuid
                    ? "Selectionne"
                    : "Selectionner"}
                </button>
                <StatusBadge label="Groupe" tone="info" />
              </div>
            ))}
          </div>
        </SectionCard>
      </div>

      <div className="request-detail-layout">
        <SectionCard ariaLabel="Creer un utilisateur AD de test">
          <h2>Creer un utilisateur de test</h2>
          <form className="form-card compact-form-card" onSubmit={handleCreateUser}>
            <div className="form-grid">
              <label>
                SamAccountName
                <input
                  onChange={(event) =>
                    setNewUser((current) => ({
                      ...current,
                      samAccountName: event.target.value,
                    }))}
                  value={newUser.samAccountName}
                />
              </label>
              <label>
                DisplayName
                <input
                  onChange={(event) =>
                    setNewUser((current) => ({
                      ...current,
                      displayName: event.target.value,
                    }))}
                  value={newUser.displayName}
                />
              </label>
            </div>
            <div className="form-grid">
              <label>
                GivenName
                <input
                  onChange={(event) =>
                    setNewUser((current) => ({
                      ...current,
                      givenName: event.target.value,
                    }))}
                  value={newUser.givenName ?? ""}
                />
              </label>
              <label>
                Surname
                <input
                  onChange={(event) =>
                    setNewUser((current) => ({
                      ...current,
                      surname: event.target.value,
                    }))}
                  value={newUser.surname ?? ""}
                />
              </label>
            </div>
            <label>
              UserPrincipalName optionnel
              <input
                onChange={(event) =>
                  setNewUser((current) => ({
                    ...current,
                    userPrincipalName: event.target.value,
                  }))}
                value={newUser.userPrincipalName ?? ""}
              />
            </label>
            <label>
              Description optionnelle
              <input
                onChange={(event) =>
                  setNewUser((current) => ({
                    ...current,
                    description: event.target.value,
                  }))}
                value={newUser.description ?? ""}
              />
            </label>
            <SubmitButton
              idleLabel="Creer l'utilisateur"
              isSubmitting={isSubmitting}
              submittingLabel="Creation..."
            />
          </form>
        </SectionCard>

        <SectionCard ariaLabel="Creer un groupe AD client">
          <h2>Creer un groupe client</h2>
          <form className="form-card compact-form-card" onSubmit={handleCreateGroup}>
            <label>
              SamAccountName
              <input
                onChange={(event) =>
                  setNewGroup((current) => ({
                    ...current,
                    samAccountName: event.target.value,
                  }))}
                value={newGroup.samAccountName}
              />
            </label>
            <label>
              DisplayName
              <input
                onChange={(event) =>
                  setNewGroup((current) => ({
                    ...current,
                    displayName: event.target.value,
                  }))}
                value={newGroup.displayName}
              />
            </label>
            <label>
              Description optionnelle
              <input
                onChange={(event) =>
                  setNewGroup((current) => ({
                    ...current,
                    description: event.target.value,
                  }))}
                value={newGroup.description ?? ""}
              />
            </label>
            <SubmitButton
              idleLabel="Creer le groupe"
              isSubmitting={isSubmitting}
              submittingLabel="Creation..."
            />
          </form>
        </SectionCard>
      </div>

      <div className="request-detail-layout">
        <SectionCard ariaLabel="Gestion des memberships AD">
          <h2>Ajouter ou retirer un membre</h2>
          <form className="form-card compact-form-card" onSubmit={handleAddMembership}>
            <p className="field-hint">
              Champs pre-remplis depuis les objets selectionnes, tout en restant
              modifiables si besoin.
            </p>
            <label>
              Groupe
              <select
                onChange={(event) => {
                  const nextGroup = findDirectoryObjectByGuid(
                    selectableGroups,
                    event.target.value,
                  );
                  if (nextGroup) {
                    selectGroup(nextGroup);
                    return;
                  }

                  setSelectedGroup(null);
                  setMembershipGroupSam("");
                }}
                value={selectedGroup?.objectGuid ?? ""}
              >
                <option value="">Selectionner un groupe</option>
                {selectableGroups.map((group) => (
                  <option key={group.objectGuid} value={group.objectGuid}>
                    {group.samAccountName} · {group.displayName}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Utilisateur
              <select
                onChange={(event) => {
                  const nextUser = findDirectoryObjectByGuid(
                    selectableUsers,
                    event.target.value,
                  );
                  if (nextUser) {
                    selectUser(nextUser);
                    return;
                  }

                  setSelectedUser(null);
                  setMembership({ userSamAccountName: "" });
                  setLifecycleSam("");
                }}
                value={selectedUser?.objectGuid ?? ""}
              >
                <option value="">Selectionner un utilisateur</option>
                {selectableUsers.map((user) => (
                  <option key={user.objectGuid} value={user.objectGuid}>
                    {user.samAccountName} · {user.displayName}
                  </option>
                ))}
              </select>
            </label>
            <div className="ad-button-row">
              <SubmitButton
                idleLabel="Ajouter au groupe"
                isSubmitting={isSubmitting}
                submittingLabel="Ajout..."
              />
              <button
                className="button button-secondary"
                disabled={isSubmitting}
                onClick={() => void handleRemoveMembership()}
                type="button"
              >
                Retirer du groupe
              </button>
            </div>
          </form>
        </SectionCard>

        <SectionCard ariaLabel="Desactivation et deplacement AD">
          <h2>Desactiver puis deplacer</h2>
          <form className="form-card compact-form-card" onSubmit={handleDisableUser}>
            <p className="field-hint">
              L&apos;utilisateur selectionne est repris automatiquement pour les
              actions de cycle de vie.
            </p>
            <label>
              Utilisateur
              <select
                onChange={(event) => {
                  const nextUser = findDirectoryObjectByGuid(
                    selectableUsers,
                    event.target.value,
                  );
                  if (nextUser) {
                    selectUser(nextUser);
                    return;
                  }

                  setSelectedUser(null);
                  setMembership({ userSamAccountName: "" });
                  setLifecycleSam("");
                }}
                value={selectedUser?.objectGuid ?? ""}
              >
                <option value="">Selectionner un utilisateur</option>
                {selectableUsers.map((user) => (
                  <option key={user.objectGuid} value={user.objectGuid}>
                    {user.samAccountName} · {user.displayName}
                  </option>
                ))}
              </select>
            </label>
            <div className="ad-button-row">
              <SubmitButton
                idleLabel="Desactiver"
                isSubmitting={isSubmitting}
                submittingLabel="Desactivation..."
              />
              <button
                className="button button-secondary"
                disabled={isSubmitting}
                onClick={() => void handleMoveToDisabled()}
                type="button"
              >
                Deplacer vers Disabled
              </button>
            </div>
          </form>
        </SectionCard>
      </div>

      <SectionCard ariaLabel="Groupes effectifs de l'utilisateur AD">
        <h2>Groupes effectifs</h2>
        <p className="field-hint">
          Lecture seule. Liste tous les groupes auxquels l&apos;utilisateur
          selectionne appartient (directement ou par imbrication). Verrouille
          au scope du client.
        </p>
        {selectedUser ? (
          <p className="field-hint">
            Utilisateur cible : <strong>{selectedUser.samAccountName}</strong>
            {effectiveGroupsForUserGuid !== null
              && effectiveGroupsForUserGuid !== selectedUser.objectGuid
              ? " — les resultats ci-dessous portent sur un autre utilisateur, relancer la recherche."
              : ""}
          </p>
        ) : (
          <p className="field-hint">
            Selectionnez d&apos;abord un utilisateur dans la section de
            recherche pour activer cette lecture.
          </p>
        )}
        <div className="ad-button-row">
          <button
            className="button button-secondary"
            disabled={isLoadingEffectiveGroups || !selectedUser}
            onClick={() => void handleLoadEffectiveGroups()}
            type="button"
          >
            {isLoadingEffectiveGroups
              ? "Chargement..."
              : "Lister les groupes effectifs"}
          </button>
        </div>
        {effectiveGroupsForUserGuid !== null
          && effectiveGroupsForUserGuid === selectedUser?.objectGuid
          && effectiveGroups.length === 0
          ? (
            <p className="field-hint">
              Aucun groupe effectif trouve pour cet utilisateur.
            </p>
          )
          : null}
        {effectiveGroups.length > 0
          && effectiveGroupsForUserGuid === selectedUser?.objectGuid
          ? (
            <div className="stack-list">
              {effectiveGroups.map((group) => (
                <div className="stack-row" key={group.objectGuid}>
                  <div className="stack-row-main">
                    <strong>{group.samAccountName}</strong>
                    <span>{group.displayName}</span>
                    <span>{group.distinguishedName}</span>
                  </div>
                  <StatusBadge label="Groupe" tone="info" />
                </div>
              ))}
            </div>
          )
          : null}
      </SectionCard>
    </div>
  );
}
