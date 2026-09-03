import type { Metadata } from "next";
import Link from "next/link";

import { PublicPackComparisonTable } from "@/components/PublicPackComparisonTable";
import { PublicPackOverviewGrid } from "@/components/PublicPackOverviewGrid";
import {
  getBillingV2FormulesCatalog,
  getPublicPackCatalogContent,
} from "@/lib/internal-api";
import { buildPublicMetadata } from "@/lib/public-metadata";
import { buildPublicPackViews } from "@/lib/public-packs";
import { isSignupEnabled } from "@/lib/public-routes";

export const metadata: Metadata = buildPublicMetadata({
  title: "Offres de sauvegarde et stockage à Guichen",
  description:
    "Quatre offres conçus pour la sauvegarde distante, le stockage documentaire et la continuité d'activité des particuliers et petites structures.",
  path: "/offres",
});

export const dynamic = "force-dynamic";

const OFFER_STORY_POINTS = [
  {
    title: "Dossier de secours numérique",
    body: "Conservez à distance factures, contrats, garanties, photos utiles et autres justificatifs qui seraient difficiles à reconstituer après un sinistre.",
  },
  {
    title: "Continuité d'activité",
    body: "Les offres aident à préserver les documents clients, devis, factures, configurations et fichiers de travail nécessaires à une reprise plus rapide.",
  },
  {
    title: "Accompagnement local",
    body: "Basé à Guichen, Zachary IT explique le cadre retenu, la localisation des données et les éventuels prestataires techniques sans promesse excessive.",
  },
];

export default async function OffresPage() {
  const [{ data: catalog }, { data: content }] = await Promise.all([
    getBillingV2FormulesCatalog(),
    getPublicPackCatalogContent(),
  ]);
  const signupEnabled = isSignupEnabled();
  const packs = buildPublicPackViews(catalog, content);

  return (
    <div className="offres-page">
      <header className="offres-header">
        {content.pageEyebrow.trim() ? (
          <p className="eyebrow">{content.pageEyebrow}</p>
        ) : null}
        <h1>{content.pageTitle}</h1>
        <p className="offres-lead">{content.pageDescription}</p>
      </header>

      <section className="offres-story">
        <div className="offres-story-highlight">
          <p className="eyebrow">Sauvegarde distante</p>
          <h2>
            Un incident matériel ne devrait pas devenir une perte définitive.
          </h2>
          <p>
            Ces offres sont pensés pour conserver une copie distante de vos
            documents importants et de vos données utiles, sans réduire le
            sujet à un discours anxiogène ou à des promesses techniques non
            confirmées.
          </p>
          <p>
            Une sauvegarde gardée dans le même logement ou les mêmes locaux que
            le matériel ne protège pas contre tous les sinistres physiques. Le
            but ici est de rendre la reprise plus simple, pas de prétendre
            supprimer tous les risques.
          </p>
          <Link className="text-link" href="/contact">
            Demander un accompagnement
          </Link>
        </div>

        <div className="offres-story-grid">
          {OFFER_STORY_POINTS.map((item) => (
            <article className="offres-story-card" key={item.title}>
              <h3>{item.title}</h3>
              <p>{item.body}</p>
            </article>
          ))}
        </div>
      </section>

      <section className="offres-demo-access" aria-labelledby="offres-formules-title">
        <div>
          <p className="eyebrow">Nouveau</p>
          <h2 id="offres-formules-title">Configurer une offre et souscrire en ligne</h2>
          <p>
            Quatre offres ajustables — capacité, sauvegarde, accès à
            distance, utilisateurs — avec le prix mis à jour à chaque
            changement et la remise d&apos;engagement affichée.
          </p>
        </div>
        <Link className="button button-primary" href="/formules">
          Voir les offres
        </Link>
      </section>

      <section className="offres-demo-access" aria-labelledby="offres-demo-title">
        <div>
          <p className="eyebrow">Espace client</p>
          <h2 id="offres-demo-title">Voir le portail avant de demander une offre</h2>
          <p>
            La démo montre le parcours client avec des données fictives, sans
            accès à un vrai compte ni à des informations de production.
          </p>
        </div>
        <Link className="button button-secondary" href="/decouvrir-espace-client">
          Découvrir l’espace client
        </Link>
      </section>

      {packs.length === 0 ? (
        <p className="offres-empty">
          Les offres ne sont pas encore disponibles en ligne. Contactez-nous
          pour obtenir une proposition adaptée.
        </p>
      ) : (
        <>
          <section className="offres-overview">
            <div className="offres-section-heading">
              <h2>Commencez par une vue simple des offres</h2>
              <p>
                Chaque offre présente son cadre d&apos;usage, sa structure
                tarifaire et l&apos;action suivante attendue. Le comparatif
                détaillé reste disponible plus bas pour arbitrer ligne par
                ligne.
              </p>
            </div>

            <PublicPackOverviewGrid
              packs={packs}
              signupEnabled={signupEnabled}
            />
          </section>

          <section className="offres-comparison">
            <div className="offres-section-heading">
              <h2>Comparer les différences utiles</h2>
              <p>
                Utilisez le comparatif détaillé pour arbitrer entre engagement,
                paiement, mise en service et niveau de couverture.
              </p>
            </div>

            <PublicPackComparisonTable
              content={content}
              packs={packs}
              signupEnabled={signupEnabled}
            />
          </section>
        </>
      )}
    </div>
  );
}
