"use client";

import Link from "next/link";
import { useDeferredValue, useMemo, useState } from "react";

import { StatusBadge } from "@/components/StatusBadge";
import { formatCurrencyFromCents } from "@/lib/formatters";
import { startingMonthlyPriceCents } from "@/lib/admin-catalog-presenters";
import type { BillingV2AdminCatalogSnapshot } from "@/lib/internal-api";
import { CatalogNavigation, CatalogField, adminCatalogStyles as styles } from "./AdminCatalogUi";

type Section = "services" | "formules" | "engagements";

export function CatalogHome({
  asOf,
  baselineByCode,
  section,
  snapshot,
}: {
  asOf: string;
  baselineByCode: Record<string, number>;
  section: Section;
  snapshot: BillingV2AdminCatalogSnapshot;
}) {
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState("all");
  const deferredQuery = useDeferredValue(query.trim().toLocaleLowerCase("fr-FR"));
  const title = section === "services" ? "Services" : section === "formules" ? "Formules" : "Engagements";

  const rows = useMemo(() => {
    const source = section === "services" ? snapshot.services : section === "formules" ? snapshot.presets : snapshot.commitments;
    return [...source]
      .sort((left, right) => left.displayOrder - right.displayOrder || left.code.localeCompare(right.code))
      .filter((item) => status === "all" || item.status === status)
      .filter((item) => !deferredQuery || `${item.name} ${item.code}`.toLocaleLowerCase("fr-FR").includes(deferredQuery));
  }, [deferredQuery, section, snapshot.commitments, snapshot.presets, snapshot.services, status]);

  const total = section === "services" ? snapshot.services.length : section === "formules" ? snapshot.presets.length : snapshot.commitments.length;
  const active = (section === "services" ? snapshot.services : section === "formules" ? snapshot.presets : snapshot.commitments)
    .filter((item) => item.status === "active").length;
  const secondary = section === "services"
    ? snapshot.services.reduce((sum, service) => sum + service.tiers.length, 0)
    : section === "formules"
      ? snapshot.presets.filter((preset) => preset.isPublic).length
      : snapshot.commitments.filter((commitment) => commitment.allowUpfrontPayment).length;

  return (
    <div className={styles.shell}>
      <CatalogNavigation active={section} />
      <div className={styles.summaryGrid}>
        <Summary label={`Total ${title.toLocaleLowerCase("fr-FR")}`} value={total} />
        <Summary label="Actifs" value={active} />
        <Summary label={section === "services" ? "Paliers" : section === "formules" ? "Visibles vitrine" : "Paiement comptant"} value={secondary} />
        <Summary label="Résultats affichés" value={rows.length} />
      </div>
      <div className={styles.toolbar}>
        <CatalogField htmlFor="catalog-search" label={`Rechercher dans ${title.toLocaleLowerCase("fr-FR")}`}>
          <input id="catalog-search" onChange={(event) => setQuery(event.currentTarget.value)} placeholder="Nom ou code" type="search" value={query} />
        </CatalogField>
        <CatalogField htmlFor="catalog-status" label="Statut">
          <select id="catalog-status" onChange={(event) => setStatus(event.currentTarget.value)} value={status}>
            <option value="all">Tous</option><option value="active">Actifs</option><option value="inactive">Inactifs</option>
          </select>
        </CatalogField>
        <div />
        <Link className="button" href={`/admin/catalog/${section === "services" ? "services" : section}/new`}>
          Créer {section === "services" ? "un service" : section === "formules" ? "une formule" : "un engagement"}
        </Link>
      </div>
      <div className={styles.tableWrap}>
        {rows.length === 0 ? <p className={styles.empty}>Aucun résultat pour ces filtres.</p> : (
          <table className={styles.table}>
            <caption className="sr-only">Liste des {title.toLocaleLowerCase("fr-FR")}</caption>
            <thead><tr>{section === "services" ? <><th>Service</th><th>Statut</th><th>Visibilité</th><th>Paliers</th><th>À partir de</th><th>Libre-service</th></> : section === "formules" ? <><th>Formule</th><th>Statut</th><th>Vitrine</th><th>Composants</th><th>Prix serveur</th></> : <><th>Engagement</th><th>Durée</th><th>Statut</th><th>Mensuel</th><th>Comptant</th></>}<th>Action</th></tr></thead>
            <tbody>
              {section === "services" ? snapshot.services.filter((item) => rows.some((row) => row.id === item.id)).map((service) => {
                const starting = startingMonthlyPriceCents(service, asOf);
                return <tr key={service.id}>
                  <td><span className={styles.primaryCell}><strong>{service.name}</strong><code>{service.code}</code></span></td>
                  <td><CatalogStatus value={service.status} /></td><td>{service.publicVisible ? "Publique" : "Interne"}</td><td>{service.tiers.length}</td>
                  <td>{starting === null ? "—" : `${formatCurrencyFromCents(starting)} / mois`}</td><td>{service.selfServiceOrderable ? "Oui" : "Non"}</td>
                  <td><Link className="table-action" href={`/admin/catalog/services/${service.id}`}>Modifier</Link></td>
                </tr>;
              }) : section === "formules" ? snapshot.presets.filter((item) => rows.some((row) => row.id === item.id)).map((preset) => <tr key={preset.id}>
                <td><span className={styles.primaryCell}><strong>{preset.name}</strong><code>{preset.code}</code></span></td><td><CatalogStatus value={preset.status} /></td><td>{preset.isPublic ? "Visible" : "Masquée"}</td><td>{preset.items.length}</td>
                <td>{baselineByCode[preset.code] === undefined ? "—" : `${formatCurrencyFromCents(baselineByCode[preset.code])} / mois`}</td>
                <td><Link className="table-action" href={`/admin/catalog/formules/${preset.id}`}>Modifier</Link></td>
              </tr>) : snapshot.commitments.filter((item) => rows.some((row) => row.id === item.id)).map((commitment) => <tr key={commitment.id}>
                <td><span className={styles.primaryCell}><strong>{commitment.name}</strong><code>{commitment.code}</code></span></td><td>{commitment.commitmentMonths} mois</td><td><CatalogStatus value={commitment.status} /></td><td>{commitment.allowMonthlyPayment ? "Autorisé" : "Non"}</td><td>{commitment.allowUpfrontPayment ? "Autorisé" : "Non"}</td>
                <td><Link className="table-action" href={`/admin/catalog/engagements/${commitment.id}`}>Modifier</Link></td>
              </tr>)}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

function Summary({ label, value }: { label: string; value: number }) { return <div className={styles.summaryCard}><span>{label}</span><strong>{value}</strong></div>; }
function CatalogStatus({ value }: { value: string }) { return <StatusBadge label={value === "active" ? "Actif" : "Inactif"} tone={value === "active" ? "success" : "neutral"} />; }
