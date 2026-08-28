import type { Metadata } from "next";
import Link from "next/link";

import { PublicDiagnosticWizard } from "@/components/PublicDiagnosticWizard";
import { resolveDiagnosticContext } from "@/lib/diagnostic-context";
import { getBillingV2FormulesCatalog } from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";

export const metadata: Metadata = buildPublicMetadata({
  title: "Diagnostic informatique adapté à votre besoin",
  description:
    "Décrivez votre besoin de sauvegarde, accès distant, réseau, messagerie, domaine, serveur ou hébergement et obtenez une orientation ciblée.",
  path: "/diagnostic",
});

export const dynamic = "force-dynamic";

type DiagnosticPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default async function DiagnosticPage({ searchParams }: DiagnosticPageProps) {
  const params = await searchParams;
  const rawContext = Array.isArray(params.context) ? params.context[0] : params.context;
  const initialContext = resolveDiagnosticContext(rawContext);
  const { data: catalog } = await getBillingV2FormulesCatalog();

  return (
    <>
      <div className="diagnostic-page diagnostic-page-nav">
        <Link className="back-link" href="/services">
          <span aria-hidden="true">{"<-"}</span> Retour aux services
        </Link>
      </div>
      <PublicDiagnosticWizard catalog={catalog} initialContext={initialContext} />
    </>
  );
}
