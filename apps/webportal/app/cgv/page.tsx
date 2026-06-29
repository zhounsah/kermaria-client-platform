import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Conditions générales de vente",
  description:
    "Conditions générales de vente applicables aux prestations de Zachary HOUNSA-HOUNKPA EI.",
};

export default function CgvPage() {
  return (
    <article className="legal-page">
      <header className="legal-page-header">
        <p className="eyebrow">Informations légales</p>
        <h1>Conditions générales de vente</h1>
        <p className="legal-page-status">
          Contenu placeholder. La version définitive sera publiée avant la mise
          en production (V1.0 RC).
        </p>
      </header>

      <section>
        <h2>Objet</h2>
        <p>
          Les présentes conditions générales de vente régissent les relations
          contractuelles entre Zachary HOUNSA-HOUNKPA EI et ses clients pour les
          prestations proposées via l&apos;espace client.
        </p>
      </section>

      <section>
        <h2>Commandes et devis</h2>
        <p>
          [À compléter : modalités d&apos;établissement et d&apos;acceptation des
          devis, durée de validité, modifications.]
        </p>
      </section>

      <section>
        <h2>Tarifs et facturation</h2>
        <p>
          [À compléter : monnaie, TVA applicable, modalités d&apos;émission des
          factures, support de transmission.]
        </p>
      </section>

      <section>
        <h2>Paiement</h2>
        <p>
          [À compléter : moyens de paiement acceptés (virement, carte bancaire,
          PayPal), délais, pénalités de retard, indemnité forfaitaire pour frais
          de recouvrement.]
        </p>
      </section>

      <section>
        <h2>Abonnements récurrents</h2>
        <p>
          [À compléter : modalités de reconduction (tacite ou expresse), préavis
          et conditions de résiliation, conséquences en cas d&apos;incident de
          paiement.]
        </p>
      </section>

      <section>
        <h2>Droit de rétractation</h2>
        <p>
          [À compléter selon la qualification des clients (professionnels ou
          consommateurs) et la nature des prestations fournies.]
        </p>
      </section>

      <section>
        <h2>Responsabilité</h2>
        <p>
          [À compléter : engagement de moyens, limitations de responsabilité,
          force majeure.]
        </p>
      </section>

      <section>
        <h2>Données personnelles</h2>
        <p>
          Le traitement des données personnelles est décrit dans la{" "}
          <a href="/politique-confidentialite">
            politique de confidentialité
          </a>
          .
        </p>
      </section>

      <section>
        <h2>Litiges</h2>
        <p>
          [À compléter : droit applicable, juridiction compétente, dispositif de
          médiation éventuel.]
        </p>
      </section>
    </article>
  );
}
