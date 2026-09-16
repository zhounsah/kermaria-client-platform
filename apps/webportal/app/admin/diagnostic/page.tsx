import Link from "next/link";

import { AdminPreDiagnosticControlCenter } from "@/components/AdminPreDiagnosticControlCenter";
import { ErrorState } from "@/components/ErrorState";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { requireAdminSession } from "@/lib/auth";
import {
  getAdminDiagnosticConfiguration,
  getBillingV2FormulesCatalog,
} from "@/lib/internal-api";

export const metadata = {
  title: "Diagnostic - Administration",
};

export const dynamic = "force-dynamic";

export default async function AdminDiagnosticPage() {
  await requireAdminSession();

  const [configurationResult, catalogResult] = await Promise.all([
    getAdminDiagnosticConfiguration(),
    getBillingV2FormulesCatalog(),
  ]);

  return (
    <>
      <PageHeader
        description="Éditez le brouillon du pré-diagnostic, validez-le puis publiez-le sans modifier le code."
        eyebrow="Administration interne"
        title="Diagnostic"
      />

      <section className="content-panel page-header-split">
        <div>
          <span className="card-kicker">Centre de pilotage</span>
          <h2>Questionnaire, scoring et orientation</h2>
          <p>
            Les décisions métier sont contenues dans une configuration versionnée.
            Le navigateur et API-INTERNAL lisent la même version publiée ; Billing
            V2 reste seul responsable des tarifs.
          </p>
        </div>
        <div className="stack-row">
          <Link className="button button-secondary" href="/diagnostic">
            Tester le diagnostic
          </Link>
          <Link className="button button-secondary" href="/admin/catalog">
            Ouvrir le catalogue Billing V2
          </Link>
        </div>
      </section>

      {catalogResult.error ? (
        <ErrorState
          compact
          description="Le catalogue Billing V2 est indisponible. Les règles restent lisibles, mais aucune nouvelle formule ne peut être sélectionnée tant que le catalogue n'est pas revenu."
          reference={catalogResult.correlationId}
          title="Catalogue commercial indisponible"
        />
      ) : null}

      {configurationResult.error || !configurationResult.data ? (
        <ErrorState
          compact
          description="Impossible de charger la configuration du diagnostic. Aucun brouillon ne peut être modifié tant que le service interne ne répond pas."
          reference={configurationResult.correlationId}
          title="Configuration indisponible"
        />
      ) : null}

      {configurationResult.data ? <AdminPreDiagnosticControlCenter catalog={catalogResult.data} initialView={configurationResult.data} /> : null}

      <MockNotice
        correlationId={configurationResult.correlationId}
        source={configurationResult.source}
      />
    </>
  );
}
