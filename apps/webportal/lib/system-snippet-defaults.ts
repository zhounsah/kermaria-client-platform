/**
 * Repli des textes systeme publics, utilisable cote client comme cote serveur.
 *
 * Les valeurs doivent rester identiques a `CommunicationTemplateRegistry` dans
 * API-INTERNAL : si la base est indisponible ou la cle absente, le portail
 * affiche exactement le meme texte que le code de reference, jamais une chaine
 * vide. Ce module ne doit contenir aucune donnee sensible : il est embarque
 * dans le bundle navigateur.
 */
export const SYSTEM_SNIPPET_DEFAULTS = {
  contact_form_confirmation:
    "Message envoyé. Nous reviendrons vers vous par e-mail.",
  contact_form_privacy_notice:
    "Vos données ne sont utilisées que pour répondre à votre message. "
    + "Aucun traceur ni cookie de mesure n'est déposé sur ce site.",
  service_temporarily_closed:
    "Ce service est momentanément indisponible. Merci de réessayer plus tard "
    + "ou de nous contacter directement.",
  commercial_footer_note:
    "Les tarifs affichés sont recalculés à partir du catalogue au moment de la commande.",
} as const;

export type SystemSnippetKey = keyof typeof SYSTEM_SNIPPET_DEFAULTS;

/** Textes systeme resolus, toujours complets. */
export type SystemSnippetMap = Record<SystemSnippetKey, string>;

/** Fusionne les textes administres avec le repli de code. */
export function mergeSystemSnippets(
  overrides: Record<string, string> | null | undefined,
): SystemSnippetMap {
  const merged: SystemSnippetMap = { ...SYSTEM_SNIPPET_DEFAULTS };
  if (!overrides) return merged;
  for (const key of Object.keys(SYSTEM_SNIPPET_DEFAULTS) as SystemSnippetKey[]) {
    // Une cle inconnue est ignoree : le registre reste ferme cote code.
    const value = overrides[key];
    if (typeof value === "string" && value.trim().length > 0) {
      merged[key] = value;
    }
  }
  return merged;
}
