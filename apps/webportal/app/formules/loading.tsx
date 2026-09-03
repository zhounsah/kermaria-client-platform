import { LoadingState } from "@/components/LoadingState";

/**
 * Etat de chargement propre au parcours des formules.
 *
 * Sans lui, c'est le `loading.tsx` racine qui s'affiche — « Chargement de
 * votre espace », « Votre espace client prepare les informations demandees ».
 * Un visiteur qui compare des offres n'a pas encore d'espace client : lui en
 * parler avant toute souscription laisse croire qu'il faut deja un compte, et
 * fait passer une page publique pour une page authentifiee.
 */
export default function Loading() {
  return (
    <LoadingState
      description="Les tarifs sont chargés depuis notre catalogue."
      title="Chargement des offres"
    />
  );
}
