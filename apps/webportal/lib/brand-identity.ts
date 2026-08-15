/**
 * Identite de marque du site public, source unique.
 *
 * `Zachary IT` est le NOM COMMERCIAL : c'est lui que le visiteur voit et
 * que les moteurs doivent retenir comme nom du site.
 * `Zachary HOUNSA-HOUNKPA EI` est la DENOMINATION JURIDIQUE de l'entreprise
 * individuelle : elle reste exposee la ou elle fait foi (mentions legales,
 * pied de page, `legalName` du balisage schema.org).
 *
 * Les deux valeurs proviennent des mentions legales publiees
 * (`apps/api-internal/SeedContent/mentions-legales.md`, section « Editeur du
 * site »), qui font foi. Ne rien reformuler ici sans les mettre a jour.
 *
 * Module volontairement SANS import : il est charge tel quel par
 * `scripts/verify-brand-identity-contract.mjs`, qui ne dispose pas de la
 * resolution d'alias `@/` de Next.
 */
export const BRAND_NAME = "Zachary IT";
export const LEGAL_NAME = "Zachary HOUNSA-HOUNKPA EI";
