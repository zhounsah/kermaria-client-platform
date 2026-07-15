import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { SectionCard } from "@/components/SectionCard";
import { StatusBadge } from "@/components/StatusBadge";
import { requireClientSession } from "@/lib/auth";
import { formatDate } from "@/lib/formatters";
import { getClientDownloads } from "@/lib/internal-api";

export const metadata = {
  title: "Téléchargements",
};

export const dynamic = "force-dynamic";

const resourceTypeLabels = {
  software: "Logiciel",
  script: "Script",
  rdp: "Fichier RDP",
  document: "Documentation",
  tool: "Outil",
  other: "Ressource",
} as const;

export default async function DownloadsPage() {
  await requireClientSession();
  const result = await getClientDownloads();
  const categories = result.data.filter((category) => category.items.length > 0);

  return (
    <>
      <PageHeader
        description="Téléchargez ici les logiciels, scripts et fichiers nécessaires à votre service, depuis un point d'accès unique et sécurisé."
        eyebrow="Espace client"
        title="Téléchargements"
      />

      <SectionCard ariaLabel="Aide téléchargements" className="downloads-help-card">
        <span className="card-kicker">Centre de ressources</span>
        <h2>Tout ce qu'il faut pour démarrer ou retrouver vos accès</h2>
        <p>
          Chaque ressource est rangée par usage pour rester simple à parcourir.
          Ouvrez une catégorie, lisez la courte description, puis lancez le
          téléchargement correspondant.
        </p>
      </SectionCard>

      {result.error ? (
        <ErrorState
          description="Impossible de charger vos téléchargements pour le moment."
          reference={result.correlationId}
          title="Téléchargements indisponibles"
        />
      ) : categories.length === 0 ? (
        <EmptyState
          description="Aucun téléchargement n'est actuellement disponible pour vos accès actifs."
          title="Aucun téléchargement"
        />
      ) : (
        <div className="downloads-accordion-list">
          {categories.map((category, categoryIndex) => (
            <details
              className="downloads-accordion"
              key={category.id}
              open={categoryIndex === 0}
            >
              <summary>
                <div>
                  <span className="card-kicker">Catégorie</span>
                  <strong>{category.title}</strong>
                  {category.description ? <span>{category.description}</span> : null}
                </div>
                <span className="downloads-count">
                  {category.items.length} élément
                  {category.items.length > 1 ? "s" : ""}
                </span>
              </summary>

              <div className="downloads-card-grid">
                {category.items.map((item) => (
                  <article className="download-card" key={item.id}>
                    <div className="download-card-top">
                      <div>
                        <h3>{item.title}</h3>
                        <p>{item.shortDescription}</p>
                      </div>
                      <StatusBadge
                        label={resourceTypeLabels[item.resourceType]}
                        tone="info"
                      />
                    </div>

                    {item.versionLabel || item.updatedAt ? (
                      <div className="download-card-meta">
                        {item.versionLabel ? (
                          <span>Version : {item.versionLabel}</span>
                        ) : null}
                        {item.updatedAt ? (
                          <span>Mise à jour : {formatDate(item.updatedAt)}</span>
                        ) : null}
                      </div>
                    ) : null}

                    {item.installationInstructions ? (
                      <div className="download-card-note">
                        <strong>Consignes</strong>
                        <p>{item.installationInstructions}</p>
                      </div>
                    ) : null}

                    <div className="download-card-actions">
                      <a
                        className="button"
                        href={`/api/downloads/${encodeURIComponent(item.id)}/file`}
                      >
                        Télécharger
                      </a>
                    </div>
                  </article>
                ))}
              </div>
            </details>
          ))}
        </div>
      )}

      <MockNotice
        correlationId={result.correlationId}
        source={result.source}
      />
    </>
  );
}
