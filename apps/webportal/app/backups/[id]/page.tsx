import Link from "next/link";
import { notFound } from "next/navigation";

import { BackupRestoreRequestForm } from "@/components/BackupRestoreRequestForm";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionHeading } from "@/components/SectionHeading";
import { StatusBadge } from "@/components/StatusBadge";
import { requireClientSession } from "@/lib/auth";
import {
  formatBytes,
  formatDateTime,
  formatDurationSeconds,
} from "@/lib/formatters";
import { getBackup } from "@/lib/internal-api";

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ id: string }>;
};

const protectionTone = {
  protected: "success",
  warning: "warning",
  critical: "danger",
  unknown: "neutral",
} as const;

const runTone = {
  success: "success",
  warning: "warning",
  failed: "danger",
  running: "info",
  unknown: "neutral",
} as const;

export default async function BackupDetailPage({ params }: PageProps) {
  await requireClientSession();
  const { id } = await params;
  const result = await getBackup(id);

  if (!result.error && result.data === null) {
    notFound();
  }

  const detail = result.data;

  return (
    <>
      <PageHeader
        action={
          <Link className="button button-secondary" href="/backups">
            Retour aux sauvegardes
          </Link>
        }
        description="Historique métier des exécutions remontées par le collecteur interne de sauvegarde."
        eyebrow="Protection des données"
        title="Détail sauvegarde"
      />

      {result.error || !detail ? (
        <ErrorState
          description="Impossible de charger cette sauvegarde pour le moment."
          reference={result.correlationId}
          title="Sauvegarde indisponible"
        />
      ) : (
        <>
          <section className="content-panel">
            <div className="section-heading">
              <div>
                <span className="card-kicker">{detail.job.serviceName}</span>
                <h2>Sauvegarde</h2>
              </div>
              <StatusBadge
                label={detail.job.protectionStatusLabel}
                tone={protectionTone[detail.job.protectionStatus]}
              />
            </div>
            <dl className="profile-details">
              <div>
                <dt>Dernière exécution</dt>
                <dd>{detail.job.lastRunAt ? formatDateTime(detail.job.lastRunAt) : "Indisponible"}</dd>
              </div>
              <div>
                <dt>Dernière réussite</dt>
                <dd>{detail.job.lastSuccessAt ? formatDateTime(detail.job.lastSuccessAt) : "Indisponible"}</dd>
              </div>
              <div>
                <dt>Resultat</dt>
                <dd>{detail.job.lastResultLabel ?? "État inconnu"}</dd>
              </div>
              <div>
                <dt>Données protégées</dt>
                <dd>{formatBytes(detail.job.protectedBytes)}</dd>
              </div>
              <div>
                <dt>Duree</dt>
                <dd>{formatDurationSeconds(detail.job.durationSeconds)}</dd>
              </div>
              <div>
                <dt>Retention</dt>
                <dd>{detail.job.retentionDays ? `${detail.job.retentionDays} jours` : "Selon configuration"}</dd>
              </div>
              <div>
                <dt>Prochaine exécution</dt>
                <dd>{detail.job.nextRunAt ? formatDateTime(detail.job.nextRunAt) : "Indisponible"}</dd>
              </div>
              <div>
                <dt>Verification restauration</dt>
                <dd>
                  {detail.job.lastVerifiedAt
                    ? formatDateTime(detail.job.lastVerifiedAt)
                    : "Non attestee"}
                </dd>
              </div>
            </dl>
            {detail.job.lastErrorPublic ? (
              <p className="field-hint">{detail.job.lastErrorPublic}</p>
            ) : null}
          </section>

          <section className="request-history-section">
            <SectionHeading
              description="Une demande crée un ticket traité manuellement par Zachary IT. Aucun accès direct à l'infrastructure de sauvegarde n'est ouvert."
              title="Demander une restauration"
            />
            <BackupRestoreRequestForm backupJobId={detail.job.id} />
          </section>

          <section className="request-history-section">
            <SectionHeading title="Historique" />
            <div className="backup-run-list">
              {detail.runs.map((run) => (
                <article className="backup-run-row" key={run.id}>
                  <div>
                    <strong>{formatDateTime(run.startedAt)}</strong>
                    <span>{run.finishedAt ? formatDurationSeconds(run.durationSeconds) : "En cours"}</span>
                  </div>
                  <StatusBadge label={run.resultLabel} tone={runTone[run.result]} />
                  <span>{formatBytes(run.protectedBytes)}</span>
                </article>
              ))}
            </div>
          </section>
        </>
      )}

      <MockNotice correlationId={result.correlationId} source={result.source} />
    </>
  );
}
