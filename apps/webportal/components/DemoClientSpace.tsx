"use client";

import Link from "next/link";
import { useEffect, useMemo, useRef, useState } from "react";

import { MetricCard } from "@/components/MetricCard";
import { SectionCard } from "@/components/SectionCard";
import { SectionHeading } from "@/components/SectionHeading";
import { StatusBadge } from "@/components/StatusBadge";
import {
  demoClientSpace,
  demoNavigation,
  type DemoBackupRun,
  type DemoInvoice,
  type DemoSectionId,
  type DemoTicket,
  type DemoUser,
} from "@/lib/demo-client-space/data";

type DemoClientSpaceProps = {
  section: DemoSectionId;
};

type ModalState =
  | { type: "invoice"; invoice: DemoInvoice }
  | { type: "ticket"; ticket: DemoTicket }
  | { type: "user"; user: DemoUser }
  | { type: "service"; name: string; text: string }
  | { type: "disabled"; action: string }
  | null;

const sectionPath: Record<DemoSectionId, string> = {
  dashboard: "/decouvrir-espace-client",
  services: "/decouvrir-espace-client/services",
  subscription: "/decouvrir-espace-client/abonnement",
  invoices: "/decouvrir-espace-client/factures",
  storage: "/decouvrir-espace-client/stockage",
  backups: "/decouvrir-espace-client/sauvegardes",
  users: "/decouvrir-espace-client/utilisateurs",
  support: "/decouvrir-espace-client/assistance",
  security: "/decouvrir-espace-client/securite",
  activity: "/decouvrir-espace-client/activite",
  profile: "/decouvrir-espace-client/profil",
};

export function DemoClientSpace({ section }: DemoClientSpaceProps) {
  const [modal, setModal] = useState<ModalState>(null);
  const [restoreConfirmation, setRestoreConfirmation] = useState(false);
  const [ticketConfirmation, setTicketConfirmation] = useState(false);
  const [backupFilter, setBackupFilter] = useState<"all" | "warnings">("all");
  const customer = demoClientSpace.customer;
  const filteredRuns = useMemo(
    () =>
      backupFilter === "warnings"
        ? demoClientSpace.backups.runs.filter((run) => run.result === "warning")
        : demoClientSpace.backups.runs,
    [backupFilter],
  );

  return (
    <div className="demo-client-page">
      <header className="demo-client-hero">
        <div>
          <span className="demo-client-mode">Compte de démonstration</span>
          <h1>Découvrez l&apos;espace client Zachary IT</h1>
          <p>
            Parcourez un compte fictif actif pour comprendre le suivi disponible
            après souscription : services, sauvegardes, stockage, factures,
            assistance et sécurité.
          </p>
        </div>
        <div className="demo-client-identity" aria-label="Compte affiche">
          <strong>{customer.organization}</strong>
          <span>{customer.pack}</span>
          <StatusBadge label="DEMO - compte fictif" tone="info" />
        </div>
      </header>

      <div className="demo-client-shell">
        <nav className="demo-client-sidebar" aria-label="Navigation demo">
          <div className="demo-client-sidebar-header">
            <span>Espace client</span>
            <strong>{customer.organization}</strong>
          </div>
          <ul>
            {demoNavigation.map((item) => (
              <li key={item.id}>
                <Link
                  aria-current={section === item.id ? "page" : undefined}
                  className={
                    section === item.id
                      ? "demo-client-nav-link demo-client-nav-active"
                      : "demo-client-nav-link"
                  }
                  href={sectionPath[item.id]}
                >
                  {item.label}
                </Link>
              </li>
            ))}
          </ul>
          <div className="demo-client-sidebar-footer">
            <Link href="/offres">Découvrir les offres</Link>
            <Link href="/contact">Nous contacter</Link>
          </div>
        </nav>

        <main className="demo-client-content">
          <div className="demo-client-warning" role="note">
            Mode DEMO : données fictives, lecture seule, aucune connexion à
            une plateforme de facturation, un système de sauvegarde,
            l&apos;authentification ou une API client réelle.
          </div>
          {section === "dashboard" ? (
            <DashboardSection onOpen={setModal} />
          ) : null}
          {section === "services" ? <ServicesSection onOpen={setModal} /> : null}
          {section === "subscription" ? (
            <SubscriptionSection onOpen={setModal} />
          ) : null}
          {section === "invoices" ? <InvoicesSection onOpen={setModal} /> : null}
          {section === "storage" ? <StorageSection /> : null}
          {section === "backups" ? (
            <BackupsSection
              backupFilter={backupFilter}
              filteredRuns={filteredRuns}
              restoreConfirmation={restoreConfirmation}
              setBackupFilter={setBackupFilter}
              setRestoreConfirmation={setRestoreConfirmation}
            />
          ) : null}
          {section === "users" ? <UsersSection onOpen={setModal} /> : null}
          {section === "support" ? (
            <SupportSection
              onOpen={setModal}
              ticketConfirmation={ticketConfirmation}
              setTicketConfirmation={setTicketConfirmation}
            />
          ) : null}
          {section === "security" ? <SecuritySection /> : null}
          {section === "activity" ? <ActivitySection /> : null}
          {section === "profile" ? <ProfileSection onOpen={setModal} /> : null}
        </main>
      </div>

      <section className="demo-client-cta" aria-label="Contact commercial">
        <div>
          <span className="card-kicker">Offre Pro / Association</span>
          <h2>Vous souhaitez disposer de cet espace pour votre organisation ?</h2>
          <p>
            La démo montre un compte fictif. Les offres et conditions réelles se
            consultent depuis la vitrine Zachary IT.
          </p>
        </div>
        <div className="button-row">
          <Link className="button" href="/offres">
            Découvrir les offres
          </Link>
          <Link className="button button-secondary" href="/contact">
            Nous contacter
          </Link>
        </div>
      </section>

      {modal ? <DemoModal modal={modal} onClose={() => setModal(null)} /> : null}
    </div>
  );
}

