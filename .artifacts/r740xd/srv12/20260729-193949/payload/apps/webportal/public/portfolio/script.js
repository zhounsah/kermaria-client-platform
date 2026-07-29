/* script.js */
document.documentElement.classList.add("js");

const body = document.body;
const navToggle = document.querySelector(".nav-toggle");
const navMenu = document.querySelector(".nav-menu");
const navLinks = document.querySelectorAll(".nav-menu a");
const revealElements = document.querySelectorAll(".reveal");
const skillMeters = document.querySelectorAll(".skill-meter");

// Menu mobile
if (navToggle && navMenu) {
  const setMenuState = (open) => {
    navMenu.classList.toggle("is-open", open);
    navToggle.classList.toggle("is-active", open);
    navToggle.setAttribute("aria-expanded", String(open));
    body.classList.toggle("menu-open", open);
  };

  navToggle.addEventListener("click", () => {
    const isOpen = navMenu.classList.contains("is-open");
    setMenuState(!isOpen);
  });

  navLinks.forEach((link) => {
    link.addEventListener("click", () => setMenuState(false));
  });

  window.addEventListener("resize", () => {
    if (window.innerWidth > 980) {
      setMenuState(false);
    }
  });
}

// Reveals au scroll + animation des barres
const animateSkillMeters = () => {
  skillMeters.forEach((meter) => meter.classList.add("is-animated"));
};

if ("IntersectionObserver" in window) {
  const revealObserver = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) {
          return;
        }

        entry.target.classList.add("is-visible");

        if (entry.target.id === "skills" || entry.target.classList.contains("skill-meter")) {
          animateSkillMeters();
        }

        revealObserver.unobserve(entry.target);
      });
    },
    {
      threshold: 0.16,
      rootMargin: "0px 0px -8% 0px",
    }
  );

  revealElements.forEach((element) => revealObserver.observe(element));
  skillMeters.forEach((meter) => revealObserver.observe(meter));
} else {
  revealElements.forEach((element) => element.classList.add("is-visible"));
  animateSkillMeters();
}

