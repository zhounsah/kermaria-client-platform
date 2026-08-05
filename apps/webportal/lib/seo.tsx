// Centralise le balisage schema.org de la vitrine publique.
//
// `.tsx` et non `.ts` : le fichier contient du JSX, cf. `JsonLd` en bas.
//
// La canonicalisation, elle, reste declaree page par page via
// `alternates.canonical` : les champs de metadata Next.js sont HERITES par
// les segments enfants, un helper pose au layout racine servirait de repli
// et masquerait l'oubli d'une canonique sur une page donnee.

import { PORTFOLIO_URL } from "@/lib/public-route-config";

const LEGAL_NAME = "Zachary HOUNSA-HOUNKPA EI";
const TRADE_NAME = "Zachary IT";

const BUSINESS_DESCRIPTION =
  "Sauvegarde distante, stockage documentaire et continuité d'activité pour "
  + "les particuliers, associations, indépendants et petites entreprises.";

/**
 * `resolvePortalAreaUrl(origin, "public")` renvoie une URL terminee par `/`
 * (son `pathname` vaut `/` par defaut). Concatener directement produirait
 * `https://…//#business`. Toutes les URL du balisage sont donc baties depuis
 * cette forme normalisee, sans slash final.
 */
function normalizeBaseUrl(baseUrl: string): string {
  return baseUrl.replace(/\/+$/, "");
}

/**
 * `LocalBusiness` plutot que `Organization`.
 *
 * `Organization` accepte deja `address` et `areaServed` — ce n'est donc pas
 * la raison du changement. Ce qui lui manque, c'est
 * `openingHoursSpecification`, et surtout le signal : Google documente
 * `LocalBusiness` comme le type attendu pour un etablissement a intention
 * locale.
 *
 * Ne pas utiliser `ProfessionalService` : schema.org l'a deprecie pour cause
 * de confusion avec `Service`, et la doc Google Local Business ne le cite
 * pas. Aucun sous-type concret de `LocalBusiness` ne correspond a de
 * l'infogerance ou de la sauvegarde de donnees : rester sur `LocalBusiness`.
 *
 * Champs volontairement absents, faute de valeur fiable dans le depot — ne
 * rien inventer, un `telephone` faux ou des horaires imaginaires sont des
 * motifs d'action manuelle :
 *   - `telephone` : seul un gabarit `+33 0 00 00 00 00` existe
 *     (`lib/mock-data.ts`), il n'est pas publiable.
 *   - `openingHoursSpecification` : aucune plage horaire n'est publiee.
 *   - `priceRange` : les tarifs vivent dans le catalogue, pas sous forme
 *     de fourchette.
 *
 * L'adresse, elle, provient des mentions legales publiees
 * (`mentions-légales.txt`, repris a l'identique dans le contenu
 * administrable `legal:mentions-legales`).
 */
export function localBusinessJsonLd(baseUrl: string) {
  const base = normalizeBaseUrl(baseUrl);

  return {
    "@context": "https://schema.org",
    "@type": "LocalBusiness",
    "@id": `${base}/#business`,
    name: LEGAL_NAME,
    alternateName: TRADE_NAME,
    url: `${base}/`,
    description: BUSINESS_DESCRIPTION,
    email: "zhounsah@home.bzh",
    address: {
      "@type": "PostalAddress",
      streetAddress: "3 Kermaria",
      postalCode: "35580",
      addressLocality: "Guichen",
      addressRegion: "Ille-et-Vilaine",
      addressCountry: "FR",
    },
    areaServed: [
      { "@type": "City", name: "Guichen" },
      { "@type": "AdministrativeArea", name: "Ille-et-Vilaine" },
      { "@type": "Country", name: "France" },
    ],
    knowsLanguage: "fr-FR",
    // Seule autre presence web reelle de l'entite. Ne jamais y ajouter un
    // profil qui n'existe pas : un `sameAs` mort est pire que pas de
    // `sameAs`.
    sameAs: [PORTFOLIO_URL],
    // taxID: "10511152000018",  // SIRET, deja publie dans les mentions
    //                           // legales : a decommenter si tu veux le
    //                           // publier aussi dans le balisage.
  };
}

/**
 * `WebSite` distinct du business : permet a Google d'associer le nom du site
 * a la marque. Pas de `SearchAction` tant qu'il n'existe pas de recherche
 * interne publique — declarer une sitelinks searchbox inexistante est une
 * cause classique de rejet du balisage.
 */
export function webSiteJsonLd(baseUrl: string) {
  const base = normalizeBaseUrl(baseUrl);

  return {
    "@context": "https://schema.org",
    "@type": "WebSite",
    "@id": `${base}/#website`,
    url: `${base}/`,
    name: LEGAL_NAME,
    inLanguage: "fr-FR",
    publisher: { "@id": `${base}/#business` },
  };
}

/**
 * Balisage d'une fiche pack. Pas d'`Offer` ni de `price` : le tarif affiche
 * vient du catalogue commercial et peut changer sans que ce balisage suive.
 * Google signale une incoherence entre le balisage et la page.
 */
export function packServiceJsonLd(
  baseUrl: string,
  pack: { slug: string; label: string; description: string },
) {
  const base = normalizeBaseUrl(baseUrl);

  return {
    "@context": "https://schema.org",
    "@type": "Service",
    "@id": `${base}/offres/${pack.slug}#service`,
    name: pack.label,
    description: pack.description,
    url: `${base}/offres/${pack.slug}`,
    serviceType: "Sauvegarde et stockage distant de données",
    // `provider` est inline plutot qu'une reference `@id` : le noeud
    // `#business` n'est emis que sur l'accueil, une reference vers lui
    // laisserait le graphe de cette page incomplet.
    provider: {
      "@type": "LocalBusiness",
      name: LEGAL_NAME,
      url: `${base}/`,
      address: {
        "@type": "PostalAddress",
        addressLocality: "Guichen",
        addressCountry: "FR",
      },
    },
    areaServed: { "@type": "Country", name: "France" },
  };
}

/**
 * Fil d'Ariane. Google l'utilise pour remplacer l'URL brute dans les SERP.
 *
 * `items` = la chaine complete APRES l'accueil, page courante incluse
 * (l'accueil est ajoute automatiquement). Google accepte que le dernier
 * maillon soit la page elle-meme.
 */
export function breadcrumbJsonLd(
  baseUrl: string,
  items: { name: string; path: string }[],
) {
  const base = normalizeBaseUrl(baseUrl);

  return {
    "@context": "https://schema.org",
    "@type": "BreadcrumbList",
    itemListElement: [{ name: "Accueil", path: "/" }, ...items].map(
      (item, index) => ({
        "@type": "ListItem",
        position: index + 1,
        name: item.name,
        item: `${base}${item.path}`,
      }),
    ),
  };
}

/**
 * Rend un bloc JSON-LD.
 *
 * `JSON.stringify` n'echappe PAS `<`. Une donnee contenant la sequence
 * `</script>` fermerait donc la balise prematurement. Ici toutes les donnees
 * sont des constantes du code, mais l'echappement est applique par principe :
 * le jour ou un champ viendra de `getPublicManagedContent`, le composant
 * restera sur.
 */
export function JsonLd({ data }: { data: unknown }) {
  const json = JSON.stringify(data).replace(/</g, "\\u003c");

  return (
    <script
      type="application/ld+json"
      dangerouslySetInnerHTML={{ __html: json }}
    />
  );
}
