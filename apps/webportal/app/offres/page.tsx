import type { Metadata } from "next";
import Link from "next/link";

import { PublicPackComparisonTable } from "@/components/PublicPackComparisonTable";
import { PublicPackOverviewGrid } from "@/components/PublicPackOverviewGrid";
import {
  getPublicCommercialCatalog,
  getPublicPackCatalogContent,
} from "@/lib/internal-api";
import { resolvePackCatalog } from "@/lib/public-packs";
import { isSignupEnabled } from "@/lib/public-routes";

export const metadata: Metadata = {
  title: "Offres",
  description:
    "Quatre packs conçus pour la sauvegarde distante, le stockage documentaire et la continuité d'activité des particuliers et petites structures.",
  alternates: { canonical: "/offres" },
};

export const revalidate = 300;

const OFFER_STORY_POINTS = [
  {
    title: "Dossier de secours numérique",
    body: "Conservez à distance factures, contrats, garanties, photos utiles et autres justificatifs qui seraient difficiles à reconstituer après un sinistre.",
  },
  {
    title: "Continuité d'activité",
    body: "Les packs aident à préserver les documents clients, devis, factures, configurations et fichiers de travail nécessaires à une reprise plus rapide.",
  },
  {
    title: "Accompagnement local",
    body: "Basé à Guichen, Zachary IT explique le cadre retenu, la localisation des données et les éventuels prestataires techniques sans promesse excessive.",
  },
];

export default async function OffresPage() {
  const [{ data: offers }, { data: content }] = await Promise.all([
    getPublicCommercialCatalog(),
    getPublicPackCatalogContent(),
  ]);
  const signupEnabled = isSignupEnabled();
  const packs = resolvePackCatalog(offers, content);

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
            Ces packs sont pensés pour conserver une copie distante de vos
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

      {packs.length === 0 ? (
        <p className="offres-empty">
          Les packs ne sont pas encore disponibles en ligne. Contactez-nous pour
          obtenir une proposition adaptée.
        </p>
      ) : (
        <>
          <section className="offres-overview">
            <div className="offres-section-heading">
              <h2>Commencez par une vue simple des packs</h2>
              <p>
                Chaque pack présente son cadre d&apos;usage, sa structure
                tarifaire et l&apos;action suivante attendue. Le comparatif
                détaillé reste disponible plus bas pour arbitrer ligne par
                ligne.
              </p>
            </div>

            <PublicPackOverviewGrid
              content={content}
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
