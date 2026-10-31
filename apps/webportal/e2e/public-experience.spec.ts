import { expect, test } from "@playwright/test";

test("accueil public lisible, navigable et sans débordement", async ({ page, isMobile }, testInfo) => {
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto("/");
  await expect(page.locator("main h1")).toHaveCount(1);
  await expect(page.locator("main h1")).toBeVisible();
  await expect(page.getByRole("link", { name: /voir les services/i }).first()).toBeVisible();
  const overflow = await page.evaluate(() => ({
    viewport: innerWidth,
    document: document.documentElement.scrollWidth,
    elements: [...document.querySelectorAll("body *")].filter((element) => {
      const rect = element.getBoundingClientRect();
      return rect.right > innerWidth + 2 && getComputedStyle(element).position !== "fixed";
    }).slice(0, 8).map((element) => ({ tag: element.tagName, className: element.className, right: Math.round(element.getBoundingClientRect().right) })),
  }));
  expect(overflow.document, JSON.stringify(overflow)).toBeLessThanOrEqual(overflow.viewport + 1);
  const methodInset = await page.locator(".vitrine-method h2").evaluate((element) => element.getBoundingClientRect().left);
  expect(methodInset).toBeGreaterThanOrEqual(20);
  await page.screenshot({ path: testInfo.outputPath("accueil.png"), fullPage: true, animations: "disabled" });
  await page.locator(".vitrine-services").scrollIntoViewIfNeeded();
  await expect(page.locator(".vitrine-services h2")).toBeVisible();
  if (isMobile) {
    await page.getByRole("button", { name: /ouvrir le menu/i }).click();
  }
  await page.getByRole("navigation", { name: "Navigation principale" }).getByRole("link", { name: "À propos" }).click();
  await expect(page).toHaveURL(/\/a-propos\/?$/);
  await expect(page.locator("main h1")).toBeVisible();
  expect(errors).toEqual([]);
});

test("mouvement réduit et pause gardent le contenu accessible", async ({ page }) => {
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("/");
  await expect(page.locator("main h1")).toBeVisible();
  await expect(page.locator(".home-intro-screen")).toBeHidden();
  const headingAnimation = await page.locator("main h1").first().evaluate((element) => getComputedStyle(element).animationName);
  expect(["none", ""]).toContain(headingAnimation);
  if (await page.getByRole("button", { name: /ouvrir le menu/i }).isVisible()) {
    await page.getByRole("button", { name: /ouvrir le menu/i }).click();
  }
  const toggle = page.getByRole("button", { name: "Mettre les animations en pause" });
  await toggle.click();
  await expect(page.getByRole("button", { name: "Relancer les animations" })).toHaveAttribute("aria-pressed", "true");
  await expect(page.locator("main h1")).toBeVisible();
});

test("le hero affiche ses actions sans écran d'introduction", async ({ page }) => {
  await page.goto("/");
  await expect(page.locator(".home-intro-screen")).toHaveCount(0);
  const actions = page.locator(".vitrine-hero-band .vitrine-hero-copy .vitrine-hero-actions").first();
  await expect(actions).toBeVisible();
  await expect(actions.getByRole("link", { name: /demander un devis/i })).toBeVisible();
});

