const recipes = [
  {
    title: "Bowl energie fruits rouges",
    category: "Petit-dejeuner",
    duration: "10 min",
    level: "Facile",
    description:
      "Un petit-dejeuner rapide avec yaourt, granola maison et fruits rouges pour commencer la journee.",
    tags: ["Frais", "Rapide", "Equilibre"],
    ingredients: ["Yaourt", "Granola", "Fruits rouges"],
  },
  {
    title: "Tartine avocat oeuf mollet",
    category: "Petit-dejeuner",
    duration: "15 min",
    level: "Facile",
    description:
      "Une tartine complete avec avocat, oeuf mollet et graines pour une assiette simple et nourrissante.",
    tags: ["Sale", "Brunch", "Protein"],
    ingredients: ["Pain", "Avocat", "Oeuf"],
  },
  {
    title: "Poulet roti citron herbes",
    category: "Plat",
    duration: "45 min",
    level: "Intermediaire",
    description:
      "Un plat principal parfume avec citron, ail et herbes fraiches, accompagne de legumes rotis.",
    tags: ["Four", "Familial", "Poulet"],
    ingredients: ["Poulet", "Citron", "Herbes"],
  },
  {
    title: "Pates creme champignons",
    category: "Plat",
    duration: "25 min",
    level: "Facile",
    description:
      "Recette reconfortante avec sauce cremeuse, champignons poeles et finition au parmesan.",
    tags: ["Pates", "Confort", "Vegetarien"],
    ingredients: ["Pates", "Champignons", "Creme"],
  },
  {
    title: "Tarte pommes cannelle",
    category: "Dessert",
    duration: "50 min",
    level: "Intermediaire",
    description:
      "Dessert classique avec pommes fines, pate croustillante et note de cannelle.",
    tags: ["Dessert", "Four", "Classique"],
    ingredients: ["Pommes", "Pate", "Cannelle"],
  },
  {
    title: "Mousse chocolat intense",
    category: "Dessert",
    duration: "20 min",
    level: "Facile",
    description:
      "Une mousse chocolat aerienne a preparer rapidement pour terminer un repas sur une note gourmande.",
    tags: ["Chocolat", "Sans cuisson", "Express"],
    ingredients: ["Chocolat", "Oeufs", "Sucre"],
  },
];

const searchInput = document.getElementById("search-input");
const categoryFilters = document.getElementById("category-filters");
const recipesGrid = document.getElementById("recipes-grid");
const resultsCopy = document.getElementById("results-copy");
const recipeCount = document.getElementById("recipe-count");

let activeCategory = "all";

const createRecipeCard = (recipe) => {
  const card = document.createElement("article");
  card.className = "recipe-card";

  const tags = recipe.tags.map((tag) => `<li>${tag}</li>`).join("");
  const ingredients = recipe.ingredients.map((ingredient) => `<li>${ingredient}</li>`).join("");

  card.innerHTML = `
    <div class="recipe-head">
      <div>
        <h3>${recipe.title}</h3>
      </div>
      <span class="recipe-category">${recipe.category}</span>
    </div>
    <p>${recipe.description}</p>
    <div class="recipe-meta">
      <span>${recipe.duration}</span>
      <span>${recipe.level}</span>
    </div>
    <ul class="recipe-tags" aria-label="Mots cles de la recette">
      ${tags}
    </ul>
    <ul class="recipe-ingredients" aria-label="Ingredients principaux">
      ${ingredients}
    </ul>
  `;

  return card;
};

const getFilteredRecipes = () => {
  const query = searchInput.value.trim().toLowerCase();

  return recipes.filter((recipe) => {
    const matchesCategory = activeCategory === "all" || recipe.category === activeCategory;
    const haystack = `${recipe.title} ${recipe.category} ${recipe.description} ${recipe.ingredients.join(" ")}`.toLowerCase();
    const matchesSearch = !query || haystack.includes(query);

    return matchesCategory && matchesSearch;
  });
};

const renderRecipes = () => {
  const filteredRecipes = getFilteredRecipes();

  recipesGrid.innerHTML = "";

  if (filteredRecipes.length === 0) {
    const emptyState = document.createElement("div");
    emptyState.className = "empty-state";
    emptyState.textContent = "Aucune recette ne correspond a la recherche actuelle.";
    recipesGrid.appendChild(emptyState);
  } else {
    filteredRecipes.forEach((recipe) => {
      recipesGrid.appendChild(createRecipeCard(recipe));
    });
  }

  recipeCount.textContent = String(filteredRecipes.length);
  resultsCopy.textContent =
    filteredRecipes.length === 1
      ? "1 recette correspond aux filtres actifs."
      : `${filteredRecipes.length} recettes correspondent aux filtres actifs.`;
};

categoryFilters?.addEventListener("click", (event) => {
  const button = event.target.closest("[data-category]");

  if (!button) {
    return;
  }

  activeCategory = button.dataset.category || "all";

  categoryFilters.querySelectorAll(".chip").forEach((chip) => {
    chip.classList.toggle("is-active", chip === button);
  });

  renderRecipes();
});

searchInput?.addEventListener("input", renderRecipes);

renderRecipes();
