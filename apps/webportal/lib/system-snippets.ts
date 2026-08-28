import "server-only";

import type { PublicSystemSnippets } from "@kermaria/shared";

import { getPublicSystemSnippets } from "@/lib/internal-api";
import {
  mergeSystemSnippets,
  type SystemSnippetMap,
} from "@/lib/system-snippet-defaults";

/**
 * Lecture serveur des textes systeme administrables, avec repli sur le code.
 * L'appel ne peut jamais faire echouer une page publique : `getPublicData`
 * renvoie `null` quand API-INTERNAL est indisponible, et la fusion retombe
 * alors integralement sur les valeurs par defaut.
 */
export async function resolveSystemSnippets(): Promise<SystemSnippetMap> {
  const result = await getPublicSystemSnippets();
  const payload = result.data as PublicSystemSnippets | null;
  return mergeSystemSnippets(payload?.snippets);
}
