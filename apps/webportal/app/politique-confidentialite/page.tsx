import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Politique de confidentialité",
  description:
    "Politique de confidentialité et utilisation des cookies sur l'espace client.",
};

export default function PolitiqueConfidentialitePage() {
  return (
    <article className="legal-page">
      <header className="legal-page-header">
        <p className="eyebrow">Informations légales</p>
        <h1>Politique de confidentialité</h1>
        <p className="legal-page-status">
          Contenu placeholder. La version définitive sera publiée avant la mise
          en production (V1.0 RC).
        </p>
      </header>

      <section>
        <h2>Données collectées</h2>
        <p>
          Les données nécessaires à la gestion de la relation commerciale et
          contractuelle (identification, coordonnées, historique de commandes,
          factures) sont traitées dans le cadre de l&apos;exécution du contrat
          conclu avec le client. Aucun profilage ni revente à un tiers n&apos;est
          effectué.
        </p>
      </section>

      <section>
        <h2>Aucun traceur ni analytique tiers</h2>
        <p>
          Position de principe : ce site n&apos;utilise{" "}
          <strong>aucun service d&apos;analytique</strong> (Google Analytics,
          Plausible, Matomo, Cloudflare Insights, Microsoft Clarity ou
          équivalent), <strong>aucun pixel publicitaire</strong> (Meta,
          LinkedIn, X ou autres) et{" "}
          <strong>aucun cookie tiers à des fins de mesure ou de publicité</strong>.
        </p>
        <p>
          En conséquence, aucune bannière de consentement cookies n&apos;est
          affichée : seuls les cookies strictement nécessaires au fonctionnement
          du service sont émis.
        </p>
      </section>

      <section>
        <h2>Cookies réellement émis</h2>
        <ul>
          <li>
            <strong>Cookie de session</strong> : déposé après connexion, il
            permet de maintenir l&apos;authentification sur l&apos;espace client. Il
            n&apos;est jamais émis sur les pages publiques.
          </li>
          <li>
            <strong>Cookie CSRF</strong> : associé aux formulaires de
            l&apos;espace client, il garantit l&apos;origine légitime des
            requêtes envoyées au serveur.
          </li>
          <li>
            <strong>Cookies hCaptcha</strong> (lorsque activé) : déposés par le
            service de protection anti-bots utilisé sur les formulaires
            d&apos;inscription ou de contact. Ils sont strictement nécessaires à
            son fonctionnement.
          </li>
        </ul>
      </section>

      <section>
        <h2>Durée de conservation</h2>
        <p>
          Les données client sont conservées pendant la durée de la relation
          contractuelle, puis pour les durées légales applicables (notamment dix
          ans pour les pièces comptables et factures).
        </p>
      </section>

      <section>
        <h2>Vos droits</h2>
        <p>
          Conformément au Règlement général sur la protection des données
          (RGPD), vous disposez d&apos;un droit d&apos;accès, de rectification,
          d&apos;effacement, d&apos;opposition, de limitation et de portabilité
          sur vos données personnelles. Pour les exercer : [adresse e-mail à
          compléter].
        </p>
      </section>
    </article>
  );
}
