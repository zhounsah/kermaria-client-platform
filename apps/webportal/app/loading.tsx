import { LoadingState } from "@/components/LoadingState";

export default function Loading() {
  return (
    <LoadingState
      description="Votre espace client prépare les informations demandées."
      title="Chargement de votre espace"
    />
  );
}
