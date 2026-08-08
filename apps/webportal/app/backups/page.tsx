import Link from "next/link";

import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { requireClientSession } from "@/lib/auth";
import {
  formatBytes,
  formatDateTime,
  formatDurationSeconds,
} from "@/lib/formatters";
import { getBackups } from "@/lib/internal-api";

export const metadata = {
  title: "Sauvegardes",
};

export const dynamic = "force-dynamic";

const protectionTone = {
  protected: "success",
  warning: "warning",
  critical: "danger",
  unknown: "neutral",
} as const;

const protectionIcon = {
  protected: "✓",
  warning: "!",
  critical: "x",
  unknown: "?",
} as const;

export default async function BackupsPage() {
  await requireClientSession();
  const result = await getBackups();

  return (
    <>
      <PageHeader
        description="Consultez l'etat metier de vos sauvegardes suivies. Une sauvegarde reussie n'est pas affichee comme restauration garantie."
        eyebrow="Protection des donnees"
        title="Mes sauvegardes"
      />

      {result.error ? (
        <ErrorState
          description="Impossible de charger l'etat des sauvegardes pour le moment."
          reference={result.correlationId}
          title="Sauvegardes indisponibles"
        />
      ) : result.data.length === 0 ? (
        <EmptyState
          description="Aucun service de sauvegarde supervise n'est associe a ce compte."
          title="Aucune sauvegarde supervisee"
        />
      ) : (
        <section className="backup-grid" aria-label="Sauvegardes du compte">
          {result.data.map((backup) => (
            <article className="content-panel backup-card" key={backup.id}>
              <div className="section-heading">
                <div>
                  <span className="card-kicker">{backup.serviceName}</span>
                  <h2>Sauvegarde</h2>
                </div>
                <StatusBadge
                  label={`${protectionIcon[backup.protectionStatus]} ${backup.protectionStatusLabel}`}
                  tone={protectionTone[backup.protectionStatus]}
                />
              </div>
              <dl className="profile-details">
                <div>
                  <dt>Derniere execution</dt>
                  <dd>{backup.lastRunAt ? formatDateTime(backup.lastRunAt) : "Indisponible"}</dd>
                </div>
                <div>
                  <dt>Derniere reussite</dt>
                  <dd>{backup.lastSuccessAt ? formatDateTime(backup.lastSuccessAt) : "Indisponible"}</dd>
                </div>
                <div>
                  <dt>Resultat</dt>
                  <dd>{backup.lastResultLabel ?? "Etat inconnu"}</dd>
                </div>
                <div>
                  <dt>Donnees protegees</dt>
                  <dd>{formatBytes(backup.protectedBytes)}</dd>
                </div>
                <div>
                  <dt>Duree</dt>
                  <dd>{formatDurationSeconds(backup.durationSeconds)}</dd>
                </div>
                <div>
                  <dt>Historique</dt>
                  <dd>{backup.retentionDays ? `${backup.retentionDays} jours` : "Selon configuration"}</dd>
                </div>
                <div>
                  <dt>Prochaine execution</dt>
                  <dd>{backup.nextRunAt ? formatDateTime(backup.nextRunAt) : "Indisponible"}</dd>
                </div>
                <div>
                  <dt>Derniere collecte</dt>
                  <dd>{backup.collectedAt ? formatDateTime(backup.collectedAt) : "Indisponible"}</dd>
                </div>
              </dl>
              {backup.lastErrorPublic ? (
                <p className="field-hint">{backup.lastErrorPublic}</p>
              ) : null}
              <div className="backup-actions">
                <Link className="button" href={`/backups/${encodeURIComponent(backup.id)}`}>
                  Consulter l&apos;historique
                </Link>
              </div>
            </article>
          ))}
        </section>
      )}

      <MockNotice correlationId={result.correlationId} source={result.source} />
    </>
  );
}
