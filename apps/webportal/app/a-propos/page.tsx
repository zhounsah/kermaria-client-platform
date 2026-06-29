import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "À propos",
  description:
    "Présentation de Zachary HOUNSA-HOUNKPA EI et de ses domaines d'intervention.",
};

export default function AProposPage() {
  return (
    <article className="legal-page">
      <header className="legal-page-header">
        <p className="eyebrow">À propos</p>
        <h1>Zachary HOUNSA-HOUNKPA EI</h1>
        <p className="legal-page-status">
          Contenu placeholder. La version définitive sera publiée avant la mise
          en production (V1.0 RC).
        </p>
      </header>

      <section>
        <h2>Présentation</h2>
        <p>
          [À compléter : présentation de l&apos;entrepreneur, parcours, domaines
          d&apos;intervention et positionnement.]
        </p>
      </section>

      <section>
        <h2>Engagement</h2>
        <p>
          [À compléter : valeurs, engagements vis-à-vis des clients,
          transparence sur la facturation et la disponibilité.]
        </p>
      </section>

      <section>
        <h2>Coordonnées</h2>
        <p>
          Pour toute prise de contact ou demande de devis, rendez-vous sur la
          page <a href="/contact">contact</a>.
        </p>
      </section>
    </article>
  );
}