function DashboardSection({ onOpen }: { onOpen: (modal: ModalState) => void }) {
  const data = demoClientSpace;

  return (
    <>
      <div className="demo-client-section-title">
        <div>
          <span className="eyebrow">Vue d&apos;ensemble</span>
          <h2>Bonjour, {data.customer.organization}</h2>
          <p>{data.customer.pack} - compte actif depuis {data.customer.since}.</p>
        </div>
        <StatusBadge label="Compte actif" tone="success" />
      </div>

      <section className="metrics-grid demo-client-metrics" aria-label="Resume">
        <MetricCard
          detail="Tous les services de l'offre sont actifs"
          label="Services actifs"
          tone="green"
          value={String(data.summary.activeServices)}
        />
        <MetricCard
          detail="Comptes fictifs rattachés"
          label="Utilisateurs"
          value={String(data.summary.users)}
        />
        <MetricCard
          detail={`${data.summary.storageUsed} / ${data.summary.storageTotal}`}
          label="Stockage utilisé"
          tone="amber"
          value={`${data.summary.storagePercent} %`}
        />
        <MetricCard
          detail={`Dernière sauvegarde : ${data.summary.lastBackup}`}
          label="Sauvegarde"
          tone="green"
          value="Protégé"
        />
      </section>

      <div className="dashboard-layout demo-client-dashboard-layout">
        <SectionCard ariaLabel="Services actifs">
          <SectionHeading
            action={<Link href={sectionPath.services}>Tout voir</Link>}
            description="Les bénéfices de l'offre sont visibles avant les détails techniques."
            title="Services disponibles"
          />
          <div className="demo-client-service-list">
            {data.services.slice(0, 6).map((service) => (
              <button
                className="demo-client-list-button"
                key={service.id}
                onClick={() =>
                  onOpen({
                    type: "service",
                    name: service.name,
                    text: service.summary,
                  })}
                type="button"
              >
                <span>✓ {service.name}</span>
                <small>{service.included}</small>
              </button>
            ))}
          </div>
        </SectionCard>

        <SectionCard ariaLabel="Notifications">
          <SectionHeading title="Notifications" />
          <div className="stack-list">
            {data.notifications.map((notification) => (
              <article className="stack-row" key={notification.title}>
                <div className="stack-row-main">
                  <strong>{notification.title}</strong>
                  <span>{notification.message}</span>
                </div>
              </article>
            ))}
          </div>
        </SectionCard>
      </div>

      <SectionCard ariaLabel="Activité récente">
        <SectionHeading
          action={<Link href={sectionPath.activity}>Voir l&apos;activite</Link>}
          title="Activité récente"
        />
        <Timeline items={data.activity.slice(0, 5)} />
      </SectionCard>
    </>
  );
}

