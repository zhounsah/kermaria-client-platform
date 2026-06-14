"use client";

import { ErrorState } from "@/components/ErrorState";
import { PageHeader } from "@/components/PageHeader";

type ErrorPageProps = {
  error: Error & { digest?: string };
  reset: () => void;
};

export default function ErrorPage({ reset }: ErrorPageProps) {
  return (
    <>
      <PageHeader
        description="La page demandée n’a pas pu être affichée correctement."
        eyebrow="État du portail"
        title="Service temporairement indisponible"
      />
      <ErrorState
        action={
          <button className="button" onClick={reset} type="button">
            Réessayer
          </button>
        }
        description="Réessayez dans quelques instants. Aucun détail technique n’est exposé."
        title="Une erreur est survenue"
      />
    </>
  );
}
