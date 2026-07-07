import type { CorrelationId, DataSource, ManagedContentDetail } from "@kermaria/shared";

import { ManagedMarkdown } from "@/components/ManagedMarkdown";
import { MockNotice } from "@/components/MockNotice";
import { formatDateTime } from "@/lib/formatters";

type PublicManagedContentArticleProps = {
  eyebrow: string;
  source: DataSource;
  correlationId: CorrelationId;
  content: ManagedContentDetail;
};

export function PublicManagedContentArticle({
  eyebrow,
  source,
  correlationId,
  content,
}: PublicManagedContentArticleProps) {
  return (
    <>
      <article className="legal-page managed-content-page">
        <header className="legal-page-header">
          <p className="eyebrow">{eyebrow}</p>
          <h1>{content.title}</h1>
          <div className="managed-content-meta">
            {content.versionLabel ? (
              <p className="managed-content-version">{content.versionLabel}</p>
            ) : null}
            {content.updatedAt ? (
              <p className="managed-content-updated">
                Mis à jour le {formatDateTime(content.updatedAt)}
              </p>
            ) : null}
          </div>
        </header>

        <ManagedMarkdown markdown={content.bodyMarkdown} />
      </article>

      <MockNotice correlationId={correlationId} source={source} />
    </>
  );
}
