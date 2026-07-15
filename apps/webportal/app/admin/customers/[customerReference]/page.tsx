import Link from "next/link";
import { notFound } from "next/navigation";

import { AdminDataTable } from "@/components/AdminDataTable";
import { AuditEventBadge } from "@/components/AuditEventBadge";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { RequestAttentionBadge } from "@/components/RequestAttentionBadge";
import { RequestStatusBadge } from "@/components/RequestStatusBadge";
import { SectionCard } from "@/components/SectionCard";
import { StatusBadge } from "@/components/StatusBadge";
import { requireAdminSession } from "@/lib/auth";
import {
  commercialDocumentStatus,
  formatCurrency,
  formatCurrencyFromCents,
  formatDate,
  formatDateTime,
  invoiceStatus,
  serviceStatus,
} from "@/lib/formatters";
import {
  getAdminCustomer,
  getAdminCustomerAdWorkspace,
} from "@/lib/internal-api";

export const metadata = { title: "Fiche client - Administration" };
export const dynamic = "force-dynamic";

type PageProps = { params: Promise<{ customerReference: string }> };

export default async function AdminCustomerDetailPage({
  params,
}: PageProps) {
  await requireAdminSession();
  const { customerReference } = await params;
  const [result, adWorkspaceResult] = await Promise.all([
    getAdminCustomer(customerReference),
    getAdminCustomerAdWorkspace(customerReference),
  ]);

  if (result.error) {
    return (
      <ErrorState
        action={<Link className="button" href="/admin/customers">Retour</Link>}
        description="Impossible de charger cette fiche client."
        reference={result.correlationId}
        title="Fiche client indisponible"
      />
    );
  }

  if (!result.data) {
    notFound();
  }

  const customer = result.data;
  const identity = customer.identity;
  const adWorkspace = adWorkspaceResult.data;
  const adMode = adWorkspace?.adStatus?.mode ?? "indisponible";
  const adModeLabelFr = localizeAdMode(adWorkspace?.adStatus?.mode);
  const adModeDescription = describeAdMode(
    adWorkspace?.adStatus?.mode,
    adWorkspace?.adStatus?.writesEnabled ?? false,
  );

  return (
    <>
      <PageHeader
        action={
          <StatusBadge
            label={identity.accountStatus === "active" ? "Compte actif" : "En attente"}
            tone={identity.accountStatus === "active" ? "success" : "warning"}
          />
        }
        description={`${identity.contactName} · ${identity.email || "Adresse e-mail non renseignée"}`}
        eyebrow={identity.customerReference}
        title={identity.companyName}
      />

      <section className="metrics-grid admin-metrics" aria-label="Synthèse client">
        <div className="metric-card">
          <span className="metric-label">Services actifs</span>
          <strong className="metric-value">{customer.activeServiceCount}</strong>
          <span className="metric-detail">Sur {customer.services.length} service(s) affiché(s)</span>
        </div>
        <div className="metric-card metric-amber">
          <span className="metric-label">Support ouvert</span>
          <strong className="metric-value">{customer.openSupportRequestCount}</strong>
          <span className="metric-detail">Demandes support non clôturées</span>
        </div>
        <div className="metric-card metric-slate">
          <span className="metric-label">Demandes service actives</span>
          <strong className="metric-value">{customer.activeServiceRequestCount}</strong>
          <span className="metric-detail">Reçues, en étude ou acceptées</span>
        </div>
        <div className="metric-card metric-amber">
          <span className="metric-label">Factures en attente</span>
          <strong className="metric-value">{customer.pendingInvoiceCount}</strong>
          <span className="metric-detail">Suivi informatif uniquement</span>
        </div>
        <div className="metric-card metric-green">
          <span className="metric-label">Docs partagés</span>
          <strong className="metric-value">{customer.sharedCommercialDocumentCount}</strong>
          <span className="metric-detail">Documents commerciaux visibles côté client</span>
        </div>
      </section>

      <div className="request-detail-layout">
        <SectionCard ariaLabel="Identité client consolidée">
          <h2>Identité et contrat de compte</h2>
          <dl className="profile-details">
            <div>
              <dt>Référence client</dt>
              <dd>{identity.customerReference}</dd>
            </div>
            <div>
              <dt>Statut client</dt>
              <dd>{identity.accountStatus}</dd>
            </div>
            <div>
              <dt>Contact principal</dt>
              <dd>{identity.contactName}</dd>
            </div>
            <div>
              <dt>E-mail</dt>
              <dd>{identity.email || "Non renseigné"}</dd>
            </div>
            <div>
              <dt>Téléphone</dt>
              <dd>{identity.phone || "Non renseigné"}</dd>
            </div>
            <div>
              <dt>Adresse</dt>
              <dd>
                {identity.address || "Non renseignée"}
                {identity.city || identity.country ? (
                  <>
                    <br />
                    {[identity.city, identity.country].filter(Boolean).join(", ")}
                  </>
                ) : null}
              </dd>
            </div>
            <div>
              <dt>Création</dt>
              <dd>{formatDateTime(customer.createdAt)}</dd>
            </div>
            <div>
              <dt>Dernière activité</dt>
              <dd>{formatDateTime(customer.lastActivityAt)}</dd>
            </div>
          </dl>
        </SectionCard>

        <SectionCard ariaLabel="Sécurité et préparation recette">
          <h2>Sécurité et préparation recette</h2>
          <div className="security-item">
            <div>
              <strong>Utilisateurs portail</strong>
              <span>
                {customer.activePortalUserCount} actif(s) sur {customer.portalUserCount}
              </span>
            </div>
            <StatusBadge label="Lecture seule" tone="info" />
          </div>
          <div className="security-item">
            <div>
              <strong>Sessions actives</strong>
              <span>Sessions non révoquées et non expirées</span>
            </div>
            <StatusBadge label={String(customer.activeSessionCount)} tone="success" />
          </div>
          <div className="security-item">
            <div>
              <strong>Isolation métier</strong>
              <span>
                Les services, demandes, documents et factures restent rattachés à{" "}
                <code>{identity.customerReference}</code>.
              </span>
            </div>
          </div>
          <div className="security-item">
            <div>
              <strong>Active Directory V0.18</strong>
              <span>
                Mode courant : {adModeLabelFr} (<code>{adMode}</code>).{" "}
                {adModeDescription} Aucune suppression définitive AD n&apos;est
                exposée et aucun flux n&apos;est autorisé hors de l&apos;OU de
                test.
              </span>
            </div>
            <StatusBadge
              label={adWorkspace?.adStatus?.writesEnabled
                ? "Écriture contrôlée bornée"
                : "Sans écriture réelle"}
              tone={adWorkspace?.adStatus?.writesEnabled ? "warning" : "info"}
            />
          </div>
        </SectionCard>

        <SectionCard ariaLabel="Résumé Active Directory client">
          <div className="section-heading">
            <div>
              <h2>Résumé Active Directory</h2>
              <p>
                Les actions AD détaillées sont maintenant regroupées sur une
                page dédiée pour alléger cette fiche client.
              </p>
            </div>
            <Link
              className="button"
              href={`/admin/customers/${encodeURIComponent(identity.customerReference)}/active-directory`}
            >
              Gérer Active Directory
            </Link>
          </div>
          <dl className="profile-details">
            <div>
              <dt>Statut AD</dt>
              <dd>{localizeProvisioningStatus(adWorkspace?.provisioningStatus)}</dd>
            </div>
            <div>
              <dt>Mode</dt>
              <dd>{adModeLabelFr}</dd>
            </div>
            <div>
              <dt>Utilisateurs liés</dt>
              <dd>{String(adWorkspace?.linkedUsers.length ?? 0)}</dd>
            </div>
            <div>
              <dt>Dernier résultat</dt>
              <dd>{adWorkspace?.lastResultCode ?? "Aucun résultat"}</dd>
            </div>
          </dl>
          {adWorkspaceResult.error ? (
            <p className="field-hint">
              Le résumé AD n&apos;a pas pu être chargé pour le moment.
            </p>
          ) : null}
        </SectionCard>
      </div>

      <div className="request-detail-layout">
        <SectionCard ariaLabel="Services associés">
          <h2>Services associés</h2>
          {customer.services.length === 0 ? (
            <EmptyState
              description="Aucun service n'est associé à ce client."
              title="Aucun service"
            />
          ) : (
            <div className="stack-list">
              {customer.services.map((service) => {
                const status = serviceStatus[service.status];
                return (
                  <article className="stack-row" key={service.id}>
                    <div className="stack-row-main">
                      <strong>{service.name}</strong>
                      <span>{service.reference} · {service.scope}</span>
                    </div>
                    <StatusBadge label={status.label} tone={status.tone} />
                  </article>
                );
              })}
            </div>
          )}
        </SectionCard>

        <SectionCard ariaLabel="Activité récente client">
          <h2>Activité récente</h2>
          {customer.recentActivity.length === 0 ? (
            <EmptyState
              description="Aucune activité publique récente n'est disponible."
              title="Aucune activité"
            />
          ) : (
            <AdminDataTable
              caption="Activité récente du client"
              columns={["Date", "Demande", "Auteur", "Statut", "Détail"]}
              rows={customer.recentActivity.map((item) => [
                formatDateTime(item.occurredAt),
                <div key={`${item.requestType}-${item.requestId}`}>
                  <strong>{item.reference}</strong>
                  <br />
                  <small>{item.subject}</small>
                </div>,
                item.authorType === "client" ? "Réponse client" : "Équipe Kermaria",
                <RequestStatusBadge
                  key={`${item.requestType}-${item.requestId}-status`}
                  requestType={item.requestType}
                  status={item.status}
                />,
                <Link
                  className="table-action"
                  href={
                    item.requestType === "support"
                      ? `/admin/support-requests/${encodeURIComponent(item.requestId)}`
                      : `/admin/service-requests/${encodeURIComponent(item.requestId)}`
                  }
                  key={`${item.requestType}-${item.requestId}-detail`}
                >
                  Consulter
                </Link>,
              ])}
            />
          )}
        </SectionCard>
      </div>

      <SectionCard ariaLabel="Demandes support associées" className="stack-panel">
        <div className="section-heading">
          <div>
            <h2>Demandes support associées</h2>
            <p>Suivi des demandes support rattachées à cette organisation.</p>
          </div>
          <Link className="section-action" href="/admin/support-requests">
            Vue globale
          </Link>
        </div>
        {customer.supportRequests.length === 0 ? (
          <EmptyState
            description="Aucune demande support n'est associée à ce client."
            title="Aucune demande support"
          />
        ) : (
          <AdminDataTable
            caption="Demandes support du client"
            columns={["Référence", "Sujet", "Priorité", "Statut", "Attention", "Détail"]}
            rows={customer.supportRequests.map((request) => [
              <code key={`${request.id}-reference`}>{request.reference}</code>,
              request.subject,
              request.priority,
              <RequestStatusBadge
                key={`${request.id}-status`}
                requestType="support"
                status={request.status}
              />,
              <RequestAttentionBadge
                hasRecentClientReply={request.hasRecentClientReply}
                key={`${request.id}-attention`}
                requiresAttention={request.requiresAttention}
              />,
              <Link
                className="table-action"
                href={`/admin/support-requests/${encodeURIComponent(request.id)}`}
                key={`${request.id}-detail`}
              >
                Consulter
              </Link>,
            ])}
          />
        )}
      </SectionCard>

      <SectionCard ariaLabel="Demandes de service associées" className="stack-panel">
        <div className="section-heading">
          <div>
            <h2>Demandes de service associées</h2>
            <p>Qualification commerciale et technique sans provisioning réel.</p>
          </div>
          <Link className="section-action" href="/admin/service-requests">
            Vue globale
          </Link>
        </div>
        {customer.serviceRequests.length === 0 ? (
          <EmptyState
            description="Aucune demande de service n'est associée à ce client."
            title="Aucune demande de service"
          />
        ) : (
          <AdminDataTable
            caption="Demandes de service du client"
            columns={["Référence", "Catalogue", "Sujet", "Statut", "Attention", "Détail"]}
            rows={customer.serviceRequests.map((request) => [
              <code key={`${request.id}-reference`}>{request.reference}</code>,
              request.catalogItemName,
              request.subject,
              <RequestStatusBadge
                key={`${request.id}-status`}
                requestType="service"
                status={request.status}
              />,
              <RequestAttentionBadge
                hasRecentClientReply={request.hasRecentClientReply}
                key={`${request.id}-attention`}
                requiresAttention={request.requiresAttention}
              />,
              <Link
                className="table-action"
                href={`/admin/service-requests/${encodeURIComponent(request.id)}`}
                key={`${request.id}-detail`}
              >
                Consulter
              </Link>,
            ])}
          />
        )}
      </SectionCard>

      <div className="request-detail-layout">
        <SectionCard ariaLabel="Documents commerciaux associés">
          <div className="section-heading">
            <div>
              <h2>Documents commerciaux associés</h2>
              <p>Documents informatifs uniquement, sans paiement ni facture fiscale réelle.</p>
            </div>
            <Link
              className="section-action"
              href={`/admin/commercial-documents?customerReference=${encodeURIComponent(identity.customerReference)}`}
            >
              Préparer ou revoir
            </Link>
          </div>
          {customer.commercialDocuments.length === 0 ? (
            <EmptyState
              description="Aucun document commercial n'est associé à ce client."
              title="Aucun document commercial"
            />
          ) : (
            <AdminDataTable
              caption="Documents commerciaux du client"
              columns={["Référence", "Titre", "Statut", "Montant", "Mise à jour"]}
              rows={customer.commercialDocuments.map((document) => {
                const status = commercialDocumentStatus[document.status];
                return [
                  <Link
                    className="table-action"
                    href={`/admin/commercial-documents/${encodeURIComponent(document.id)}`}
                    key={`${document.id}-detail`}
                  >
                    {document.internalReference}
                  </Link>,
                  document.title,
                  <StatusBadge
                    key={`${document.id}-status`}
                    label={status.label}
                    tone={status.tone}
                  />,
                  formatCurrencyFromCents(document.totalAmountCents),
                  formatDateTime(document.updatedAt),
                ];
              })}
            />
          )}
        </SectionCard>

        <SectionCard ariaLabel="Factures associées">
          <h2>Factures associées</h2>
          {customer.invoices.length === 0 ? (
            <EmptyState
              description="Aucune facture informative n'est associée à ce client."
              title="Aucune facture"
            />
          ) : (
            <AdminDataTable
              caption="Factures du client"
              columns={["Numéro", "Période", "Échéance", "Montant", "Statut"]}
              rows={customer.invoices.map((invoice) => {
                const status = invoiceStatus[invoice.status];
                return [
                  invoice.number,
                  invoice.period,
                  formatDate(invoice.dueAt),
                  formatCurrency(invoice.totalAmount),
                  <StatusBadge
                    key={`${invoice.id}-status`}
                    label={status.label}
                    tone={status.tone}
                  />,
                ];
              })}
            />
          )}
        </SectionCard>
      </div>

      <SectionCard ariaLabel="Audits récents du client" className="stack-panel">
        <h2>Audits récents du client</h2>
        {customer.recentAuditLogs.length === 0 ? (
          <EmptyState
            description="Aucun audit récent n'est disponible pour ce client."
            title="Aucun audit"
          />
        ) : (
          <AdminDataTable
            caption="Audits récents du client"
            columns={["Date", "Acteur", "Action", "Résultat", "Corrélation"]}
            rows={customer.recentAuditLogs.map((audit) => [
              formatDateTime(audit.occurredAt),
              audit.actor,
              audit.action,
              <AuditEventBadge
                key={`${audit.correlationId}-outcome`}
                outcome={audit.outcome}
              />,
              <code key={`${audit.correlationId}-id`}>{audit.correlationId}</code>,
            ])}
          />
        )}
      </SectionCard>

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}

function describeAdMode(mode: string | undefined, writesEnabled: boolean) {
  switch (mode) {
    case "disabled":
      return "Aucune connexion AD ni action AD n'est autorisée.";
    case "mock":
      return "Les résultats et mutations AD restent simulés.";
    case "read_only":
      return "Les lectures AD sont autorisées mais toutes les écritures sont refusées.";
    case "controlled_write":
      return writesEnabled
        ? "Les écritures AD réelles sont strictement bornées à OU=TEST_SITE_WEB,DC=home,DC=bzh."
        : "Le mode écriture contrôlée est configuré sans écriture disponible.";
    default:
      return "Le statut AD n'est pas disponible.";
  }
}

function localizeAdMode(mode: string | undefined) {
  switch (mode) {
    case "disabled":
      return "Désactivé";
    case "mock":
      return "Simulé";
    case "read_only":
      return "Lecture seule";
    case "controlled_write":
      return "Écriture contrôlée";
    default:
      return "Indisponible";
  }
}

function localizeProvisioningStatus(status: string | undefined) {
  switch (status) {
    case "succeeded":
      return "Synchronisé";
    case "ready":
      return "Prêt";
    case "failed":
      return "En échec";
    case "mixed":
      return "État mixte";
    case "not_required":
      return "Non requis";
    case "not_configured":
      return "À configurer";
    default:
      return "Indisponible";
  }
}