function ServicesSection({ onOpen }: { onOpen: (modal: ModalState) => void }) {
  return (
    <SectionCard ariaLabel="Liste des services">
      <SectionHeading
        description="Chaque service est actif dans ce compte fictif Offre Pro / Association."
        title="Services actifs"
      />
      <div className="demo-client-card-grid">
        {demoClientSpace.services.map((service) => (
          <article className="demo-client-mini-card" key={service.id}>
            <StatusBadge label="✓ Actif" tone="success" />
            <h3>{service.name}</h3>
            <p>{service.summary}</p>
            <button
              className="button button-secondary button-compact"
              onClick={() =>
                onOpen({ type: "service", name: service.name, text: service.included })}
              type="button"
            >
              Details
            </button>
          </article>
        ))}
      </div>
    </SectionCard>
  );
}

function SubscriptionSection({ onOpen }: { onOpen: (modal: ModalState) => void }) {
  const subscription = demoClientSpace.subscription;

  return (
    <>
      <SectionCard ariaLabel="Abonnement">
        <SectionHeading
          action={<StatusBadge label="✓ Actif" tone="success" />}
          description="Abonnement fictif, sans lien avec une plateforme de paiement ou une facturation réelle."
          title={subscription.plan}
        />
        <dl className="profile-details">
          <Detail label="Statut" value={subscription.status} />
          <Detail label="Cycle" value={subscription.cycle} />
          <Detail label="Date de souscription" value={subscription.subscribedAt} />
          <Detail label="Prochaine échéance" value={subscription.nextBillingAt} />
          <Detail label="Stockage inclus" value={subscription.storageIncluded} />
          <Detail label="Utilisateurs" value={subscription.users} />
          <Detail label="Mensualite" value={subscription.monthlyPrice} />
          <Detail label="Services" value={subscription.services} />
        </dl>
        <div className="button-row">
          <button
            className="button button-secondary"
            onClick={() =>
              onOpen({ type: "disabled", action: "Changer l'abonnement" })}
            type="button"
          >
            Changer l&apos;abonnement
          </button>
          <button
            className="button button-secondary"
            onClick={() => onOpen({ type: "disabled", action: "Annuler le service" })}
            type="button"
          >
            Annuler un service
          </button>
        </div>
      </SectionCard>

      <SectionCard ariaLabel="Historique du plan">
        <SectionHeading title="Historique du plan" />
        <ul className="demo-client-simple-list">
          {subscription.history.map((entry) => (
            <li key={entry}>{entry}</li>
          ))}
        </ul>
      </SectionCard>
    </>
  );
}

