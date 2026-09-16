import "server-only";

import type { DiagnosticConfiguration, PreDiagnosticConfiguration } from "@kermaria/shared";

import { DEFAULT_DIAGNOSTIC_CONFIGURATION } from "@/lib/diagnostic-context";
import { DEFAULT_PRE_DIAGNOSTIC_CONFIGURATION_V2 } from "@/lib/pre-diagnostic";
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

/** La v2 est la seule configuration capable de piloter le nouveau
 * pré-diagnostic. Une v1 publiée reste lisible pour l'ancien moteur mais ne
 * change pas le parcours validé du commit 77fc11c. */
export async function resolvePublishedPreDiagnosticConfiguration(): Promise<{
  configuration: PreDiagnosticConfiguration | null;
  version: number;
}> {
  const result = await getPublicDiagnosticConfiguration();
  const payload = result.data?.configuration ?? null;
  const version = result.data?.source === "database" && Number.isInteger(result.data.version) && result.data.version > 0
    ? result.data.version
    : 0;
  if (payload === null) {
    return { configuration: DEFAULT_PRE_DIAGNOSTIC_CONFIGURATION_V2, version: 0 };
  }
  const { configuration } = validateDiagnosticConfiguration(payload);
  return {
    configuration: configuration?.schemaVersion === 2
      ? configuration
      : DEFAULT_PRE_DIAGNOSTIC_CONFIGURATION_V2,
    version,
  };
}