test("le rappel du diagnostic expose les champs obligatoires et leurs erreurs", async ({ page }) => {
  await page.goto("/diagnostic?context=network");
  for (let step = 0; step < 20; step += 1) {
    const finish = page.getByRole("button", { name: "Voir mon résultat" });
    if (await finish.isVisible()) {
      await page.locator(".diagnostic-step input[type=radio]").first().check();
      await finish.click();
      break;
    }
    await page.locator(".diagnostic-step input[type=radio]").first().check();
    await page.getByRole("button", { name: "Continuer" }).click();
  }
  await expect(page.getByRole("button", { name: "Être rappelé" })).toBeVisible();
  await page.getByRole("button", { name: "Être rappelé" }).click();
  const name = page.getByRole("textbox", { name: /nom \/ prénom/i });
  const phone = page.getByRole("textbox", { name: /téléphone/i });
  const consent = page.getByRole("checkbox", { name: /j'accepte/i });
  await expect(name).toHaveAttribute("required", "");
  await expect(phone).toHaveAttribute("required", "");
  await expect(consent).toHaveAttribute("required", "");
  await page.route("**/api/diagnostic/callback", (route) => route.fulfill({
    status: 400,
    contentType: "application/json",
    body: JSON.stringify({ code: "INVALID_REQUEST", message: "Vérifiez les champs signalés.", correlation_id: "test", field_errors: { phone: "Le téléphone est invalide.", consent: "Votre accord est nécessaire." } }),
  }));
  await name.fill("Essai fictif");
  await phone.fill("0299000000");
  await consent.check();
  await page.getByRole("button", { name: "Envoyer ma demande" }).click();
  await expect(phone).toHaveAttribute("aria-invalid", "true");
  await expect(phone).toHaveAttribute("aria-describedby", "diagnostic-callback-phone-error");
  await expect(consent).toHaveAttribute("aria-invalid", "true");
  await expect(consent).toHaveAttribute("aria-describedby", "diagnostic-callback-consent-error");
});

test("le rail présente les quatre domaines et les lignes de services ne bougent pas au focus", async ({ page, isMobile }) => {
  await page.goto("/");
  const rail = page.locator(".vitrine-service-rail-track span");
  await expect(rail).toHaveCount(4);
  await page.locator(".vitrine-service-rail").scrollIntoViewIfNeeded();
  const positions = await rail.evaluateAll((elements) => elements.map((element) => {
    const rect = element.getBoundingClientRect();
    return { left: rect.left, right: rect.right, text: element.textContent?.trim() };
  }));
  const viewport = await page.evaluate(() => document.documentElement.clientWidth);
  expect(positions.map((item) => item.text)).toEqual(["Maintenance", "Réseau", "Sauvegarde", "Accès à distance"]);
  for (const item of positions) {
    expect(item.left).toBeGreaterThanOrEqual(-1);
    expect(item.right).toBeLessThanOrEqual(viewport + 1);
  }

  const card = page.locator(".vitrine-service-card").first();
  await card.scrollIntoViewIfNeeded();
  const paddingBefore = await card.evaluate((element) => getComputedStyle(element).paddingLeft);
  if (!isMobile) await card.hover();
  await card.getByRole("link", { name: /voir le service/i }).focus();
  await expect(card).toHaveCSS("background-color", "rgba(255, 255, 255, 0.05)");
  expect(await card.evaluate((element) => getComputedStyle(element).paddingLeft)).toBe(paddingBefore);
});

test("la pause arrête la vague du H1 et persiste après rechargement", async ({ page, isMobile }) => {
  await page.goto("/");
  if (await page.getByRole("button", { name: /ouvrir le menu/i }).isVisible()) {
    await page.getByRole("button", { name: /ouvrir le menu/i }).click();
  }
  await page.getByRole("button", { name: "Mettre les animations en pause" }).click();
  await expect(page.getByRole("button", { name: "Relancer les animations" })).toHaveAttribute("aria-pressed", "true");
  const animation = await page.locator("main .h1-letter").first().evaluate((element) => getComputedStyle(element).animationName);
  expect(animation).toBe("none");
  await page.reload();
  if (isMobile) await page.getByRole("button", { name: /ouvrir le menu/i }).click();
  await expect(page.getByRole("button", { name: "Relancer les animations" })).toHaveAttribute("aria-pressed", "true");
  await expect(page.locator(".home-intro-screen")).toBeHidden();
});

test("le hub explique l'intérêt de l'abonnement et mène aux formules", async ({ page }, testInfo) => {
  await page.goto("/assistance-maintenance");
  const section = page.getByRole("region", { name: /votre informatique compte chaque jour/i });
  await expect(section.getByRole("heading", { name: /son suivi aussi/i })).toBeVisible();
  await expect(section.getByText(/une prestation à la carte répond à un besoin ponctuel/i)).toBeVisible();
  await expect(section.getByRole("listitem")).toHaveCount(3);
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(
    await page.evaluate(() => document.documentElement.clientWidth + 1),
  );
  await section.screenshot({ path: testInfo.outputPath("valeur-abonnement.png"), animations: "disabled" });
  await section.getByRole("link", { name: /voir les formules particuliers/i }).click();
  await expect(page).toHaveURL(/\/assistance-maintenance\/particuliers\/?$/);
});
