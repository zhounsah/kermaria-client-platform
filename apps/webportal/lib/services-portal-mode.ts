import type { PortalArea, PortalRole } from "@/lib/public-route-config";

export type ServicesPortalMode = "public" | "client" | "admin";

/**
 * `/services` est une route partagée : vitrine sur l'hôte commercial et
 * espace client sur le tableau de bord. En développement, localhost réunit
 * les trois hôtes ; seul le rôle de session peut alors choisir la vue.
 */
export function resolveServicesPortalMode(
  area: PortalArea | null,
  role: PortalRole | null | undefined,
): ServicesPortalMode {
  if (area === "public") return "public";
  if (area === "admin") return "admin";

  if (area === "local") {
    if (role === "client_user") return "client";
    if (role === "internal_admin") return "admin";
    return "public";
  }

  // Conserve le garde de session existant pour l'hôte client et toute origine
  // non reconnue : aucune donnée client n'est rendue par défaut.
  return "client";
}
