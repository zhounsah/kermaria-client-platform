import type { Metadata } from "next";
import Link from "next/link";
import { AnimatedPublicH1 } from "@/components/AnimatedPublicH1";

export const metadata: Metadata = {
  title: "Page introuvable",
  robots: { index: false, follow: false },
};

export default function NotFound() {
  return (
    <section className="not-found-page" aria-labelledby="not-found-title">
      <p className="eyebrow">Erreur 404</p>
      <AnimatedPublicH1 id="not-found-title" text="Page introuvable" />
      <p>
        Cette adresse ne correspond à aucune page publique disponible. Vous
        pouvez revenir aux offres ou expliquer votre besoin directement.
      </p>
      <div className="not-found-actions">
        <Link className="button" href="/offres">
          Voir les offres
        </Link>
        <Link className="button button-secondary" href="/contact">
          Expliquer mon besoin
        </Link>
      </div>
    </section>
  );
}
