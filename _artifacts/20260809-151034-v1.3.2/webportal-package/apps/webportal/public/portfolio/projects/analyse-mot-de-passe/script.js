const passwordInput = document.getElementById("password-input");
const toggleVisibilityButton = document.getElementById("toggle-visibility");
const strengthLabel = document.getElementById("strength-label");
const scoreValue = document.getElementById("score-value");
const scoreBar = document.getElementById("score-bar");
const lengthValue = document.getElementById("length-value");
const levelValue = document.getElementById("level-value");
const riskValue = document.getElementById("risk-value");
const recommendationText = document.getElementById("recommendation-text");

const ruleElements = {
  length: document.querySelector('[data-rule="length"]'),
  lowercase: document.querySelector('[data-rule="lowercase"]'),
  uppercase: document.querySelector('[data-rule="uppercase"]'),
  number: document.querySelector('[data-rule="number"]'),
  symbol: document.querySelector('[data-rule="symbol"]'),
};

const strengthScale = [
  {
    max: 24,
    label: "Très faible",
    level: "Critique",
    risk: "Exposition immédiate",
    recommendation:
      "Allonger fortement le mot de passe et ajouter plusieurs types de caractères.",
  },
  {
    max: 49,
    label: "Faible",
    level: "Fragile",
    risk: "Facile à deviner",
    recommendation:
      "Ajouter de la longueur ainsi qu'un mélange de majuscules, chiffres et symboles.",
  },
  {
    max: 74,
    label: "Correct",
    level: "Intermédiaire",
    risk: "Peut être amélioré",
    recommendation:
      "Renforcer encore la longueur et éviter les suites trop simples ou répétitives.",
  },
  {
    max: 89,
    label: "Solide",
    level: "Bon niveau",
    risk: "Bonne résistance",
    recommendation:
      "Le mot de passe est cohérent. Vous pouvez encore augmenter sa longueur pour plus de marge.",
  },
  {
    max: 100,
    label: "Excellent",
    level: "Très bon niveau",
    risk: "Faible risque",
    recommendation:
      "Mot de passe robuste. Conservez un usage unique et pensez à un gestionnaire si besoin.",
  },
];

const computePasswordScore = (value) => {
  const checks = {
    length: value.length >= 12,
    lowercase: /[a-z]/.test(value),
    uppercase: /[A-Z]/.test(value),
    number: /\d/.test(value),
    symbol: /[^A-Za-z0-9]/.test(value),
  };

  let score = 0;

  score += Math.min(value.length * 4, 40);
  score += checks.lowercase ? 12 : 0;
  score += checks.uppercase ? 14 : 0;
  score += checks.number ? 14 : 0;
  score += checks.symbol ? 18 : 0;

  if (/([a-zA-Z0-9])\1{2,}/.test(value)) {
    score -= 10;
  }

  if (/123|abc|qwerty|password|azerty/i.test(value)) {
    score -= 18;
  }

  return {
    score: Math.max(0, Math.min(100, score)),
    checks,
  };
};

const getStrengthState = (score) =>
  strengthScale.find((item) => score <= item.max) || strengthScale[strengthScale.length - 1];

const updateRuleStates = (checks) => {
  Object.entries(ruleElements).forEach(([rule, element]) => {
    element?.classList.toggle("is-valid", Boolean(checks[rule]));
  });
};

const updateAnalyzer = (value) => {
  const { score, checks } = computePasswordScore(value);
  const strength = getStrengthState(score);

  strengthLabel.textContent = strength.label;
  scoreValue.textContent = `${score}/100`;
  scoreBar.style.width = `${score}%`;
  lengthValue.textContent = `${value.length} caractère${value.length > 1 ? "s" : ""}`;
  levelValue.textContent = strength.level;
  riskValue.textContent = strength.risk;
  recommendationText.textContent = value
    ? strength.recommendation
    : "Saisir un mot de passe pour afficher une analyse détaillée.";

  if (!value) {
    strengthLabel.textContent = "À analyser";
    levelValue.textContent = "Insuffisant";
    riskValue.textContent = "Trop faible";
  }

  updateRuleStates(checks);
};

toggleVisibilityButton?.addEventListener("click", () => {
  const nextType = passwordInput.type === "password" ? "text" : "password";
  passwordInput.type = nextType;
  toggleVisibilityButton.textContent = nextType === "password" ? "Afficher" : "Masquer";
});

passwordInput?.addEventListener("input", (event) => {
  updateAnalyzer(event.target.value);
});

updateAnalyzer("");
