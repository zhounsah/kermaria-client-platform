import { expect, test } from "@playwright/test";

const widths = [320, 390, 768, 1024, 1440, 1920] as const;
// Ces pages restent rendues par le fallback local sans API-INTERNAL.
// Les catalogues /services, /offres et /tarifs exigent la recette DEV intégrée.
const routes = ["/", "/assistance-maintenance", "/contact", "/diagnostic"] as const;

test("les parcours publics essentiels ne débordent pas aux largeurs cibles", async ({ page, isMobile }, testInfo) => {
  test.skip(isMobile, "La matrice de largeurs est vérifiée dans le projet desktop.");
  const pageErrors: string[] = [];
  page.on("pageerror", (error) => pageErrors.push(error.message));

  for (const width of widths) {
    await page.setViewportSize({ width, height: 900 });
    for (const route of routes) {
      const response = await page.goto(route);
      expect(response?.status(), `${route} à ${width} px`).toBe(200);
      await expect(page.locator("main h1").first()).toBeVisible();
      const size = await page.evaluate(() => ({
        viewport: document.documentElement.clientWidth,
        document: document.documentElement.scrollWidth,
      }));
      expect(size.document, `${route} à ${width} px`).toBeLessThanOrEqual(size.viewport + 1);
      if ((width === 320 || width === 390 || width === 1440 || width === 1920) && (route === "/" || route === "/assistance-maintenance")) {
        await page.screenshot({ path: testInfo.outputPath(`${route === "/" ? "accueil" : "assistance"}-${width}.png`), animations: "disabled" });
      }
    }
  }
  expect(pageErrors).toEqual([]);
});