function InvoicesSection({ onOpen }: { onOpen: (modal: ModalState) => void }) {
  return (
    <>
      <section className="metrics-grid metrics-grid-three demo-client-metrics">
        <MetricCard
          detail="Factures fictives affichées"
          label="Factures"
          value={String(demoClientSpace.invoices.length)}
        />
        <MetricCard
          detail="Aucune action de paiement réelle"
          label="A payer"
          tone="green"
          value="0 EUR"
        />
        <MetricCard
          detail="Cycle mensuel de l'offre"
          label="Dernière facture"
          tone="amber"
          value="39,90 EUR"
        />
      </section>
      <section className="table-card">
        <table className="invoice-table">
          <thead>
            <tr>
              <th>Référence</th>
              <th>Date</th>
              <th>Objet</th>
              <th>Statut</th>
              <th>Montant</th>
              <th>Document</th>
            </tr>
          </thead>
          <tbody>
            {demoClientSpace.invoices.map((invoice) => (
              <tr key={invoice.reference}>
                <td data-label="Référence">{invoice.reference}</td>
                <td data-label="Date">{invoice.date}</td>
                <td data-label="Objet">{invoice.title}</td>
                <td data-label="Statut">
                  <StatusBadge label="Payée" tone="success" />
                </td>
                <td data-label="Montant">{invoice.amount}</td>
                <td data-label="Document">
                  <button
                    className="button button-compact button-secondary"
                    onClick={() => onOpen({ type: "invoice", invoice })}
                    type="button"
                  >
                    Ouvrir
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </>
  );
}

function StorageSection() {
  const storage = demoClientSpace.storage;

  return (
    <>
      <SectionCard ariaLabel="Stockage utilisé">
        <SectionHeading
          description={`${storage.used} utilisés sur ${storage.total}, soit ${storage.percent} %.`}
          title="Stockage utilisé"
        />
        <div
          aria-label={`Stockage utilisé ${storage.percent} pour cent`}
          className="demo-client-progress"
          role="img"
        >
          <span style={{ width: `${storage.percent}%` }} />
        </div>
        <dl className="profile-details">
          <Detail label="Capacite totale" value={storage.total} />
          <Detail label="Espace utilisé" value={storage.used} />
          <Detail label="Disponible" value={storage.available} />
          <Detail label="Dossiers principaux" value={String(storage.folders.length)} />
        </dl>
      </SectionCard>
      <div className="dashboard-layout demo-client-dashboard-layout">
        <SectionCard ariaLabel="Repartition">
          <SectionHeading title="Repartition fictive" />
          <div className="demo-client-bars">
            {storage.categories.map((item) => (
              <div className="demo-client-bar-row" key={item.label}>
                <div>
                  <strong>{item.label}</strong>
                  <span>{item.value}</span>
                </div>
                <div className="demo-client-bar" aria-hidden="true">
                  <span style={{ width: `${item.percent}%` }} />
                </div>
              </div>
            ))}
          </div>
        </SectionCard>
        <SectionCard ariaLabel="Evolution">
          <SectionHeading title="Evolution sur 6 mois" />
          <div className="demo-client-chart" aria-label="Historique stockage">
            {storage.history.map((item) => (
              <div key={item.month}>
                <span style={{ height: `${Math.round(item.value * 5)}px` }} />
                <small>{item.month}</small>
              </div>
            ))}
          </div>
        </SectionCard>
      </div>
    </>
  );
}

function BackupsSection({
  backupFilter,
  filteredRuns,
  restoreConfirmation,
  setBackupFilter,
  setRestoreConfirmation,
}: {
  backupFilter: "all" | "warnings";
  filteredRuns: readonly DemoBackupRun[];
  restoreConfirmation: boolean;
  setBackupFilter: (value: "all" | "warnings") => void;
  setRestoreConfirmation: (value: boolean) => void;
}) {
  const backups = demoClientSpace.backups;

  return (
    <>
      <SectionCard ariaLabel="État sauvegarde">
        <SectionHeading
          action={<StatusBadge label="✓ Protégé" tone="success" />}
          description="Simulation de suivi de sauvegarde : aucune interrogation d'une infrastructure réelle."
          title="Vos données sont protégées"
        />
        <dl className="profile-details">
          <Detail label="Dernière exécution" value={backups.lastRun} />
          <Detail label="Dernière réussite" value={backups.lastSuccess} />
          <Detail label="Données protégées" value={backups.protectedData} />
          <Detail label="Retention" value={backups.retention} />
          <Detail label="Prochaine exécution" value={backups.nextRun} />
          <Detail label="Vérification" value={backups.verification} />
        </dl>
      </SectionCard>

      <SectionCard ariaLabel="Demande restauration">
        <SectionHeading
          description="La soumission est simulée en local et ne crée aucun ticket réel."
          title="Demander une restauration"
        />
        <form
          className="backup-restore-form"
          onSubmit={(event) => {
            event.preventDefault();
            setRestoreConfirmation(true);
          }}
        >
          <label>
            Element a restaurer
            <input defaultValue="Comptabilité / Factures 2026" name="item" />
          </label>
          <label>
            Date souhaitee
            <input defaultValue="06/08/2026" name="date" />
          </label>
          <label>
            Priorite
            <select defaultValue="Normale" name="priority">
              <option>Normale</option>
              <option>Haute</option>
            </select>
          </label>
          <label className="backup-restore-form-wide">
            Description
            <textarea
              defaultValue="Fichier supprimé accidentellement."
              name="description"
            />
          </label>
          <div className="backup-restore-actions">
            <button className="button" type="submit">
              Demander une restauration
            </button>
            {restoreConfirmation ? (
              <p className="demo-client-confirmation" role="status">
                Demande de démonstration enregistrée. Dans un véritable espace
                client, votre demande serait transmise au support Zachary IT.
              </p>
            ) : null}
          </div>
        </form>
      </SectionCard>

      <SectionCard ariaLabel="Historique sauvegardes">
        <SectionHeading
          action={
            <div className="demo-client-filter" aria-label="Filtrer l'historique">
              <button
                aria-pressed={backupFilter === "all"}
                className={backupFilter === "all" ? "is-active" : ""}
                onClick={() => setBackupFilter("all")}
                type="button"
              >
                Tout
              </button>
              <button
                aria-pressed={backupFilter === "warnings"}
                className={backupFilter === "warnings" ? "is-active" : ""}
                onClick={() => setBackupFilter("warnings")}
                type="button"
              >
                Avertissements
              </button>
            </div>
          }
          title="Historique fictif"
        />
        <div className="backup-run-list">
          {filteredRuns.map((run) => (
            <article className="backup-run-row" key={run.date}>
              <div>
                <strong>{run.date}</strong>
                <span>{run.duration}</span>
              </div>
              <StatusBadge
                label={run.result === "success" ? `✓ ${run.label}` : `! ${run.label}`}
                tone={run.result === "success" ? "success" : "warning"}
              />
              <span>{run.protectedData}</span>
            </article>
          ))}
        </div>
      </SectionCard>
    </>
  );
}

function UsersSection({ onOpen }: { onOpen: (modal: ModalState) => void }) {
  return (
    <SectionCard ariaLabel="Utilisateurs">
      <SectionHeading
        action={<StatusBadge label="6 actifs" tone="success" />}
        description="Personnes fictives créées uniquement pour la démonstration."
        title="Utilisateurs"
      />
      <div className="demo-client-card-grid">
        {demoClientSpace.users.map((user) => (
          <article className="demo-client-mini-card" key={user.name}>
            <StatusBadge label={user.status} tone="success" />
            <h3>{user.name}</h3>
            <p>{user.role}</p>
            <p className="field-hint">Dernière connexion : {user.lastLogin}</p>
            <button
              className="button button-secondary button-compact"
              onClick={() => onOpen({ type: "user", user })}
              type="button"
            >
              Ouvrir
            </button>
          </article>
        ))}
      </div>
    </SectionCard>
  );
}

function SupportSection({
  onOpen,
  ticketConfirmation,
  setTicketConfirmation,
}: {
  onOpen: (modal: ModalState) => void;
  ticketConfirmation: boolean;
  setTicketConfirmation: (value: boolean) => void;
}) {
  return (
    <>
      <SectionCard ariaLabel="Créer une demande support">
        <SectionHeading
          description="Formulaire simulé : aucun ticket réel n'est créé."
          title="Nouvelle demande"
        />
        <form
          className="backup-restore-form"
          onSubmit={(event) => {
            event.preventDefault();
            setTicketConfirmation(true);
          }}
        >
          <label>
            Service
            <select defaultValue="Sauvegarde automatique" name="service">
              {demoClientSpace.services.map((service) => (
                <option key={service.id}>{service.name}</option>
              ))}
            </select>
          </label>
          <label>
            Priorité
            <select defaultValue="Normale" name="priority">
              <option>Normale</option>
              <option>Haute</option>
            </select>
          </label>
          <label>
            Objet
            <input defaultValue="Question sur une restauration" name="subject" />
          </label>
          <label className="backup-restore-form-wide">
            Description
            <textarea defaultValue="Je souhaite vérifier une version sauvegardée." />
          </label>
          <div className="backup-restore-actions">
            <button className="button" type="submit">
              Créer une demande démo
            </button>
            {ticketConfirmation ? (
              <p className="demo-client-confirmation" role="status">
                Ticket de démonstration préparé. Dans le vrai portail, il serait
                transmis au support Zachary IT.
              </p>
            ) : null}
          </div>
        </form>
      </SectionCard>

      <SectionCard ariaLabel="Tickets support">
        <SectionHeading title="Tickets fictifs" />
        <ul className="support-request-list">
          {demoClientSpace.tickets.map((ticket) => (
            <li className="support-request-row" key={ticket.reference}>
              <div className="support-request-row-main">
                <div className="support-request-row-head">
                  <span className="card-kicker">#{ticket.reference}</span>
                  <StatusBadge
                    label={ticket.status}
                    tone={ticket.status === "En cours" ? "warning" : "success"}
                  />
                </div>
                <h3>{ticket.subject}</h3>
                <p className="support-request-row-meta">
                  {ticket.category} - Priorite {ticket.priority} - {ticket.date}
                </p>
              </div>
              <button
                className="button button-secondary button-compact"
                onClick={() => onOpen({ type: "ticket", ticket })}
                type="button"
              >
                Consulter
              </button>
            </li>
          ))}
        </ul>
      </SectionCard>
    </>
  );
}

function SecuritySection() {
  return (
    <>
      <SectionCard ariaLabel="Sécurité du compte">
        <SectionHeading
          action={<StatusBadge label="Aucune alerte" tone="success" />}
          title="Sécurité du compte"
        />
        <ul className="demo-client-security-list">
          {demoClientSpace.security.checks.map((check) => (
            <li key={check}>✓ {check}</li>
          ))}
        </ul>
      </SectionCard>
      <SectionCard ariaLabel="Connexions récentes">
        <SectionHeading title="Connexions récentes" />
        <div className="stack-list">
          {demoClientSpace.security.logins.map((login) => (
            <article className="stack-row" key={`${login.date}-${login.ip}`}>
              <div className="stack-row-main">
                <strong>{login.location}</strong>
                <span>{login.date} - {login.ip}</span>
              </div>
              <StatusBadge label={login.result} tone="success" />
            </article>
          ))}
        </div>
      </SectionCard>
    </>
  );
}

function ActivitySection() {
  return (
    <SectionCard ariaLabel="Activité récente">
      <SectionHeading
        description="Événements fictifs pour donner un aperçu d'un compte utilisé depuis plusieurs mois."
        title="Activité récente"
      />
      <Timeline items={demoClientSpace.activity} />
    </SectionCard>
  );
}

function ProfileSection({ onOpen }: { onOpen: (modal: ModalState) => void }) {
  const customer = demoClientSpace.customer;

  return (
    <SectionCard ariaLabel="Profil de démonstration">
      <SectionHeading
        action={<StatusBadge label="Profil démo" tone="info" />}
        title={customer.organization}
      />
      <dl className="profile-details">
        <Detail label="Type" value={customer.type} />
        <Detail label="Référence client" value={customer.reference} />
        <Detail label="Offre" value={customer.pack} />
        <Detail label="Statut" value={customer.status} />
        <Detail label="Adresse" value={customer.address} />
        <Detail label="Téléphone" value={customer.phone} />
        <Detail label="E-mail" value={customer.email} />
        <Detail label="Client depuis" value={customer.since} />
      </dl>
      <div className="button-row">
        <button
          className="button button-secondary"
          onClick={() => onOpen({ type: "disabled", action: "Modifier la facturation" })}
          type="button"
        >
          Modifier fictivement
        </button>
      </div>
    </SectionCard>
  );
}

function Timeline({
  items,
}: {
  items: readonly { time: string; text: string; type: string }[];
}) {
  return (
    <ol className="demo-client-timeline">
      {items.map((item) => (
        <li key={`${item.time}-${item.text}`}>
          <span>{item.time}</span>
          <strong>{item.text}</strong>
          <small>{item.type}</small>
        </li>
      ))}
    </ol>
  );
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}

function DemoModal({
  modal,
  onClose,
}: {
  modal: NonNullable<ModalState>;
  onClose: () => void;
}) {
  const closeButtonRef = useRef<HTMLButtonElement | null>(null);
  let title = "";
  let content = null;

  useEffect(() => {
    closeButtonRef.current?.focus();

    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === "Escape") {
        onClose();
      }
    }

    window.addEventListener("keydown", closeOnEscape);
    return () => window.removeEventListener("keydown", closeOnEscape);
  }, [onClose]);

  if (modal.type === "invoice") {
    title = modal.invoice.reference;
    content = (
      <>
        <p>
          Aperçu fictif de facture de démonstration. Aucun PDF de paiement ou
          document de production n&apos;est chargé.
        </p>
        <dl className="profile-details">
          <Detail label="Date" value={modal.invoice.date} />
          <Detail label="Objet" value={modal.invoice.title} />
          <Detail label="Statut" value="Payée" />
          <Detail label="Montant" value={modal.invoice.amount} />
        </dl>
      </>
    );
  }

  if (modal.type === "ticket") {
    title = `Ticket #${modal.ticket.reference}`;
    content = (
      <div className="stack-list">
        {modal.ticket.messages.map((message) => (
          <article className="stack-row" key={`${message.author}-${message.date}`}>
            <div className="stack-row-main">
              <strong>{message.author}</strong>
              <span>{message.date}</span>
              <p>{message.text}</p>
            </div>
          </article>
        ))}
      </div>
    );
  }

  if (modal.type === "user") {
    title = modal.user.name;
    content = (
      <>
        <dl className="profile-details">
          <Detail label="Rôle" value={modal.user.role} />
          <Detail label="Statut" value={modal.user.status} />
          <Detail label="Dernière connexion" value={modal.user.lastLogin} />
          <Detail label="Services" value={modal.user.services.join(", ")} />
        </dl>
        <button
          className="button button-secondary"
          onClick={onClose}
          type="button"
        >
          Fermer
        </button>
      </>
    );
  }

  if (modal.type === "service") {
    title = modal.name;
    content = <p>{modal.text}</p>;
  }

  if (modal.type === "disabled") {
    title = modal.action;
    content = (
      <p>
        Cette action est désactivée dans le compte de démonstration.
      </p>
    );
  }

  return (
    <div
      aria-labelledby="demo-modal-title"
      aria-modal="true"
      className="demo-client-modal-backdrop"
      role="dialog"
    >
      <div className="demo-client-modal">
        <div className="section-heading">
          <div>
            <span className="card-kicker">Demonstration</span>
            <h2 id="demo-modal-title">{title}</h2>
          </div>
          <button
            aria-label="Fermer la fenetre"
            className="button button-secondary button-compact"
            onClick={onClose}
            ref={closeButtonRef}
            type="button"
          >
            Fermer
          </button>
        </div>
        {content}
      </div>
    </div>
  );
}
