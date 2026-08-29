import { AdminSettingsNavigation } from "@/components/AdminSettingsNavigation";
import { PageHeader } from "@/components/PageHeader";
export default function AdminSettingsLoading() {
  return (
    <>
      <PageHeader
        eyebrow="Centre de configuration"
        title="Chargement de la configuration"
        description={"R\u00e9cup\u00e9ration des param\u00e8tres et \u00e9tats administratifs."}
      />
      <AdminSettingsNavigation />
      <section aria-busy="true" aria-label="Chargement du Centre de configuration" className="admin-settings-loading">
        <div className="admin-settings-loading-summary" />
        <div className="admin-settings-loading-grid">
          <div className="admin-settings-loading-card" />
          <div className="admin-settings-loading-card" />
          <div className="admin-settings-loading-card" />
        </div>
      </section>
    </>
  );
}
