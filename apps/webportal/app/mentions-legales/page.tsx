import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Mentions légales",
  description: "Mentions légales de Zachary HOUNSA-HOUNKPA EI.",
};

export default function MentionsLegalesPage() {
  return (
    <article className="legal-page">
      <header className="legal-page-header">
        <p className="eyebrow">Informations légales</p>
        <h1>Mentions légales</h1>
        <p className="legal-page-status">
          Contenu placeholder. La version définitive sera publiée avant la mise
          en production (V1.0 RC).
        </p>
      </header>

      <section>
        <h2>Éditeur du site</h2>
        <p>
          Zachary HOUNSA-HOUNKPA, entrepreneur individuel (EI).
          <br />
          Adresse postale : [à compléter].
          <br />
          SIRET : [à compléter].
          <br />
          Contact : [adresse e-mail à compléter].
        </p>
      </section>

      <section>
        <h2>Directeur de la publication</h2>
        <p>Zachary HOUNSA-HOUNKPA.</p>
      </section>

      <section>
        <h2>Hébergement</h2>
        <p>
          Identification et coordonnées de l&apos;hébergeur : [à compléter selon
          l&apos;infrastructure cible].
        </p>
      </section>

      <section>
        <h2>Propriété intellectuelle</h2>
        <p>
          L&apos;ensemble des contenus présents sur ce site (textes, marques,
          logos, éléments graphiques) est protégé par le droit d&apos;auteur et le
          droit des marques. Toute reproduction, représentation, modification ou
          diffusion, totale ou partielle, sans autorisation écrite préalable est
          interdite.
        </p>
      </section>

      <section>
        <h2>Contact</h2>
        <p>
          Pour toute demande relative au site ou à son contenu : [adresse e-mail
          à compléter].
        </p>
      </section>
    </article>
  );
}
