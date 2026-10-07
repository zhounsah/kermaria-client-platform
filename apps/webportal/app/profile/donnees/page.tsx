import Link from "next/link";
import { DataSubjectRequestForm } from "@/components/DataSubjectRequestForm";
import { ErrorState } from "@/components/ErrorState";
import { PageHeader } from "@/components/PageHeader";
import { SitePageFrame } from "@/components/SitePageFrame";
import { requireClientSession } from "@/lib/auth";
import { formatDate, formatDateTime } from "@/lib/formatters";
import { dataRequestTypeLabels } from "@/lib/data-subject-request-labels";
import { getClientDataRequests, getClientDataRequestPageLayout } from "@/lib/internal-api";

export const metadata = { title: "Mes données" };
export const dynamic = "force-dynamic";

export default async function ClientDataRequestsPage() {
  await requireClientSession();
  const [result, layout] = await Promise.all([
    getClientDataRequests(), getClientDataRequestPageLayout(),
  ]);
  const slots = {
    data_request_intro: <PageHeader title="Mes données" description="Faites une demande et suivez la réponse en toute confidentialité." eyebrow="Compte" />,
    data_request_form: <DataSubjectRequestForm />,
    data_request_history: <section className="content-panel data-request-list">
        <h2>Mes demandes</h2>
        {result.error ? <ErrorState title="Demandes indisponibles" description="Réessayez plus tard." reference={result.correlationId} />
          : result.data.length === 0 ? <p>Vous n’avez pas encore fait de demande.</p>
            : <ul>{result.data.map((item) => <li key={item.id}>
              <Link href={`/profile/donnees/${encodeURIComponent(item.id)}`}><strong>{dataRequestTypeLabels[item.requestType]}</strong> · {item.reference}</Link>
              <span>Reçue le {formatDateTime(item.createdAt)} · Réponse prévue avant le {formatDate(item.dueAt)}</span>
            </li>)}</ul>}
      </section>,
    data_request_help: <section className="content-panel data-request-info-card">
      <h2>Besoin d’aide ?</h2>
      <p>Expliquez votre demande avec vos mots. Si vous n’avez plus accès à votre compte, <Link href="/contact">contactez-nous</Link>.</p>
    </section>,
  };
  return <SitePageFrame area="client" pageKey="/profile/donnees" initialLayout={layout.data} slots={slots}>{null}</SitePageFrame>;
}
