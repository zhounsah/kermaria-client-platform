import type { SettingsAuditView } from "@kermaria/shared";
import { NextRequest } from "next/server";

import { handleAdminGet } from "@/lib/admin-bff";

/**
 * Filtres autorises. La liste est fermee volontairement : recopier la chaine de
 * requete telle quelle laisserait passer des parametres inconnus vers
 * API-INTERNAL, alors que le contrat serveur est explicite.
 *
 * Les valeurs, elles, ne sont pas interpretees ici. C'est API-INTERNAL qui
 * decide ce qu'un filtre inconnu selectionne — une normalisation cote portail
 * pourrait diverger de la regle serveur et laisser croire a une recherche
 * exhaustive.
 */
const ALLOWED_FILTERS = [
  "from",
  "to",
  "actor",
  "category",
  "risk",
  "outcome",
  "correlationId",
  "target",
  "limit",
] as const;

const MAX_FILTER_LENGTH = 200;

export function GET(request: NextRequest) {
  const query = new URLSearchParams();
  for (const key of ALLOWED_FILTERS) {
    const value = request.nextUrl.searchParams.get(key);
    if (value && value.trim().length > 0) {
      query.set(key, value.trim().slice(0, MAX_FILTER_LENGTH));
    }
  }

  const suffix = query.size > 0 ? `?${query.toString()}` : "";
  return handleAdminGet<SettingsAuditView>(
    request,
    `/internal/admin/settings/audit${suffix}`,
  );
}
