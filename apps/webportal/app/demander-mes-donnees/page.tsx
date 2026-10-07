import type { Metadata } from "next";
import Link from "next/link";
import { SitePageFrame } from "@/components/SitePageFrame";
import { getPublicSitePageLayout } from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";

export const dynamic = "force-dynamic";

export const metadata: Metadata = buildPublicMetadata({
  title: "Demander mes données",
  description: "Exercez vos droits sur les données personnelles liées à votre compte Zachary IT.",
  path: "/demander-mes-donnees",
});

export default async function DataRequestInformationPage() {
  const layout = await getPublicSitePageLayout("/demander-mes-donnees");
  const actions = <div className="data-request-actions">
    <Link className="button" href="/login?next=%2Fprofile%2Fdonnees">Faire une demande dans mon espace client</Link>
    <Link className="button button-secondary" href="/contact">Je n’ai pas accès à mon compte</Link>
  </div>;

  return <div className="data-request-public-page">
    <SitePageFrame area="public" pageKey="/demander-mes-donnees"
      initialLayout={layout.data} slots={{ data_rights_actions: actions }}>
    <header className="data-request-intro">
      <h1>Vos données, vos choix</h1>
      <p>Vous pouvez demander à consulter, corriger ou supprimer les données personnelles qui vous concernent, ainsi qu’exercer vos autres droits.</p>
      {actions}
    </header>
    <section className="data-request-info-card">
      <h2>Comment se passe votre demande ?</h2>
      <ol>
        <li>Connectez-vous et décrivez votre demande en quelques mots.</li>
        <li>Suivez son traitement dans votre espace client.</li>
        <li>Consultez notre réponse depuis ce même espace sécurisé.</li>
      </ol>
      <p>Nous répondons dans les meilleurs délais, en principe sous un mois. Si votre identité doit être confirmée, nous vous demanderons uniquement les éléments nécessaires.</p>
      <p>Pour en savoir plus, consultez notre <Link href="/politique-confidentialite">politique de confidentialité</Link>.</p>
    </section>
    </SitePageFrame>
  </div>;
}
