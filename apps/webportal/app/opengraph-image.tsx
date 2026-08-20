import { ImageResponse } from "next/og";

/**
 * Image Open Graph par defaut du portail, generee a la volee plutot que
 * committee en binaire : le texte reste modifiable en relisant ce fichier,
 * et il n'y a pas d'asset a regenerer a la main quand l'accroche change.
 *
 * Posee a la racine de `app/`, elle s'applique par heritage a toutes les
 * routes qui ne declarent pas leur propre `opengraph-image`.
 *
 * Aucun nom d'hote n'y figure : la meme image est servie sur
 * `zachary-it.fr`, l'ancien domaine et `home.bzh` en recette ; une URL en dur serait
 * fausse sur l'un des deux.
 *
 * Les couleurs viennent des tokens officiels 2026 et sont recopiees en dur :
 * Satori ne resout pas les variables CSS.
 */
export const alt =
  "Zachary IT — votre informatique, gérée, sécurisée, disponible.";
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

export default async function OpengraphImage() {
  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          justifyContent: "space-between",
          backgroundColor: "#0B1220",
          padding: "72px 80px",
          fontFamily: "Inter, sans-serif",
        }}
      >
        <div style={{ display: "flex", flexDirection: "column" }}>
          <div
            style={{
              display: "flex",
              fontSize: 30,
              letterSpacing: 4,
              color: "#38BDF8",
            }}
          >
            ZACHARY IT — GUICHEN (35)
          </div>
          <div
            style={{
              display: "flex",
              width: 96,
              height: 6,
              marginTop: 28,
              backgroundColor: "#2563EB",
            }}
          />
        </div>

        <div
          style={{
            display: "flex",
            fontSize: 68,
            lineHeight: 1.15,
            color: "#ffffff",
            letterSpacing: -1,
          }}
        >
          Votre informatique. Gérée, sécurisée, disponible.
        </div>

        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "flex-end",
            fontSize: 26,
            color: "#F8FAFC",
          }}
        >
          <div style={{ display: "flex" }}>
            Particuliers, associations et petites entreprises
          </div>
          <div style={{ display: "flex" }}>Zachary HOUNSA-HOUNKPA EI</div>
        </div>
      </div>
    ),
    size,
  );
}
