import { LoadingState } from "@/components/LoadingState";

export default function Loading() {
  return (
    <div className="route-loading-shell" style={{ minHeight: "100vh" }}>
      <LoadingState
        description="Les informations de cette page sont en cours de récupération."
        title="Chargement de la page"
      />
    </div>
  );
}
