import "server-only";

import type { DiagnosticConfiguration } from "@kermaria/shared";

import { DEFAULT_DIAGNOSTIC_CONFIGURATION } from "@/lib/diagnostic-context";
import { validateDiagnosticConfiguration } from "@/lib/diagnostic-configuration-validation";
import { getPublicDiagnosticConfiguration } from "@/lib/internal-api";

/**
 * Lecture serveur de la version publiee du diagnostic, avec repli ferme sur la
 * configuration integree au code. Trois cas retombent volontairement sur le
 * code plutot que de degrader le parcours :
 *
 * - API-INTERNAL indisponible ;
 * - aucune version publiee ;
 * - version publiee qui ne passe plus la validation du registre ferme.
 *
 * Le brouillon n'est jamais lu ici : une redaction en cours ne peut pas
 * atteindre un visiteur.
 */
export async function resolvePublishedDiagnosticConfiguration(): Promise<
  DiagnosticConfiguration
> {
  const result = await getPublicDiagnosticConfiguration();
  const payload = result.data?.configuration ?? null;
  if (payload === null) return DEFAULT_DIAGNOSTIC_CONFIGURATION;

  const { configuration } = validateDiagnosticConfiguration(payload);
  return configuration ?? DEFAULT_DIAGNOSTIC_CONFIGURATION;
}
